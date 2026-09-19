#!/usr/bin/env python3
"""Build one commit-pinned managed/native SDK bundle; never publish it."""
from __future__ import annotations
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MESSAGEPACK_PROJECT = 'GamePlatform.Serialization.MessagePack'
MESSAGEPACK_PACKAGES = {
    'MessagePack': '3.1.8', 'MessagePack.Annotations': '3.1.8',
    'MessagePackAnalyzer': '3.1.8', 'Microsoft.NET.StringTools': '17.11.4',
    'System.Buffers': '4.5.1', 'System.Collections.Immutable': '8.0.0',
    'System.Memory': '4.5.5', 'System.Numerics.Vectors': '4.4.0',
    'System.Runtime.CompilerServices.Unsafe': '6.0.0',
}
RUNTIME_PACKAGE_IDS = set(MESSAGEPACK_PACKAGES) - {'MessagePackAnalyzer'}
NATIVE_TARGETS = {
    'windows-x86_64': 'Plugins/lib/windows/x86_64/gilzoide-sqlite-net.dll',
    'macos-universal': 'Plugins/lib/macos/libgilzoide-sqlite-net.dylib',
    'android-arm64': 'Plugins/lib/android/arm64/libgilzoide-sqlite-net.so',
}


def run(args, capture=False, cwd=ROOT):
    result = subprocess.run(args, cwd=cwd, check=True, text=True,
                            stdout=subprocess.PIPE if capture else None)
    return result.stdout.strip() if capture else None


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect(assemblies):
    project = ROOT / 'scripts/package-inspector/PackageInspector.csproj'
    run(['dotnet', 'build', str(project), '-c', 'Release', '--nologo', '-v', 'quiet'])
    inspector = project.parent / 'bin/Release/net9.0/PackageInspector.dll'
    return json.loads(run(['dotnet', str(inspector), *map(str, assemblies)], capture=True))


def validate_closure(metadata, expected_names):
    names = [item['name'] for item in metadata]
    if len(names) != len(set(names)):
        raise ValueError('duplicate_assembly_name')
    if set(names) != set(expected_names):
        raise ValueError('assembly_inventory_mismatch')
    for assembly in metadata:
        # Several BCL compatibility packages omit TargetFrameworkAttribute; their
        # exact netstandard2.0 asset path is independently pinned in managedPackages.
        if assembly['targetFramework'] not in (None, '.NETStandard,Version=v2.0', '.NETStandard,Version=v2.1'):
            raise ValueError('unsupported_managed_target: ' + assembly['name'])
        for reference in assembly['references']:
            if reference['name'] not in names and reference['name'] != 'netstandard':
                raise ValueError('unresolved_runtime_dependency: ' + reference['name'])


def package_lock(project):
    return json.loads(project.with_name('packages.lock.json').read_text(encoding='utf-8'))


def validate_inputs(root, projects):
    for project in projects:
        references = [item.attrib['Include'] for item in ET.parse(project).findall('.//PackageReference')]
        lock_packages = {}
        for group in package_lock(project).get('dependencies', {}).values():
            lock_packages.update({name: item for name, item in group.items() if item.get('type') != 'Project'})
        if project.stem == MESSAGEPACK_PROJECT:
            if references != ['MessagePack'] or set(lock_packages) != set(MESSAGEPACK_PACKAGES):
                raise ValueError('messagepack_package_inventory_mismatch')
            for name, version in MESSAGEPACK_PACKAGES.items():
                if lock_packages[name].get('resolved') != version or not lock_packages[name].get('contentHash'):
                    raise ValueError('messagepack_package_pin_mismatch: ' + name)
        elif references or lock_packages:
            raise ValueError('runtime_package_requires_bundle_license_review: ' + project.stem)
    for required in ('LICENSE-NOTICE.md', 'integration/unity/package/LICENSE.MessagePack-CSharp.txt',
                     'integration/unity/package/link.xml', 'integration/unity/package/aot-inventory.json',
                     'integration/unity/package/lifecycle-manifest.json'):
        if not (root / required).is_file():
            raise ValueError('bundle_input_missing: ' + required)


