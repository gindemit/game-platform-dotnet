"""Capture the bounded epoch-1 client review checks; no runtime execution."""
import importlib.metadata
import json
import platform
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
EVIDENCE = ROOT / "docs/implementation/evidence/CL-003/epoch1"
COMMANDS = [
    ("review-tool-tests", ["python", "-m", "unittest", "discover", "-s", "docs/implementation/evidence/CL-003", "-p", "test_*.py", "-v"]),
    ("manifest", ["python", "docs/implementation/validate_manifest.py"]),
    ("manifest-tests", ["python", "-m", "unittest", "discover", "-s", "docs/implementation", "-p", "test_*.py"]),
    ("architecture", ["python", "scripts/validate.py"]),
    ("architecture-tests", ["python", "-m", "unittest", "discover", "-s", "scripts", "-p", "test_*.py"]),
    ("candidate", ["python", "docs/implementation/evidence/CL-003/review_candidate.py", "--backend", "../game-platform-backend", "--commit", "c6aa77054ec63c0ae46fe2113d3add994bf382fb", "--output", "docs/implementation/evidence/CL-003/epoch1/candidate-review.json"]),
    ("diff-check", ["git", "diff", "--check"]),
]


def main():
    report = {"scope": "CL-003 P1 epoch 1 diagnostic schema and review tooling; G1 remains blocked",
              "python": platform.python_version(), "jsonschema": importlib.metadata.version("jsonschema"),
              "commands": [], "runtime_suites": "not rerun; no runtime source changed"}
    for name, command in COMMANDS:
        argv = ["rtk", "proxy", *command]
        result = subprocess.run(argv, cwd=ROOT, capture_output=True, encoding="utf-8", errors="replace")
        raw = EVIDENCE / f"{name}.raw"
        raw.write_text("ARGV: " + json.dumps(argv) + "\nSTDOUT:\n" + result.stdout + "\nSTDERR:\n" + result.stderr + f"\nEXIT: {result.returncode}\n", encoding="utf-8")
        report["commands"].append({"name": name, "argv": argv, "exit_code": result.returncode, "raw": raw.relative_to(ROOT).as_posix()})
        print(f"{name}: exit {result.returncode}")
    (EVIDENCE / "verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0 if all(c["exit_code"] == 0 for c in report["commands"]) else 1


if __name__ == "__main__":
    raise SystemExit(main())
