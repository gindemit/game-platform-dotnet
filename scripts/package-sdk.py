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
CANONICAL_REPOSITORY_URL = 'https://github.com/gindemit/game-platform-dotnet'
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
NATIVE_IMPORT_POLICIES = {
    'windows-x86_64': {'enabled': {'Editor', 'Win64'}, 'editorCpu': 'x86_64',
                       'editorOs': 'Windows', 'win64Cpu': 'x86_64'},
    'macos-universal': {'enabled': {'Editor', 'OSXUniversal'}, 'editorCpu': 'AnyCPU',
                        'editorOs': 'OSX', 'osxCpu': 'AnyCPU'},
    'android-arm64': {'enabled': {'Android'}, 'androidCpu': 'ARM64', 'is16KbAligned': 'true'},
}
REQUIRED_NOTICES = {
    'licenses/LICENSE-NOTICE.md',
    'licenses/LICENSE.MessagePack-CSharp.txt',
    'licenses/LICENSE.unity-sqlite-net.txt',
    'licenses/LICENSE.sqlite-net.txt',
    'licenses/LICENSE.SQLite3MultipleCiphers.txt',
}


def run(args, capture=False, cwd=ROOT):
    result = subprocess.run(args, cwd=cwd, check=True, text=True,
                            stdout=subprocess.PIPE if capture else None)
    return result.stdout.strip() if capture else None


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def canonical_source_link(revision):
    if not re.fullmatch(r'[0-9a-f]{40}', revision):
        raise ValueError('source_revision_not_full_sha')
    return json.dumps({
        'documents': {
            '/_/*': f'https://raw.githubusercontent.com/gindemit/game-platform-dotnet/{revision}/*'
        }
    }, separators=(',', ':'), sort_keys=True) + '\n'


def deterministic_build_arguments(root, revision, source_link):
    return [
        'dotnet', 'build', 'GamePlatform.sln', '-c', 'Release', '--no-restore', '-t:Rebuild',
        '-p:PathMap=' + str(root) + '=/_/',
        '-p:ContinuousIntegrationBuild=true',
        '-p:DeterministicSourcePaths=true',
        '-p:EnableSourceControlManagerQueries=false',
        '-p:RepositoryUrl=' + CANONICAL_REPOSITORY_URL,
        '-p:SourceRevisionId=' + revision,
        '-p:IncludeSourceRevisionInInformationalVersion=true',
        '-p:EmbedUntrackedSources=false',
        '-p:CanonicalPackagingSourceLink=' + str(source_link),
    ]


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


def validate_debug_identity(metadata, project_names, revision):
    expected = canonical_source_link(revision)
    by_name = {item['name']: item for item in metadata}
    for name in project_names:
        if by_name[name].get('sourceLink') != expected:
            raise ValueError('canonical_source_link_missing: ' + name)


def package_lock(project):
    return json.loads(project.with_name('packages.lock.json').read_text(encoding='utf-8'))


