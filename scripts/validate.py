#!/usr/bin/env python3
"""Validate repository topology, truthful feature status, references and protocol pins."""
from __future__ import annotations

import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

PROJECTS = {
    "GamePlatform.Core", "GamePlatform.Diagnostics.Abstractions", "GamePlatform.Features.Contracts",
    "GamePlatform.Backend.Contracts", "GamePlatform.Storage.Abstractions", "GamePlatform.Features",
    "GamePlatform.Sync", "GamePlatform.Navigation", "GamePlatform.Features.Presentation",
    "GamePlatform.Wire.Contracts", "GamePlatform.Transport.Abstractions", "GamePlatform.Transport.Http",
    "GamePlatform.Storage.Sqlite", "GamePlatform.Serialization.MessagePack", "GamePlatform.Serialization.Json",
}
FEATURES = {
    "accounts", "profiles", "catalog", "inventory", "wallet", "entitlements", "reward-fulfillment",
    "progression", "quests", "achievements", "store", "purchases", "leaderboards", "teams",
    "remote-config", "inbox",
}
FEATURE_DIRS = {"reward-fulfillment": "RewardFulfillment", "remote-config": "RemoteConfig"}
FEATURE_STATUSES = {"stubbed", "planned", "implemented", "unverified", "blocked"}
FEATURE_CLASSES = {
    feature: FEATURE_DIRS.get(feature, "".join(part.title() for part in feature.split("-"))) + "Feature"
    for feature in FEATURES
}


def read_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def project_references(project: Path) -> set[str]:
    root = ET.parse(project).getroot()
    return {Path(node.attrib["Include"].replace("\\", "/")).stem for node in root.findall(".//ProjectReference")}


def package_references(project: Path) -> set[str]:
    root = ET.parse(project).getroot()
    return {node.attrib["Include"] for node in root.findall(".//PackageReference")}


def locked_packages(lock_file: Path) -> set[str]:
    lock = read_json(lock_file)
    return {
        package
        for framework in lock.get("dependencies", {}).values()
        for package, metadata in framework.items()
        if metadata.get("type") != "Project"
    }


def transitive_references(project: str, graph: dict[str, set[str]]) -> set[str]:
    result: set[str] = set()
    pending = list(graph.get(project, set()))
    while pending:
        dependency = pending.pop()
        if dependency not in result:
            result.add(dependency)
            pending.extend(graph.get(dependency, set()))
    return result


def evidence_path(root: Path, relative: object, task_id: str) -> Path | None:
    if not isinstance(relative, str) or not relative or "\\" in relative:
        return None
    candidate = Path(relative)
    if candidate.is_absolute() or ".." in candidate.parts:
        return None
    required_parent = Path("docs") / "implementation" / "evidence" / task_id
    if candidate.parts[:len(required_parent.parts)] != required_parent.parts:
        return None
    resolved = (root / candidate).resolve()
    try:
        resolved.relative_to(root.resolve())
    except ValueError:
        return None
    return resolved


def existing_repository_file(root: Path, relative: object) -> bool:
    if not isinstance(relative, str) or not relative or "\\" in relative:
        return False
    candidate = Path(relative)
    if candidate.is_absolute() or ".." in candidate.parts:
        return False
    resolved = (root / candidate).resolve()
    try:
        resolved.relative_to(root.resolve())
    except ValueError:
        return False
    return resolved.is_file()


def feature_is_unavailable(root: Path, feature_id: str) -> bool:
    class_name = FEATURE_CLASSES[feature_id]
    marker = re.compile(rf"\bclass\s+{re.escape(class_name)}\s*:\s*UnavailableFeature\b")
    return any(marker.search(path.read_text(encoding="utf-8")) for path in (root / "src" / "GamePlatform.Features").rglob("*.cs"))


def validate_test_report(root: Path, relative: object, selected_tests: object, test_names: object) -> str | None:
    if not existing_repository_file(root, relative):
        return "missing or unsafe test report path"
    try:
        report = ET.parse(root / str(relative)).getroot()
        counters = report.find(".//{*}Counters")
        if counters is None:
            return "test report has no counters"
        total = int(counters.attrib.get("total", "-1"))
        executed = int(counters.attrib.get("executed", "-1"))
        passed = int(counters.attrib.get("passed", "-1"))
        failed = int(counters.attrib.get("failed", "-1"))
        if not isinstance(selected_tests, int) or selected_tests <= 0:
            return "selected test count is not positive"
        if failed != 0 or total != selected_tests or executed != selected_tests or passed != selected_tests:
            return f"test report counters do not match selected tests ({total}/{executed}/{passed}/{failed})"
        if not isinstance(test_names, list) or len(test_names) != selected_tests or not all(isinstance(name, str) and name for name in test_names):
            return "test name selection does not match selected test count"
        actual_names = {node.attrib.get("testName") for node in report.findall(".//{*}UnitTestResult")}
        if actual_names != set(test_names):
            return "test report result names do not match declared selection"
    except (OSError, ET.ParseError, TypeError, ValueError) as exc:
        return f"invalid test report: {exc}"
    return None


