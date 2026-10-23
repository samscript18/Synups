#!/usr/bin/env python3
"""Bounded, explicit real-loopback endurance of production Core; no fake probe or installed-data access."""
import argparse, os, subprocess, tempfile, xml.etree.ElementTree as ET
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet',default='dotnet');p.add_argument('--seconds',type=int,default=7200);p.add_argument('--output',type=Path,required=True)
a=p.parse_args()
if not 10 <= a.seconds <= 28800:p.error('seconds must be 10–28800')
repo=Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory(prefix='synups-endurance-') as temp:
 root=Path(temp).resolve();project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');props=ET.SubElement(project,'PropertyGroup')
 for key,value in dict(TargetFramework='net10.0',OutputType='Exe',Nullable='enable',ImplicitUsings='enable',TreatWarningsAsErrors='true',UseAppHost='false').items():ET.SubElement(props,key).text=value
 items=ET.SubElement(project,'ItemGroup');ET.SubElement(items,'ProjectReference',Include=str(repo/'synups/Synups.Core/Synups.Core.csproj'));ET.SubElement(items,'Compile',Include=str(repo/'eng/engine-endurance.cs'))
 path=root/'Synups.EngineEndurance.csproj';ET.ElementTree(project).write(path,encoding='unicode')
 subprocess.run([a.dotnet,'build',str(path),'-c','Release','-m:1','-nr:false','-p:UseSharedCompilation=false'],cwd=repo/'synups',check=True)
 result=subprocess.run([a.dotnet,str(root/'bin/Release/net10.0/Synups.EngineEndurance.dll'),str(a.output.resolve()),str(a.seconds)],timeout=a.seconds+120)
 raise SystemExit(result.returncode)
