#!/usr/bin/env python3
"""Read-only verification of the aligned release upload set; never builds or signs."""
import argparse
import importlib.util
import json
import stat
import tarfile
import tempfile
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
REVISION = '6c812fde9a29eeb8cfd6399a2f1a75e333e81e03'
VERSION = '0.1.1-rc.4'
PACKAGES = {
    'win-x64': ('Synups-win-x64.zip', 'cc02117abc393cc0e6baf39e75aceebf529c8d695e5d61e93a18262b36a9d570'),
    'osx-arm64': ('Synups-osx-arm64.tar.gz', 'fa00e6a06255b4cd116ecb985806a05a02fb4e93db6977e34420a63f0cc31200'),
    'linux-x64': ('Synups-linux-x64.tar.gz', '49387bf6358b8d06478a64091f8780a64f41861f820df8f0ee1b04e17117c757'),
}
spec = importlib.util.spec_from_file_location('synups_archive_tools', REPO / 'eng/published-smoke.py')
archive_tools = importlib.util.module_from_spec(spec)
spec.loader.exec_module(archive_tools)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify(folder):
    review = json.loads((Path(__file__).parent / 'ASSET_MANIFEST.json').read_text())
    require(review['application_revision'] == REVISION and review['internal_binary_version'] == VERSION, 'Wrong review revision/version')
    expected = {a['name']: a for a in review['assets']}
    require(len(expected) == 14, 'Expected14 payload assets')
    require({p.name for p in folder.iterdir()} == set(expected) | {'SHA256SUMS'}, 'Unexpected/missing upload files')
    for name, item in expected.items():
        p = folder / name
        require(p.is_file() and not p.is_symlink(), 'Unsafe asset: ' + name)
        require(p.stat().st_size == item['bytes'] and archive_tools.digest(p) == item['sha256'], 'Asset mismatch: ' + name)
    require(archive_tools.digest(folder / 'SHA256SUMS') == review['checksum_file_sha256'], 'Checksum-list hash mismatch')
    sums = {}
    for line in (folder / 'SHA256SUMS').read_text().splitlines():
        digest, name = line.split(None, 1)
        require(name not in sums, 'Duplicate checksum entry')
        sums[name] = digest
    require(sums == {n: a['sha256'] for n, a in expected.items()}, 'Checksum entries differ from reviewed manifest')
    evidence = json.loads((folder / 'VERIFICATION.json').read_text())
    require(evidence['application_revision'] == REVISION and evidence['version'] == VERSION and evidence['all_jobs_conclusion'] == 'success', 'Evidence source mismatch')
    require((folder / 'SOURCE_REVISION.txt').read_text().splitlines()[0] == REVISION, 'Source text mismatch')
    require((folder / 'LICENSE').read_bytes() == (REPO / 'LICENSE').read_bytes(), 'MIT licence mismatch')
    for rid, (name, digest) in PACKAGES.items():
        package = folder / name
        require(archive_tools.digest(package) == digest, 'Frozen archive mismatch: ' + name)
        require((folder / (name + '.sha256')).read_text().split()[0] == digest, 'Sidecar mismatch: ' + name)
        if rid == 'win-x64':
            with zipfile.ZipFile(package) as z:
                require(z.testzip() is None, 'ZIP CRC error')
        else:
            with tarfile.open(package) as t:
                app = 'Synups-' + rid + ('/Synups.app/Contents/MacOS/Synups' if rid == 'osx-arm64' else '/Synups')
                require(t.getmember(app).mode & 0o111, 'Archive loses executable permissions')
        with tempfile.TemporaryDirectory(prefix='synups-release-review-') as temporary:
            destination = Path(temporary)
            archive_tools.unpack(package, destination)
            root = destination / ('Synups-' + rid)
            manifest = json.loads((root / 'ARTIFACT_MANIFEST.json').read_text())
            require(manifest['source_revision'] == REVISION and manifest['version'] == VERSION and manifest['rid'] == rid, 'Embedded identity mismatch')
            require({p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()} == set(manifest['files_sha256']) | {'ARTIFACT_MANIFEST.json'}, 'Unexpected embedded files')
            for member, expected_hash in manifest['files_sha256'].items():
                require(archive_tools.digest(root / member) == expected_hash, 'Embedded hash mismatch: ' + member)
            require((root / 'LICENSE').read_bytes() == (REPO / 'LICENSE').read_bytes(), 'Embedded MIT mismatch')
            apphost = root / manifest['apphost_relative_path']
            machine = {'win-x64': '0x8664', 'osx-arm64': '0x100000c', 'linux-x64': '0x3e'}[rid]
            require(machine in archive_tools.machine_types(apphost), 'Wrong executable CPU')
            runtime = root / manifest.get('runtime_relative_path', str(Path(manifest['apphost_relative_path']).parent))
            sqlite = runtime / {'win-x64': 'e_sqlite3.dll', 'osx-arm64': 'libe_sqlite3.dylib', 'linux-x64': 'libe_sqlite3.so'}[rid]
            require(machine in archive_tools.machine_types(sqlite), 'Wrong SQLite CPU')
            if rid != 'win-x64':
                require(apphost.stat().st_mode & stat.S_IXUSR, 'Extracted executable mode missing')
            notices = json.loads((root / 'DEPENDENCY_NOTICES.json').read_text())
            require(notices['runtime_packages'] and (root / 'licenses/dotnet/THIRD-PARTY-NOTICES.TXT').is_file(), 'Missing runtime notices')
            for dependency in notices['runtime_packages']:
                for member in dependency['retained_notices']:
                    require((root / member).is_file(), 'Missing dependency licence')
        print('VERIFIED', rid, digest)
    with zipfile.ZipFile(folder / 'VERIFICATION_EVIDENCE.zip') as z:
        require(z.testzip() is None, 'Evidence ZIP CRC error')
    print('GO:15 reviewed assets; exact frozen rc.4 archives and supplemental hashes match. Publication still requires authorization.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    verify(parser.parse_args().folder.resolve())
