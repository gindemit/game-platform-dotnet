"""Generate trusted schema constants from the SHA-pinned reviewed mirror; never fetch."""
import json
import argparse
import hashlib
from pathlib import Path

root = Path(__file__).resolve().parents[2]
def emit(v):
    if v is None: return 'V.Null'
    if isinstance(v, bool): return 'V.Boolean(' + str(v).lower() + ')'
    if isinstance(v, int): return f'V.Integer({v}L)'
    if isinstance(v, str): return 'V.String(' + json.dumps(v, ensure_ascii=True) + ')'
    if isinstance(v, list): return 'V.Array(new V[]{' + ','.join(map(emit, v)) + '})'
    return 'V.Object(new KeyValuePair<string,V>[]{' + ','.join('new KeyValuePair<string,V>(' + json.dumps(k) + ',' + emit(x) + ')' for k,x in v.items()) + '})'

rows = []
pins = json.loads((root/'contracts/snapshot.json').read_text(encoding='utf-8'))['files']
paths = [*sorted((root/'contracts/v1/schemas').glob('*.json')),
         root/'contracts/live-slice/receipt.schema.json',
         root/'contracts/g3/reward-receipt.schema.json',
         root/'contracts/a06/store-quest.schema.json']
for path in paths:
    if hashlib.sha256(path.read_bytes()).hexdigest() != pins[path.relative_to(root/'contracts').as_posix()]:
        raise SystemExit(f'Schema pin mismatch: {path.name}')
    key = path.relative_to(root/'contracts').as_posix()
    if key.startswith('v1/schemas/'): key = key[len('v1/schemas/'):]
    rows.append('{' + json.dumps(key) + ',' + emit(json.loads(path.read_text(encoding='utf-8'))) + '}')
out = '''// Generated from reviewed contracts by generate-schema.py. No runtime schema loading.
using System.Collections.Generic;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
namespace GamePlatform.Serialization.MessagePack {
 internal static class QualificationSchemas {
 internal static readonly IReadOnlyDictionary<string,V> Documents = new Dictionary<string,V> {
''' + ',\n'.join(rows) + '\n};\n}\n}\n'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--check',action='store_true')
args=parser.parse_args()
target=Path(__file__).with_name('QualificationSchemas.g.cs')
if args.check:
    if target.read_text(encoding='utf-8') != out: raise SystemExit('Generated schema constants drifted; regenerate explicitly.')
else:
    target.write_text(out,encoding='utf-8')
