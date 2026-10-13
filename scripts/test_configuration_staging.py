import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


STAGE = module(ROOT / 'integration/RemoteConfig/stage.py', 'config_stage')
PACKAGE = module(ROOT / 'scripts/package-sdk.py', 'config_package')


class ConfigurationStagingTests(unittest.TestCase):
    def test_optional_project_excluded_from_regular_payload(self):
        projects = PACKAGE.regular_runtime_projects(ROOT)
        self.assertEqual(len(projects), 15)
        self.assertNotIn('GamePlatform.Configuration', {project.stem for project in projects})
        self.assertIn('GamePlatform.Storage.Sqlite', {project.stem for project in projects})

    def test_stage_has_only_optional_dll_and_android_webgl_import_policy(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            assembly = root / 'GamePlatform.Configuration.dll'
            assembly.write_bytes(b'isolated fixture; real builds use PackageInspector')
            metadata = root / 'metadata.json'
            metadata.write_text(json.dumps(dict(assemblySha256=hashlib.sha256(assembly.read_bytes()).hexdigest(), assemblies=[dict(name='GamePlatform.Configuration',
                targetFramework='.NETStandard,Version=v2.1', references=[dict(name='netstandard')])])) )
            output = root / 'stage'
            inventory = STAGE.stage(assembly, metadata, output)
            self.assertEqual(inventory['runtimeThirdPartyPackages'], 0)
            self.assertEqual([p.name for p in output.rglob('*.dll')], ['GamePlatform.Configuration.dll'])
            meta = next(output.rglob('*.meta')).read_text()
            self.assertIn('Android: Android', meta)
            self.assertIn('WebGL: WebGL', meta)
            self.assertIn('validateReferences: 1', meta)
            with self.assertRaisesRegex(ValueError, 'output_must_be_empty'):
                STAGE.stage(assembly, metadata, output)
            metadata.write_text(json.dumps(dict(assemblySha256=hashlib.sha256(assembly.read_bytes()).hexdigest(), assemblies=[dict(name='GamePlatform.Configuration',
                targetFramework='.NETStandard,Version=v2.1', references=[dict(name='GamePlatform.Core')])])) )
            with self.assertRaisesRegex(ValueError, 'config_only_closure_required'):
                STAGE.stage(assembly, metadata, root / 'bad')


if __name__ == '__main__':
    unittest.main()
