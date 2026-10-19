import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch, MagicMock

spec = importlib.util.spec_from_file_location('deploy', Path(__file__).parents[1] / 'ci_deploy_backend.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def application(image='registry/old:stable', revision='old-sha'):
    return {'properties': {'template': {'containers': [{'image': image, 'env': [
        {'name': 'Deployment__Revision', 'value': revision}]}]},
        'configuration': {'ingress': {'fqdn': 'example.invalid'}}}}


class DeploymentTests(unittest.TestCase):
    def test_success_requires_expected_image_and_revision(self):
        with patch.object(module, 'az', return_value=application('registry/new:sha')) as azure, patch.object(module, 'healthy') as health:
            module.deploy(application(), 'registry/new:sha', 'new-sha', 'group', 'api')
        self.assertEqual(2, azure.call_count)
        health.assert_called_once_with('https://example.invalid/health', 'new-sha')

    def test_update_failure_restores_old_image_and_revision(self):
        with patch.object(module, 'az', side_effect=[RuntimeError('Azure update failed'), None]) as azure, patch.object(module, 'healthy') as health:
            with self.assertRaisesRegex(RuntimeError, 'previous image was restored'):
                module.deploy(application(), 'registry/new:sha', 'new-sha', 'group', 'api')
        self.assertIn('registry/old:stable', azure.call_args_list[-1].args)
        self.assertIn('Deployment__Revision=old-sha', azure.call_args_list[-1].args)
        health.assert_called_once_with('https://example.invalid/health', 'old-sha')

    def test_unhealthy_new_version_triggers_rollback(self):
        with patch.object(module, 'az', side_effect=[None, application('registry/new:sha'), None]) as azure, patch.object(module, 'healthy', side_effect=[RuntimeError('unhealthy'), None]) as health:
            with self.assertRaisesRegex(RuntimeError, 'previous image was restored'):
                module.deploy(application(), 'registry/new:sha', 'new-sha', 'group', 'api')
        self.assertEqual(3, azure.call_count)
        self.assertEqual('old-sha', health.call_args.args[1])

    def test_rollback_failure_is_not_reported_as_success(self):
        with patch.object(module, 'az', side_effect=[RuntimeError('update'), RuntimeError('rollback')]), patch.object(module, 'healthy'):
            with self.assertRaisesRegex(RuntimeError, 'Deployment AND rollback failed'):
                module.deploy(application(), 'registry/new:sha', 'new-sha', 'group', 'api')

    def test_old_healthy_version_is_not_mistaken_for_new_deployment(self):
        response = MagicMock(status=200, headers={'X-PACE-Revision': 'old-sha'})
        response.__enter__.return_value = response
        with patch.object(module.urllib.request, 'urlopen', return_value=response), patch.object(module.time, 'monotonic', side_effect=[0, 0, 0, 4]), patch.object(module.time, 'sleep'):
            with self.assertRaisesRegex(RuntimeError, 'expected application version'):
                module.healthy('https://example.invalid/health', 'new-sha', timeout=3)

    def test_smoke_failure_never_pushes_or_updates_azure(self):
        with patch('sys.argv', ['deploy', '--image', 'registry/new:sha', '--revision', 'sha', '--push']), patch.object(module, 'az', return_value=application()) as azure, patch.object(module, 'smoke', side_effect=RuntimeError('image will not start')), patch.object(module, 'deploy') as deploy, patch.object(module.subprocess, 'run') as run:
            with self.assertRaisesRegex(RuntimeError, 'will not start'):
                module.main()
        self.assertEqual(1, azure.call_count)  # Read-only app snapshot; no secrets access.
        deploy.assert_not_called()
        run.assert_not_called()


if __name__ == '__main__':
    unittest.main()
