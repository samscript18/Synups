#!/usr/bin/env python3
"""Real loopback diagnostics, copied-history upgrade, export and separate-process restart.

Creates a NEW local-only output directory. Never modifies the supplied previous-data
directory. Optional --query-public-dns authorizes one example.com A query against
the configured resolver; otherwise there are no public diagnostic targets.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import sqlite3
import subprocess
import tempfile
import xml.etree.ElementTree as ET


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def source_files(root):
    database = root / 'history.v1.db'
    if not database.is_file() or database.is_symlink():
        raise RuntimeError('Previous-data must contain a regular closed history.v1.db')
    for suffix in ('-wal', '-shm', '-journal'):
        if (root / ('history.v1.db' + suffix)).exists():
            raise RuntimeError('Refusing SQLite sidecars: close the source application cleanly first')
    files = [database]
    for name in ('configuration.v1.json', 'preferences.v1.json'):
        path = root / name
        if path.exists():
            if not path.is_file() or path.is_symlink():
                raise RuntimeError('Refusing nonregular source file: ' + name)
            files.append(path)
    return files


def run_logged(command, log, cwd, timeout=240):
    print(' '.join(str(argument) for argument in command), flush=True)
    with log.open('w', encoding='utf-8') as stream:
        result = subprocess.run(command, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT,
                                text=True, timeout=timeout, check=False)
    print('Exit:', result.returncode, '| local-only stdout/stderr:', log, flush=True)
    return result.returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--previous-data', type=Path)
    parser.add_argument('--query-public-dns', action='store_true')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    output = args.output.absolute()
    if output.exists() or output.is_symlink():
        parser.error('--output must be a new directory; existing evidence is never removed')
    source = args.previous_data.resolve(strict=True) if args.previous_data else None
    if source and (output == source or source in output.parents):
        parser.error('Output must be outside the source data directory')
    originals = source_files(source) if source else []
    original_hashes = {path.name: sha(path) for path in originals}
    output.mkdir(parents=True, mode=0o700)
    data = output / 'data'
    data.mkdir(mode=0o700)
    (output / 'LOCAL_ONLY.txt').write_text(
        'LOCAL ONLY: copied history/configuration/preferences, raw SQLite, checkpoint and console logs may contain private data.\n'
        'Only the explicitly redacted evidence directories and summary reports are intended for separate review.\n'
        'This harness never changes source data or host network configuration.\n', encoding='utf-8')
    for path in originals:
        shutil.copyfile(path, data / path.name)
        os.chmod(data / path.name, 0o600)
        if sha(data / path.name) != original_hashes[path.name]:
            raise RuntimeError('Source changed while copying: ' + path.name)
    # Verify the copied, closed database without running recovery against the source.
    if originals:
        with sqlite3.connect((data / 'history.v1.db').as_uri() + '?mode=ro&immutable=1', uri=True) as connection:
            if connection.execute('PRAGMA quick_check').fetchone()[0] != 'ok':
                raise RuntimeError('Copied previous database failed integrity check')
    logs = output / 'logs'
    logs.mkdir(mode=0o700)
    sdk = subprocess.check_output([args.dotnet, '--version'], cwd=repo / 'synups', text=True).strip()
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo, text=True).strip()
    dirty = bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=repo, text=True).strip())
    source_digest = hashlib.sha256()
    for path in sorted((repo / 'synups' / 'Synups.Core').rglob('*.cs')):
        if 'obj' not in path.parts and 'bin' not in path.parts:
            source_digest.update(str(path.relative_to(repo)).encode())
            source_digest.update(path.read_bytes())
    metadata = {'sdk': sdk, 'source_git_head': revision, 'source_has_uncommitted_changes': dirty,
                'core_cs_sha256': source_digest.hexdigest(), 'harness_cs_sha256': sha(repo / 'eng/diagnostics-smoke.cs'),
                'host_system': platform.system(), 'host_machine': platform.machine(),
                'previous_data_supplied': bool(source), 'copied_source_file_hashes': original_hashes,
                'public_dns_explicitly_requested': args.query_public_dns,
                'claim_scope': 'Real Core diagnostics; two separate application processes; not published apphost or physical UI'}
    (output / 'environment.json').write_text(json.dumps(metadata, indent=2) + '\n', encoding='utf-8')
    statuses = {}
    try:
        with tempfile.TemporaryDirectory(prefix='synups-diagnostics-harness-') as temporary:
            root = Path(temporary)
            project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
            props = ET.SubElement(project, 'PropertyGroup')
            for key, value in dict(TargetFramework='net10.0', OutputType='Exe', Nullable='enable',
                                   ImplicitUsings='enable', TreatWarningsAsErrors='true',
                                   EnableDefaultCompileItems='false').items():
                ET.SubElement(props, key).text = value
            items = ET.SubElement(project, 'ItemGroup')
            ET.SubElement(items, 'ProjectReference', Include=str(repo / 'synups/Synups.Core/Synups.Core.csproj'))
            ET.SubElement(items, 'Compile', Include=str(repo / 'eng/diagnostics-smoke.cs'))
            project_path = root / 'Synups.DiagnosticsSmoke.csproj'
            ET.ElementTree(project).write(project_path, encoding='unicode')
            (output / 'harness-project.xml').write_text(ET.tostring(project, encoding='unicode'), encoding='utf-8')
            statuses['restore'] = run_logged([args.dotnet, 'restore', str(project_path), '--disable-parallel', '-m:1', '-nr:false'], logs / 'restore.txt', repo / 'synups')
            if statuses['restore']:
                raise RuntimeError('Harness restore failed; inspect local-only log')
            statuses['build'] = run_logged([args.dotnet, 'build', str(project_path), '-c', 'Release', '--no-restore', '-m:1', '-nr:false', '-p:UseSharedCompilation=false'], logs / 'build.txt', repo / 'synups')
            if statuses['build']:
                raise RuntimeError('Harness build failed; inspect local-only log')
            assembly = root / 'bin/Release/net10.0/Synups.DiagnosticsSmoke.dll'
            observe = [args.dotnet, str(assembly), str(output), 'observe']
            if args.query_public_dns:
                observe.append('--query-public-dns')
            statuses['observe'] = run_logged(observe, logs / 'observe.txt', repo / 'synups', timeout=120)
            # Preserve and inspect partial observe evidence even if native permissions caused failure.
            if (output / 'checkpoint.json').exists():
                for cycle in range(1, 4):
                    statuses['reload_' + str(cycle)] = run_logged([args.dotnet, str(assembly), str(output), 'reload'], logs / ('reload-' + str(cycle) + '.txt'), repo / 'synups', timeout=60)
                statuses['reload'] = max(statuses['reload_' + str(cycle)] for cycle in range(1, 4))
            else:
                statuses['reload'] = None
        passed = statuses.get('observe') == 0 and statuses.get('reload') == 0
        report = output / 'report.json'
        if report.exists():
            summary = json.loads(report.read_text(encoding='utf-8'))
            summary['EngineeringEnvironment'] = metadata
            summary['ProcessExitCodes'] = statuses
            summary['ObserveAndSeparateReloadPassed'] = passed
            report.write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
        if not passed:
            raise RuntimeError('Real diagnostics/restart gate failed; partial evidence remains at ' + str(output))
    finally:
        current = {path.name: sha(path) for path in originals}
        source_unchanged = current == original_hashes
        if source and {path.name for path in source_files(source)} != set(original_hashes):
            source_unchanged = False
        (output / 'source-preservation.json').write_text(json.dumps({'source_files_unchanged': source_unchanged,
            'before_sha256': original_hashes, 'after_sha256': current, 'process_exit_codes': statuses}, indent=2) + '\n', encoding='utf-8')
        if not source_unchanged:
            raise RuntimeError('Source files changed during verification; source application may not have been closed')
    print('REAL_DIAGNOSTICS_PERSISTENCE_BASELINE_EXPORT_RESTART=PASS', flush=True)
    print('Redacted summary:', output / 'report.json', flush=True)


if __name__ == '__main__':
    main()