def validate_inputs(root, projects):
    for project in projects:
        document = ET.parse(project)
        references = [item.attrib['Include'] for item in document.findall('.//PackageReference')]
        lock_packages = {}
        for group in package_lock(project).get('dependencies', {}).values():
            lock_packages.update({name: item for name, item in group.items() if item.get('type') != 'Project'})
        if project.stem == MESSAGEPACK_PROJECT:
            if references != ['MessagePack'] or set(lock_packages) != set(MESSAGEPACK_PACKAGES):
                raise ValueError('messagepack_package_inventory_mismatch')
            for name, version in MESSAGEPACK_PACKAGES.items():
                if lock_packages[name].get('resolved') != version or not lock_packages[name].get('contentHash'):
                    raise ValueError('messagepack_package_pin_mismatch: ' + name)
        elif project.stem == 'GamePlatform.Transport.Http':
            # HTTP composes the reviewed codec; it does not acquire packages itself.
            project_references = {Path(item.attrib['Include']).stem
                                  for item in document.findall('.//ProjectReference')}
            if references or MESSAGEPACK_PROJECT not in project_references:
                raise ValueError('runtime_package_requires_bundle_license_review: ' + project.stem)
            if set(lock_packages) != set(MESSAGEPACK_PACKAGES):
                raise ValueError('http_messagepack_package_inventory_mismatch')
            reviewed = package_lock(root / f'src/{MESSAGEPACK_PROJECT}/{MESSAGEPACK_PROJECT}.csproj')
            reviewed_packages = reviewed['dependencies']['.NETStandard,Version=v2.1']
            for name, version in MESSAGEPACK_PACKAGES.items():
                item = lock_packages[name]
                if (item.get('type') not in ('Transitive', 'CentralTransitive') or
                        item.get('resolved') != version or not item.get('contentHash') or
                        item.get('contentHash') != reviewed_packages[name].get('contentHash') or
                        item.get('dependencies', {}) != reviewed_packages[name].get('dependencies', {})):
                    raise ValueError('http_messagepack_package_pin_mismatch: ' + name)
        elif references or lock_packages:
            raise ValueError('runtime_package_requires_bundle_license_review: ' + project.stem)
    for required in ('LICENSE-NOTICE.md', 'integration/unity/package/LICENSE.MessagePack-CSharp.txt',
                     'integration/unity/package/link.xml', 'integration/unity/package/aot-inventory.json',
                     'integration/unity/package/lifecycle-manifest.json',
                     'integration/unity/package/managed-guid-overrides.json'):
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
        if not path.is_file():
            raise ValueError('native_target_missing: ' + target)
        if digest(path) != expected[target]['sha256']:
            raise ValueError('native_target_hash_mismatch: ' + target)
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
    guid = managed_guid_overrides().get(name, hashlib.sha256(('game-platform-cl015:' + name).encode()).hexdigest()[:32])
    return (f'fileFormatVersion: 2\nguid: {guid}\nPluginImporter:\n  externalObjects: {{}}\n'
            '  serializedVersion: 3\n  iconMap: {}\n  executionOrder: {}\n  defineConstraints: []\n'
            '  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 1\n'
            '  validateReferences: 1\n  platformData:\n'
            '    Any:\n      enabled: 1\n      settings:\n        Exclude WebGL: 1\n'
            '    Editor:\n      enabled: 1\n      settings:\n        CPU: AnyCPU\n'
            '        DefaultValueInitialized: true\n        OS: AnyOS\n'
            '    WindowsStoreApps:\n      enabled: 0\n      settings:\n        CPU: AnyCPU\n'
            '  userData: CL-015 explicit asmdef reference only\n'
            '  assetBundleName: \n  assetBundleVariant: \n')


def managed_guid_overrides(root=ROOT):
    path = root / 'integration/unity/package/managed-guid-overrides.json'
    document = json.loads(path.read_text(encoding='utf-8'))
    if document.get('schemaVersion') != 1:
        raise ValueError('managed_guid_override_schema_invalid')
    entries = document.get('entries', {})
    expected_names = {'GamePlatform.Core.dll', 'GamePlatform.Features.Contracts.dll'}
    if set(entries) != expected_names:
        raise ValueError('managed_guid_override_inventory_mismatch')
    result = {}
    for name, item in entries.items():
        bundle_guid = item.get('bundleGuid', '')
        consumer_guid = item.get('consumerGuid', '')
        generated = hashlib.sha256(('game-platform-cl015:' + name).encode()).hexdigest()[:32]
        if bundle_guid != generated or not re.fullmatch(r'[0-9a-f]{32}', consumer_guid):
            raise ValueError('managed_guid_override_invalid: ' + name)
        result[name] = consumer_guid
    return result


def validate_managed_importer(text, name, expected_guid=None, root=ROOT):
    guid = expected_guid or managed_guid_overrides(root).get(
        name, hashlib.sha256(('game-platform-cl015:' + name).encode()).hexdigest()[:32])
    actual_guid = re.search(r'^guid: ([0-9a-f]{32})$', text, re.M)
    if actual_guid is None or actual_guid.group(1) != guid:
        raise ValueError('managed_import_guid_mismatch: ' + name)
    if not re.search(r'^  isExplicitlyReferenced: 1$', text, re.M):
        raise ValueError('managed_import_not_explicitly_referenced: ' + name)
    required = (
        r'^    Any:\n      enabled: 1\n      settings:\n        Exclude WebGL: 1$',
        r'^    Editor:\n      enabled: 1\n      settings:\n        CPU: AnyCPU\n'
        r'        DefaultValueInitialized: true\n        OS: AnyOS$',
        r'^    WindowsStoreApps:\n      enabled: 0\n      settings:\n        CPU: AnyCPU$',
    )
    if any(re.search(pattern, text, re.M) is None for pattern in required):
        raise ValueError('managed_import_platform_policy_mismatch: ' + name)
    return True


