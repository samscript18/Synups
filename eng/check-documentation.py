#!/usr/bin/env python3
"""Offline Markdown/source-link and frozen-release claim checks; never builds."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
PUBLIC = '6c812fde9a29eeb8cfd6399a2f1a75e333e81e03'
ALPHA = '3c4462a71cbb90032603cd3eaf4366c2561474d7'
REQUIRED = ['README.md', 'SECURITY.md', 'docs/PARITY.md', 'docs/migration/FEATURE_PARITY.md',
    'docs/migration/FEATURE_PARITY_HISTORY.md', 'docs/migration/ARCHITECTURE.md', 'docs/migration/MIGRATION_LOG.md',
    'docs/migration/CROSS_PLATFORM.md', 'docs/competition/JUDGES_GUIDE.md', 'docs/release/v0.1.1/RELEASE_BODY.md']
CURRENT = ['README.md', 'SECURITY.md', 'docs/PARITY.md', 'docs/migration/FEATURE_PARITY.md',
    'docs/competition/JUDGES_GUIDE.md', 'docs/release/v0.1.1/RELEASE_BODY.md']
ASSETS = ['Synups-win-x64.zip', 'Synups-osx-arm64.tar.gz', 'Synups-linux-x64.tar.gz']


def without_fences(text):
    return re.sub(r'^```[^\n]*\n.*?^```\s*$', '', text, flags=re.M | re.S)


def targets(text):
    text = without_fences(text)
    # Inline/image links and reference definitions. Optional titles are not paths.
    for match in re.finditer(r'!?\[[^\]\n]*\]\((<[^>]+>|[^\s)]+)(?:\s+["\'][^\n]*?["\'])?\)', text):
        yield match.group(1).strip('<>')
    for match in re.finditer(r'^\s*\[[^\]]+\]:\s*(<[^>]+>|\S+)', text, flags=re.M):
        yield match.group(1).strip('<>')
    for match in re.finditer(r'<(?:img|a)\b[^>]*\b(?:src|href)=["\']([^"\']+)', text, flags=re.I):
        yield match.group(1)


def anchors(text):
    found = set(re.findall(r'<a\s+(?:name|id)=["\']([^"\']+)', text, flags=re.I))
    counts = Counter()
    for line in without_fences(text).splitlines():
        match = re.match(r'^#{1,6}\s+(.+?)(?:\s+#+)?$', line)
        if not match:
            continue
        title = re.sub(r'!?(?:\[([^\]]+)\]\([^)]*\))', r'\1', match.group(1))
        title = re.sub(r'<[^>]+>', '', title).lower()
        slug = re.sub(r'[^\w\- ]', '', title).replace(' ', '-')
        index = counts[slug]; counts[slug] += 1
        found.add(slug + ('' if index == 0 else '-' + str(index)))
    return found


def link_errors(root, files):
    errors = []; checked = 0; images = 0; pinned = set()
    for file in files:
        text = file.read_text(encoding='utf-8')
        for target in targets(text):
            checked += 1
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc:
                match = re.match(r'https://github.com/samscript18/Synups/blob/([0-9a-f]{40})/(.+)', target)
                if match:
                    pinned.add((match.group(1), unquote(urlsplit(match.group(2)).path)))
                continue
            path = (file.parent / unquote(parsed.path)).resolve() if parsed.path else file
            if not path.exists():
                errors.append(f'{file.relative_to(root)}: missing {target}')
                continue
            if path.suffix.lower() in ['.png', '.jpg', '.jpeg', '.svg', '.gif']:
                images += 1
            if parsed.fragment and path.suffix.lower() == '.md' and unquote(parsed.fragment) not in anchors(path.read_text(encoding='utf-8')):
                errors.append(f'{file.relative_to(root)}: missing anchor {target}')
    return errors, checked, images, pinned


def claim_errors(root):
    errors = []
    def require(condition, text):
        if not condition:
            errors.append(text)
    for path in REQUIRED:
        require((root/path).is_file(), 'Required documentation missing: ' + path)
    for path in CURRENT:
        text = (root/path).read_text(encoding='utf-8')
        require(path == 'SECURITY.md' or '91.7%' in text, 'Missing current parity: ' + path)
        require(PUBLIC in text, 'Missing public frozen source: ' + path)
        require('v0.1.1' in text and '0.1.1-rc.4' in text, 'Missing public/internal version distinction: ' + path)
    rows = [line for line in (root/'docs/migration/FEATURE_PARITY.md').read_text().splitlines() if re.match(r'^\| L\d\d · ', line)]
    statuses = Counter(line.split('|')[5].strip() for line in rows)
    require(len(rows) == 18 and statuses == {'Complete': 15, 'Partial': 3}, 'Legacy inventory/weights changed')
    for path in ['README.md', 'docs/competition/JUDGES_GUIDE.md', 'docs/release/v0.1.1/RELEASE_BODY.md']:
        text = (root/path).read_text()
        require('not included in the v0.1.1 downloads' in text, 'Alpha/public boundary missing: ' + path)
        require('v0.2.0-alpha.1' in text and 'HARDENING_REPORT.md' in text, 'Alpha status/link missing: ' + path)
        for asset in ASSETS:
            require('https://github.com/samscript18/Synups/releases/download/v0.1.1/' + asset in text, 'Missing current archive URL: ' + path + ' / ' + asset)
        require('Prepared for review; not published.' not in text and 'Proposed tag' not in text, 'Stale current release wording: ' + path)
        require('verbatim release body remain unchanged' not in text, 'Stale unchanged-description claim: ' + path)
        require('Bill Menees' in text, 'Missing original attribution: ' + path)
        download = re.search(r'## Download(?: and run)?\n(.*?)(?=\n## |\Z)', text, re.S)
        if download:
            recommended = re.findall(r'https://github.com/samscript18/Synups/releases/download/([^/]+)/', download.group(1))
            require(all(tag == 'v0.1.1' for tag in recommended), 'Deprecated recommended download: ' + path)
        affirmative_capture = re.search(r'(?:packet capture (?:is|has been) (?:implemented|enabled|verified)|supports? (?:live )?packet capture)', text, re.I)
        require(not affirmative_capture, 'Unsupported capture claim: ' + path)
        require(not re.search(r'(?<!not )verified physical (?:tray|notification)|physical (?:tray|notification)\b[^.\n]*? (?:is|are) (?:verified|certified)', text, re.I), 'Unsupported physical acceptance claim: ' + path)
    require('Copyright (c) 2020 Bill Menees' in (root/'LICENSE').read_text(), 'MIT attribution changed')
    security = (root/'SECURITY.md').read_text()
    require('Raw TCP SYN | Unavailable' in security and 'Local packet capture | Blocked and disabled' in security,
            'Unsupported protocol/capture status changed')
    require('does not authenticate' in security or 'does **not authenticate' in security, 'Missing checksum authenticity boundary')
    require('physical' in security and 'unverified' in security, 'Missing physical verification boundary')
    require('not secure erasure' in security, 'Missing deletion limitation')
    architecture = (root/'docs/migration/ARCHITECTURE.md').read_text()
    require(PUBLIC in architecture and ALPHA in architecture and architecture.count('```mermaid') >= 5, 'Architecture snapshots/diagrams missing')
    for file in [root/p for p in CURRENT]:
        for line in without_fences(file.read_text()).splitlines():
            if re.search(r'100\s*%\s*(?:legacy\s+)?parity', line, re.I):
                require(bool(re.search(r'not|no|isn.t|unverified', line, re.I)), 'Unsupported full-parity claim: ' + str(file.relative_to(root)))
    return errors, dict(statuses)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--release-snapshot', type=Path)
    args = parser.parse_args()
    files = sorted([ROOT/'README.md', ROOT/'SECURITY.md', *ROOT.glob('docs/**/*.md')])
    errors, checked, images, pinned = link_errors(ROOT, files)
    claims, statuses = claim_errors(ROOT); errors += claims
    for commit, path in sorted(pinned):
        if subprocess.run(['git', 'cat-file', '-e', commit + ':' + path], cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode:
            errors.append('Invalid pinned source path: ' + commit + ':' + path)
    # Original code and licence are immutable for this documentation task.
    licence = subprocess.check_output(['git', 'show', PUBLIC+':LICENSE'], cwd=ROOT)
    if (ROOT/'LICENSE').read_bytes() != licence:
        errors.append('Protected original MIT licence bytes changed')
    if args.release_snapshot:
        release = json.loads(args.release_snapshot.read_text())
        if not release.get('body_only_update_verified') or release['body'] != (ROOT/'docs/release/v0.1.1/RELEASE_BODY.md').read_text():
            errors.append('Published description differs from reviewed replacement or lacks verification')
    report = {'passed': not errors, 'documentation_files': len(files), 'link_targets_checked': checked,
        'image_references_checked': images, 'pinned_code_paths_checked': len(pinned),
        'legacy_statuses': statuses, 'legacy_parity': '91.7%', 'public_application': PUBLIC, 'alpha_application': ALPHA,
        'licence_sha256': hashlib.sha256(licence).hexdigest(), 'errors': errors}
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if not errors else 1)


if __name__ == '__main__':
    main()