def validate_implemented_evidence(root: Path, entry: dict, ledger: dict, task: dict) -> list[str]:
    feature_id = entry.get("id", "<unknown>")
    task_id = ledger.get("sdk_task", "")
    errors: list[str] = []
    if ledger.get("sdk_status") != "implemented":
        errors.append(f"{feature_id}: catalog implemented but manifest ledger is not implemented")
    if task.get("status") != "complete":
        errors.append(f"{feature_id}: implemented without completed task {task_id}")
    if feature_id in FEATURE_CLASSES and feature_is_unavailable(root, feature_id):
        errors.append(f"{feature_id}: implemented while {FEATURE_CLASSES[feature_id]} derives from UnavailableFeature")
    catalog_evidence = entry.get("evidence")
    ledger_evidence = ledger.get("sdk_evidence")
    task_evidence = task.get("evidence")
    if not isinstance(catalog_evidence, list) or not catalog_evidence:
        errors.append(f"{feature_id}: implemented without catalog evidence")
        return errors
    if catalog_evidence != ledger_evidence or catalog_evidence != task_evidence:
        errors.append(f"{feature_id}: catalog, task and ledger evidence must match exactly")
        return errors
    for relative in catalog_evidence:
        path = evidence_path(root, relative, task_id)
        if path is None:
            errors.append(f"{feature_id}: invalid evidence path {relative!r}")
            continue
        if not path.is_file():
            errors.append(f"{feature_id}: missing evidence file {relative}")
            continue
        try:
            report = read_json(path)
            if report.get("taskId") != task_id or report.get("featureId") != feature_id:
                errors.append(f"{feature_id}: evidence identity mismatch in {relative}")
            commit = report.get("implementationCommit")
            if not isinstance(commit, str) or len(commit) != 40 or any(c not in "0123456789abcdef" for c in commit):
                errors.append(f"{feature_id}: invalid implementation commit in {relative}")
            source_paths = report.get("sourcePaths")
            test_paths = report.get("testPaths")
            commands = report.get("commands")
            if not isinstance(source_paths, list) or not source_paths or not all(existing_repository_file(root, p) for p in source_paths):
                errors.append(f"{feature_id}: evidence lacks existing source paths in {relative}")
            if not isinstance(test_paths, list) or not test_paths or not all(existing_repository_file(root, p) for p in test_paths):
                errors.append(f"{feature_id}: evidence lacks existing test paths in {relative}")
            if not isinstance(commands, list) or not commands:
                errors.append(f"{feature_id}: evidence lacks passing non-empty test commands in {relative}")
            else:
                for command in commands:
                    if not isinstance(command, dict) or command.get("exitCode") != 0:
                        errors.append(f"{feature_id}: evidence contains a failing or malformed command in {relative}")
                        continue
                    report_error = validate_test_report(root, command.get("reportPath"), command.get("selectedTests"), command.get("testNames"))
                    if report_error:
                        errors.append(f"{feature_id}: {report_error} in {relative}")
        except (OSError, AttributeError, json.JSONDecodeError) as exc:
            errors.append(f"{feature_id}: invalid evidence report {relative}: {exc}")
    return errors


