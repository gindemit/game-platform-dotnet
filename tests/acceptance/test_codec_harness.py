"""Fail-closed CLI tests, not peer interoperability evidence. Build Release first."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT / 'tests/acceptance/GamePlatform.CodecHarness/bin/Release/net9.0/GamePlatform.CodecHarness.dll'


class HarnessFailureTests(unittest.TestCase):
    def run_cli(self, *args):
        result = subprocess.run(['dotnet', str(DLL), *args], cwd=ROOT, capture_output=True, text=True)
        self.assertIn(result.returncode, (0, 1), result.stdout + result.stderr)
        return result

    def test_live_mode_fails_closed(self):
        self.assertEqual(1, self.run_cli('live', '--root', str(ROOT)).returncode)

    def test_missing_revision_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            self.assertEqual(1, self.run_cli('produce', '--root', str(ROOT), '--output', str(Path(temp)/'out.json')).returncode)

    def test_duplicate_option_fails(self):
        self.assertEqual(1, self.run_cli('self-test', '--root', str(ROOT), '--root', str(ROOT)).returncode)

    def test_raw_peer_token_decode_supports_compact_and_rejects_overflow(self):
        with tempfile.TemporaryDirectory() as temp:
            source, output = Path(temp)/'raw.bin', Path(temp)/'decoded.json'
            source.write_bytes(bytes.fromhex('cd0001'))
            result = self.run_cli('decode', '--root', str(ROOT), '--schema', 'common.schema.json#/$defs/signedInt64',
                '--input', str(source), '--output', str(output))
            self.assertEqual(0, result.returncode)
            self.assertEqual('"1"', output.read_text(encoding='utf-8'))
            source.write_bytes(bytes.fromhex('cf8000000000000000'))
            rejected = Path(temp)/'rejected.json'
            result = self.run_cli('decode', '--root', str(ROOT), '--schema', 'common.schema.json#/$defs/signedInt64',
                '--input', str(source), '--output', str(rejected))
            self.assertEqual(1, result.returncode)
            self.assertFalse(rejected.exists())

    def test_empty_peer_corpus_and_self_producer_fail(self):
        # Synthetic labels/commits are intentionally insufficient for acceptance.
        revision = 'a' * 40
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'peer.json'
            for producer in ('csharp', 'typescript'):
                path.write_text(json.dumps({'formatVersion': 1, 'contractVersion': '0.2.0-core-schema.1',
                    'producer': producer, 'commits': dict(sdk=revision, backend=revision, unity=revision),
                    'cases': [], 'fingerprints': []}), encoding='utf-8')
                result = self.run_cli('consume', '--root', str(ROOT), '--input', str(path),
                    '--sdk-commit', revision, '--backend-commit', revision, '--unity-commit', revision)
                self.assertEqual(1, result.returncode)

    def test_existing_output_is_preserved(self):
        revision = 'a' * 40
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'existing.json'
            path.write_text('preserve', encoding='utf-8')
            result = self.run_cli('produce', '--root', str(ROOT), '--output', str(path),
                '--sdk-commit', revision, '--backend-commit', revision, '--unity-commit', revision)
            self.assertEqual(1, result.returncode)
            self.assertEqual('preserve', path.read_text(encoding='utf-8'))


if __name__ == '__main__':
    unittest.main()
