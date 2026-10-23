import importlib.util
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('path_smoke', Path(__file__).resolve().parents[1] / 'path-smoke.py')
path_smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(path_smoke)


class PathSafetyTests(unittest.TestCase):
    def test_guard_rejection_precedes_all_mutations_and_children(self):
        with patch.object(sys, 'argv', ['path-smoke', '--worker', '/unused.dll', '--output', '/unused-output']), patch.object(path_smoke.incident, 'namespace_guard', side_effect=RuntimeError('guard rejected')), patch.object(path_smoke.subprocess, 'Popen') as child, patch.object(path_smoke, 'run') as command:
            with self.assertRaisesRegex(RuntimeError, 'guard rejected'):
                path_smoke.main()
            child.assert_not_called()
            command.assert_not_called()
