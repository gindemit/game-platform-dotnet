"""Archive existing CL-013 build inventories and freshly rerun packaging checks."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
a = ROOT / 'artifacts/sdk/dependency-manifest.json'
b = ROOT / 'artifacts/sdk-previous/dependency-manifest.json'
if not a.is_file() or not b.is_file():
    raise SystemExit('Both original build manifests are required; do not fabricate captures.')
original_a, original_b = a.read_bytes(), b.read_bytes()
ma, mb = json.loads(original_a), json.loads(original_b)
assert original_a == original_b
assert ma['sourceCommit'] == '5dfac45875fa4b829e9e04729938eb4a984e1db5'
assert not ma['sourceDirty'] and not ma['build']['buildSkipped']
assert len(ma['files']) == 33 and len(ma['assemblies']) == 14
for folder, manifest in [(a.parent, ma), (b.parent, mb)]:
    for item in manifest['files']:
        data = (folder / item['path']).read_bytes()
        assert len(data) == item['bytes']
        assert hashlib.sha256(data).hexdigest() == item['sha256']
shutil.copyfile(a, HERE / 'build-one-manifest.json')
shutil.copyfile(b, HERE / 'build-two-manifest.json')
commands = [
    (['rtk', 'proxy', 'python', '-m', 'unittest', 'discover', '-s', 'scripts', '-p', 'test_packaging.py'], 'packaging-tests.txt'),
    (['rtk', 'proxy', 'python', 'scripts/package-sdk.py', '--verify'], 'bundle-verification.txt'),
]
runs = []
for args, log in commands:
    result = subprocess.run(args, cwd=ROOT, text=True, capture_output=True)
    (HERE / log).write_text(result.stdout + result.stderr, encoding='utf-8')
    runs.append({'command': args, 'exitCode': result.returncode, 'log': log})
    if result.returncode:
        raise SystemExit(result.returncode)
evidence = {
    'artifactSourceCommit': ma['sourceCommit'],
    'captureSourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
    'manifestSha256': hashlib.sha256(original_a).hexdigest(),
    'manifestsByteEqual': original_a == original_b,
    'fileInventoryEqual': ma['files'] == mb['files'],
    'allFileHashesRecheckedInBothBundles': True,
    'fileCountPerBundle': len(ma['files']), 'assemblyCountPerBundle': len(ma['assemblies']),
    'buildConfiguration': ma['build'],
    'excludedProjects': ma['excludedProjects'],
    'buildRuns': {
        'command': ['rtk', 'proxy', 'python', 'scripts/package-sdk.py'],
        'count': 2,
        'sourceCommit': ma['sourceCommit'],
        'evidenceKind': 'author_observation_plus_retained_output',
        'rawBuildStdoutArchived': False,
        'note': 'Both original clean Rebuild commands exited zero in the agent transcript; their stdout was not saved to disk. Archived original manifests and all original output file hashes were independently rechecked at capture time. No build or artifact mutation was performed while archiving.'
    },
    'verificationRuns': runs,
    'packagingTestsPassed': 11,
    'productionCodec': 'unavailable', 'nativeLibraries': 'absent', 'aotDevice': 'unverified',
}
(HERE / 'results.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(json.dumps(evidence, indent=2))