def native_meta(target):
    policy = NATIVE_IMPORT_POLICIES.get(target)
    if policy is None:
        raise ValueError('unknown_native_import_target: ' + target)
    enabled = policy['enabled']
    guid = hashlib.sha256(('game-platform-cl015-native:' + target).encode()).hexdigest()[:32]
    excluded = {
        'Android': '0' if 'Android' in enabled else '1', 'Editor': '0' if 'Editor' in enabled else '1',
        'Linux64': '1', 'OSXUniversal': '0' if 'OSXUniversal' in enabled else '1',
        'VisionOS': '1', 'WebGL': '1', 'Win': '1', 'Win64': '0' if 'Win64' in enabled else '1',
        'WindowsStoreApps': '1', 'iOS': '1', 'tvOS': '1',
    }
    blocks = ['  - first:\n      : Any\n    second:\n      enabled: 0\n      settings:\n' +
              ''.join(f'        Exclude {name}: {value}\n' for name, value in excluded.items())]
    settings_by_platform = {
        'Android': [('AndroidSharedLibraryType', 'Executable'), ('CPU', policy.get('androidCpu', 'None')),
                    ('Is16KbAligned', policy.get('is16KbAligned', 'false'))],
        'Editor': [('CPU', policy.get('editorCpu', 'None')), ('DefaultValueInitialized', 'true'),
                   ('OS', policy.get('editorOs', 'AnyOS'))],
        'Linux64': [('CPU', 'None')], 'OSXUniversal': [('CPU', policy.get('osxCpu', 'None'))],
        'Win': [('CPU', 'None')], 'Win64': [('CPU', policy.get('win64Cpu', 'None'))],
        'WindowsStoreApps': [('CPU', 'None'), ('DontProcess', 'false'), ('SDK', 'AnySDK'),
                             ('ScriptingBackend', 'AnyScriptingBackend')],
        'VisionOS': [('CPU', 'ARM64')], 'iOS': [('CPU', 'AnyCPU')], 'tvOS': [('CPU', 'AnyCPU')],
    }
    first_keys = {'Linux64': 'Standalone', 'OSXUniversal': 'Standalone', 'Win': 'Standalone',
                  'Win64': 'Standalone', 'WindowsStoreApps': 'Windows Store Apps', 'iOS': 'iPhone'}
    for platform, settings in settings_by_platform.items():
        first = first_keys.get(platform, platform)
        blocks.append(f'  - first:\n      {first}: {platform}\n    second:\n'
                      f'      enabled: {1 if platform in enabled else 0}\n      settings:\n' +
                      ''.join(f'        {key}: {value}\n' for key, value in settings))
    text = (f'fileFormatVersion: 2\nguid: {guid}\nPluginImporter:\n  externalObjects: {{}}\n'
            '  serializedVersion: 2\n  iconMap: {}\n  executionOrder: {}\n  defineConstraints: []\n'
            '  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n'
            '  validateReferences: 1\n  platformData:\n' + ''.join(blocks) +
            f'  userData: CL-015 isolated {target}\n  assetBundleName: \n  assetBundleVariant: \n')
    validate_native_importer(text, target)
    return text


