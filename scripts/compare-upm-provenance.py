#!/usr/bin/env python3
"""Compare two existing verified UPM trees without generating or changing either.

The caller must separately establish how the reproduced input was built from the
explicit source commit. Tree equality alone is not proof of that build history.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import re
import stat
from pathlib import Path


def checked_directory(path: Path) -> Path:
    # Inspect before resolve(), which would otherwise hide a symbolic root.
    for candidate in (path, *path.parents):
        if candidate.exists() or candidate.is_symlink():
            attributes = getattr(candidate.lstat(), 'st_file_attributes', 0)
            if candidate.is_symlink() or attributes & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0):
                raise ValueError('linked_input: ' + str(candidate))
    if not path.is_dir():
        raise ValueError('input_directory_missing: ' + str(path))
    return path.resolve()


def inventory(path: Path) -> dict[str, tuple[int, str]]:
    entries = {}
    for item in sorted(path.rglob('*')):
        attributes = getattr(item.lstat(), 'st_file_attributes', 0)
        if item.is_symlink() or attributes & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0):
            raise ValueError('linked_input: ' + str(item))
        if item.is_file():
            data = item.read_bytes()
            entries[item.relative_to(path).as_posix()] = (len(data), hashlib.sha256(data).hexdigest())
        elif not item.is_dir():
            raise ValueError('unsupported_input: ' + str(item))
    return entries


def distinct_directories(committed: Path, reproduced: Path) -> tuple[Path, Path]:
    left, right = checked_directory(committed), checked_directory(reproduced)
    if left == right or left in right.parents or right in left.parents:
        raise ValueError('inputs_must_be_distinct_nonoverlapping_directories')
    return left, right


def compare_trees(committed: Path, reproduced: Path) -> dict:
    left, right = distinct_directories(committed, reproduced)
    expected, actual = inventory(left), inventory(right)
    missing = sorted(expected.keys() - actual.keys())
    extra = sorted(actual.keys() - expected.keys())
    changed = sorted(name for name in expected.keys() & actual.keys() if expected[name] != actual[name])
    if missing or extra or changed:
        raise ValueError('payload_mismatch: ' + json.dumps(
            {'missing': missing, 'extra': extra, 'changed': changed}, sort_keys=True))
    if not expected:
        raise ValueError('empty_input_tree')
    return {'filesCompared': len(expected), 'bytesCompared': sum(item[0] for item in expected.values())}


def require_source_identity(expected: str, *manifests: dict) -> None:
    if re.fullmatch(r'[0-9a-f]{40}', expected) is None:
        raise ValueError('source_commit_must_be_full_lowercase_sha')
    if not manifests or any(manifest.get('sourceCommit') != expected for manifest in manifests):
        raise ValueError('source_commit_mismatch')


def compare_packages(committed: Path, reproduced: Path, source_commit: str,
                     package_version: str) -> dict:
    require_source_identity(source_commit, {'sourceCommit': source_commit})
    left, right = distinct_directories(committed, reproduced)
    # Reject links before asking the existing self-integrity verifier to read files.
    inventory(left)
    inventory(right)
    spec = importlib.util.spec_from_file_location(
        'provenance_upm_verifier', Path(__file__).with_name('build-upm-package.py'))
    assert spec and spec.loader
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    left_manifest = verifier.verify_package(left, package_version)
    right_manifest = verifier.verify_package(right, package_version)
    require_source_identity(source_commit, left_manifest, right_manifest)
    result = compare_trees(left, right)
    return {'status': 'equal_verified_package_trees', 'sourceCommit': source_commit,
            'packageVersion': package_version, **result,
            'limitation': 'Caller must separately prove reproduced input was built from sourceCommit.'}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--committed', required=True, type=Path)
    parser.add_argument('--reproduced', required=True, type=Path)
    parser.add_argument('--source-commit', required=True)
    parser.add_argument('--package-version', required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(compare_packages(args.committed, args.reproduced,
                                          args.source_commit, args.package_version), sort_keys=True))
    except (ValueError, OSError, KeyError, TypeError) as error:
        print(json.dumps({'status': 'failed', 'error': str(error)}, sort_keys=True))
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
