import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('package_sdk', Path(__file__).with_name('package-sdk.py'))
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


def assembly(name='A', refs=()):
    return {'name': name, 'targetFramework': '.NETStandard,Version=v2.1',
            'references': [{'name': value} for value in refs]}


class PackageTests(unittest.TestCase):
    def test_dependency_closure_accepts_only_bundled_and_framework(self):
        package.validate_closure([assembly('A', ['B', 'netstandard']), assembly('B')], ['A', 'B'])

    def test_missing_transitive_dependency_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'unresolved_runtime_dependency'):
            package.validate_closure([assembly('A', ['Missing'])], ['A'])

    def test_duplicate_assembly_names_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'duplicate_assembly_name'):
            package.validate_closure([assembly(), assembly()], ['A'])

    def test_wrong_target_is_rejected(self):
        item = assembly()
        item['targetFramework'] = '.NETCoreApp,Version=v9.0'
        with self.assertRaisesRegex(ValueError, 'unsupported_managed_target'):
            package.validate_closure([item], ['A'])

    def test_missing_project_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'assembly_inventory_mismatch'):
            package.validate_closure([assembly()], ['A', 'B'])

    def test_new_runtime_package_requires_explicit_review(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            project = root / 'A.csproj'
            project.write_text('<Project><ItemGroup><PackageReference Include="New"/></ItemGroup></Project>')
            with self.assertRaisesRegex(ValueError, 'runtime_package_requires'):
                package.validate_inputs(root, [project])

    def test_transitive_runtime_lock_dependency_requires_review(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            project = root / 'A.csproj'
            project.write_text('<Project/>')
            (root / 'packages.lock.json').write_text(json.dumps({'dependencies': {'netstandard2.1': {'New': {'type': 'Transitive'}}}}))
            with self.assertRaisesRegex(ValueError, 'unreviewed_runtime_lock_dependency'):
                package.validate_inputs(root, [project])

    def test_wrong_output_directory_cannot_delete_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            sentinel = root / 'evidence.txt'
            sentinel.write_text('keep')
            with self.assertRaisesRegex(ValueError, 'output_must_be'):
                package.build_bundle(root)
            self.assertEqual('keep', sentinel.read_text())

    def bundle(self, root):
        (root / 'A.dll').write_bytes(b'fixture')
        manifest = {'projects': ['A'], 'assemblies': [assembly()], 'files': [
            {'path': 'A.dll', 'sha256': package.digest(root / 'A.dll'), 'bytes': 7}]}
        (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
        return manifest

    def test_inventory_rejects_changed_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.bundle(root)
            package.verify_bundle(root)
            (root / 'A.dll').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'bundle_hash_mismatch'):
                package.verify_bundle(root)

    def test_inventory_rejects_extra_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.bundle(root)
            (root / 'duplicate.dll').write_bytes(b'fixture')
            with self.assertRaisesRegex(ValueError, 'bundle_inventory_mismatch'):
                package.verify_bundle(root)

    def test_inventory_rejects_duplicate_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['files'].append(manifest['files'][0])
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'duplicate_bundle_path'):
                package.verify_bundle(root)


if __name__ == '__main__':
    unittest.main()