def validate_native_importer(text, target):
    policy = NATIVE_IMPORT_POLICIES.get(target)
    if policy is None:
        raise ValueError('unknown_native_import_target: ' + target)
    blocks = {}
    pattern = re.compile(r'  - first:\n      (?P<first>[^\n]+)\n    second:\n(?P<body>.*?)(?=  - first:|  userData:)', re.S)
    for match in pattern.finditer(text):
        first, body = match.group('first'), match.group('body')
        platform = first.split(':', 1)[1].strip() if ':' in first else first.strip()
        if platform in blocks:
            raise ValueError('native_import_duplicate_platform: ' + platform)
        enabled = re.search(r'^      enabled: ([01])$', body, re.M)
        if enabled is None:
            raise ValueError('native_import_enabled_missing: ' + platform)
        blocks[platform] = {'enabled': enabled.group(1) == '1',
                            'settings': dict(re.findall(r'^        ([^:\n]+):\s*(.*)$', body, re.M))}
    required = {'Any', 'Android', 'Editor', 'Linux64', 'OSXUniversal', 'Win', 'Win64',
                'WindowsStoreApps', 'VisionOS', 'iOS', 'tvOS'}
    if set(blocks) != required:
        raise ValueError('native_import_platform_inventory_mismatch: ' + target)
    if {name for name, block in blocks.items() if block['enabled']} != policy['enabled']:
        raise ValueError('native_import_cross_platform_enablement: ' + target)
    exclusions = {name: ('0' if name in policy['enabled'] else '1')
                  for name in ('Android', 'Editor', 'OSXUniversal', 'Win64')}
    exclusions.update({'Linux64': '1', 'VisionOS': '1', 'WebGL': '1', 'Win': '1',
                       'WindowsStoreApps': '1', 'iOS': '1', 'tvOS': '1'})
    for name, expected in exclusions.items():
        if blocks['Any']['settings'].get('Exclude ' + name) != expected:
            raise ValueError('native_import_exclusion_conflict: ' + target + ':' + name)
    if target == 'windows-x86_64':
        checks = [('Editor', 'CPU', 'x86_64'), ('Editor', 'OS', 'Windows'), ('Win64', 'CPU', 'x86_64')]
    elif target == 'macos-universal':
        checks = [('Editor', 'CPU', 'AnyCPU'), ('Editor', 'OS', 'OSX'), ('OSXUniversal', 'CPU', 'AnyCPU')]
    else:
        checks = [('Android', 'CPU', 'ARM64'), ('Android', 'Is16KbAligned', 'true')]
    for platform, key, expected in checks:
        if blocks[platform]['settings'].get(key) != expected:
            raise ValueError('native_import_setting_conflict: ' + target + ':' + platform + ':' + key)
    return True


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
        validate_native_importer(meta.read_text(encoding='utf-8'), target)
        if item.get('importMetadataSha256') != qualified[target]['generatedImportMetadataSha256'] or digest(meta) != item['importMetadataSha256']:
            raise ValueError('native_import_metadata_hash_mismatch: ' + target)
    if manifest.get('pinvoke') != (expected_pinvoke or pinvoke_inventory()):
        raise ValueError('pinvoke_inventory_mismatch')
    if not (output / 'aot/link.xml').is_file() or not (output / 'aot/aot-inventory.json').is_file():
        raise ValueError('aot_inventory_missing')


def validate_licenses(output, packages):
    for relative in sorted(REQUIRED_NOTICES):
        if not (output / relative).is_file():
            raise ValueError('required_license_notice_missing: ' + relative)
    for item in packages:
        relative = f'licenses/nuget-metadata/{item["id"]}.{item["version"]}.nuspec'
        path = output / relative
        if not path.is_file():
            raise ValueError('package_license_metadata_missing: ' + item['id'])
        document = ET.parse(path)
        declarations = [document.find('.//{*}license'), document.find('.//{*}licenseUrl')]
        if not any(node is not None and (node.text or '').strip() for node in declarations):
            raise ValueError('package_license_declaration_missing: ' + item['id'])


