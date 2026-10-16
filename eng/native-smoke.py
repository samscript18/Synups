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
args = parser.parse_args()
repo = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory(prefix='synups-native-') as folder:
    root = Path(folder).resolve()
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    props = ET.SubElement(project, 'PropertyGroup')
    for key, value in dict(TargetFramework='net10.0', OutputType='Exe', Nullable='enable', ImplicitUsings='enable', UseAppHost='false').items():
        ET.SubElement(props, key).text = value
    items = ET.SubElement(project, 'ItemGroup')
    ET.SubElement(items, 'ProjectReference', Include=str(repo / 'synups/Synups.Desktop/Synups.Desktop.csproj'))
    ET.SubElement(items, 'Compile', Include=str(repo / 'eng/native-smoke.cs'))
    path = root / 'Synups.NativeSmoke.csproj'
    ET.ElementTree(project).write(path, encoding='unicode')
    subprocess.run([args.dotnet, 'restore', str(path), '--disable-parallel', '-m:1', '-nr:false'], cwd=repo / 'synups', check=True)
    subprocess.run([args.dotnet, 'build', str(path), '-c', 'Release', '--no-restore', '-m:1', '-nr:false', '-p:UseSharedCompilation=false'], cwd=repo / 'synups', check=True)
    result = subprocess.run([args.dotnet, str(root / 'bin/Release/net10.0/Synups.NativeSmoke.dll'), str(root / 'configuration.v1.json')], timeout=45)
    raise SystemExit(result.returncode)
