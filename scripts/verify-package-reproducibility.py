#!/usr/bin/env python3
"""Build the SDK bundle and Git UPM package in two clean clones and compare outputs."""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


def run(args, cwd, log):
    result = subprocess.run(args, cwd=cwd, text=True, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, check=False)
    log.write_text(result.stdout, encoding='utf-8', newline='\n')
    if result.returncode:
        raise RuntimeError(f'command_failed_{result.returncode}: {args!r}; see {log}')


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inventory(root):
    return {
        path.relative_to(root).as_posix(): sha256(path)
        for path in sorted(root.rglob('*')) if path.is_file()
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--revision', required=True)
    parser.add_argument('--sqlite-source', type=Path, required=True)
    parser.add_argument('--evidence-output', type=Path, required=True)
    args = parser.parse_args()
    if not args.source.resolve().is_dir() or not args.sqlite_source.resolve().is_dir():
        parser.error('source and sqlite-source must be existing directories')
    if args.evidence_output.exists():
        parser.error('evidence-output must not already exist')

    evidence = args.evidence_output.resolve()
    evidence.mkdir(parents=True)
    with tempfile.TemporaryDirectory(prefix='cl015-independent-clones-') as temporary:
        temporary = Path(temporary)
        manifests, inventories, package_manifests, package_inventories = [], [], [], []
        for number, label in enumerate(('clone-a', 'clone-b'), start=1):
            clone = temporary / label
            clone_log = evidence / f'{label}-clone.log'
            run(['git', 'clone', '--no-local', str(args.source.resolve()), str(clone)],
                temporary, clone_log)
            checkout_log = evidence / f'{label}-checkout.log'
            run(['git', 'checkout', '--detach', args.revision], clone, checkout_log)
            # Deliberately vary checkout repository metadata. Packaging must derive
            # source identity from the pinned revision and canonical URL, not origin.
            if number == 2:
                remote_log = evidence / f'{label}-remote.log'
                run(['git', 'remote', 'set-url', 'origin',
                     'https://github.com/gindemit/game-platform-dotnet'], clone, remote_log)
            package_log = evidence / f'{label}-package.log'
            run([sys.executable, 'scripts/package-sdk.py', '--sqlite-source',
                 str(args.sqlite_source.resolve())], clone, package_log)
            verify_log = evidence / f'{label}-verify.log'
            run([sys.executable, 'scripts/package-sdk.py', '--verify'], clone, verify_log)
            upm_log = evidence / f'{label}-upm.log'
            run([sys.executable, 'scripts/build-upm-package.py'], clone, upm_log)
            upm_verify_log = evidence / f'{label}-upm-verify.log'
            run([sys.executable, 'scripts/build-upm-package.py', '--verify'], clone, upm_verify_log)
            bundle = clone / 'artifacts/sdk'
            manifest = bundle / 'dependency-manifest.json'
            shutil.copy2(manifest, evidence / f'{label}-manifest.json')
            manifests.append(manifest.read_bytes())
            inventories.append(inventory(bundle))
            upm = clone / 'upm/com.gindemit.game-platform'
            upm_manifest = upm / 'package-content-manifest.json'
            shutil.copy2(upm_manifest, evidence / f'{label}-upm-manifest.json')
            package_manifests.append(upm_manifest.read_bytes())
            package_inventories.append(inventory(upm))

        if manifests[0] != manifests[1]:
            raise RuntimeError('independent_clone_manifests_differ')
        if inventories[0] != inventories[1]:
            raise RuntimeError('independent_clone_complete_artifacts_differ')
        if package_manifests[0] != package_manifests[1]:
            raise RuntimeError('independent_clone_upm_manifests_differ')
        if package_inventories[0] != package_inventories[1]:
            raise RuntimeError('independent_clone_upm_packages_differ')
        parsed = json.loads(manifests[0])
        summary = {
            'schemaVersion': 1,
            'revision': args.revision,
            'independentCleanClones': 2,
            'freshOutputs': True,
            'variedOriginMetadata': True,
            'manifestsByteEqual': True,
            'completeArtifactsByteEqual': True,
            'upmManifestsByteEqual': True,
            'completeUpmPackagesByteEqual': True,
            'upmContentManifestSha256': hashlib.sha256(package_manifests[0]).hexdigest(),
            'upmFileCountIncludingManifest': len(package_inventories[0]),
            'manifestSha256': hashlib.sha256(manifests[0]).hexdigest(),
            'fileCountIncludingManifest': len(inventories[0]),
            'managedMvids': {item['name']: item['moduleVersionId'] for item in parsed['assemblies']},
            'pdbHashes': {item['path']: item['sha256'] for item in parsed['files']
                          if item['path'].endswith('.pdb')},
            'completeFileHashes': inventories[0],
        }
        (evidence / 'reproducibility-summary.json').write_text(
            json.dumps(summary, indent=2) + '\n', encoding='utf-8', newline='\n')
        print(json.dumps({key: summary[key] for key in (
            'revision', 'manifestSha256', 'fileCountIncludingManifest',
            'manifestsByteEqual', 'completeArtifactsByteEqual')}))


if __name__ == '__main__':
    main()
