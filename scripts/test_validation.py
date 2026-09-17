import json
import tempfile
import unittest
from pathlib import Path

import validate


class ValidationTests(unittest.TestCase):
    def test_project_inventory_has_fifteen(self): self.assertEqual(15, len(validate.PROJECTS))
    def test_feature_inventory_has_sixteen(self): self.assertEqual(16, len(validate.FEATURES))

    def test_sha256_is_byte_exact(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "value"
            path.write_bytes(b"abc")
            self.assertEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", validate.sha256(path))

    def test_read_json(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "value.json"
            path.write_text('{"ok": true}', encoding="utf-8")
            self.assertEqual({"ok": True}, validate.read_json(path))

    def test_missing_root_reports_errors(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertTrue(validate.validate(Path(tmp)))

    def test_project_reference_parser(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "x.csproj"
            path.write_text('<Project><ItemGroup><ProjectReference Include="../A/A.csproj" /></ItemGroup></Project>', encoding="utf-8")
            self.assertEqual({"A"}, validate.project_references(path))

    def test_real_repository_passes(self):
        root = Path(__file__).resolve().parents[1]
        self.assertEqual([], validate.validate(root))


if __name__ == "__main__": unittest.main()
