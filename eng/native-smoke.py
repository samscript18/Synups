#!/usr/bin/env python3
"""Explicit native UI/real loopback smoke. Uses temporary files and the production App/VM/probe.
Run: python3 eng/native-smoke.py --dotnet /path/to/dotnet
"""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet', default='dotnet')
parser.add_argument('--screenshots', type=Path, help='Capture the actual native window client area as PNG (no OS chrome).')
parser.add_argument('--repeat', type=int, default=2, choices=range(1, 4), help='Separate process launches against the same temporary history.')
parser.add_argument('--seconds', type=int, default=1, choices=range(1, 301), help='Observe real monitoring for this many seconds per process.')
args = parser.parse_args()
repo = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory(prefix='synups-native-') as folder:
    root = Path(folder).resolve()
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    props = ET.SubElement(project, 'PropertyGroup')
    for key, value in dict(TargetFramework='net10.0', OutputType='Exe', Nullable='enable', ImplicitUsings='enable', UseAppHost='false', TreatWarningsAsErrors='true').items():
        ET.SubElement(props, key).text = value
    items = ET.SubElement(project, 'ItemGroup')
    ET.SubElement(items, 'ProjectReference', Include=str(repo / 'synups/Synups.Desktop/Synups.Desktop.csproj'))
    ET.SubElement(items, 'Compile', Include=str(repo / 'eng/native-smoke.cs'))
    path = root / 'Synups.NativeSmoke.csproj'
    ET.ElementTree(project).write(path, encoding='unicode')
    subprocess.run([args.dotnet, 'restore', str(path), '--disable-parallel', '-m:1', '-nr:false'], cwd=repo / 'synups', check=True)
    subprocess.run([args.dotnet, 'build', str(path), '-c', 'Release', '--no-restore', '-m:1', '-nr:false', '-p:UseSharedCompilation=false'], cwd=repo / 'synups', check=True)
    environment = os.environ.copy()
    environment['SYNUPS_SMOKE_SECONDS'] = str(args.seconds)
    if args.screenshots:
        environment['SYNUPS_SCREENSHOT_DIR'] = str(args.screenshots.resolve())
    for iteration in range(args.repeat):
        result = subprocess.run([args.dotnet, str(root / 'bin/Release/net10.0/Synups.NativeSmoke.dll'), str(root / 'configuration.v1.json')], timeout=args.seconds + 45, env=environment)
        if result.returncode:
            raise SystemExit(result.returncode)
        print(f'PROCESS_RESTART_PASS={iteration + 1}', flush=True)
