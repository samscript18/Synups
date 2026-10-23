import importlib.util
import json
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('migration_measurements', Path(__file__).resolve().parents[1]/'measure-migration.py')
measure = importlib.util.module_from_spec(spec)
spec.loader.exec_module(measure)


class MigrationMeasurementsTests(unittest.TestCase):
    def test_bom_crlf_comments_and_blank_lines_have_documented_meaning(self):
        data = '\ufeff// comment\r\n\r\nclass Example {}\r\n'.encode('utf-8')
        self.assertEqual(measure.count_lines(data), {'physical_lines': 3, 'nonblank_lines': 2})
        self.assertEqual(measure.count_lines(data), measure.count_lines(data.replace(b'\r\n', b'\n')))

    def test_retained_measurements_match_frozen_git_blobs(self):
        report = json.loads((measure.ROOT/'docs/evidence/verification/2026-10-23-readme-presentation/migration-measurements.json').read_text())
        self.assertEqual(int(measure.git('rev-list', '--count', report['git_range'])), report['commits'])
        for key in ['original_production', 'synups_production', 'synups_tests']:
            retained = report[key]
            self.assertEqual(measure.measure(retained['revision'], retained['prefixes']), retained)
        self.assertEqual(len(report['original_production']['excluded_generated']), 2)
        self.assertEqual(len(report['original_production']['excluded_artwork']), 2)

    def test_every_readme_image_is_unique_and_matches_provenance(self):
        import hashlib
        import re
        root = measure.ROOT
        paths = re.findall(r'!\[[^\]]+\]\(([^)]+)\)', (root/'README.md').read_text())
        manifest = json.loads((root/'docs/evidence/verification/2026-10-23-readme-presentation/screenshots.json').read_text())
        self.assertEqual(len(paths), 5)
        self.assertEqual(len(set(paths)), 5)
        self.assertEqual(set(paths), {item['path'] for item in manifest})
        for item in manifest:
            self.assertEqual(hashlib.sha256((root/item['path']).read_bytes()).hexdigest(), item['sha256'])
