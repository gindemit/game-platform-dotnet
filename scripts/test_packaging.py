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
    def test_canonical_source_link_is_checkout_independent_and_revision_bound(self):
        revision = 'a' * 40
        source_link = package.canonical_source_link(revision)
        self.assertEqual(
            {'documents': {'/_/*':
             'https://raw.githubusercontent.com/gindemit/game-platform-dotnet/' + revision + '/*'}},
            json.loads(source_link))
        with self.assertRaisesRegex(ValueError, 'source_revision_not_full_sha'):
            package.canonical_source_link('HEAD')

    def test_deterministic_build_arguments_pin_repository_and_debug_inputs(self):
        root = Path('C:/one/checkout')
        source_link = root / 'artifacts/build-inputs/source-link.json'
        arguments = package.deterministic_build_arguments(root, 'b' * 40, source_link)
        self.assertIn('-p:PathMap=C:\\one\\checkout=/_/', arguments)
        self.assertIn('-p:ContinuousIntegrationBuild=true', arguments)
        self.assertIn('-p:DeterministicSourcePaths=true', arguments)
        self.assertIn('-p:EnableSourceControlManagerQueries=false', arguments)
        self.assertIn('-p:RepositoryUrl=' + package.CANONICAL_REPOSITORY_URL, arguments)
        self.assertIn('-p:SourceRevisionId=' + ('b' * 40), arguments)
        self.assertIn('-p:CanonicalPackagingSourceLink=' + str(source_link), arguments)

    def test_debug_identity_requires_revision_bound_source_link_for_projects(self):
        revision = 'c' * 40
        item = assembly()
        item['sourceLink'] = package.canonical_source_link(revision)
        package.validate_debug_identity([item], ['A'], revision)
        item['sourceLink'] = None
        with self.assertRaisesRegex(ValueError, 'canonical_source_link_missing'):
            package.validate_debug_identity([item], ['A'], revision)

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
            (root / 'packages.lock.json').write_text(json.dumps({'dependencies': {}}))
            with self.assertRaisesRegex(ValueError, 'runtime_package_requires'):
                package.validate_inputs(root, [project])

    def test_transitive_runtime_lock_dependency_requires_review(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            project = root / 'A.csproj'
            project.write_text('<Project/>')
            (root / 'packages.lock.json').write_text(json.dumps({'dependencies': {'netstandard2.1': {'New': {'type': 'Transitive'}}}}))
            with self.assertRaisesRegex(ValueError, 'runtime_package_requires_bundle_license_review'):
                package.validate_inputs(root, [project])

    def test_wrong_output_directory_cannot_delete_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            sentinel = root / 'evidence.txt'
            sentinel.write_text('keep')
            with self.assertRaisesRegex(ValueError, 'output_must_be'):
                package.build_bundle(root, root / 'sqlite')
            self.assertEqual('keep', sentinel.read_text())

    def test_http_composition_reuses_reviewed_messagepack_closure(self):
        projects = sorted((package.ROOT / 'src').glob('*/*.csproj'))
        package.validate_inputs(package.ROOT, projects)

    def test_http_rejects_unreviewed_dependency_changes(self):
        source = package.ROOT / 'src/GamePlatform.Transport.Http/GamePlatform.Transport.Http.csproj'
        cases = ('extra', 'missing', 'version', 'hash', 'missing_hash', 'direct_lock',
                 'dependencies', 'direct_reference', 'missing_codec_reference')
        for case in cases:
            with self.subTest(case=case), tempfile.TemporaryDirectory() as temp:
                project = Path(temp) / source.name
                project.write_text(source.read_text())
                locked = package.package_lock(source)
                packages = locked['dependencies']['.NETStandard,Version=v2.1']
                if case == 'extra':
                    packages['New'] = {'type': 'Transitive', 'resolved': '1.0.0', 'contentHash': 'new'}
                elif case == 'missing':
                    del packages['System.Memory']
                elif case == 'version':
                    packages['MessagePack']['resolved'] = '3.1.9'
                elif case == 'hash':
                    packages['MessagePack']['contentHash'] = 'different'
                elif case == 'missing_hash':
                    del packages['MessagePack']['contentHash']
                elif case == 'direct_lock':
                    packages['MessagePack']['type'] = 'Direct'
                elif case == 'dependencies':
                    packages['MessagePack']['dependencies']['New'] = '1.0.0'
                elif case == 'direct_reference':
                    project.write_text(project.read_text().replace('</Project>',
                        '<ItemGroup><PackageReference Include="MessagePack"/></ItemGroup></Project>'))
                elif case == 'missing_codec_reference':
                    project.write_text('<Project/>')
                project.with_name('packages.lock.json').write_text(json.dumps(locked))
                with self.assertRaisesRegex(ValueError,
                        'http_messagepack_package_|runtime_package_requires_bundle_license_review'):
                    package.validate_inputs(package.ROOT, [project])

    def test_reviewed_package_is_not_implicitly_allowed_in_other_projects(self):
        with tempfile.TemporaryDirectory() as temp:
            project = Path(temp) / 'A.csproj'
            project.write_text('<Project/>')
            source = package.ROOT / 'src/GamePlatform.Transport.Http/GamePlatform.Transport.Http.csproj'
            project.with_name('packages.lock.json').write_text(json.dumps(package.package_lock(source)))
            with self.assertRaisesRegex(ValueError, 'runtime_package_requires_bundle_license_review'):
                package.validate_inputs(package.ROOT, [project])

    def bundle(self, root):
        (root / 'A.dll').write_bytes(b'fixture')
        (root / 'aot').mkdir()
        (root / 'aot/link.xml').write_text('<linker/>')
        (root / 'aot/aot-inventory.json').write_text('{}')
        native = []
        for target in package.NATIVE_TARGETS:
            path = root / 'native' / (target + '.bin')
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(target.encode())
            meta = path.with_name(path.name + '.meta')
            meta.write_text(package.native_meta(target))
            native.append({'target': target, 'path': path.relative_to(root).as_posix(),
                           'importMetadata': meta.relative_to(root).as_posix(),
                           'sha256': package.digest(path),
                           'importMetadataSha256': package.digest(meta)})
        locked = package.package_lock(package.ROOT / 'src/GamePlatform.Serialization.MessagePack/GamePlatform.Serialization.MessagePack.csproj')['dependencies']['.NETStandard,Version=v2.1']
        for relative in package.REQUIRED_NOTICES:
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('reviewed fixture notice')
        metadata = root / 'licenses/nuget-metadata'
        metadata.mkdir(exist_ok=True)
        for name, version in package.MESSAGEPACK_PACKAGES.items():
            (metadata / f'{name}.{version}.nuspec').write_text(
                '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><license type="expression">MIT</license></metadata></package>')
        files = [{'path': p.relative_to(root).as_posix(), 'sha256': package.digest(p), 'bytes': p.stat().st_size}
                 for p in sorted(root.rglob('*')) if p.is_file()]
        manifest = {'projects': ['A'], 'managedAssemblyNames': ['A'], 'assemblies': [assembly()],
                    'managedPackages': [{'id': name, 'version': version, 'contentHash': locked[name]['contentHash']}
                                        for name, version in package.MESSAGEPACK_PACKAGES.items()],
                    'duplicateUpmAcquisitionAllowed': False, 'nativeLibraries': native,
                    'pinvoke': package.pinvoke_inventory(),
                    'sourceDirty': False, 'build': {'buildSkipped': False},
                    'capabilities': {'productionMessagePack': 'unavailable'}, 'files': files}
        (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
        return manifest

    def verify(self, root, manifest):
        qualification = {'inputs': [
            {'target': item['target'], 'sha256': item['sha256'],
             'generatedImportMetadataSha256': item['importMetadataSha256']}
            for item in manifest['nativeLibraries']]}
        locked = {item['id']: {'contentHash': item['contentHash']} for item in manifest['managedPackages']}
        return package.verify_bundle(root, qualification, locked, package.pinvoke_inventory())

    def test_inventory_rejects_changed_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            self.verify(root, manifest)
            (root / 'A.dll').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'bundle_hash_mismatch'):
                self.verify(root, json.loads((root / 'dependency-manifest.json').read_text()))

    def test_inventory_rejects_extra_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            (root / 'duplicate.dll').write_bytes(b'fixture')
            with self.assertRaisesRegex(ValueError, 'bundle_inventory_mismatch'):
                self.verify(root, manifest)

    def test_inventory_rejects_duplicate_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['files'].append(manifest['files'][0])
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'duplicate_bundle_path'):
                self.verify(root, manifest)

    def test_missing_native_target_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['nativeLibraries'].pop()
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'native_target_inventory_mismatch'):
                self.verify(root, manifest)

    def test_duplicate_upm_acquisition_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['duplicateUpmAcquisitionAllowed'] = True
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'duplicate_upm'):
                self.verify(root, manifest)

    def test_qualification_codec_cannot_be_labeled_production(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['capabilities']['productionMessagePack'] = 'included'
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'qualification_codec_mislabeled'):
                self.verify(root, manifest)

    def test_pinvoke_inventory_drift_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['pinvoke'] = {'libraryNames': ['sqlite3'], 'entryPoints': [], 'declarationCount': 0}
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'pinvoke_inventory_mismatch'):
                self.verify(root, manifest)

    def test_required_native_notice_cannot_disappear_from_manifest(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            relative = 'licenses/LICENSE.SQLite3MultipleCiphers.txt'
            (root / relative).unlink()
            manifest['files'] = [item for item in manifest['files'] if item['path'] != relative]
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'required_license_notice_missing'):
                self.verify(root, manifest)

    def test_generated_native_importers_are_target_isolated(self):
        for target in package.NATIVE_TARGETS:
            with self.subTest(target=target):
                self.assertTrue(package.validate_native_importer(package.native_meta(target), target))

    def test_windows_importer_rejects_cross_platform_enablement(self):
        text = package.native_meta('windows-x86_64').replace(
            'Standalone: Linux64\n    second:\n      enabled: 0',
            'Standalone: Linux64\n    second:\n      enabled: 1')
        with self.assertRaisesRegex(ValueError, 'cross_platform_enablement'):
            package.validate_native_importer(text, 'windows-x86_64')

    def test_importer_rejects_exclusion_conflict(self):
        text = package.native_meta('windows-x86_64').replace('Exclude OSXUniversal: 1',
                                                              'Exclude OSXUniversal: 0')
        with self.assertRaisesRegex(ValueError, 'exclusion_conflict'):
            package.validate_native_importer(text, 'windows-x86_64')

    def test_importer_rejects_cpu_and_os_conflicts(self):
        cases = [
            ('windows-x86_64', 'OS: Windows', 'OS: OSX'),
            ('macos-universal', 'CPU: AnyCPU', 'CPU: x86_64'),
            ('android-arm64', 'CPU: ARM64', 'CPU: ARMv7'),
        ]
        for target, before, after in cases:
            with self.subTest(target=target):
                text = package.native_meta(target).replace(before, after, 1)
                with self.assertRaisesRegex(ValueError, 'setting_conflict'):
                    package.validate_native_importer(text, target)

    def test_default_verify_rejects_android_alignment_conflict(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            native = next(item for item in manifest['nativeLibraries']
                          if item['target'] == 'android-arm64')
            meta = root / native['importMetadata']
            meta.write_text(meta.read_text().replace('Is16KbAligned: true',
                                                      'Is16KbAligned: false'))
            changed_hash = package.digest(meta)
            native['importMetadataSha256'] = changed_hash
            for item in manifest['files']:
                if item['path'] == native['importMetadata']:
                    item['sha256'] = changed_hash
                    item['bytes'] = meta.stat().st_size
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'native_import_setting_conflict'):
                self.verify(root, manifest)

    def test_allow_dirty_artifact_is_rejected_by_default_verify(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['sourceDirty'] = True
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'development_bundle_not_importable'):
                self.verify(root, manifest)
            qualification = {'inputs': [
                {'target': item['target'], 'sha256': item['sha256'],
                 'generatedImportMetadataSha256': item['importMetadataSha256']}
                for item in manifest['nativeLibraries']]}
            locked = {item['id']: {'contentHash': item['contentHash']} for item in manifest['managedPackages']}
            package.verify_bundle(root, qualification, locked, package.pinvoke_inventory(), allow_development=True)

    def test_no_build_artifact_is_rejected_by_default_verify(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.bundle(root)
            manifest['build']['buildSkipped'] = True
            (root / 'dependency-manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'development_bundle_not_importable'):
                self.verify(root, manifest)


if __name__ == '__main__':
    unittest.main()