def resolve_package_assets(project):
    assets_path = project.parent / 'obj/project.assets.json'
    if not assets_path.is_file():
        raise ValueError('restore_assets_missing: ' + str(assets_path))
    assets = json.loads(assets_path.read_text(encoding='utf-8'))
    target_name = next((name for name in assets['targets'] if name.startswith('.NETStandard,Version=v2.1')), None)
    if target_name is None:
        raise ValueError('netstandard21_assets_missing')
    package_root = Path(next(iter(assets['packageFolders'])))
    locked = package_lock(project)['dependencies']['.NETStandard,Version=v2.1']
    runtime, packages = [], []
    for identity, item in sorted(assets['targets'][target_name].items()):
        if item.get('type') != 'package':
            continue
        package_id, version = identity.rsplit('/', 1)
        library = assets['libraries'][identity]
        runtime_paths = sorted(path for path in item.get('runtime', {}) if not path.endswith('/_._'))
        if runtime_paths:
            if len(runtime_paths) != 1 or not runtime_paths[0].lower().endswith('.dll'):
                raise ValueError('unexpected_package_runtime_assets: ' + identity)
            source = package_root / library['path'] / runtime_paths[0]
            if not source.is_file():
                raise ValueError('package_runtime_asset_missing: ' + str(source))
            runtime.append((package_id, version, source))
        packages.append({'id': package_id, 'version': version,
                         'contentHash': locked[package_id]['contentHash'],
                         'runtimeAssets': runtime_paths})
    if {item['id'] for item in packages} != set(MESSAGEPACK_PACKAGES):
        raise ValueError('resolved_package_inventory_mismatch')
    if {package_id for package_id, _, _ in runtime} != RUNTIME_PACKAGE_IDS:
        raise ValueError('resolved_runtime_package_inventory_mismatch')
    return runtime, packages, package_root


def acquire_sqlite(source):
    qualification = json.loads((ROOT / 'integration/unity/sqlite-qualification/qualification.json').read_text())
    candidate = qualification['candidate']
    if not source.exists():
        source.parent.mkdir(parents=True, exist_ok=True)
        run(['git', 'clone', '--filter=blob:none', candidate['repository'], str(source)])
    if run(['git', 'status', '--porcelain', '--untracked-files=all'], capture=True, cwd=source):
        raise ValueError('sqlite_candidate_dirty')
    run(['git', 'fetch', 'origin', candidate['commit']], cwd=source)
    run(['git', 'checkout', '--detach', candidate['commit']], cwd=source)
    if run(['git', 'rev-parse', 'HEAD'], capture=True, cwd=source) != candidate['commit']:
        raise ValueError('sqlite_candidate_revision_mismatch')
    expected = {item['target']: item for item in qualification['inputs']}
    for target, relative in NATIVE_TARGETS.items():
        path = source / relative
        if not path.is_file() or not path.with_name(path.name + '.meta').is_file():
            raise ValueError('native_target_missing: ' + target)
        if digest(path) != expected[target]['sha256']:
            raise ValueError('native_target_hash_mismatch: ' + target)
        if digest(path.with_name(path.name + '.meta')) != expected[target]['importMetadataSha256']:
            raise ValueError('native_import_metadata_hash_mismatch: ' + target)
    return qualification


def pinvoke_inventory():
    text = (ROOT / 'src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs').read_text(encoding='utf-8-sig')
    names = sorted(set(re.findall(r'LibraryPath\s*=\s*"([^"]+)"', text)))
    declarations = re.findall(r'\[DllImport\(LibraryPath,\s*EntryPoint\s*=\s*"([^"]+)"', text)
    if not declarations or text.count('[DllImport(') != len(declarations):
        raise ValueError('unknown_pinvoke_declaration')
    return {'libraryNames': names, 'entryPoints': sorted(declarations), 'declarationCount': len(declarations)}


