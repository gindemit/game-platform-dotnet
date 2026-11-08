"""Non-UPM fixtures for read-only tree comparison and source identity validation."""
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name('compare-upm-provenance.py')
SPEC = importlib.util.spec_from_file_location('compare_upm_provenance', SCRIPT)
COMPARE = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(COMPARE)


class ProvenanceComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='provenance-non-upm-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.left, self.right = self.root / 'left', self.root / 'right'
        for tree in (self.left, self.right):
            tree.mkdir()
            (tree / 'data').mkdir()
            (tree / 'data/value.bin').write_bytes(b'\x00\xff\x01')
            (tree / '.metadata').write_bytes(b'opaque metadata\n')

    def test_equal_trees_are_compared_without_modification(self):
        before = [COMPARE.inventory(tree) for tree in (self.left, self.right)]
        self.assertEqual({'filesCompared': 2, 'bytesCompared': 19},
                         COMPARE.compare_trees(self.left, self.right))
        self.assertEqual(before, [COMPARE.inventory(tree) for tree in (self.left, self.right)])

    def test_same_length_payload_change_is_rejected(self):
        (self.right / 'data/value.bin').write_bytes(b'\x00\xff\x02')
        with self.assertRaisesRegex(ValueError, 'payload_mismatch.*data/value.bin'):
            COMPARE.compare_trees(self.left, self.right)

    def test_metadata_change_is_not_normalized(self):
        (self.right / '.metadata').write_bytes(b'different metadata\n')
        with self.assertRaisesRegex(ValueError, 'payload_mismatch.*.metadata'):
            COMPARE.compare_trees(self.left, self.right)

    def test_missing_and_extra_files_are_rejected(self):
        (self.right / 'data/value.bin').rename(self.right / 'data/renamed.bin')
        with self.assertRaises(ValueError) as error:
            COMPARE.compare_trees(self.left, self.right)
        detail = json.loads(str(error.exception).split(': ', 1)[1])
        self.assertEqual(['data/value.bin'], detail['missing'])
        self.assertEqual(['data/renamed.bin'], detail['extra'])

    def test_same_or_nested_inputs_are_rejected(self):
        for other in (self.left, self.left / 'data', self.root):
            with self.subTest(other=other), self.assertRaisesRegex(ValueError, 'distinct_nonoverlapping'):
                COMPARE.compare_trees(self.left, other)

    def test_missing_directory_or_regular_file_is_rejected(self):
        for other in (self.root / 'missing', self.right / '.metadata'):
            with self.subTest(other=other), self.assertRaisesRegex(ValueError, 'input_directory_missing'):
                COMPARE.compare_trees(self.left, other)

    def test_empty_trees_are_rejected(self):
        a, b = self.root / 'empty-a', self.root / 'empty-b'
        a.mkdir()
        b.mkdir()
        with self.assertRaisesRegex(ValueError, 'empty_input_tree'):
            COMPARE.compare_trees(a, b)

    def test_explicit_recorded_source_identity_is_accepted(self):
        sha = 'a' * 40
        COMPARE.require_source_identity(sha, {'sourceCommit': sha}, {'sourceCommit': sha})

    def test_bad_or_different_source_identity_is_rejected(self):
        for sha in ('HEAD', 'a' * 39, 'A' * 40, '../source', ''):
            with self.subTest(sha=sha), self.assertRaisesRegex(ValueError, 'full_lowercase_sha'):
                COMPARE.require_source_identity(sha, {'sourceCommit': sha})
        for manifests in ((), ({},), ({'sourceCommit': 'a' * 40}, {'sourceCommit': 'b' * 40})):
            with self.subTest(manifests=manifests), self.assertRaisesRegex(ValueError, 'source_commit_mismatch'):
                COMPARE.require_source_identity('a' * 40, *manifests)

    def test_real_cli_rejects_non_upm_inputs_and_preserves_them(self):
        before = [COMPARE.inventory(tree) for tree in (self.left, self.right)]
        result = subprocess.run([sys.executable, str(SCRIPT), '--committed', str(self.left),
                                 '--reproduced', str(self.right), '--source-commit', 'a' * 40,
                                 '--package-version', '0.1.0-draft'], capture_output=True, text=True)
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertEqual('malformed_package_json', json.loads(result.stdout)['error'])
        self.assertEqual(before, [COMPARE.inventory(tree) for tree in (self.left, self.right)])


if __name__ == '__main__':
    unittest.main()
