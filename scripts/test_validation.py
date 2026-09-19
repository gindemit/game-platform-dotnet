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

    def arrange_implemented_accounts(self, root: Path, selected_tests: int = 1, report_path: str = "docs/implementation/evidence/CL-101/accounts.trx") -> None:
        stubs = root / "src/GamePlatform.Features/FeatureStubs.cs"
        stubs.write_text(stubs.read_text(encoding="utf-8").replace(
            '    public sealed class AccountsFeature : UnavailableFeature { public AccountsFeature() : base("accounts") { } }\n', ""), encoding="utf-8")
        implementation = root / "src/GamePlatform.Features/Accounts/AccountsFeature.cs"
        implementation.write_text(
            "using System.Threading; using System.Threading.Tasks; using GamePlatform.Core; using GamePlatform.Features.Contracts; "
            "namespace GamePlatform.Features { public sealed class AccountsFeature : IFeatureCapability { "
            "public string FeatureId => \"accounts\"; public FeatureStatus Status => FeatureStatus.Implemented; "
            "public Task<PlatformResult<string>> ExecuteUnavailableAsync(CancellationToken token) => "
            "Task.FromResult(PlatformResult<string>.Success(\"ready\")); } }", encoding="utf-8")
        behavior_test = root / "tests/GamePlatform.Tests/Features/AccountsFeatureTests.cs"
        behavior_test.parent.mkdir(parents=True, exist_ok=True)
        behavior_test.write_text(
            "using GamePlatform.Features; using GamePlatform.Features.Contracts; using Xunit; "
            "public sealed class AccountsFeatureTests { [Fact] public void ReportsImplemented() => "
            "Assert.Equal(FeatureStatus.Implemented, new AccountsFeature().Status); }", encoding="utf-8")
        raw_report = root / report_path
        raw_report.parent.mkdir(parents=True, exist_ok=True)
        raw_report.write_text(
            '<?xml version="1.0" encoding="utf-8"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
            '<Results><UnitTestResult testName="AccountsFeatureTests.ReportsImplemented" outcome="Passed" /></Results>'
            f'<ResultSummary outcome="Completed"><Counters total="{selected_tests}" executed="{selected_tests}" passed="{selected_tests}" failed="0" /></ResultSummary></TestRun>',
            encoding="utf-8")
        relative = "docs/implementation/evidence/CL-101/runtime.json"
        evidence = root / relative
        evidence.write_text(json.dumps({
            "taskId": "CL-101",
            "featureId": "accounts",
            "implementationCommit": "91929801c07c28e40610e7e4f9a25a3e5661406a",
            "sourcePaths": ["src/GamePlatform.Features/Accounts/AccountsFeature.cs"],
            "testPaths": ["tests/GamePlatform.Tests/Features/AccountsFeatureTests.cs"],
            "commands": [{"command": "dotnet test --filter AccountsFeatureTests", "exitCode": 0, "selectedTests": selected_tests,
                          "testNames": ["AccountsFeatureTests.ReportsImplemented"], "reportPath": report_path}],
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
            self.arrange_implemented_accounts(root)
            self.assertEqual([], validate.validate(root))

    def test_implemented_status_rejects_unavailable_feature(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            self.arrange_implemented_accounts(root)
            implementation = root / "src/GamePlatform.Features/Accounts/AccountsFeature.cs"
            implementation.unlink()
            stubs = root / "src/GamePlatform.Features/FeatureStubs.cs"
            text = stubs.read_text(encoding="utf-8").replace(
                "namespace GamePlatform.Features\n{", 'namespace GamePlatform.Features\n{\n    public sealed class AccountsFeature : UnavailableFeature { public AccountsFeature() : base("accounts") { } }')
            stubs.write_text(text, encoding="utf-8")
            self.assertTrue(any("derives from UnavailableFeature" in error for error in validate.validate(root)))

    def test_implemented_status_rejects_missing_report(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            self.arrange_implemented_accounts(root, report_path="docs/implementation/evidence/CL-101/missing.trx")
            (root / "docs/implementation/evidence/CL-101/missing.trx").unlink()
            errors = validate.validate(root)
            self.assertTrue(any("missing or unsafe test report path" in error for error in errors))

    def test_implemented_status_rejects_zero_selected_tests(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            self.arrange_implemented_accounts(root, selected_tests=0)
            errors = validate.validate(root)
            self.assertTrue(any("selected test count is not positive" in error for error in errors))

    def test_implemented_status_rejects_evidence_escape(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            self.arrange_implemented_accounts(root)
            escaped = "docs/implementation/evidence/CL-101/../../escape.json"
            catalog = validate.read_json(root / "features.json")
            catalog["features"][0]["evidence"] = [escaped]
            (root / "features.json").write_text(json.dumps(catalog), encoding="utf-8")
            manifest_path = root / "docs/implementation/execution-manifest.json"
            manifest = validate.read_json(manifest_path)
            next(task for task in manifest["tasks"] if task["id"] == "CL-101")["evidence"] = [escaped]
            next(row for row in manifest["feature_ledger"] if row["feature"] == "Accounts")["sdk_evidence"] = [escaped]
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            self.assertTrue(any("invalid evidence path" in error for error in validate.validate(root)))

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

    def test_forbidden_locked_transitive_package_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.copy_repository(Path(tmp))
            lock_path = root / "src/GamePlatform.Core/packages.lock.json"
            lock = validate.read_json(lock_path)
            framework = next(iter(lock["dependencies"].values()))
            framework["Microsoft.Data.Sqlite"] = {"type": "Transitive", "resolved": "9.0.0", "contentHash": "test"}
            lock_path.write_text(json.dumps(lock), encoding="utf-8")
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
