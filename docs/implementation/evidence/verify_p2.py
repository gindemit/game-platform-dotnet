"""Archive exact P2 desktop checks. This does not approve G2 or device acceptance."""
import json
from pathlib import Path
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'docs/implementation/evidence/P2'
COMMANDS = [
    ('architecture', ['python', 'scripts/validate.py']),
    ('validator-tests', ['python', '-m', 'unittest', 'discover', '-s', 'scripts', '-p', 'test_*.py']),
    ('manifest', ['python', 'docs/implementation/validate_manifest.py']),
    ('manifest-tests', ['python', '-m', 'unittest', 'discover', '-s', 'docs/implementation', '-p', 'test_*.py']),
    ('schema-generation', ['python', 'src/GamePlatform.Serialization.MessagePack/generate-schema.py', '--check']),
    ('dto-mapper-generation', ['python', 'src/GamePlatform.Serialization.MessagePack/generate-dto-mapper.py', '--check']),
    ('restore', ['dotnet', 'restore', 'GamePlatform.sln', '--locked-mode']),
    ('build', ['dotnet', 'build', 'GamePlatform.sln', '-c', 'Release', '--no-restore']),
    ('dotnet-tests', ['dotnet', 'test', 'tests/GamePlatform.Tests', '-c', 'Release', '--no-restore', '--no-build',
                      '--logger', 'trx;LogFileName=p2.trx', '--results-directory', str(OUT / 'trx')]),
    ('codec-self-test', ['dotnet', 'run', '--project', 'tests/acceptance/GamePlatform.CodecHarness', '-c', 'Release',
                         '--no-build', '--no-restore', '--', 'self-test', '--root', '.']),
    ('harness-tests', ['python', '-m', 'unittest', 'discover', '-s', 'tests/acceptance', '-p', 'test_*.py']),
    ('legacy-consumer', ['python', 'tests/GamePlatform.Tests/Compatibility/verify_legacy_consumer.py', '../MrSquareUnity-platform-p1']),
    ('packages', ['dotnet', 'list', 'GamePlatform.sln', 'package', '--include-transitive']),
    ('diff', ['git', 'diff', '--check']),
]


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    records = []
    failed = False
    for name, command in COMMANDS:
        started = time.monotonic()
        argv = ['rtk', 'proxy', *command]
        result = subprocess.run(argv, cwd=ROOT, capture_output=True, encoding='utf-8', errors='replace')
        log = OUT / (name + '.log')
        log.write_text(result.stdout + '\nSTDERR:\n' + result.stderr, encoding='utf-8')
        record = dict(name=name, argv=argv, exit_code=result.returncode,
                      seconds=round(time.monotonic()-started, 3), log=str(log.relative_to(ROOT)).replace('\\','/'))
        if name == 'dotnet-tests' and result.returncode == 0:
            counters = ET.parse(OUT / 'trx/p2.trx').find('.//{*}Counters')
            if counters is None:
                raise RuntimeError('No actual test counters')
            record['tests'] = counters.attrib
            if int(counters.attrib.get('executed', '0')) == 0 or int(counters.attrib.get('failed', '0')) != 0:
                record['exit_code'] = 1
        records.append(record)
        failed |= record['exit_code'] != 0
        print(name, record['exit_code'], flush=True)
    summary = dict(phase='P2', epoch=2, records=records,
                   unrun=['G2 independent peer exchange and live-slice schema review', 'A01-A13 whole production acceptance',
                          'live backend journey', 'IL2CPP/AOT/device', 'provider sandbox'],
                   note='Desktop qualification evidence; Unity import acceptance recorded separately.')
    (OUT / 'results.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
    return int(failed)


if __name__ == '__main__':
    sys.exit(main())
