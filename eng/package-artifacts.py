#!/usr/bin/env python3
"""Prepare portable Synups archives, an ad-hoc macOS app bundle, complete notices and per-file integrity metadata."""
import argparse,hashlib,importlib.util,json,os,plistlib,shutil,subprocess,tarfile,tempfile,zipfile
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source',type=Path,required=True);parser.add_argument('--rid',choices=['win-x64','osx-arm64','linux-x64'],required=True)
parser.add_argument('--dotnet',default='dotnet');parser.add_argument('--output',type=Path,required=True);parser.add_argument('--revision',required=True)
parser.add_argument('--nuget-cache',type=Path,default=Path(os.environ.get('NUGET_PACKAGES',str(Path.home()/'.nuget/packages'))))
args=parser.parse_args();source=args.source.resolve();output=args.output.resolve();output.mkdir(parents=True,exist_ok=True)
repo=Path(__file__).resolve().parent.parent
original='Synups.Desktop.exe' if args.rid=='win-x64' else 'Synups.Desktop'
executable='Synups.exe' if args.rid=='win-x64' else 'Synups'
runtime={'win-x64':'coreclr.dll','osx-arm64':'libcoreclr.dylib','linux-x64':'libcoreclr.so'}[args.rid]
sqlite={'win-x64':'e_sqlite3.dll','osx-arm64':'libe_sqlite3.dylib','linux-x64':'libe_sqlite3.so'}[args.rid]
for required in [original,'Synups.Desktop.dll','Synups.Desktop.deps.json','Synups.Desktop.runtimeconfig.json',runtime,sqlite,'LICENSE']:
 if not (source/required).is_file():parser.error('Missing published dependency: '+required)
for path in source.rglob('*'):
 if path.is_symlink():parser.error('Review published symlink before packaging: '+path.relative_to(source).as_posix())
 if path.is_file() and any(s in path.name.lower() for s in ['xunit','testhost','synups.tests','nativesmoke','engineendurance','avalonia.headless','avalonia.diagnostics']):parser.error('Unexpected test/development runtime asset: '+path.name)