def copy_package_notices(stage, runtime_assets, packages, package_root):
    notices = stage / 'licenses'
    notices.mkdir()
    shutil.copy2(ROOT / 'LICENSE-NOTICE.md', notices / 'LICENSE-NOTICE.md')
    shutil.copy2(ROOT / 'integration/unity/package/LICENSE.MessagePack-CSharp.txt', notices / 'LICENSE.MessagePack-CSharp.txt')
    for source in sorted((ROOT / 'src/GamePlatform.Storage.Sqlite/Vendor').glob('LICENSE.*.txt')):
        shutil.copy2(source, notices / source.name)
    runtime_roots = {package_id: source.parents[2] for package_id, _, source in runtime_assets}
    metadata = notices / 'nuget-metadata'
    metadata.mkdir()
    for item in packages:
        package_id = item['id']
        root = runtime_roots.get(package_id, package_root / package_id.lower() / item['version'])
        nuspec = next(root.glob('*.nuspec'), None)
        if nuspec is None:
            raise ValueError('package_license_metadata_missing: ' + package_id)
        shutil.copy2(nuspec, metadata / f'{package_id}.{item["version"]}.nuspec')
        for pattern in ('LICENSE*', 'THIRD-PARTY-NOTICES*', 'notices/THIRDPARTYNOTICES*'):
            for source in sorted(root.glob(pattern)):
                if source.is_file():
                    shutil.copy2(source, metadata / f'{package_id}.{item["version"]}.{source.name}')


def managed_meta(name):
    guid = hashlib.sha256(('game-platform-cl015:' + name).encode()).hexdigest()[:32]
    return (f'fileFormatVersion: 2\nguid: {guid}\nPluginImporter:\n  externalObjects: {{}}\n'
            '  serializedVersion: 2\n  iconMap: {}\n  executionOrder: {}\n  defineConstraints: []\n'
            '  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 1\n'
            '  validateReferences: 1\n  platformData: []\n  userData: CL-015 explicit asmdef reference only\n'
            '  assetBundleName: \n  assetBundleVariant: \n')


def validate_native_manifest(manifest, output, qualification=None, expected_pinvoke=None):
    if manifest.get('duplicateUpmAcquisitionAllowed') is not False:
        raise ValueError('duplicate_upm_acquisition_not_forbidden')
    qualification = qualification or json.loads((ROOT / 'integration/unity/sqlite-qualification/qualification.json').read_text())
    qualified = {item['target']: item for item in qualification['inputs']}
    actual = {item['target']: item for item in manifest.get('nativeLibraries', [])}
    if set(actual) != set(NATIVE_TARGETS):
        raise ValueError('native_target_inventory_mismatch')
    for target in NATIVE_TARGETS:
        item = actual[target]
        path, meta = output / item['path'], output / item['importMetadata']
        if not path.is_file() or not meta.is_file():
            raise ValueError('native_target_missing: ' + target)
        if item['sha256'] != qualified[target]['sha256'] or digest(path) != item['sha256']:
            raise ValueError('native_target_hash_mismatch: ' + target)
        if item.get('importMetadataSha256') != qualified[target]['importMetadataSha256'] or digest(meta) != item['importMetadataSha256']:
            raise ValueError('native_import_metadata_hash_mismatch: ' + target)
    if manifest.get('pinvoke') != (expected_pinvoke or pinvoke_inventory()):
        raise ValueError('pinvoke_inventory_mismatch')
    if not (output / 'aot/link.xml').is_file() or not (output / 'aot/aot-inventory.json').is_file():
        raise ValueError('aot_inventory_missing')


