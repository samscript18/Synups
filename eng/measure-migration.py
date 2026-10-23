#!/usr/bin/env python3
"""Measure tracked C# and XAML-family physical lines at two frozen Git revisions."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '61ae14391151ca9f209223e62c98fae3df5482b8'
PUBLIC = '6c812fde9a29eeb8cfd6399a2f1a75e333e81e03'


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def count_lines(data):
    lines = data.decode('utf-8-sig').splitlines()
    return {'physical_lines': len(lines), 'nonblank_lines': sum(bool(line.strip()) for line in lines)}


def generated(path):
    return path.lower().endswith(('.designer.cs', '.g.cs', '.g.i.cs'))


def measure(revision, prefixes):
    files = []; excluded = []; third_party = []
    for path in git('ls-tree', '-r', '--name-only', revision, '--', *prefixes).decode().splitlines():
        if not path.endswith(('.cs', '.xaml', '.axaml')):
            continue
        record = {'path': path, 'language': 'C#' if path.endswith('.cs') else 'XAML',
                  **count_lines(git('show', revision+':'+path))}
        if '/Images/' in path or '/Assets/' in path:
            third_party.append(record)
        else:
            (excluded if generated(path) else files).append(record)
    return {'revision': revision, 'prefixes': prefixes, 'files': files, 'excluded_generated': excluded,
            'excluded_artwork': third_party,
            'file_count': len(files),
            **{key: sum(item[key] for item in files) for key in ['physical_lines', 'nonblank_lines']},
            'by_language': {language: {'files': sum(item['language'] == language for item in files),
                **{key: sum(item[key] for item in files if item['language'] == language)
                   for key in ['physical_lines', 'nonblank_lines']}} for language in ['C#', 'XAML']}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = {'method': 'UTF-8-sig splitlines; physical and nonblank lines, including comments; tracked C# and XAML-family only',
              'git_range': BASELINE+'..'+PUBLIC,
              'commits': int(git('rev-list', '--count', BASELINE+'..'+PUBLIC)),
              'original_production': measure(BASELINE, ['src/WirePeep', 'src/WirePeep.Common']),
              'synups_production': measure(PUBLIC, ['synups/Synups.Core', 'synups/Synups.Desktop']),
              'synups_tests': measure(PUBLIC, ['synups/Synups.Tests'])}
    encoded = json.dumps(report, indent=2)+'\n'
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded)
    print(json.dumps({key: ({k: v for k, v in value.items() if k not in ['files', 'excluded_generated', 'excluded_artwork']}
                           if isinstance(value, dict) else value) for key, value in report.items()}, indent=2))


if __name__ == '__main__':
    main()
