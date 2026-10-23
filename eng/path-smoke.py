#!/usr/bin/env python3
"""Real three-hop IPv4 trace inside disposable, disconnected Linux namespaces.
Prepare without privileges. Worker requires explicit `unshare --net`; never changes
host interfaces, routes, sysctls or firewall. No public destinations or named netns.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET
import importlib.util
spec = importlib.util.spec_from_file_location("incident_smoke", Path(__file__).with_name("incident-smoke.py"))
incident = importlib.util.module_from_spec(spec)
spec.loader.exec_module(incident)


def run(*command):
    subprocess.run(command, check=True, timeout=15, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare', type=Path)
    parser.add_argument('--worker', type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    if args.prepare:
        root = args.prepare.resolve(); root.mkdir(parents=True, exist_ok=True)
        project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
        props = ET.SubElement(project, 'PropertyGroup')
        for key, value in dict(TargetFramework='net10.0', OutputType='Exe', Nullable='enable', ImplicitUsings='enable', TreatWarningsAsErrors='true', EnableDefaultCompileItems='false').items():
            ET.SubElement(props, key).text = value
        items = ET.SubElement(project, 'ItemGroup')
        ET.SubElement(items, 'ProjectReference', Include=str(repo/'synups/Synups.Core/Synups.Core.csproj'))
        ET.SubElement(items, 'Compile', Include=str(repo/'eng/path-smoke.cs'))
        path = root/'Synups.PathSmoke.csproj'; ET.ElementTree(project).write(path, encoding='unicode')
        subprocess.run([args.dotnet, 'build', str(path), '-c', 'Release', '-m:1', '-nr:false', '-p:UseSharedCompilation=false'], cwd=repo/'synups', check=True)
        return
    if not args.worker or not args.output:
        parser.error('Choose --prepare DIRECTORY or --worker ASSEMBLY --output NEW_DIRECTORY')
    namespace = incident.namespace_guard()  # All mutations must follow this check.
    output = args.output.resolve()
    if output.exists():
        raise RuntimeError('Output must be new; existing evidence is never removed')
    output.mkdir(parents=True)
    children = []
    try:
        for _ in range(3):
            child = subprocess.Popen(['unshare', '--net', '--', 'sleep', '180'])
            children.append(child)
            deadline = time.monotonic() + 5
            while child.poll() is None and os.readlink(f'/proc/{child.pid}/ns/net') == namespace:
                if time.monotonic() > deadline:
                    raise TimeoutError('Child did not enter its isolated network namespace')
                time.sleep(.01)
            if child.poll() is not None:
                raise RuntimeError('Cannot create a disposable child network namespace')
            child_ns = os.readlink(f'/proc/{child.pid}/ns/net')
            if child_ns == os.readlink('/proc/1/ns/net') or child_ns == namespace:
                raise RuntimeError('Refusing non-isolated child namespace')
            links = json.loads(subprocess.check_output(['nsenter', '-t', str(child.pid), '-n', 'ip', '-j', 'link'], text=True))
            if {link['ifname'] for link in links} != {'lo'}:
                raise RuntimeError('Child namespace must contain only loopback before configuration')
        if len({os.readlink(f'/proc/{c.pid}/ns/net') for c in children}) != 3:
            raise RuntimeError('Child namespaces must be distinct')
        r1, r2, destination = [str(c.pid) for c in children]
        def ip(pid, *arguments):
            if pid is None:
                run('ip', *arguments)
            else:
                run('nsenter', '-t', pid, '-n', 'ip', *arguments)
        def pair(left, right, a, b, subnet):
            ip(left, 'link', 'add', a, 'type', 'veth', 'peer', 'name', b)
            ip(left, 'link', 'set', b, 'netns', right)
            ip(left, 'addr', 'add', subnet+'.1/30', 'dev', a)
            ip(right, 'addr', 'add', subnet+'.2/30', 'dev', b)
            ip(left, 'link', 'set', a, 'up'); ip(right, 'link', 'set', b, 'up')
        ip(None, 'link', 'set', 'lo', 'up')
        for pid in (r1, r2, destination):
            ip(pid, 'link', 'set', 'lo', 'up')
        pair(None, r1, 's0', 'r1a', '10.77.1')
        pair(r1, r2, 'r1b', 'r2a', '10.77.2')
        pair(r2, destination, 'r2b', 'd0', '10.77.3')
        for pid in (r1, r2):
            run('nsenter', '-t', pid, '-n', 'sysctl', '-w', 'net.ipv4.ip_forward=1')
        ip(None, 'route', 'add', '10.77.3.0/30', 'via', '10.77.1.2')
        ip(r1, 'route', 'add', '10.77.3.0/30', 'via', '10.77.2.2')
        ip(r2, 'route', 'add', '10.77.1.0/30', 'via', '10.77.2.1')
        ip(destination, 'route', 'add', '10.77.1.0/30', 'via', '10.77.3.1')
        with (output/'console.txt').open('w') as log:
            result = subprocess.run([args.dotnet, str(args.worker.resolve()), str(output)], stdout=log, stderr=subprocess.STDOUT, timeout=90)
        if result.returncode:
            print((output/'console.txt').read_text())
            raise RuntimeError('Actual multi-hop trace gate failed')
        (output/'namespace-evidence.json').write_text(json.dumps({'passed': True, 'network_namespace': namespace,
            'child_namespaces': [os.readlink(f'/proc/{c.pid}/ns/net') for c in children],
            'host_namespace_untouched': True, 'internet_routes': False, 'target': '10.77.3.2',
            'scope': 'Real IPv4 ICMP TTL expired at two isolated Linux routers, then destination echo; no IPv6 or physical-network certification'}, indent=2)+'\n')
        print('REAL_ISOLATED_THREE_HOP_TRACE=PASS', flush=True)
    finally:
        for child in reversed(children):
            if child.poll() is None:
                child.terminate()
            child.wait(timeout=10)


if __name__ == '__main__':
    main()
