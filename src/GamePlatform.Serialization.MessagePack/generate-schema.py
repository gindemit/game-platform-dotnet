"""Generate trusted schema constants from the SHA-pinned reviewed mirror; never fetch."""
import json
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
for path in sorted((root/'contracts/v1/schemas').glob('*.json')):
    rows.append('{' + json.dumps(path.name) + ',' + emit(json.loads(path.read_text())) + '}')
out = '''// Generated from reviewed contracts by generate-schema.py. No runtime schema loading.
using System.Collections.Generic;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
namespace GamePlatform.Serialization.MessagePack {
 internal static class QualificationSchemas {
 internal static readonly IReadOnlyDictionary<string,V> Documents = new Dictionary<string,V> {
''' + ',\n'.join(rows) + '\n};\n}\n}\n'
Path(__file__).with_name('QualificationSchemas.g.cs').write_text(out, encoding='utf-8')