def verify_bundle(output, qualification=None, expected_packages=None, expected_pinvoke=None, allow_development=False):
    manifest = json.loads((output / 'dependency-manifest.json').read_text())
    if (manifest.get('sourceDirty') is not False or manifest.get('build', {}).get('buildSkipped') is not False) and not allow_development:
        raise ValueError('development_bundle_not_importable')
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
    for dll in sorted((output / 'managed').glob('*.dll')):
        meta = dll.with_name(dll.name + '.meta')
        if not meta.is_file():
            raise ValueError('managed_import_metadata_missing: ' + dll.name)
        validate_managed_importer(meta.read_text(encoding='utf-8'), dll.name)
    validate_closure(manifest['assemblies'], manifest['managedAssemblyNames'])
    if manifest.get('schemaVersion', 0) >= 3:
        validate_debug_identity(manifest['assemblies'], manifest['projects'], manifest['sourceCommit'])
    package_items = {item['id']: item for item in manifest.get('managedPackages', [])}
    if set(package_items) != set(MESSAGEPACK_PACKAGES):
        raise ValueError('managed_package_inventory_mismatch')
    locked = expected_packages or package_lock(ROOT / f'src/{MESSAGEPACK_PROJECT}/{MESSAGEPACK_PROJECT}.csproj')['dependencies']['.NETStandard,Version=v2.1']
    for package_id, version in MESSAGEPACK_PACKAGES.items():
        if package_items[package_id]['version'] != version or package_items[package_id]['contentHash'] != locked[package_id]['contentHash']:
            raise ValueError('managed_package_pin_mismatch: ' + package_id)
    if manifest.get('capabilities', {}).get('productionMessagePack') != 'included':
        raise ValueError('production_codec_missing')
    validate_licenses(output, manifest['managedPackages'])
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
        build_inputs = root / 'artifacts/build-inputs'
        build_inputs.mkdir(parents=True, exist_ok=True)
        source_link = build_inputs / 'source-link.json'
        source_link.write_text(canonical_source_link(revision), encoding='utf-8', newline='\n')
        run(deterministic_build_arguments(root, revision, source_link))
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
        validate_debug_identity(metadata, [project.stem for project in projects], revision)
        copy_package_notices(stage, runtime_assets, packages, package_root)
        native_entries, qualified = [], {item['target']: item for item in qualification['inputs']}
        for target, relative in NATIVE_TARGETS.items():
            source = sqlite_source / relative
            destination = stage / 'native' / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            meta_destination = destination.with_name(destination.name + '.meta')
            meta_destination.write_text(native_meta(target), encoding='utf-8', newline='\n')
            native_entries.append({'target': target, 'path': destination.relative_to(stage).as_posix(),
                                   'importMetadata': meta_destination.relative_to(stage).as_posix(),
                                   'sha256': qualified[target]['sha256'],
                                   'importMetadataSha256': qualified[target]['generatedImportMetadataSha256'],
                                   'execution': qualified[target]['execution']})
        aot = stage / 'aot'
        aot.mkdir()
        shutil.copy2(root / 'integration/unity/package/link.xml', aot / 'link.xml')
        shutil.copy2(root / 'integration/unity/package/aot-inventory.json', aot / 'aot-inventory.json')
        for source, target in [('architecture.json', 'architecture.json'), ('features.json', 'features.json'),
                               ('contracts/snapshot.json', 'contract-snapshot.json'), ('scripts/sdk-bundle-README.md', 'README.md'),
                               ('integration/unity/package/lifecycle-manifest.json', 'lifecycle-manifest.json'),
                               ('integration/unity/package/managed-guid-overrides.json', 'managed-guid-overrides.json')]:
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
                'messagePackQualificationCandidate': 'included', 'productionMessagePack': 'included',
                'nativeSqliteInputs': 'included-unexecuted-by-unity', 'unityImport': 'metadata-only-unverified',
                'aotAndDevice': 'unverified', 'liveBackendIntegration': 'unverified'},
            'features': json.loads((root / 'features.json').read_text()),
            'build': {'configuration': 'Release', 'pathMap': '/_/', 'sdkVersion': run(['dotnet', '--version'], capture=True),
                      'repositoryUrl': CANONICAL_REPOSITORY_URL,
                      'sourceLinkSha256': hashlib.sha256(canonical_source_link(revision).encode()).hexdigest(),
                      'continuousIntegrationBuild': True, 'deterministicSourcePaths': True,
                      'sourceControlManagerQueries': False,
                      'buildSkipped': skip_build}, 'files': files,
        }
        (stage / 'dependency-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
        verify_bundle(stage, allow_development=dirty or skip_build)
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
        if dirty or skip_build:
            print(f'Generated DEVELOPMENT-ONLY bundle at {output}; default verification rejects it and it must not be imported or published.')
        else:
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
    parser.add_argument('--verify-development', action='store_true',
                        help='Inspect a dirty/skipped artifact as DEVELOPMENT ONLY; never makes it importable.')
    parser.add_argument('--sqlite-source', type=Path, default=ROOT / 'artifacts/acquisition/unity-sqlite-net',
                        help='Clean exact candidate checkout; cloned into artifacts/acquisition by default.')
    args = parser.parse_args()
    output = ROOT / 'artifacts/sdk'
    if args.verify and args.verify_development:
        parser.error('--verify and --verify-development are mutually exclusive')
    if args.verify or args.verify_development:
        verify_bundle(output, allow_development=args.verify_development)
        if args.verify_development:
            print('DEVELOPMENT-ONLY inspection passed; artifact remains non-importable and unpublished.')
        else:
            print('Bundle inventory, hashes, managed closure, licenses, native targets and AOT metadata verified.')
    else:
        build_bundle(output, args.sqlite_source, args.allow_dirty, args.no_build)


if __name__ == '__main__':
    main()
