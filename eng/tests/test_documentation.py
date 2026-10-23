import importlib.util
import json
import shutil
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


def module(name, file):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).resolve().parents[1]/file)
    loaded = importlib.util.module_from_spec(spec); spec.loader.exec_module(loaded)
    return loaded


docs = module('documentation_checks', 'check-documentation.py')
release = module('release_description', 'audit-release-description.py')


class DocumentationChecksTests(unittest.TestCase):
    def test_broken_image_and_anchor_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); page = root/'README.md'
            page.write_text('# Actual heading\n![capture](missing.png)\n[section](#wrong)\n')
            errors, count, _, _ = docs.link_errors(root, [page])
            self.assertEqual(count, 2); self.assertEqual(len(errors), 2)

    def test_existing_image_reference_and_duplicate_heading_anchors(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); page = root/'README.md'; (root/'actual.png').write_bytes(b'fixture')
            page.write_text('# Same heading\n# Same heading\n![capture][image]\n[second](#same-heading-1)\n[image]: actual.png\n')
            errors, _, images, _ = docs.link_errors(root, [page])
            self.assertEqual(errors, []); self.assertEqual(images, 1)

    def test_fenced_examples_are_not_documentation_links(self):
        text = '```md\n[example](missing.md)\n```\n[actual](real.md)\n'
        self.assertEqual(list(docs.targets(text)), ['real.md'])

    def test_parity_and_release_claims_accept_the_reviewed_documents(self):
        errors, score = docs.claim_errors(docs.ROOT)
        self.assertEqual(errors, []); self.assertEqual(score, {'Complete': 15, 'Partial': 3})

    def test_inflated_parity_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in docs.REQUIRED + ['LICENSE']:
                target = root/name; target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(docs.ROOT/name, target)
            path = root/'docs/migration/FEATURE_PARITY.md'
            path.write_text(path.read_text().replace('| Partial |', '| Complete |', 1))
            self.assertIn('Legacy inventory/weights changed', docs.claim_errors(root)[0])

    def test_stale_unpublished_status_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in docs.REQUIRED + ['LICENSE']:
                target = root/name; target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(docs.ROOT/name, target)
            path = root/'README.md'; path.write_text(path.read_text()+'\nPrepared for review; not published.\n')
            self.assertIn('Stale current release wording: README.md', docs.claim_errors(root)[0])

    def test_external_links_are_not_treated_as_missing_local_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); page = root/'README.md'
            page.write_text('[private reporting](https://github.com/example/security/advisories/new)\n')
            self.assertEqual(docs.link_errors(root, [page])[0], [])


class ReleaseProtectionTests(unittest.TestCase):
    def fixtures(self):
        manifest = json.loads((release.REPO/'docs/release/v0.1.1/ASSET_MANIFEST.json').read_text())
        receipt = json.loads((release.REPO/'docs/evidence/verification/synups-0.1.1-publication.json').read_text())
        expected = manifest['assets'] + [{'name': 'SHA256SUMS', 'bytes': next(a['bytes'] for a in receipt['assets'] if a['name'] == 'SHA256SUMS'), 'sha256': manifest['checksum_file_sha256']}]
        assets = [{'id': i, 'node_id': str(i), 'name': a['name'], 'size': a['bytes'], 'digest': 'sha256:'+a['sha256'],
            'state': 'uploaded', 'content_type': 'application/octet-stream', 'created_at': 'fixture', 'updated_at': 'fixture',
            'browser_download_url': 'https://github.com/samscript18/Synups/releases/download/v0.1.1/'+a['name']} for i, a in enumerate(expected)]
        return {'id': release.RELEASE_ID, 'tag_name': 'v0.1.1', 'name': release.TITLE, 'draft': False, 'prerelease': False,
            'assets': assets, 'body': 'public fixture only', 'created_at': 'fixture', 'published_at': 'fixture', 'updated_at': 'fixture', 'html_url': 'https://github.com/samscript18/Synups/releases/tag/v0.1.1'}

    def api(self, record, tag=None):
        def respond(path, _token, body=None):
            self.assertIsNone(body, 'Preflight must never mutate anything')
            if path == '': return {'permissions': {'push': True}}
            if path == '/releases/tags/v0.1.1': return record
            if path == '/git/ref/tags/v0.1.1': return {'object': {'type': 'commit', 'sha': tag or release.COMMIT}}
            self.fail('Unexpected API endpoint: '+path)
        return respond

    def test_reviewed_asset_set_and_exact_tag_are_required(self):
        with patch.object(release, 'api', side_effect=self.api(self.fixtures())):
            snapshot = release.snapshot('test-only-token')
        self.assertEqual(len(snapshot['invariant']['assets']), 15)
        self.assertEqual(snapshot['invariant']['application_commit'], release.COMMIT)

    def test_changed_tag_is_rejected_before_mutation(self):
        with patch.object(release, 'api', side_effect=self.api(self.fixtures(), '0'*40)):
            with self.assertRaisesRegex(ValueError, 'tag target differs'): release.snapshot('test-only-token')

    def test_changed_asset_hash_is_rejected_before_mutation(self):
        fixture = self.fixtures(); fixture['assets'][0]['digest'] = 'sha256:'+'0'*64
        with patch.object(release, 'api', side_effect=self.api(fixture)):
            with self.assertRaisesRegex(ValueError, 'size/hash/state differs'): release.snapshot('test-only-token')