def verify_bundle(output, qualification=None, expected_packages=None, expected_pinvoke=None):
    manifest = json.loads((output / 'dependency-manifest.json').read_text())
    expected = {entry['path']: entry for entry in manifest['files']}
    if len(expected) != len(manifest['files']):
        raise ValueError('duplicate_bundle_path')
    actual = {p.relative_to(output).as_posix() for p in output.rglob('*') if p.is_file()}
    if actual != set(expected) | {'dependency-manifest.json'}:
        raise ValueError('bundle_inventory_mismatch')
    for relative, entry in expected.items():
        path = output / relative
        if not path.resolve().is_relative_to(output.resolve()):
            raise ValueError('bundle_path_escape')
        if path.stat().st_size != entry['bytes'] or digest(path) != entry['sha256']:
            raise ValueError('bundle_hash_mismatch: ' + relative)
    validate_closure(manifest['assemblies'], manifest['managedAssemblyNames'])
    package_items = {item['id']: item for item in manifest.get('managedPackages', [])}
    if set(package_items) != set(MESSAGEPACK_PACKAGES):
        raise ValueError('managed_package_inventory_mismatch')
    locked = expected_packages or package_lock(ROOT / f'src/{MESSAGEPACK_PROJECT}/{MESSAGEPACK_PROJECT}.csproj')['dependencies']['.NETStandard,Version=v2.1']
    for package_id, version in MESSAGEPACK_PACKAGES.items():
        if package_items[package_id]['version'] != version or package_items[package_id]['contentHash'] != locked[package_id]['contentHash']:
            raise ValueError('managed_package_pin_mismatch: ' + package_id)
    if manifest.get('capabilities', {}).get('productionMessagePack') != 'unavailable':
        raise ValueError('qualification_codec_mislabeled')
    validate_native_manifest(manifest, output, qualification, expected_pinvoke)
    return manifest


