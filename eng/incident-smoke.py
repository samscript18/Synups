#!/usr/bin/env python3
"""Real ICMP incident verification, confined to a disposable Linux network namespace.

Prepare outside the namespace; run --worker only under `sudo unshare --net` in CI.
No host firewall, route, interface or production user data is changed.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import time
import xml.etree.ElementTree as ET


def namespace_guard():
    if platform.system() != 'Linux' or os.geteuid() != 0:
        raise RuntimeError('Worker requires root inside a new disposable Linux network namespace')
    current = os.readlink('/proc/self/ns/net')
    if current == os.readlink('/proc/1/ns/net'):
        raise RuntimeError('Refusing host network namespace')
    interfaces = json.loads(subprocess.check_output(['ip', '-j', 'link'], text=True))
    if {i['ifname'] for i in interfaces} != {'lo'}:
        raise RuntimeError('Isolated namespace must contain only loopback')
    if subprocess.check_output(['ip', 'route', 'show', 'default'], text=True).strip():
        raise RuntimeError('Isolated namespace must have no default route')
    return current


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare', type=Path)
    parser.add_argument('--worker', type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    if args.prepare:
        root = args.prepare.resolve()
        root.mkdir(parents=True, exist_ok=True)
        project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
        props = ET.SubElement(project, 'PropertyGroup')
        for key, value in dict(TargetFramework='net10.0', OutputType='Exe', Nullable='enable', ImplicitUsings='enable', TreatWarningsAsErrors='true', EnableDefaultCompileItems='false').items():
            ET.SubElement(props, key).text = value
        items = ET.SubElement(project, 'ItemGroup')
        ET.SubElement(items, 'ProjectReference', Include=str(repo/'synups/Synups.Desktop/Synups.Desktop.csproj'))
        ET.SubElement(items, 'Compile', Include=str(repo/'eng/incident-smoke.cs'))
        path = root/'Synups.IncidentSmoke.csproj'
        ET.ElementTree(project).write(path, encoding='unicode')
        subprocess.run([args.dotnet, 'restore', str(path), '--disable-parallel', '-m:1', '-nr:false'], cwd=repo/'synups', check=True)
        subprocess.run([args.dotnet, 'build', str(path), '-c', 'Release', '--no-restore', '-m:1', '-nr:false', '-p:UseSharedCompilation=false'], cwd=repo/'synups', check=True)
        return
    if not args.worker or not args.output:
        parser.error('Choose --prepare DIRECTORY or --worker ASSEMBLY --output NEW_DIRECTORY')
    namespace = namespace_guard()  # Must pass before any mutation.
    subprocess.run(['ip', 'link', 'set', 'lo', 'up'], check=True)
    output = args.output.resolve()
    if output.exists():
        raise RuntimeError('Output must be new; previous evidence is never removed')
    output.mkdir(parents=True)
    assembly = args.worker.resolve()
    rule = ['OUTPUT', '-d', '127.0.0.1/32', '-p', 'icmp', '--icmp-type', 'echo-request', '-m', 'comment', '--comment', 'synups-isolated-incident', '-j', 'DROP']
    applied = False
    process = None
    actions = []

    def wait_for(name):
        deadline = time.monotonic() + 90
        while not (output/name).exists():
            if process.poll() is not None:
                raise RuntimeError('Incident application exited before '+name)
            if time.monotonic() > deadline:
                raise TimeoutError('No real observation checkpoint: '+name)
            time.sleep(.05)

    try:
        with (output/'incident-console.txt').open('w') as log:
            process = subprocess.Popen([args.dotnet, str(assembly), str(output), 'observe'], stdout=log, stderr=subprocess.STDOUT)
            wait_for('healthy.ready')
            namespace_guard()
            subprocess.run(['iptables', '-A']+rule, check=True)
            applied = True
            actions.append({'action': 'drop_real_loopback_icmp_requests_in_isolated_namespace', 'monotonic_seconds': time.monotonic()})
            wait_for('outage.ready')
            namespace_guard()
            subprocess.run(['iptables', '-D']+rule, check=True)
            applied = False
            actions.append({'action': 'restore_real_loopback_icmp_in_isolated_namespace', 'monotonic_seconds': time.monotonic()})
            if process.wait(timeout=90) != 0:
                raise RuntimeError('Real incident verification failed; inspect native report/log')
        with (output/'restart-console.txt').open('w') as log:
            subprocess.run([args.dotnet, str(assembly), str(output), 'reload'], stdout=log, stderr=subprocess.STDOUT, timeout=90, check=True)
        with (output/'real-incident.csv').open(newline='') as exported:
            rows = list(csv.DictReader(exported))
        incident = json.loads((output/'incident.json').read_text())['outage']
        if len(rows) != 1 or rows[0]['OutageId'] != incident['Id'] or not rows[0]['RecoveredUtc'] or float(rows[0]['ElapsedSeconds']) <= 0 or 'namespace' not in rows[0]['Comment']:
            raise RuntimeError('CSV does not contain the actual recovered incident/context')
        record = {'network_namespace': namespace, 'host_namespace_untouched': True, 'target': '127.0.0.1', 'actions': actions,
                  'measurements': 'Production IcmpProbe/MonitoringService, actual kernel-filtered ICMP; no fake probe or injected result',
                  'limits': 'Controlled loopback ICMP disruption, not an Internet outage; source-component native UI harness, not published-apphost or physical interaction.'}
        record['files_sha256'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in output.iterdir() if p.is_file()}
        (output/'namespace-evidence.json').write_text(json.dumps(record, indent=2)+'\n')
        print('REAL_ISOLATED_INCIDENT_RECOVERY_RESTART=PASS', flush=True)
    finally:
        if applied:
            namespace_guard()
            subprocess.run(['iptables', '-D']+rule, check=True)
        if process is not None and process.poll() is None:
            process.kill()
            process.wait(timeout=10)


if __name__ == '__main__':
    main()
