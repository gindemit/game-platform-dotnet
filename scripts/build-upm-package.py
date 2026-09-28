#!/usr/bin/env python3
"""Export and verify the complete Git-installable Unity package from the SDK bundle."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE_NAME = 'com.gindemit.game-platform'
PACKAGE_VERSION = '0.1.0-draft'
UPM_NATIVE_TARGETS = {'windows-x86_64', 'android-arm64'}
WITHHELD_NATIVE_TARGETS = {'macos-universal':
                           'Pinned disposable qualification input; not shipped in the production Git UPM payload.'}
OUTPUT = ROOT / 'upm/com.gindemit.game-platform'


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inventory(root: Path) -> list[dict[str, object]]:
    return [{'path': path.relative_to(root).as_posix(), 'bytes': path.stat().st_size,
             'sha256': sha256(path)} for path in sorted(root.rglob('*'))
            if path.is_file() and path.name != 'package-content-manifest.json']


def fail(code: str) -> None:
    raise ValueError(code)


def load_sdk_packager():
    from importlib.util import module_from_spec, spec_from_file_location
    spec = spec_from_file_location('package_sdk', ROOT / 'scripts/package-sdk.py')
    module = module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def verify_package(root: Path) -> dict:
    if not root.is_dir() or root.is_symlink():
        fail('package_directory_missing_or_unsafe')
    try:
        package = json.loads((root / 'package.json').read_text(encoding='utf-8'))
    except (OSError, json.JSONDecodeError):
        fail('malformed_package_json')
    if package.get('name') != PACKAGE_NAME or package.get('version') != PACKAGE_VERSION:
        fail('package_identity_mismatch')
    try:
        manifest = json.loads((root / 'package-content-manifest.json').read_text(encoding='utf-8'))
    except (OSError, json.JSONDecodeError):
        fail('malformed_content_manifest')
    expected = manifest.get('files')
    if not isinstance(expected, list) or len({item.get('path') for item in expected if isinstance(item, dict)}) != len(expected):
        fail('malformed_content_inventory')

    dlls = sorted(path for path in root.glob('Runtime/Plugins/Managed/**/*')
                  if path.is_file() and path.suffix.casefold() == '.dll')
    names = [path.stem.casefold() for path in dlls]
    if len(names) != len(set(names)):
        fail('duplicate_managed_assembly')
    for required in ('Runtime/link.xml', 'Documentation~/aot-inventory.json',
                     'Documentation~/lifecycle-manifest.json', 'LICENSES/LICENSE-NOTICE.md',
                     'Documentation~/sdk/dependency-manifest.json'):
        if not (root / required).is_file():
            fail('required_package_file_missing: ' + required)

    dependency = json.loads((root / 'Documentation~/sdk/dependency-manifest.json').read_text(encoding='utf-8'))
    if dependency.get('sourceDirty') is not False or dependency.get('build', {}).get('buildSkipped') is not False:
        fail('source_bundle_not_importable')
    if dependency.get('sourceCommit') != manifest.get('sourceCommit'):
        fail('source_identity_mismatch')
    if manifest.get('sourceBundleManifestSha256') != sha256(root / 'Documentation~/sdk/dependency-manifest.json'):
        fail('source_bundle_manifest_hash_mismatch')
    packager = load_sdk_packager()
    guid_overrides = packager.managed_guid_overrides()
    if manifest.get('preservedManagedGuids') != guid_overrides:
        fail('managed_guid_provenance_mismatch')
    expected_managed = {name + '.dll' for name in dependency.get('managedAssemblyNames', [])}
    actual_managed = {path.name for path in dlls}
    if expected_managed != actual_managed:
        fail('managed_payload_inventory_mismatch')
    source_files = {item.get('path'): item for item in dependency.get('files', [])}
    for path in dlls:
        meta = path.with_name(path.name + '.meta')
        if not meta.is_file():
            fail('managed_import_metadata_missing: ' + path.name)
        source_meta = source_files.get('managed/' + path.name + '.meta')
        if source_meta is None or sha256(meta) != source_meta.get('sha256'):
            fail('managed_import_metadata_hash_mismatch: ' + path.name)
        try:
            packager.validate_managed_importer(meta.read_text(encoding='utf-8'), path.name)
        except ValueError as error:
            fail(str(error))
    native_entries = [item for item in dependency.get('nativeLibraries', [])
                      if item.get('target') in UPM_NATIVE_TARGETS]
    expected_native = {item['path'] for item in native_entries}
    expected_withheld = {item['target'] for item in dependency.get('nativeLibraries', [])
                         if item.get('target') in WITHHELD_NATIVE_TARGETS}
    if expected_withheld != set(WITHHELD_NATIVE_TARGETS) or manifest.get('withheldNativeTargets') != WITHHELD_NATIVE_TARGETS:
        fail('withheld_native_target_policy_mismatch')
    actual_native = {path.relative_to(root).as_posix().replace('Runtime/Plugins/native/', 'native/')
                     for path in root.glob('Runtime/Plugins/native/**/*') if path.is_file() and not path.name.endswith('.meta')}
    if expected_native != actual_native:
        fail('native_payload_inventory_mismatch')
    expected_native_metadata = {item['importMetadata'] for item in native_entries}
    actual_native_metadata = {path.relative_to(root).as_posix().replace('Runtime/Plugins/native/', 'native/')
                              for path in root.glob('Runtime/Plugins/native/**/*')
                              if path.is_file() and path.name.endswith('.meta')
                              and path.with_name(path.name[:-5]).is_file()
                              and path.with_name(path.name[:-5]).suffix.lower() in ('.so', '.dll', '.dylib')}
    if expected_native_metadata != actual_native_metadata:
        fail('native_import_metadata_inventory_mismatch')
    for item in native_entries:
        binary = root / 'Runtime/Plugins/native' / item['path'].removeprefix('native/')
        meta = root / 'Runtime/Plugins/native' / item['importMetadata'].removeprefix('native/')
        if not binary.is_file() or not meta.is_file():
            fail('native_import_metadata_missing: ' + item['target'])
        if sha256(binary) != item.get('sha256'):
            fail('native_payload_hash_mismatch: ' + item['target'])
        if sha256(meta) != item.get('importMetadataSha256'):
            fail('native_import_metadata_hash_mismatch: ' + item['target'])
        expected_guid = hashlib.sha256(('game-platform-cl015-native:' + item['target']).encode()).hexdigest()[:32]
        text = meta.read_text(encoding='utf-8')
        actual_guid = re.search(r'^guid: ([0-9a-f]{32})$', text, re.M)
        if actual_guid is None or actual_guid.group(1) != expected_guid:
            fail('native_import_guid_mismatch: ' + item['target'])
        try:
            packager.validate_native_importer(text, item['target'])
        except ValueError as error:
            fail(str(error))

    verify_stable_asset_metadata(root)
    actual = inventory(root)
    if actual != expected:
        expected_by_path = {item.get('path'): item for item in expected}
        actual_by_path = {item['path']: item for item in actual}
        if set(actual_by_path) != set(expected_by_path):
            fail('package_file_inventory_mismatch')
        for path in sorted(actual_by_path):
            if actual_by_path[path] != expected_by_path[path]:
                fail('package_file_hash_mismatch: ' + path)
    return manifest


def put(source: Path, stage: Path, relative: str) -> None:
    target = stage / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)


def asset_meta_guid(relative: str) -> str:
    return hashlib.sha256(('game-platform-upm-asset:' + relative).encode()).hexdigest()[:32]


def asset_importer(relative: str) -> str:
    return ('TextScriptImporter' if Path(relative).suffix.lower() in
            {'.json', '.xml', '.md', '.txt', '.nuspec'} else 'DefaultImporter')


def write_stable_asset_metadata(root: Path) -> None:
    """Write stable Unity sidecars for all visible generic assets and folders."""
    visible_dirs = [path for path in root.rglob('*') if path.is_dir()
                    and 'Documentation~' not in path.relative_to(root).parts]
    for path in sorted(visible_dirs, key=lambda item: (len(item.relative_to(root).parts), item.as_posix())):
        relative = path.relative_to(root).as_posix()
        meta = path.with_name(path.name + '.meta')
        if not meta.exists():
            guid = asset_meta_guid(relative + '/')
            meta.write_text(
                f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n'
                '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',
                encoding='utf-8', newline='\n')
    visible_files = [path for path in root.rglob('*') if path.is_file()
                     and path.suffix != '.meta'
                     and 'Documentation~' not in path.relative_to(root).parts]
    for path in sorted(visible_files):
        relative = path.relative_to(root).as_posix()
        meta = path.with_name(path.name + '.meta')
        if not meta.exists():
            guid = asset_meta_guid(relative)
            meta.write_text(
                f'fileFormatVersion: 2\nguid: {guid}\n{asset_importer(relative)}:\n'
                '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',
                encoding='utf-8', newline='\n')


def verify_stable_asset_metadata(root: Path) -> None:
    visible_dirs = [path for path in root.rglob('*') if path.is_dir()
                    and 'Documentation~' not in path.relative_to(root).parts]
    for path in visible_dirs:
        relative = path.relative_to(root).as_posix()
        meta = path.with_name(path.name + '.meta')
        if not meta.is_file():
            fail('package_folder_meta_missing: ' + relative)
        text = meta.read_text(encoding='utf-8')
        guid = re.search(r'^guid: ([0-9a-f]{32})$', text, re.M)
        if (guid is None or guid.group(1) != asset_meta_guid(relative + '/') or
                not re.search(r'^folderAsset: yes$', text, re.M)):
            fail('package_folder_meta_invalid: ' + relative)
    visible_files = [path for path in root.rglob('*') if path.is_file()
                     and path.suffix != '.meta'
                     and 'Documentation~' not in path.relative_to(root).parts]
    for path in visible_files:
        relative = path.relative_to(root).as_posix()
        meta = path.with_name(path.name + '.meta')
        if not meta.is_file():
            fail('package_asset_meta_missing: ' + relative)
        text = meta.read_text(encoding='utf-8')
        if relative.startswith('Runtime/Plugins/Managed/') and relative.endswith('.dll'):
            continue
        if relative.startswith('Runtime/Plugins/native/') and path.suffix.lower() in ('.so', '.dll', '.dylib'):
            continue
        guid = re.search(r'^guid: ([0-9a-f]{32})$', text, re.M)
        if (guid is None or guid.group(1) != asset_meta_guid(relative) or
                f'{asset_importer(relative)}:' not in text):
            fail('package_asset_meta_invalid: ' + relative)


def export_package(bundle: Path, output: Path) -> None:
    module = load_sdk_packager()
    source_manifest = module.verify_bundle(bundle)
    if source_manifest['version'] != PACKAGE_VERSION:
        fail('source_package_version_mismatch')

    output = output.resolve()
    if output != OUTPUT.resolve() or OUTPUT.is_symlink():
        fail('output_must_be_upm_package_directory')
    output.parent.mkdir(parents=True, exist_ok=True)
    previous = output.with_name(output.name + '.previous')
    if previous.is_symlink() or (previous.exists() and previous.resolve() != output.parent / (output.name + '.previous')):
        fail('previous_package_path_invalid')
    stage = Path(tempfile.mkdtemp(prefix='.game-platform-upm-', dir=output.parent))
    try:
        (stage / 'Runtime').mkdir()
        (stage / 'Documentation~').mkdir()
        (stage / 'LICENSES').mkdir()
        package_json = {
            'name': PACKAGE_NAME, 'version': PACKAGE_VERSION,
            'displayName': 'Game Platform SDK',
            'description': 'Portable game platform contracts, storage, synchronization and transport runtime.',
            'unity': '2021.3',
            'documentationUrl': 'https://github.com/gindemit/game-platform-dotnet/tree/main/upm/com.gindemit.game-platform',
            'author': {'name': 'gindemit'},
        }
        (stage / 'package.json').write_text(json.dumps(package_json, indent=2) + '\n', encoding='utf-8', newline='\n')
        (stage / 'README.md').write_text(
            '# Game Platform SDK for Unity\n\n'
            'This Git UPM package contains the deterministic, verified managed and native payload '
            'built from the portable .NET SDK source. Install this package by a full Git commit '
            'and package subpath. The package does not contain game rules, scenes, grids, levels, '
            'or campaign policy. See `Documentation~/sdk/README.md` and the lifecycle manifest '
            'for supported ownership and update behavior. DLL importer metadata preserves the '
            'Core and Features.Contracts consumer GUIDs, requires asmdef references, and excludes '
            'WebGL. Native plugins are limited to the '
            'reviewed Android ARM64 and Windows x86-64 targets; the pinned macOS dylib is '
            'withheld for disposable qualification only.\n', encoding='utf-8', newline='\n')

        native_targets = {}
        for item in source_manifest['nativeLibraries']:
            native_targets[item['path']] = item['target']
            native_targets[item['importMetadata']] = item['target']
        for item in source_manifest['files']:
            source = bundle / item['path']
            relative = item['path']
            if relative.startswith('managed/'):
                leaf = relative.removeprefix('managed/')
                if leaf.endswith('.dll') or leaf.endswith('.dll.meta'):
                    put(source, stage, 'Runtime/Plugins/Managed/' + leaf)
                else:
                    put(source, stage, 'Documentation~/symbols/' + leaf)
            elif relative.startswith('native/'):
                target = native_targets.get(relative)
                if target not in UPM_NATIVE_TARGETS:
                    continue
                put(source, stage, 'Runtime/Plugins/native/' + relative.removeprefix('native/'))
            elif relative == 'aot/link.xml':
                put(source, stage, 'Runtime/link.xml')
            elif relative.startswith('aot/'):
                put(source, stage, 'Documentation~/' + relative.removeprefix('aot/'))
            elif relative.startswith('licenses/'):
                put(source, stage, 'LICENSES/' + relative.removeprefix('licenses/'))
            elif relative == 'lifecycle-manifest.json':
                put(source, stage, 'Documentation~/lifecycle-manifest.json')
            else:
                put(source, stage, 'Documentation~/sdk/' + relative)

        put(bundle / 'dependency-manifest.json', stage,
            'Documentation~/sdk/dependency-manifest.json')

        package_manifest_path = stage / 'package-content-manifest.json'
        package_manifest_path.write_text('{}\n', encoding='utf-8', newline='\n')
        write_stable_asset_metadata(stage)
        manifest = {
            'schemaVersion': 1, 'name': PACKAGE_NAME, 'version': PACKAGE_VERSION,
            'sourceCommit': source_manifest['sourceCommit'],
            'sourceBundleManifestSha256': sha256(bundle / 'dependency-manifest.json'),
            'preservedManagedGuids': module.managed_guid_overrides(),
            'withheldNativeTargets': WITHHELD_NATIVE_TARGETS,
            'runtimeSource': 'src/ (canonical .NET SDK source)',
            'generatedPayload': True, 'files': inventory(stage),
        }
        package_manifest_path.write_text(
            json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
        verify_package(stage)
        if previous.exists():
            shutil.rmtree(previous)
        if output.exists():
            output.rename(previous)
        try:
            stage.rename(output)
        except OSError:
            if previous.exists():
                previous.rename(output)
            raise
        if previous.exists():
            shutil.rmtree(previous)
    finally:
        if stage.exists():
            shutil.rmtree(stage)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path, default=ROOT / 'artifacts/sdk')
    parser.add_argument('--verify', action='store_true')
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    if args.verify:
        verify_package(args.output.resolve())
        print('UPM package identity, complete inventory, hashes and native payload verified.')
    else:
        export_package(args.bundle.resolve(), args.output)
        print(f'Generated complete Git UPM package at {args.output}; nothing published.')


if __name__ == '__main__':
    main()
