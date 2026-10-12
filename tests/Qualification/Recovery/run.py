"""Strict opt-in joined qualification; run only from an owned disposable SDK copy."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture-config", type=Path, required=True)
    parser.add_argument("--results", type=Path, required=True)
    args = parser.parse_args()
    sdk = Path(__file__).resolve().parents[3]
    if (sdk / ".git").exists():
        parser.error("Use a disposable exact-source SDK copy without a git index.")
    config_path = args.fixture_config.resolve(strict=True)
    config = json.loads(config_path.read_text(encoding="utf-8"))
    if not config.get("baseUri", "").startswith("https://") or len(config.get("certificateSha256", "")) != 64:
        parser.error("Fixture config requires HTTPS origin and the generated certificate SHA256.")
    results = args.results.resolve()
    results.mkdir(parents=True, exist_ok=True)
    trx = results / "joined-recovery.trx"
    if trx.exists():
        parser.error("Results destination already contains a receipt; choose a fresh directory.")
    env = os.environ.copy()
    for key in list(env):
        if key.startswith("GP_RECOVERY_CHILD_"):
            del env[key]
    env["GP_RECOVERY_FIXTURE_CONFIG"] = str(config_path)
    command = ["dotnet", "test", "tests/GamePlatform.Tests/GamePlatform.Tests.csproj", "-c", "Release",
               "--filter", "FullyQualifiedName~JoinedCommandStreamRecoveryTests&FullyQualifiedName!~ProcessRecoveryChild",
               "--verbosity", "minimal", "--logger", "trx;LogFileName=joined-recovery.trx",
               "--results-directory", str(results)]
    completed = subprocess.run(command, cwd=sdk, env=env, check=False)
    if completed.returncode:
        return completed.returncode
    counters = ET.parse(trx).find(".//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters")
    expected = {"total": "8", "executed": "8", "passed": "8", "failed": "0", "notExecuted": "0"}
    if counters is None or any(counters.get(key) != value for key, value in expected.items()):
        raise RuntimeError("Joined qualification must execute all eight parent cases with no skips or failures.")
    print(f"Joined actual-provider recovery: 8 passed, 0 skipped; receipt {trx}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
