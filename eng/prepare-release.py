#!/usr/bin/env python3
"""Assemble reviewed CI bytes for publication. No network, rebuild, tag or release operation."""
import argparse
import importlib.util
import json
import shutil
import tempfile
import zipfile
from pathlib import Path


def prepare(evidence, artifacts, output):
    repo = Path(__file__).resolve().parent.parent
    spec = importlib.util.spec_from_file_location('published_smoke', repo / 'eng/published-smoke.py')
    smoke = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(smoke)
    record = json.loads(evidence.read_text())
    revision = record['candidate_source_revision']
    if record['workflow_conclusion'] != 'success':
        raise ValueError('Candidate workflow did not succeed')
    if {a['rid'] for a in record['artifacts']} != {'win-x64', 'osx-arm64', 'linux-x64'} or len(record['artifacts']) != 3:
        raise ValueError('Exactly three reviewed native artifacts are required')
    if output.exists():
        raise ValueError('Output must not exist; previous evidence is never overwritten')
    # Validate all inputs before creating publication assets. Keep original archive bytes.
    with tempfile.TemporaryDirectory(prefix='synups-release-review-') as temporary:
        inspected = []
        for artifact in record['artifacts']:
            rid = artifact['rid']
            outer = artifacts / (rid + '.zip')
            if smoke.digest(outer) != artifact['artifact_sha256']:
                raise ValueError('GitHub artifact digest mismatch: ' + rid)
            target = Path(temporary) / rid
            smoke.unpack(outer, target)
            name = 'Synups-' + rid + ('.zip' if rid == 'win-x64' else '.tar.gz')
            package = target / 'packages' / name
            if smoke.digest(package) != artifact['package_sha256'] or package.with_name(name + '.sha256').read_text().split()[0] != artifact['package_sha256']:
                raise ValueError('Reviewed inner package checksum mismatch: ' + rid)
            unpacked = target / 'unpacked'
            smoke.unpack(package, unpacked)
            root = unpacked / ('Synups-' + rid)
            manifest = json.loads((root / 'ARTIFACT_MANIFEST.json').read_text())
            if manifest['source_revision'] != revision or manifest['rid'] != rid:
                raise ValueError('Mixed source revisions or RID')
            for name, expected in manifest['files_sha256'].items():
                if smoke.digest(root / name) != expected:
                    raise ValueError('Packaged file hash mismatch: ' + name)
            if set(manifest['files_sha256']) | {'ARTIFACT_MANIFEST.json'} != {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}:
                raise ValueError('Unexpected packaged files')
            if (root / 'LICENSE').read_bytes() != (repo / 'LICENSE').read_bytes():
                raise ValueError('Original licence differs')
            summary = json.loads((target / 'published-apphost/launch-summary.json').read_text())
            if summary['source_revision'] != revision or summary['package_sha256'] != artifact['package_sha256']:
                raise ValueError('Runtime evidence differs from the package')
            if len(summary['launches']) != 6 or any(a['status'] != ('ready-for-termination' if a['stage'] == 'interruption' else 'passed') or (a['stage'] != 'interruption' and a['exit_code'] != 0) for a in summary['launches']):
                raise ValueError('Published-apphost stages did not pass')
            inspected.append((rid, target, package))
        output.mkdir(parents=True)
        for rid, target, package in inspected:
            shutil.copyfile(package, output / package.name)
            shutil.copyfile(package.with_name(package.name + '.sha256'), output / (package.name + '.sha256'))
        shutil.copyfile(repo / 'LICENSE', output / 'LICENSE')
        shutil.copyfile(evidence, output / 'VERIFICATION.json')
        for source, name in [('docs/release/INSTALLATION.md', 'INSTALLATION.md'), ('docs/release/PUBLICATION.md', 'PUBLICATION.md'), ('docs/release/SUBMISSION.md', 'SUBMISSION.md'), ('docs/release/RELEASE_BODY.md', 'RELEASE_NOTES.md')]:
            shutil.copyfile(repo / source, output / name)
        (output / 'SOURCE_REVISION.txt').write_text(revision + '\n' + record['workflow_url'] + '\nApplication archives are unchanged reviewed CI bytes. Supplemental documents may be newer.\n')
        with zipfile.ZipFile(output / 'VERIFICATION_EVIDENCE.zip', 'w', compression=zipfile.ZIP_DEFLATED) as archive:
            for rid, target, _ in inspected:
                # Exclude live/isolated databases, user settings and raw artifact containers.
                for folder in ['logs', 'tests', 'real-network', 'published-apphost/reports']:
                    for path in sorted((target / folder).rglob('*')):
                        if path.is_file():
                            archive.write(path, Path(rid) / path.relative_to(target))
                archive.write(target / 'published-apphost/launch-summary.json', rid + '/published-apphost/launch-summary.json')
            archive.write(evidence, 'VERIFICATION.json')
            archive.write(repo / 'docs/evidence/before/wirepeep-upstream.png', 'before/wirepeep-upstream.png')
            archive.write(repo / 'docs/evidence/README.md', 'SCREENSHOT_PROVENANCE.md')
        sums = ''.join(smoke.digest(path) + '  ' + path.name + '\n' for path in sorted(output.iterdir()) if path.is_file())
        (output / 'SHA256SUMS').write_text(sums)
    return revision


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--artifacts', type=Path, required=True, help='Directory containing win-x64.zip, osx-arm64.zip and linux-x64.zip from GitHub Actions')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print('RELEASE_MATERIALS_PREPARED=' + prepare(args.evidence.resolve(), args.artifacts.resolve(), args.output.resolve()))
