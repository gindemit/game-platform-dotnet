"""Compile the immutable real consumer's 14 baseline cases against canonical SDK contracts.

Creates a temporary standalone project; does not edit the consumer or copy its fakes
into SDK production assemblies. This is desktop source compatibility, not Unity import.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("consumer_repo")
    parser.add_argument("--revision", default="bc53723a8eec3521dd7bd8e05eaa726fca29795d")
    args = parser.parse_args()
    sdk = Path(__file__).resolve().parents[3]
    names = ["PlatformId", "ProviderFailure", "OperationResult", "DataOwnership", "StateRecord",
             "GameplayOutcome", "IGameplayOutcomeSink", "ProgressEvent", "IProgressObserver"]
    paths = ["Assets/Scripts/Platform/PlatformContracts.cs", "Assets/Scripts/Platform/PlatformRuntime.cs",
             "Assets/Scripts/Platform/FakeProviders.cs", "Tools/PlatformTests/Program.cs"]
    with tempfile.TemporaryDirectory(prefix="cl002-consumer-") as temp:
        root = Path(temp)
        for path in paths:
            source = subprocess.check_output(["git", "-C", args.consumer_repo, "show", f"{args.revision}:{path}"], text=True)
            if path.endswith("PlatformContracts.cs"):
                for name in names:
                    match = re.search(r"    public (?:readonly struct|sealed class|enum|interface) " + name + r"\b", source)
                    if not match:
                        raise ValueError(f"Missing expected legacy declaration {name}")
                    start = source.index("{", match.start())
                    depth = 1
                    end = start + 1
                    while depth:
                        if source[end] == "{": depth += 1
                        elif source[end] == "}": depth -= 1
                        end += 1
                    source = source[:match.start()] + source[end:]
            source = re.sub(r"\bProviderFailure\b", "PlatformFailure", source)
            source = re.sub(r"\bOperationResult\b", "PlatformResult", source)
            source = "using GamePlatform.Core;\nusing GamePlatform.Features.Contracts;\n" + source
            (root / Path(path).name).write_text(source, encoding="utf-8")
        references = "".join(f'<ProjectReference Include="{sdk / "src" / name / (name + ".csproj")}" />'
                             for name in ["GamePlatform.Core", "GamePlatform.Features.Contracts"])
        (root / "Consumer.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net9.0</TargetFramework><Nullable>disable</Nullable>'
            '<ImplicitUsings>disable</ImplicitUsings></PropertyGroup><ItemGroup>' + references + '</ItemGroup></Project>', encoding="utf-8")
        result = subprocess.run(["dotnet", "run", "--project", str(root / "Consumer.csproj"), "-c", "Release"], capture_output=True, text=True)
        print(json.dumps({"consumer_revision": args.revision, "sdk_root": str(sdk), "exit_code": result.returncode,
                          "stdout": result.stdout, "stderr": result.stderr}, indent=2))
        if result.returncode:
            raise SystemExit(result.returncode)
        if "Platform: 14 passed, 0 failed." not in result.stdout:
            raise SystemExit("Expected nonzero baseline test counter absent")


if __name__ == "__main__":
    main()
