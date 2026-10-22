import importlib.util
import unittest
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('incident_smoke', Path(__file__).resolve().parents[1] / 'incident-smoke.py')
incident = importlib.util.module_from_spec(spec)
spec.loader.exec_module(incident)


class NamespaceSafetyTests(unittest.TestCase):
    def test_non_linux_is_rejected_before_network_commands(self):
        with patch.object(incident.platform, 'system', return_value='Darwin'), patch.object(incident.subprocess, 'check_output') as command:
            with self.assertRaisesRegex(RuntimeError, 'disposable Linux'):
                incident.namespace_guard()
            command.assert_not_called()

    def test_host_namespace_is_rejected_before_network_commands(self):
        with patch.object(incident.platform, 'system', return_value='Linux'), patch.object(incident.os, 'geteuid', return_value=0), patch.object(incident.os, 'readlink', return_value='net:[same]'), patch.object(incident.subprocess, 'check_output') as command:
            with self.assertRaisesRegex(RuntimeError, 'host network'):
                incident.namespace_guard()
            command.assert_not_called()

    def test_non_loopback_interface_is_rejected(self):
        with patch.object(incident.platform, 'system', return_value='Linux'), patch.object(incident.os, 'geteuid', return_value=0), patch.object(incident.os, 'readlink', side_effect=['net:[isolated]', 'net:[host]']), patch.object(incident.subprocess, 'check_output', return_value='[{"ifname":"lo"},{"ifname":"eth0"}]'):
            with self.assertRaisesRegex(RuntimeError, 'only loopback'):
                incident.namespace_guard()

    def test_default_route_is_rejected(self):
        with patch.object(incident.platform, 'system', return_value='Linux'), patch.object(incident.os, 'geteuid', return_value=0), patch.object(incident.os, 'readlink', side_effect=['net:[isolated]', 'net:[host]']), patch.object(incident.subprocess, 'check_output', side_effect=['[{"ifname":"lo"}]', 'default via 10.0.0.1']):
            with self.assertRaisesRegex(RuntimeError, 'no default route'):
                incident.namespace_guard()
