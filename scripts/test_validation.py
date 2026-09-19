import json
import shutil
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

    def copy_repository(self, target: Path) -> Path:
        source = Path(__file__).resolve().parents[1]
        root = target / "repository"
        shutil.copytree(source, root, ignore=shutil.ignore_patterns("bin", "obj", ".git"))
        return root

    def test_unknown_feature_status_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            catalog = validate.read_json(root / "features.json")
            catalog["features"][0]["status"] = "done"
            (root / "features.json").write_text(json.dumps(catalog), encoding="utf-8")
            self.assertTrue(any("unknown feature status" in error for error in validate.validate(root)))

    def test_fake_implemented_status_without_evidence_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            catalog = validate.read_json(root / "features.json")
            catalog["features"][0]["status"] = "implemented"
            (root / "features.json").write_text(json.dumps(catalog), encoding="utf-8")
            errors = validate.validate(root)
            self.assertTrue(any("manifest ledger is not implemented" in error for error in errors))
            self.assertTrue(any("without catalog evidence" in error for error in errors))

    def test_implemented_status_with_correlated_evidence_is_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            relative = "docs/implementation/evidence/CL-101/runtime.json"
            evidence = root / relative
            evidence.parent.mkdir(parents=True)
            evidence.write_text(json.dumps({
                "taskId": "CL-101",
                "featureId": "accounts",
                "implementationCommit": "a" * 40,
                "sourcePaths": ["src/GamePlatform.Features/FeatureStubs.cs"],
                "testPaths": ["tests/GamePlatform.Tests/M0ScaffoldTests.cs"],
                "commands": [{"command": "dotnet test", "exitCode": 0, "selectedTests": 1}],
            }), encoding="utf-8")
            catalog = validate.read_json(root / "features.json")
            catalog["features"][0].update({"status": "implemented", "evidence": [relative]})
            (root / "features.json").write_text(json.dumps(catalog), encoding="utf-8")
            manifest_path = root / "docs/implementation/execution-manifest.json"
            manifest = validate.read_json(manifest_path)
            task = next(task for task in manifest["tasks"] if task["id"] == "CL-101")
            task.update({"status": "complete", "blocked_reason": "", "evidence": [relative]})
            ledger = next(row for row in manifest["feature_ledger"] if row["feature"] == "Accounts")
            ledger.update({"sdk_status": "implemented", "sdk_evidence": [relative]})
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            self.assertEqual([], validate.validate(root))

    def test_forbidden_direct_and_transitive_references_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            project = root / "src/GamePlatform.Core/GamePlatform.Core.csproj"
            text = project.read_text(encoding="utf-8")
            text = text.replace("</Project>", '<ItemGroup><ProjectReference Include="../GamePlatform.Storage.Sqlite/GamePlatform.Storage.Sqlite.csproj" /></ItemGroup></Project>')
            project.write_text(text, encoding="utf-8")
            errors = validate.validate(root)
            self.assertTrue(any("forbidden references" in error for error in errors))

    def test_forbidden_runtime_package_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            project = root / "src/GamePlatform.Core/GamePlatform.Core.csproj"
            text = project.read_text(encoding="utf-8")
            text = text.replace("</Project>", '<ItemGroup><PackageReference Include="UnityEngine" Version="1.0.0" /></ItemGroup></Project>')
            project.write_text(text, encoding="utf-8")
            errors = validate.validate(root)
            self.assertTrue(any("forbidden runtime packages" in error for error in errors))

    def test_missing_feature_module_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            (root / "src/GamePlatform.Features/Accounts/README.md").unlink()
            self.assertTrue(any("accounts: missing module README" in error for error in validate.validate(root)))

    def test_changed_contract_bytes_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            contract = root / "contracts/platform-policy.json"
            contract.write_bytes(contract.read_bytes() + b" ")
            self.assertTrue(any("contract hash mismatch" in error for error in validate.validate(root)))

    def test_real_repository_passes(self):
        root = Path(__file__).resolve().parents[1]
        self.assertEqual([], validate.validate(root))


if __name__ == "__main__": unittest.main()