if args.rid!='win-x64' and not (source/original).stat().st_mode&0o111:parser.error('Published apphost lacks executable permission')
dependencies=json.loads((source/'Synups.Desktop.deps.json').read_text())
app_version=next(n.split('/',1)[1] for n in dependencies['libraries'] if n.startswith('Synups.Desktop/'))
runtime_package=next((n.removeprefix('runtimepack.') for n in dependencies['libraries'] if n.startswith('runtimepack.Microsoft.NETCore.App.Runtime.')),None)
if runtime_package is None:parser.error('No self-contained runtime pack in dependency manifest')
package_id,version=runtime_package.lower().split('/');runtime_cache=args.nuget_cache/package_id/version
notice_module=importlib.util.spec_from_file_location('synups_dependency_notices',repo/'eng/dependency-notices.py');notices=importlib.util.module_from_spec(notice_module);notice_module.loader.exec_module(notices)
name='Synups-'+args.rid
with tempfile.TemporaryDirectory(prefix='synups-package-') as temporary:
 root=Path(temporary)/name
 shutil.copytree(source,root,ignore=shutil.ignore_patterns('*.pdb','ARTIFACT_MANIFEST.json'))
 (root/original).rename(root/executable)
 dotnet=root/'licenses/dotnet';dotnet.mkdir(parents=True,exist_ok=True)
 for notice in ['LICENSE.TXT','THIRD-PARTY-NOTICES.TXT']:
  if not (runtime_cache/notice).is_file():parser.error('Restored runtime notice missing: '+notice)
  shutil.copyfile(runtime_cache/notice,dotnet/notice)
 notice_count=notices.collect(root,dependencies,args.nuget_cache)
 shutil.copyfile(repo/'synups/Synups.Desktop/Assets/Synups.png',root/'Synups.png')
 signing='No publisher signature; not an installer.'
 if args.rid=='osx-arm64':
  if os.uname().sysname!='Darwin':parser.error('macOS bundles must be assembled/ad-hoc signed and verified on macOS')
  app=Path(temporary)/'Synups.app';macos=app/'Contents/MacOS';resources=app/'Contents/Resources';resources.mkdir(parents=True);macos.mkdir()
  runtime_files=resources/'runtime';root.rename(runtime_files);root.mkdir()
  for copy in ['LICENSE','README.md','THIRD_PARTY_NOTICES.md','DEPENDENCY_NOTICES.json','Synups.png']:
   if (runtime_files/copy).exists():
    shutil.copyfile(runtime_files/copy,root/copy)
    shutil.move(str(runtime_files/copy),str(resources/copy))
  shutil.move(str(runtime_files/'licenses'),str(resources/'licenses'))
  shutil.copytree(resources/'licenses',root/'licenses')
  shutil.copyfile(repo/'eng/packaging/Synups.icns',resources/'Synups.icns')
  # Keep managed PE assemblies as sealed resources. Deep-signing PE files in
  # MacOS creates xattr signatures that ordinary portable archives cannot retain.
  (runtime_files/executable).unlink() # Replaced by the SDK-bound distribution apphost below.
  for managed in runtime_files.glob('*.dll'):managed.chmod(0o644)
  subprocess.run([args.dotnet,'msbuild',str(repo/'eng/packaging/BundleHost.proj'),'-t:Build','-nologo',
      '-p:BundleHost='+str(macos/executable),'-p:BundleAssembly='+str(runtime_files/'Synups.Desktop.dll')],check=True,cwd=repo/'synups')
  for native in runtime_files.glob('*.dylib'):
   subprocess.run(['codesign','--force','--sign','-','--timestamp=none',str(native)],check=True)
  info=dict(CFBundleName='Synups',CFBundleDisplayName='Synups',CFBundleIdentifier='io.github.samscript18.synups',CFBundleExecutable='Synups',CFBundleIconFile='Synups.icns',CFBundlePackageType='APPL',CFBundleShortVersionString=app_version.split('-',1)[0],CFBundleVersion='1',NSHighResolutionCapable=True,NSSupportsAutomaticGraphicsSwitching=True)
  (app/'Contents/Info.plist').write_bytes(plistlib.dumps(info))
  subprocess.run(['codesign','--force','--deep','--sign','-','--timestamp=none',str(app)],check=True)
  subprocess.run(['codesign','--verify','--deep','--strict',str(app)],check=True)
  shutil.move(str(app),str(root/'Synups.app'))
  apphost='Synups.app/Contents/MacOS/Synups'
  signing='Ad-hoc signed for bundle integrity only; no Developer ID identity, no notarization, no trusted publisher claim.'
  instructions='Move Synups.app to a local folder such as Applications, then open it. For terminal diagnostics run ./Synups.app/Contents/MacOS/Synups.\nGatekeeper can block an unnotarized download. No security bypass or signed-publisher claim is provided.\n'
 else:
  apphost=executable
  instructions=('Extract the whole ZIP, then open Synups.exe. SmartScreen/publisher policy can warn because this is an unsigned portable application.\n' if args.rid=='win-x64' else
   'Extract with tar -xzf Synups-linux-x64.tar.gz, then run ./Synups.\nA graphical session and X11/font/graphics libraries are required; see INSTALLATION.md. Do not run as root.\n')
  if args.rid=='linux-x64':
   (root/'Synups.desktop.example').write_text('[Desktop Entry]\nType=Application\nName=Synups\nComment=Local network reliability monitor\nExec=/ABSOLUTE/PATH/TO/Synups\nIcon=/ABSOLUTE/PATH/TO/Synups.png\nTerminal=false\nCategories=Network;Monitor;\nStartupWMClass=Synups\n')
 if (repo/'docs/release/INSTALLATION.md').exists():shutil.copyfile(repo/'docs/release/INSTALLATION.md',root/'INSTALLATION.md')
 if (repo/'docs/release/RELEASE_NOTES.md').exists():shutil.copyfile(repo/'docs/release/RELEASE_NOTES.md',root/'RELEASE_NOTES.md')
 (root/'RUNNING.txt').write_text(f'Synups {app_version} candidate — {args.rid}\nSource: {args.revision}\n\n'+instructions+'\nThe .NET runtime is included. Keep every extracted runtime/native/SQLite file together.\nMonitoring is stopped by default; configure targets and explicitly start, unless you enable the saved launch preference.\nSettings/history use per-user LocalApplicationData/Synups. --data-directory selects a separate local workspace.\nUse --help for an explicit isolated real-loopback installation check. This is not physical OS input/accessibility certification.\n'+signing+'\nNo release or competition submission is implied by this verification artifact.\nhttps://github.com/samscript18/Synups\n')
 files={p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(root.rglob('*')) if p.is_file()}
 runtime_relative='Synups.app/Contents/Resources/runtime' if args.rid=='osx-arm64' else '.'
 manifest=dict(version=app_version,rid=args.rid,source_revision=args.revision,apphost_relative_path=apphost,runtime_relative_path=runtime_relative,installation_check_protocol=1,signing=signing,runtime_libraries=list(dependencies['libraries']),runtime_package_notice_count=notice_count,files_sha256=files)
 (root/'ARTIFACT_MANIFEST.json').write_text(json.dumps(manifest,indent=2)+'\n')
 archive=output/(name+('.zip' if args.rid=='win-x64' else '.tar.gz'))
 if args.rid=='win-x64':
  with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED) as package:
   for path in sorted(root.rglob('*')):
    if path.is_file():package.write(path,Path(name)/path.relative_to(root))
  with zipfile.ZipFile(archive) as package:
   if package.testzip() is not None:raise RuntimeError('Archive CRC failed')
   for relative,expected in files.items():
    if hashlib.sha256(package.read(name+'/'+relative)).hexdigest()!=expected:raise RuntimeError('Archive content mismatch: '+relative)
 else:
  with tarfile.open(archive,'w:gz') as package:package.add(root,arcname=name)
  with tarfile.open(archive) as package:
   if not package.getmember(name+'/'+apphost).mode&0o111:raise RuntimeError('Unix executable permission lost')
   for relative,expected in files.items():
    with package.extractfile(name+'/'+relative) as data:
     if hashlib.sha256(data.read()).hexdigest()!=expected:raise RuntimeError('Archive content mismatch: '+relative)
 if args.rid=='osx-arm64':
  extracted=Path(temporary)/'post-archive-check'
  check_module=importlib.util.spec_from_file_location('synups_archive_check',repo/'eng/published-smoke.py');archive_check=importlib.util.module_from_spec(check_module);check_module.loader.exec_module(archive_check)
  archive_check.unpack(archive,extracted)
  subprocess.run(['codesign','--verify','--deep','--strict',str(extracted/name/'Synups.app')],check=True)
 with archive.open('rb') as data:
  sha=hashlib.sha256()
  for block in iter(lambda:data.read(1024*1024),b''):sha.update(block)
 archive.with_name(archive.name+'.sha256').write_text(sha.hexdigest()+'  '+archive.name+'\n')
 print('PACKAGED='+str(archive)+'; SHA256='+sha.hexdigest()+'; FILES='+str(len(files))+'; RUNTIME_PACKAGE_NOTICES='+str(notice_count))
