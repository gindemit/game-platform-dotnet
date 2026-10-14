"""Reproduce SDK P1 checks and preserve raw reports; never publishes or touches player data."""
from pathlib import Path
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent / 'P1' / 'raw'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    commands = [
        ('toolchain', ['dotnet', '--info']),
        ('architecture', ['python', 'scripts/validate.py']),
        ('validator-tests', ['python', '-m', 'unittest', 'discover', '-s', 'scripts', '-p', 'test_*.py']),
        ('manifest', ['python', 'docs/implementation/validate_manifest.py']),
        ('manifest-tests', ['python', '-m', 'unittest', 'discover', '-s', 'docs/implementation', '-p', 'test_*.py']),
        ('restore', ['dotnet', 'restore', 'GamePlatform.sln', '--locked-mode']),
        ('build', ['dotnet', 'build', 'GamePlatform.sln', '-c', 'Release', '--no-restore']),
        ('test', ['dotnet', 'test', 'GamePlatform.sln', '-c', 'Release', '--no-build', '--no-restore',
                  '--logger', 'trx;LogFileName=p1.trx', '--results-directory', str(OUT)]),
        ('diagnostics', ['dotnet', 'test', 'tests/GamePlatform.Tests/GamePlatform.Tests.csproj', '-c', 'Release',
                         '--no-build', '--no-restore', '--filter', 'FullyQualifiedName~GamePlatform.Tests.Diagnostics',
                         '--logger', 'trx;LogFileName=diagnostics.trx', '--results-directory', str(OUT)]),
        ('packages', ['dotnet', 'list', 'GamePlatform.sln', 'package', '--include-transitive']),
        ('package', ['python', 'scripts/package-sdk.py']),
        ('diff', ['git', 'diff', '--check']),
    ]
    results = []
    env = dict(os.environ, PYTHONUTF8='1')
    for name, command in commands:
        actual = ['rtk', 'proxy', *command]
        proc = subprocess.run(actual, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              text=True, encoding='utf-8', errors='replace')
        report = OUT / (name + ('.json' if name == 'packages' else '.txt'))
        # JSON preserves the package table's literal trailing spaces without whitespace-only source diffs.
        report.write_text(json.dumps({'stdout': proc.stdout}, indent=2) + '\n' if name == 'packages' else proc.stdout, encoding='utf-8')
        row = {'name': name, 'command': actual, 'exit_code': proc.returncode,
               'raw_report': report.relative_to(ROOT).as_posix()}
        if name in ('test', 'diagnostics') and proc.returncode == 0:
            trx = OUT / ('p1.trx' if name == 'test' else 'diagnostics.trx')
            counter = ET.parse(trx).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
            if counter is None:
                raise RuntimeError('Missing TRX counters')
            row['tests'] = dict(counter.attrib)
            if int(counter.attrib['executed']) <= 0 or int(counter.attrib['failed']) != 0:
                raise RuntimeError('Zero tests or failed tests')
        results.append(row)
        (OUT.parent / 'results.json').write_text(json.dumps(results, indent=2) + '\n', encoding='utf-8')
        print(f'{name}: exit {proc.returncode}', flush=True)
        if proc.returncode:
            print(proc.stdout[-3000:])
            return proc.returncode
    return 0


if __name__ == '__main__':
    sys.exit(main())
