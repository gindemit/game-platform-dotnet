#!/usr/bin/env python3
"""Build a commit-pinned managed SDK bundle with PE-verified dependency closure."""
from __future__ import annotations
import argparse
import hashlib
import json
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXCLUDED = {'GamePlatform.Serialization.MessagePack':
            'qualification_only; complete transitive license/native/AOT bundle remains CL-015'}


def run(args, capture=False):
    result = subprocess.run(args, cwd=ROOT, check=True, text=True,
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
        if assembly['targetFramework'] != '.NETStandard,Version=v2.1':
            raise ValueError('unsupported_managed_target: ' + assembly['name'])
        for reference in assembly['references']:
            # Broaden only with reviewed package/license inputs.
            if reference['name'] not in names and reference['name'] != 'netstandard':
                raise ValueError('unresolved_runtime_dependency: ' + reference['name'])


def validate_inputs(root, projects):
    for project in projects:
        if ET.parse(project).findall('.//PackageReference'):
            raise ValueError('runtime_package_requires_bundle_license_review: ' + project.stem)
        lock = json.loads(project.with_name('packages.lock.json').read_text())
        for group in lock.get('dependencies', {}).values():
            for name, item in group.items():
                if item.get('type') != 'Project':
                    raise ValueError('unreviewed_runtime_lock_dependency: ' + name)
    if not (root / 'LICENSE-NOTICE.md').is_file():
        raise ValueError('license_notice_missing')


def verify_bundle(output):
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
    validate_closure(manifest['assemblies'], manifest['projects'])
    return manifest


def build_bundle(output, allow_dirty=False, skip_build=False):
    root, output = ROOT.resolve(), output.resolve()
    # Never recursively delete all artifacts: other tasks keep evidence there.
    if output != root / 'artifacts/sdk':
        raise ValueError('output_must_be_dedicated_artifacts_sdk_directory')
    if (root / 'artifacts/sdk').is_symlink() or (root / 'artifacts').resolve() != root / 'artifacts':
        raise ValueError('artifact_symlink_not_supported')
    revision = run(['git', 'rev-parse', 'HEAD'], capture=True)
    dirty = bool(run(['git', 'status', '--porcelain', '--untracked-files=normal'], capture=True))
    if dirty and not allow_dirty:
        raise ValueError('source_tree_dirty: commit changes or use --allow-dirty for development')
    projects = [p for p in sorted((root / 'src').glob('*/*.csproj')) if p.stem not in EXCLUDED]
    validate_inputs(root, projects)
    if not skip_build:
        run(['dotnet', 'restore', 'GamePlatform.sln', '--locked-mode'])
        run(['dotnet', 'build', 'GamePlatform.sln', '-c', 'Release', '--no-restore',
             '-t:Rebuild', '-p:PathMap=' + str(root) + '=/_/'])
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
        metadata = inspect(sorted(managed.glob('*.dll')))
        validate_closure(metadata, [p.stem for p in projects])
        notices = stage / 'licenses'
        notices.mkdir()
        shutil.copy2(root / 'LICENSE-NOTICE.md', notices / 'LICENSE-NOTICE.md')
        for source, target in [('architecture.json', 'architecture.json'), ('features.json', 'features.json'),
                               ('contracts/snapshot.json', 'contract-snapshot.json'),
                               ('scripts/sdk-bundle-README.md', 'README.md')]:
            shutil.copy2(root / source, stage / target)
        files = [{'path': p.relative_to(stage).as_posix(), 'sha256': digest(p), 'bytes': p.stat().st_size}
                 for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = {
            'schemaVersion': 2, 'version': ET.parse(root / 'Directory.Build.props').findtext('.//PackageVersion'),
            'sourceCommit': revision, 'sourceDirty': dirty, 'managedRuntime': 'netstandard2.1',
            'nativeLibrariesIncluded': False, 'published': False,
            'projects': [p.stem for p in projects], 'assemblies': metadata,
            'excludedProjects': EXCLUDED,
            'capabilities': {'coreContracts': 'included', 'wireDtoContracts': 'included',
                'safeDiagnostics': 'included', 'productionMessagePack': 'unavailable', 'nativeSqlite': 'absent',
                'unityImport': 'unverified', 'aotAndDevice': 'unverified', 'liveBackendIntegration': 'unverified'},
            'features': json.loads((root / 'features.json').read_text()),
            'build': {'configuration': 'Release', 'pathMap': '/_/', 'sdkVersion': run(['dotnet', '--version'], capture=True),
                      'buildSkipped': skip_build},
            'files': files,
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
        print(f'Packaged {len(metadata)} managed assemblies with symbols, dependency closure and licenses at {output}; nothing published.')
        return manifest
    finally:
        if stage.exists() and stage.parent == artifacts.resolve():
            shutil.rmtree(stage)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--allow-dirty', action='store_true', help='Mark development bundle dirty; not for pinned import.')
    parser.add_argument('--no-build', action='store_true', help='Inspect existing outputs; records buildSkipped=true.')
    parser.add_argument('--verify', action='store_true', help='Verify existing inventory, hashes and declared closure.')
    args = parser.parse_args()
    output = ROOT / 'artifacts/sdk'
    if args.verify:
        verify_bundle(output)
        print('Bundle inventory, hashes and declared dependency closure verified.')
    else:
        build_bundle(output, args.allow_dirty, args.no_build)


if __name__ == '__main__':
    main()