def validate(root: Path) -> list[str]:
    errors: list[str] = []
    required = ["AGENTS.md", "README.md", "SECURITY.md", "LICENSE-NOTICE.md", "GamePlatform.sln",
                "Directory.Build.props", "Directory.Packages.props", "architecture.json", "features.json",
                "contracts/snapshot.json", "docs/IMPLEMENTATION_STATUS.md"]
    for item in required:
        if not (root / item).is_file(): errors.append(f"missing required file: {item}")

    try:
        architecture = read_json(root / "architecture.json")
        declared = set(architecture["projects"])
        if declared != PROJECTS: errors.append(f"architecture projects differ: {sorted(declared ^ PROJECTS)}")
        actual_projects = {p.parent.name for p in (root / "src").glob("*/*.csproj")}
        if actual_projects != PROJECTS: errors.append(f"runtime projects differ: {sorted(actual_projects ^ PROJECTS)}")
        allowed_packages = architecture["allowedRuntimePackages"]
        if set(allowed_packages) != PROJECTS:
            errors.append(f"runtime package policy projects differ: {sorted(set(allowed_packages) ^ PROJECTS)}")
        declared_graph = {name: set(refs) for name, refs in architecture["projects"].items()}
        actual_graph: dict[str, set[str]] = {}
        for name in sorted(PROJECTS):
            project = root / "src" / name / f"{name}.csproj"
            tree = ET.parse(project).getroot()
            target = tree.findtext(".//TargetFramework")
            if target != "netstandard2.1": errors.append(f"{name}: target is {target!r}")
            refs = project_references(project)
            actual_graph[name] = refs
            allowed = set(architecture["projects"][name])
            if not refs <= allowed: errors.append(f"{name}: forbidden references {sorted(refs - allowed)}")
            packages = package_references(project) | locked_packages(project.parent / "packages.lock.json")
            forbidden = packages & set(architecture["forbiddenRuntimePackages"])
            undeclared = packages - set(allowed_packages.get(name, []))
            if forbidden: errors.append(f"{name}: forbidden runtime packages {sorted(forbidden)}")
            if undeclared: errors.append(f"{name}: undeclared runtime packages {sorted(undeclared)}")
            if not (project.parent / "README.md").is_file(): errors.append(f"{name}: missing README.md")
        for name in sorted(PROJECTS):
            unknown = declared_graph[name] - PROJECTS
            if unknown: errors.append(f"{name}: unknown allowed references {sorted(unknown)}")
            actual_transitive = transitive_references(name, actual_graph)
            allowed_transitive = transitive_references(name, declared_graph)
            if name in actual_transitive: errors.append(f"{name}: project reference cycle")
            if name in allowed_transitive: errors.append(f"{name}: allowed reference graph contains a cycle")
            if not actual_transitive <= allowed_transitive:
                errors.append(f"{name}: forbidden transitive references {sorted(actual_transitive - allowed_transitive)}")
    except (OSError, KeyError, json.JSONDecodeError, ET.ParseError) as exc:
        errors.append(f"architecture validation failed: {exc}")

    try:
        catalog = read_json(root / "features.json")
        manifest = read_json(root / "docs" / "implementation" / "execution-manifest.json")
        tasks = {task["id"]: task for task in manifest["tasks"]}
        ledgers = {row["feature"].lower().replace("rewardfulfillment", "reward-fulfillment").replace("remoteconfig", "remote-config"): row for row in manifest["feature_ledger"]}
        entries = catalog["features"]
        ids = [entry["id"] for entry in entries]
        if set(ids) != FEATURES or len(ids) != len(FEATURES): errors.append("feature identifiers are missing or duplicated")
        for entry in entries:
            status = entry.get("status")
            if status not in FEATURE_STATUSES: errors.append(f"{entry.get('id')}: unknown feature status {status!r}")
            ledger = ledgers.get(entry["id"])
            if ledger is None:
                errors.append(f"{entry['id']}: missing feature ledger row")
            elif ledger.get("sdk_status") != status:
                errors.append(f"{entry['id']}: catalog status differs from manifest ledger")
            if status == "implemented" and ledger is not None:
                errors.extend(validate_implemented_evidence(root, entry, ledger, tasks.get(ledger.get("sdk_task"), {})))
            dirname = FEATURE_DIRS.get(entry["id"], entry["name"])
            if not (root / "src" / "GamePlatform.Features" / dirname / "README.md").is_file():
                errors.append(f"{entry['id']}: missing module README")
    except (OSError, KeyError, json.JSONDecodeError) as exc:
        errors.append(f"feature validation failed: {exc}")

    try:
        snapshot = read_json(root / "contracts" / "snapshot.json")
        if snapshot.get("contractVersion") not in {"0.1.0-draft", "0.2.0-core-schema.1"}: errors.append("unexpected contract version")
        for relative, expected in snapshot["files"].items():
            contract = root / "contracts" / relative
            if not contract.is_file(): errors.append(f"missing contract: {relative}")
            elif sha256(contract) != expected: errors.append(f"contract hash mismatch: {relative}")
    except (OSError, KeyError, json.JSONDecodeError) as exc:
        errors.append(f"contract validation failed: {exc}")
    return errors


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    errors = validate(root)
    if errors:
        for error in errors: print(f"ERROR: {error}")
        print(f"Validation failed with {len(errors)} error(s).")
        return 1
    print("Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and reviewed contract pins.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
