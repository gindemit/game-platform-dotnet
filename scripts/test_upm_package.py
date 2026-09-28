import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name('build-upm-package.py')
SPEC = importlib.util.spec_from_file_location('build_upm_package', SCRIPT)
UPM = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(UPM)


class UpmPackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='game-platform-upm-test-')
        self.root = Path(self.temporary.name) / 'package'
        self.root.mkdir()
        (self.root / 'Runtime').mkdir()
        (self.root / 'Runtime/Plugins/Managed').mkdir(parents=True)
        (self.root / 'Documentation~/sdk').mkdir(parents=True)
        (self.root / 'Documentation~/aot-inventory.json').write_text('{}\n')
        (self.root / 'Documentation~/lifecycle-manifest.json').write_text('{}\n')
        (self.root / 'Runtime/link.xml').write_text('<linker/>\n')
        (self.root / 'LICENSES').mkdir()
        (self.root / 'LICENSES/LICENSE-NOTICE.md').write_text('notice\n')
        source_files = []
        managed_names = ['GamePlatform.Core', 'GamePlatform.Features.Contracts']
        for name in managed_names:
            dll_name = name + '.dll'
            (self.root / 'Runtime/Plugins/Managed' / dll_name).write_bytes(b'managed fixture')
            meta = self.root / 'Runtime/Plugins/Managed' / (dll_name + '.meta')
            meta.write_text(UPM.load_sdk_packager().managed_meta(dll_name))
            source_files.append({'path': 'managed/' + dll_name + '.meta', 'sha256': UPM.sha256(meta)})
        packager = UPM.load_sdk_packager()
        native = []
        for target, suffix in (('android-arm64', 'android/arm64/libsqlite.so'),
                               ('windows-x86_64', 'windows/x86_64/sqlite.dll')):
            source_path = 'native/Plugins/lib/' + suffix
            importer_path = source_path + '.meta'
            package_parent = self.root / 'Runtime/Plugins/native' / Path(source_path).relative_to('native')
            package_parent.parent.mkdir(parents=True, exist_ok=True)
            package_parent.write_bytes(('native ' + target).encode())
            meta_path = package_parent.with_name(package_parent.name + '.meta')
            meta_text = packager.native_meta(target)
            meta_path.write_text(meta_text)
            native.append({'target': target, 'path': source_path, 'importMetadata': importer_path,
                           'sha256': UPM.sha256(package_parent),
                           'importMetadataSha256': UPM.sha256(meta_path)})
        native.append({'target': 'macos-universal', 'path': 'native/Plugins/lib/macos/sqlite.dylib',
                       'importMetadata': 'native/Plugins/lib/macos/sqlite.dylib.meta',
                       'sha256': 'c' * 64, 'importMetadataSha256': 'd' * 64})
        source = {'sourceCommit': 'a' * 40, 'sourceDirty': False,
                  'managedAssemblyNames': managed_names,
                  'files': source_files, 'build': {'buildSkipped': False}, 'nativeLibraries': native}
        dependency_file = self.root / 'Documentation~/sdk/dependency-manifest.json'
        dependency_file.write_text(json.dumps(source))
        (self.root / 'package.json').write_text(json.dumps({
            'name': UPM.PACKAGE_NAME, 'version': UPM.PACKAGE_VERSION}))
        (self.root / 'package-content-manifest.json').write_text('{}\n')
        UPM.write_stable_asset_metadata(self.root)
        self.refresh_manifest()

    def tearDown(self):
        self.temporary.cleanup()

    def refresh_manifest(self):
        files = UPM.inventory(self.root)
        (self.root / 'package-content-manifest.json').write_text(json.dumps({
            'schemaVersion': 1, 'name': UPM.PACKAGE_NAME, 'version': UPM.PACKAGE_VERSION,
            'sourceCommit': 'a' * 40,
            'sourceBundleManifestSha256': UPM.sha256(self.root / 'Documentation~/sdk/dependency-manifest.json'),
            'preservedManagedGuids': UPM.load_sdk_packager().managed_guid_overrides(),
            'withheldNativeTargets': UPM.WITHHELD_NATIVE_TARGETS,
            'files': files}))

    def assert_failure(self, expected):
        with self.assertRaisesRegex(ValueError, expected):
            UPM.verify_package(self.root)

    def test_valid_complete_minimal_package_verifies(self):
        self.assertEqual(UPM.verify_package(self.root)['sourceCommit'], 'a' * 40)

    def test_malformed_package_json_is_rejected(self):
        (self.root / 'package.json').write_text('{')
        self.assert_failure('malformed_package_json')

    def test_missing_native_payload_is_rejected(self):
        dependency = json.loads((self.root / 'Documentation~/sdk/dependency-manifest.json').read_text())
        dependency['nativeLibraries'][0]['path'] = 'native/Plugins/lib/android/arm64/missing.so'
        dependency['nativeLibraries'][0]['target'] = 'android-arm64'
        dependency['nativeLibraries'][0]['importMetadata'] = 'native/Plugins/lib/android/arm64/missing.so.meta'
        (self.root / 'Documentation~/sdk/dependency-manifest.json').write_text(json.dumps(dependency))
        self.refresh_manifest()
        self.assert_failure('native_payload_inventory_mismatch')

    def test_duplicate_managed_assembly_is_rejected(self):
        (self.root / 'Runtime/Plugins/Managed/One.dll').write_bytes(b'one')
        duplicate = self.root / 'Runtime/Plugins/Managed/Duplicate'
        duplicate.mkdir()
        (duplicate / 'One.dll').write_bytes(b'two')
        self.refresh_manifest()
        self.assert_failure('duplicate_managed_assembly')

    def test_native_importer_metadata_is_present_and_pinned(self):
        package = UPM.verify_package(self.root)
        self.assertEqual(package['preservedManagedGuids']['GamePlatform.Core.dll'],
                         '68b7ad586b1f56d6a838a26b7ff2333f')
        self.assertEqual(package['preservedManagedGuids']['GamePlatform.Features.Contracts.dll'],
                         '827c6f3773525912a40a393e14fa0406')

    def test_missing_native_importer_metadata_is_rejected(self):
        path = self.root / 'Runtime/Plugins/native/Plugins/lib/android/arm64/libsqlite.so.meta'
        path.unlink()
        self.refresh_manifest()
        self.assert_failure('native_import_metadata_inventory_mismatch')

    def test_tampered_native_importer_metadata_is_rejected(self):
        path = self.root / 'Runtime/Plugins/native/Plugins/lib/android/arm64/libsqlite.so.meta'
        path.write_text(path.read_text().replace('Is16KbAligned: true', 'Is16KbAligned: false'))
        self.assert_failure('native_import_metadata_hash_mismatch')

    def test_android_importer_cannot_enable_windows_or_drop_webgl_exclusion(self):
        path = self.root / 'Runtime/Plugins/native/Plugins/lib/android/arm64/libsqlite.so.meta'
        text = path.read_text().replace('Exclude WebGL: 1', 'Exclude WebGL: 0')
        dependency_path = self.root / 'Documentation~/sdk/dependency-manifest.json'
        dependency = json.loads(dependency_path.read_text())
        item = next(entry for entry in dependency['nativeLibraries'] if entry['target'] == 'android-arm64')
        path.write_text(text)
        item['importMetadataSha256'] = UPM.sha256(path)
        dependency_path.write_text(json.dumps(dependency))
        self.refresh_manifest()
        self.assert_failure('native_import_exclusion_conflict')

    def test_managed_importer_cannot_leak_to_webgl(self):
        path = self.root / 'Runtime/Plugins/Managed/GamePlatform.Core.dll.meta'
        text = path.read_text().replace('Exclude WebGL: 1', 'Exclude WebGL: 0')
        path.write_text(text)
        dependency_path = self.root / 'Documentation~/sdk/dependency-manifest.json'
        dependency = json.loads(dependency_path.read_text())
        entry = next(item for item in dependency['files'] if item['path'] == 'managed/GamePlatform.Core.dll.meta')
        entry['sha256'] = UPM.sha256(path)
        dependency_path.write_text(json.dumps(dependency))
        self.refresh_manifest()
        self.assert_failure('managed_import_platform_policy_mismatch')

    def test_managed_consumer_guid_is_preserved(self):
        path = self.root / 'Runtime/Plugins/Managed/GamePlatform.Core.dll.meta'
        text = path.read_text().replace('68b7ad586b1f56d6a838a26b7ff2333f', 'a45392e189e6d6ab8c50f63897a0ab38')
        path.write_text(text)
        dependency_path = self.root / 'Documentation~/sdk/dependency-manifest.json'
        dependency = json.loads(dependency_path.read_text())
        entry = next(item for item in dependency['files'] if item['path'] == 'managed/GamePlatform.Core.dll.meta')
        entry['sha256'] = UPM.sha256(path)
        dependency_path.write_text(json.dumps(dependency))
        self.refresh_manifest()
        self.assert_failure('managed_import_guid_mismatch')

    def test_bad_content_hash_is_rejected(self):
        (self.root / 'LICENSES/LICENSE-NOTICE.md').write_text('tampered\n')
        self.assert_failure('package_file_hash_mismatch')

    def test_visible_package_assets_and_folders_have_stable_meta(self):
        UPM.verify_package(self.root)
        self.assertTrue((self.root / 'Runtime.meta').is_file())
        self.assertTrue((self.root / 'LICENSES/LICENSE-NOTICE.md.meta').is_file())
        self.assertIn('TextScriptImporter:',
                      (self.root / 'package.json.meta').read_text())

    def test_missing_visible_folder_meta_is_rejected(self):
        (self.root / 'Runtime.meta').unlink()
        self.refresh_manifest()
        self.assert_failure('package_folder_meta_missing')

    def test_missing_visible_file_meta_is_rejected(self):
        (self.root / 'LICENSES/LICENSE-NOTICE.md.meta').unlink()
        self.refresh_manifest()
        self.assert_failure('package_asset_meta_missing')

    def test_tampered_visible_file_meta_is_rejected(self):
        path = self.root / 'LICENSES/LICENSE-NOTICE.md.meta'
        path.write_text(path.read_text().replace('TextScriptImporter:', 'DefaultImporter:'))
        self.refresh_manifest()
        self.assert_failure('package_asset_meta_invalid')


if __name__ == '__main__':
    unittest.main()
