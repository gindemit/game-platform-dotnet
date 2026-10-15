#!/usr/bin/env python3
"""Generate/check the optional navigation source UPM from one committed canonical source revision."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = 'src/GamePlatform.Navigation.Kernel/'
OUTPUT = ROOT / 'upm/com.gindemit.game-platform-navigation'


def git(*args: str) -> bytes:
    return subprocess.check_output(['rtk', 'proxy', 'git', *args], cwd=ROOT)


def guid(path: str) -> str:
    return hashlib.sha256(('game-platform-navigation:' + path).encode()).hexdigest()[:32]


def encode(value: object) -> bytes:
    return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()


def expected(revision: str) -> dict[str, bytes]:
    if not re.fullmatch('[0-9a-f]{40}', revision):
        raise ValueError('A full committed source revision is required.')
    files: dict[str, bytes] = {}
    inventory = []
    for source in git('ls-tree', '-r', '--name-only', revision, '--', SOURCE).decode().splitlines():
        if not source.endswith('.cs') or any(p in source.split('/') for p in ('bin', 'obj')):
            continue
        data = git('show', revision + ':' + source)
        path = 'Runtime/' + source.removeprefix(SOURCE)
        files[path] = data
        inventory.append({'source': source, 'path': path, 'sha256': hashlib.sha256(data).hexdigest()})
    if not inventory:
        raise ValueError('No committed kernel source at selected revision.')
    files['package.json'] = encode({'name': 'com.gindemit.game-platform-navigation', 'version': '0.1.0',
        'displayName': 'Game Platform Navigation Kernel', 'unity': '6000.0',
        'description': 'Optional dependency-free navigation, entry, tutorial and prompt contracts. No mobile SDK.'})
    files['Runtime/GamePlatform.Navigation.Kernel.asmdef'] = encode({'name': 'GamePlatform.Navigation.Kernel',
        'rootNamespace': 'GamePlatform.Navigation.Kernel', 'references': [], 'includePlatforms': [],
        'excludePlatforms': [], 'overrideReferences': True, 'precompiledReferences': [],
        'autoReferenced': False, 'noEngineReferences': True})
    files['Documentation~/provenance.json'] = encode({'sourceRevision': revision, 'sourceRoot': SOURCE,
        'files': inventory, 'delivery': 'source', 'dependencies': [],
        'compatibility': 'Do not install alongside a full SDK payload containing GamePlatform.Navigation.Kernel.dll.'})
    files['README.md'] = (
        '# Optional navigation kernel\n\nGenerated from canonical SDK src at `' + revision + '`.\n\n'
        'Install this Git subdirectory at a full commit SHA and explicitly reference\n'
        '`GamePlatform.Navigation.Kernel` from a consuming asmdef. It includes no Unity,\n'
        'account, HTTP, SQLite, shell artwork or native dependencies.\n\n'
        'The existing mobile SDK pin may coexist only while it does not ship\n'
        '`GamePlatform.Navigation.Kernel.dll`. A later full SDK update must select\n'
        'one delivery of that assembly; the Unity import validator rejects duplicates.\n'
        'This source package does not update the mobile SDK or activate any feature.\n').encode()
    for path in list(files):
        if 'Documentation~' in path.split('/'):
            continue
        importer = 'MonoImporter' if path.endswith('.cs') else 'AssemblyDefinitionImporter' if path.endswith('.asmdef') else 'TextScriptImporter'
        files[path + '.meta'] = (f'fileFormatVersion: 2\nguid: {guid(path)}\n{importer}:\n  externalObjects: {{}}\n').encode()
    directories = {str(parent).replace('\\', '/') for path in files for parent in Path(path).parents
                   if str(parent) != '.' and 'Documentation~' not in str(parent)}
    for path in directories:
        files[path + '.meta'] = (f'fileFormatVersion: 2\nguid: {guid(path + "/")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n').encode()
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--revision')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    revision = args.revision
    if revision is None and args.check:
        revision = json.loads((OUTPUT / 'Documentation~/provenance.json').read_text())['sourceRevision']
    if revision is None:
        parser.error('--revision is required when generating')
    files = expected(revision)
    actual = {p.relative_to(OUTPUT).as_posix(): p.read_bytes() for p in OUTPUT.rglob('*') if p.is_file()} if OUTPUT.exists() else {}
    if args.check:
        if actual != files:
            raise ValueError('Generated navigation package differs from its committed source revision.')
        # Package provenance must also match the current canonical source bytes.
        sources = {SOURCE + path.removeprefix('Runtime/'): data for path, data in files.items() if path.endswith('.cs')}
        if any((ROOT / path).read_bytes().replace(b'\r\n', b'\n') != data.replace(b'\r\n', b'\n') for path, data in sources.items()):
            raise ValueError('Current canonical kernel source differs from generated package.')
        print(json.dumps({'ok': True, 'sourceRevision': revision, 'sourceFiles': len(sources)}))
        return
    if OUTPUT.is_symlink() or (OUTPUT.exists() and any(p.is_symlink() for p in OUTPUT.rglob('*'))):
        raise ValueError('Refuse an unsafe package output.')
    # Only remove previously generated files inside this exact package directory.
    for path in set(actual) - set(files):
        (OUTPUT / path).unlink()
    for path, data in files.items():
        destination = OUTPUT / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    print(json.dumps({'ok': True, 'sourceRevision': revision, 'files': len(files)}))


if __name__ == '__main__':
    main()