def build_bundle(output, sqlite_source, allow_dirty=False, skip_build=False):
    root, output = ROOT.resolve(), output.resolve()
    if output != root / 'artifacts/sdk':
        raise ValueError('output_must_be_dedicated_artifacts_sdk_directory')
    if (root / 'artifacts/sdk').is_symlink() or (root / 'artifacts').resolve() != root / 'artifacts':
        raise ValueError('artifact_symlink_not_supported')
    revision = run(['git', 'rev-parse', 'HEAD'], capture=True)
    dirty = bool(run(['git', 'status', '--porcelain', '--untracked-files=normal'], capture=True))
    if dirty and not allow_dirty:
        raise ValueError('source_tree_dirty: commit changes or use --allow-dirty for development')
    projects = sorted((root / 'src').glob('*/*.csproj'))
    validate_inputs(root, projects)
    if not skip_build:
        run(['dotnet', 'restore', 'GamePlatform.sln', '--locked-mode'])
        run(['dotnet', 'build', 'GamePlatform.sln', '-c', 'Release', '--no-restore', '-t:Rebuild',
             '-p:PathMap=' + str(root) + '=/_/'])
    runtime_assets, packages, package_root = resolve_package_assets(root / f'src/{MESSAGEPACK_PROJECT}/{MESSAGEPACK_PROJECT}.csproj')
    qualification = acquire_sqlite(sqlite_source.resolve())
    artifacts = root / 'artifacts'
    artifacts.mkdir(exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix='sdk-stage-', dir=artifacts)).resolve()
    try:
        managed = stage / 'managed'
        managed.mkdir()
        for project in projects:
            folder = project.parent / 'bin/Release/netstandard2.1'
            for suffix in ('.dll', '.pdb'):
                source = folder / (project.stem + suffix)
                if not source.is_file():
                    raise ValueError('built_artifact_missing: ' + str(source))
                shutil.copy2(source, managed / source.name)
            xml = folder / (project.stem + '.xml')
            if xml.is_file():
                shutil.copy2(xml, managed / xml.name)
        for _, _, source in runtime_assets:
            target = managed / source.name
            if target.exists():
                raise ValueError('duplicate_managed_asset: ' + source.name)
            shutil.copy2(source, target)
        for dll in sorted(managed.glob('*.dll')):
            (managed / (dll.name + '.meta')).write_text(managed_meta(dll.name), encoding='utf-8', newline='\n')
        metadata = inspect(sorted(managed.glob('*.dll')))
        validate_closure(metadata, [item['name'] for item in metadata])
        copy_package_notices(stage, runtime_assets, packages, package_root)
        native_entries, qualified = [], {item['target']: item for item in qualification['inputs']}
        for target, relative in NATIVE_TARGETS.items():
            source = sqlite_source / relative
            destination = stage / 'native' / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            shutil.copy2(source.with_name(source.name + '.meta'), destination.with_name(destination.name + '.meta'))
            native_entries.append({'target': target, 'path': destination.relative_to(stage).as_posix(),
                                   'importMetadata': destination.with_name(destination.name + '.meta').relative_to(stage).as_posix(),
                                   'sha256': qualified[target]['sha256'],
                                   'importMetadataSha256': qualified[target]['importMetadataSha256'],
                                   'execution': qualified[target]['execution']})
        aot = stage / 'aot'
        aot.mkdir()
        shutil.copy2(root / 'integration/unity/package/link.xml', aot / 'link.xml')
        shutil.copy2(root / 'integration/unity/package/aot-inventory.json', aot / 'aot-inventory.json')
        for source, target in [('architecture.json', 'architecture.json'), ('features.json', 'features.json'),
                               ('contracts/snapshot.json', 'contract-snapshot.json'), ('scripts/sdk-bundle-README.md', 'README.md'),
                               ('integration/unity/package/lifecycle-manifest.json', 'lifecycle-manifest.json')]:
            shutil.copy2(root / source, stage / target)
        files = [{'path': p.relative_to(stage).as_posix(), 'sha256': digest(p), 'bytes': p.stat().st_size}
                 for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = {
            'schemaVersion': 3, 'version': ET.parse(root / 'Directory.Build.props').findtext('.//PackageVersion'),
            'sourceCommit': revision, 'sourceDirty': dirty, 'managedRuntime': 'netstandard2.1',
            'nativeLibrariesIncluded': True, 'published': False, 'duplicateUpmAcquisitionAllowed': False,
            'projects': [p.stem for p in projects], 'managedAssemblyNames': [item['name'] for item in metadata],
            'assemblies': metadata, 'managedPackages': packages, 'nativeLibraries': native_entries,
            'pinvoke': pinvoke_inventory(), 'sqliteCandidate': qualification['candidate'],
            'capabilities': {'coreContracts': 'included', 'wireDtoContracts': 'included', 'safeDiagnostics': 'included',
                'messagePackQualificationCandidate': 'included', 'productionMessagePack': 'unavailable',
                'nativeSqliteInputs': 'included-unexecuted-by-unity', 'unityImport': 'metadata-only-unverified',
                'aotAndDevice': 'unverified', 'liveBackendIntegration': 'unverified'},
            'features': json.loads((root / 'features.json').read_text()),
            'build': {'configuration': 'Release', 'pathMap': '/_/', 'sdkVersion': run(['dotnet', '--version'], capture=True),
                      'buildSkipped': skip_build}, 'files': files,
        }
        (stage / 'dependency-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
        verify_bundle(stage)
        backup = artifacts / 'sdk-previous'
        if backup.exists():
            if backup.is_symlink() or backup.resolve() != root / 'artifacts/sdk-previous':
                raise ValueError('backup_path_invalid')
            shutil.rmtree(backup)
        if output.exists():
            output.rename(backup)
        try:
            stage.rename(output)
        except OSError:
            if backup.exists():
                backup.rename(output)
            raise
        print(f'Packaged {len(metadata)} managed assemblies and {len(native_entries)} native targets with exact notices at {output}; nothing published.')
        return manifest
    finally:
        if stage.exists() and stage.parent == artifacts.resolve():
            shutil.rmtree(stage)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--allow-dirty', action='store_true', help='Mark development bundle dirty; not for pinned import.')
    parser.add_argument('--no-build', action='store_true', help='Inspect existing outputs; records buildSkipped=true.')
    parser.add_argument('--verify', action='store_true', help='Verify existing inventory, hashes and declared closure.')
    parser.add_argument('--sqlite-source', type=Path, default=ROOT / 'artifacts/acquisition/unity-sqlite-net',
                        help='Clean exact candidate checkout; cloned into artifacts/acquisition by default.')
    args = parser.parse_args()
    output = ROOT / 'artifacts/sdk'
    if args.verify:
        verify_bundle(output)
        print('Bundle inventory, hashes, managed closure, licenses, native targets and AOT metadata verified.')
    else:
        build_bundle(output, args.sqlite_source, args.allow_dirty, args.no_build)


if __name__ == '__main__':
    main()
