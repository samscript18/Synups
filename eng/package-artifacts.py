#!/usr/bin/env python3
"""Package a published build; Unix tar preserves executable permissions through CI upload."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tarfile
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=Path, required=True)
parser.add_argument('--rid', choices=['win-x64', 'osx-arm64', 'linux-x64'], required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--revision', help='Source commit being packaged (set by CI).')
parser.add_argument('--nuget-cache', type=Path, default=Path(os.environ.get('NUGET_PACKAGES', str(Path.home() / '.nuget/packages'))))
args = parser.parse_args()
source = args.source.resolve()
executable = 'Synups.Desktop.exe' if args.rid == 'win-x64' else 'Synups.Desktop'
runtime = {'win-x64': 'coreclr.dll', 'osx-arm64': 'libcoreclr.dylib', 'linux-x64': 'libcoreclr.so'}[args.rid]
sqlite = {'win-x64': 'e_sqlite3.dll', 'osx-arm64': 'libe_sqlite3.dylib', 'linux-x64': 'libe_sqlite3.so'}[args.rid]
for required in [executable, 'Synups.Desktop.dll', 'Synups.Desktop.deps.json', 'Synups.Desktop.runtimeconfig.json', runtime, sqlite, 'LICENSE']:
    if not (source / required).is_file():
        parser.error(f'Missing published file: {required}')
if args.rid != 'win-x64' and not (source / executable).stat().st_mode & 0o111:
    parser.error('Published Unix executable has no executable permission.')
args.output.mkdir(parents=True, exist_ok=True)
name = f'Synups-{args.rid}'
dependencies = json.loads((source / 'Synups.Desktop.deps.json').read_text())
runtime_package = next((name.removeprefix('runtimepack.') for name in dependencies['libraries'] if name.startswith('runtimepack.Microsoft.NETCore.App.Runtime.')), None)
if runtime_package is None:
    parser.error('Expected a self-contained runtime pack in the dependency manifest.')
package_id, version = runtime_package.lower().split('/')
package_root = args.nuget_cache / package_id / version
notice_folder = source / 'licenses/dotnet'
notice_folder.mkdir(parents=True, exist_ok=True)
for notice in ['LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT']:
    if not (package_root / notice).is_file():
        parser.error(f'Runtime licence unavailable in restored cache: {notice}')
    shutil.copyfile(package_root / notice, notice_folder / notice)
(source / 'RUNNING.txt').write_text(
    f'Synups verification build ({args.rid})\n'
    f'Extract the whole archive. Run {executable} from the extracted directory.\n'
    'The .NET runtime is included. Native OS/display libraries are still required.\n'
    'Monitoring is stopped by default; configure targets and explicitly start it.\n'
    'These outputs are unsigned verification builds, not installers or a published release.\n'
    'Windows/Linux runtime validation is pending. Source and current limitations:\n'
    'https://github.com/samscript18/Synups\n')
manifest_path = source / 'ARTIFACT_MANIFEST.json'
files = {}
for path in sorted(source.rglob('*')):
    if path.is_symlink():
        parser.error(f'Review symbolic link before packaging: {path.relative_to(source)}')
    if path.is_file() and path != manifest_path:
        files[path.relative_to(source).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
manifest_path.write_text(json.dumps({'rid': args.rid, 'source_revision': args.revision,
    'runtime_libraries': list(dependencies['libraries']), 'files_sha256': files}, indent=2) + '\n')
if args.rid == 'win-x64':
    archive = args.output / f'{name}.zip'
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED) as package:
        for path in sorted(source.rglob('*')):
            if path.is_file():
                package.write(path, Path(name) / path.relative_to(source))
    with zipfile.ZipFile(archive) as package:
        if package.testzip() is not None:
            raise RuntimeError('Archive integrity check failed.')
        for relative, expected in files.items():
            if hashlib.sha256(package.read(f'{name}/{relative}')).hexdigest() != expected:
                raise RuntimeError(f'Archive content mismatch: {relative}')
else:
    archive = args.output / f'{name}.tar.gz'
    with tarfile.open(archive, 'w:gz') as package:
        package.add(source, arcname=name)
    with tarfile.open(archive) as package:
        if not package.getmember(f'{name}/{executable}').mode & 0o111:
            raise RuntimeError('Archive lost executable permissions.')
        for relative, expected in files.items():
            with package.extractfile(f'{name}/{relative}') as archived:
                if hashlib.sha256(archived.read()).hexdigest() != expected:
                    raise RuntimeError(f'Archive content mismatch: {relative}')
with archive.open('rb') as data:
    digest = hashlib.sha256()
    for chunk in iter(lambda: data.read(1024 * 1024), b''):
        digest.update(chunk)
    checksum = digest.hexdigest()
archive.with_name(archive.name + '.sha256').write_text(f'{checksum}  {archive.name}\n')
print(f'PACKAGED={archive}; SHA256={checksum}')
