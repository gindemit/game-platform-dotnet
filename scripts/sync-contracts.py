#!/usr/bin/env python3
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath


def sync(source, target):
    source, target = source.resolve(), target.resolve()
    snapshot_bytes = (source / 'snapshot.json').read_bytes()
    snapshot = json.loads(snapshot_bytes)
    if snapshot.get('schemaVersion') != 1 or not snapshot.get('files'):
        raise ValueError('Nonempty reviewed schemaVersion 1 snapshot required')
    copies = []
    for relative, expected in snapshot['files'].items():
        path = PurePosixPath(relative)
        if path.is_absolute() or '..' in path.parts or '\\' in relative or ':' in relative or relative == 'snapshot.json':
            raise ValueError('Unsafe canonical path: ' + relative)
        origin, destination = (source / relative).resolve(), (target / relative).resolve()
        if not origin.is_relative_to(source) or not destination.is_relative_to(target):
            raise ValueError('Canonical path escapes root')
        content = origin.read_bytes()
        if hashlib.sha256(content).hexdigest() != expected:
            raise ValueError('Canonical digest mismatch: ' + relative)
        copies.append((destination, content))
    if (target / 'snapshot.json').is_symlink():
        raise ValueError('Snapshot destination must not be a symlink')
    for destination, content in copies:
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)
    (target / 'snapshot.json').write_bytes(snapshot_bytes)
    return len(copies)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Explicitly copy the complete reviewed canonical snapshot.')
    parser.add_argument('--source', required=True, type=Path, help='reviewed backend repository root')
    args = parser.parse_args()
    count = sync(args.source / 'contracts', Path(__file__).resolve().parents[1] / 'contracts')
    print(f'Copied {count} reviewed canonical files and snapshot; verify pins and review the diff.')
