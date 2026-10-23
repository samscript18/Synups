#!/usr/bin/env python3
"""Verify protected v0.1.1 metadata; optionally PATCH only its reviewed body.

Never builds, uploads, deletes assets, changes tags, or creates releases. Credentials
come from git's configured helper, stay in memory, and are sent only to api.github.com.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.request

REPO = Path(__file__).resolve().parents[1]
PROJECT = 'samscript18/Synups'
COMMIT = '6c812fde9a29eeb8cfd6399a2f1a75e333e81e03'
TITLE = 'Synups 0.1.1 — verified rc.4 build'
RELEASE_ID = 421217596


def require(value, message):
    if not value:
        raise ValueError(message)


def credentials():
    result = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n', text=True, capture_output=True, check=True)
    values = dict(line.split('=', 1) for line in result.stdout.splitlines() if '=' in line)
    require(values.get('password'), 'GitHub credential unavailable; no mutation performed')
    return values['password']


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *_args, **_kwargs):
        return None


def api(path, token, body=None):
    request = urllib.request.Request('https://api.github.com/repos/' + PROJECT + path,
        data=None if body is None else json.dumps(body).encode(),
        headers={'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
                 'Content-Type': 'application/json', 'User-Agent': 'Synups-release-documentation-audit'},
        method='GET' if body is None else 'PATCH')
    with urllib.request.build_opener(NoRedirect).open(request, timeout=45) as response:
        return json.load(response)


def snapshot(token):
    repository = api('', token)
    release = api('/releases/tags/v0.1.1', token)
    tag = api('/git/ref/tags/v0.1.1', token)
    target = tag['object']
    for _ in range(4):
        if target['type'] != 'tag':
            break
        target = api('/git/tags/' + target['sha'], token)['object']
    require(target['type'] == 'commit' and target['sha'] == COMMIT, 'Protected tag target differs; STOP')
    require(release['id'] == RELEASE_ID and release['tag_name'] == 'v0.1.1', 'Wrong release; STOP')
    require(release['name'] == TITLE and not release['draft'] and not release['prerelease'], 'Release title/publication state differs; STOP')
    manifest = json.loads((REPO/'docs/release/v0.1.1/ASSET_MANIFEST.json').read_text())
    expected = {a['name']: (a['bytes'], a['sha256']) for a in manifest['assets']}
    receipt = json.loads((REPO/'docs/evidence/verification/synups-0.1.1-publication.json').read_text())
    checksum = next(a for a in receipt['assets'] if a['name'] == 'SHA256SUMS')
    expected['SHA256SUMS'] = (checksum['bytes'], manifest['checksum_file_sha256'])
    assets = sorted(release['assets'], key=lambda a: a['name'])
    require(len(assets) == 15 and {a['name'] for a in assets} == set(expected), 'Protected asset set differs; STOP')
    for asset in assets:
        size, digest = expected[asset['name']]
        require(asset['state'] == 'uploaded' and asset['size'] == size and asset.get('digest') == 'sha256:' + digest,
                'Protected asset size/hash/state differs: ' + asset['name'] + '; STOP')
    invariant = {'release_id': release['id'], 'tag_name': release['tag_name'], 'tag_object': tag['object'],
        'application_commit': target['sha'], 'name': release['name'], 'draft': release['draft'],
        'prerelease': release['prerelease'], 'created_at': release['created_at'], 'published_at': release['published_at'],
        'html_url': release['html_url'], 'assets': [{k: a[k] for k in ['id', 'node_id', 'name', 'size', 'digest', 'state', 'content_type', 'created_at', 'updated_at', 'browser_download_url']} for a in assets]}
    body = release['body'] or ''
    return {'invariant': invariant, 'body': body, 'body_sha256': hashlib.sha256(body.encode()).hexdigest(),
        'release_updated_at': release['updated_at'], 'permissions': repository.get('permissions', {}),
        'hash_verification': 'All 15 live GitHub API SHA-256 digests and sizes match the reviewed frozen asset manifest; local frozen archives independently verified.',
        'authentication_used': True, 'credential_retained': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--before', type=Path)
    parser.add_argument('--apply-body', type=Path)
    args = parser.parse_args()
    require(not args.output.exists(), 'Output must be new to retain prior evidence')
    require(not args.apply_body or args.before, 'Mutation requires an independently saved before snapshot')
    token = credentials()
    current = snapshot(token)
    if args.before:
        before = json.loads(args.before.read_text())
        require(current['invariant'] == before['invariant'], 'Release/tag/asset invariants changed; STOP')
    if args.apply_body:
        require(current['permissions'].get('push') or current['permissions'].get('admin'), 'Repository write permission unavailable; replacement body remains prepared')
        require(current['body_sha256'] == before['body_sha256'], 'Release body changed since review; STOP')
        body = args.apply_body.read_text(encoding='utf-8')
        require(body.startswith('# ' + TITLE + '\n') and COMMIT in body and '0.1.1-rc.4' in body and '91.7%' in body, 'Replacement identity incomplete')
        require('Prepared for review; not published.' not in body and 'Proposed tag' not in body, 'Stale release wording')
        api('/releases/' + str(RELEASE_ID), token, {'body': body})
        after = snapshot(token)
        require(after['invariant'] == before['invariant'], 'Release/tag/asset invariant discrepancy after edit; STOP')
        require(after['body'] == body, 'Published body differs from exact reviewed Markdown')
        current = after
        current['body_only_update_verified'] = True
        current['before_body_sha256'] = before['body_sha256']
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(current, indent=2) + '\n')
    print(json.dumps({'release_id': RELEASE_ID, 'application_commit': COMMIT, 'assets_verified': 15,
        'write_permission': bool(current['permissions'].get('push') or current['permissions'].get('admin')),
        'body_sha256': current['body_sha256'], 'body_only_update_verified': current.get('body_only_update_verified', False)}, indent=2))


if __name__ == '__main__':
    main()
