"""Generate explicit AOT-friendly DTO mapping; never runtime reflection/activation."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
catalog = json.loads((ROOT/'src/GamePlatform.Wire.Contracts/dto-catalog.json').read_text(encoding='utf-8'))
objects = {o['type']: o for o in catalog['objects']}
unions = {o['type']: o for o in catalog['unions']}


def literal(text):
    return json.dumps(text)


def convert(typ, expression, schema, direction, depth=0):
    read = direction == 'Read'
    if typ.endswith('?'):
        base = typ[:-1]
        branches = [branch for branch in schema.get('oneOf', []) if branch.get('type') != 'null']
        if len(branches) == 1:
            schema = branches[0]
        inner = expression if base not in ('long', 'int', 'Guid', 'bool') else expression+'.Value'
        return f'({expression}.Kind == K.Null ? ({typ})null : {convert(base, expression, schema, direction, depth+1)})' if read else f'({expression} == null ? b.Null() : {convert(base, inner+"!", schema, direction, depth+1)})'
    if typ.startswith('IReadOnlyList<'):
        element = typ[14:-1]
        variable = 'item'+str(depth)
        body = convert(element, variable, schema.get('items', {}), direction, depth+1)
        return f'{expression}.Items.Select({variable} => {body}).ToArray()' if read else f'Array({expression}, {variable} => {body}, b)'
    if typ.startswith('IReadOnlyDictionary<string, '):
        element = typ[28:-1]
        variable = 'pair'+str(depth)
        body = convert(element, variable+'.Value', schema.get('additionalProperties', {}), direction, depth+1)
        return f'{expression}.Properties.ToDictionary({variable} => {variable}.Key, {variable} => {body}, StringComparer.Ordinal)' if read else f'Map({expression}, {variable} => {body}, b)'
    if typ == 'string': return expression+'.StringValue' if read else f'b.String({expression})'
    if typ == 'bool': return expression+'.BooleanValue' if read else f'b.Boolean({expression})'
    if typ == 'Guid': return f'Guid.ParseExact({expression}.StringValue, "D")' if read else f'b.String({expression}.ToString("D"))'
    if typ in ('int', 'long'):
        wide = typ == 'long' and 'timestamp' not in schema.get('$ref', '')
        if read:
            return f'long.Parse({expression}.StringValue, CultureInfo.InvariantCulture)' if wide else f'checked(({typ}){expression}.IntegerValue)'
        return f'b.String({expression}.ToString(CultureInfo.InvariantCulture))' if wide else f'b.Integer({expression})'
    if typ == 'ExtensionValue': return f'{direction}Extension({expression}, b, 1)'
    if typ == 'ErrorDetailValue': return f'{direction}Detail({expression}, b)'
    if typ not in objects and typ not in unions: raise ValueError(typ)
    return f'{direction}{typ}({expression}, b)'


lines = ['// Generated from the reviewed DTO catalog. Do not edit by hand.', '#nullable enable',
 'using System;', 'using System.Collections.Generic;', 'using System.Globalization;', 'using System.Linq;',
 'using GamePlatform.Wire.Contracts;', 'using V = GamePlatform.Serialization.MessagePack.QualificationValue;',
 'using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;',
 'namespace GamePlatform.Serialization.MessagePack {',
 'public sealed class TypedQualificationCodec {',
 'private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();',
 'public byte[] Encode<T>(T value) where T : class { var diagnostic=ToDiagnostic(value); new MappingBudget(4).Reserve(diagnostic); return codec.Encode(Schema<T>(), diagnostic); }',
 'public T Decode<T>(byte[] bytes) where T : class => FromDiagnostic<T>(codec.Decode(Schema<T>(), bytes));',
 'public V ToDiagnostic<T>(T value) where T : class { if(value == null) throw new ArgumentNullException(nameof(value)); var b=new MappingBudget(); switch(value) {']
for typ in objects:
    lines.append(f'case {typ} dto: return Write{typ}(dto,b);')
lines += ['default: throw new QualificationCodecException("Unsupported DTO implementation."); } }',
 'public T FromDiagnostic<T>(V value) where T : class { codec.ValidateDiagnostic(Schema<T>(),value); new MappingBudget().Reserve(value); var b=new MappingBudget();']
for typ in [*objects, *unions]:
    lines.append(f'if(typeof(T) == typeof({typ})) return (T)(object)Read{typ}(value,b);')
lines += ['throw new QualificationCodecException("Unsupported DTO type."); }', 'public static string Schema<T>() where T : class {']
for typ, obj in objects.items():
    lines.append(f'if(typeof(T) == typeof({typ})) return {literal(obj["sourceSchema"])};')
for typ, union in unions.items():
    # These three interfaces represent inline/conditional branches rather than named schema defs.
    if typ in ('IPushPayload','IPushFinalResult','IPushAcceptedResult'): continue
    lines.append(f'if(typeof(T) == typeof({typ})) return {literal(union["schema"])};')
lines += ['throw new QualificationCodecException("Unsupported top-level DTO type."); }',
 'public V NormalizeDto(string schema,V value) { new MappingBudget(4).Reserve(value); switch(schema) {']
for typ, obj in objects.items():
    lines.append(f'case {literal(obj["sourceSchema"])}: return ToDiagnostic(FromDiagnostic<{typ}>(value));')
for typ, union in unions.items():
    if typ in ('IPushPayload','IPushFinalResult','IPushAcceptedResult'): continue
    lines.append(f'case {literal(union["schema"])}: return ToDiagnostic(FromDiagnostic<{typ}>(value));')
for schema, typ in catalog['aliases'].items():
    lines.append(f'case {literal(schema)}: return ToDiagnostic(FromDiagnostic<{typ}>(value));')
lines += ['default: throw new QualificationCodecException("Unsupported DTO schema."); } }']
for typ, obj in objects.items():
    lines += [f'private static V Write{typ}({typ} value, MappingBudget b) {{ b.Node(); var fields=new List<KeyValuePair<string,V>>();']
    for p in obj['properties']:
        expr = 'value.'+p['property']; ptype = p['type']
        if p['optional']:
            ptype = ptype[len('WireOptional<'):-1]
            lines.append(f'if({expr}.IsPresent) fields.Add(new KeyValuePair<string,V>(b.Key({literal(p["wire"])}), {convert(ptype, expr+".Value", p["schema"], "Write")}));')
        else:
            lines.append(f'fields.Add(new KeyValuePair<string,V>(b.Key({literal(p["wire"])}), {convert(ptype, expr, p["schema"], "Write")}));')
    lines += ['return V.Object(fields); }', f'private static {typ} Read{typ}(V value, MappingBudget b) {{ b.Node(); return new {typ}(']
    args=[]
    for p in obj['properties']:
        if p['constant']: continue
        expr = f'value.Properties[{literal(p["wire"])}]'; ptype=p['type']
        if p['optional']:
            inner=ptype[len('WireOptional<'):-1]
            args.append(f'value.Properties.ContainsKey({literal(p["wire"])}) ? {ptype}.Present({convert(inner,expr,p["schema"],"Read")}) : default')
        else: args.append(convert(ptype,expr,p['schema'],'Read'))
    lines += [',\n'.join(args)+'); }']
for typ, union in unions.items():
    lines += [f'private static V Write{typ}({typ} value, MappingBudget b) {{ switch(value) {{']
    for branch in union['branches']: lines.append(f'case {branch} dto: return Write{branch}(dto,b);')
    lines += ['default: throw new QualificationCodecException("Unsupported DTO union implementation."); } }',
              f'private static {typ} Read{typ}(V value, MappingBudget b) {{']
    for branch in union['branches']:
        lines.append(f'if(Matches({literal(objects[branch]["sourceSchema"])},value)) return Read{branch}(value,b);')
    lines += ['throw new QualificationCodecException("Unsupported DTO union value."); }']
lines += ['''
private static bool Matches(string schema,V value) { try { QualificationSchemaValidator.Validate(schema,value); return true; } catch(QualificationCodecException) { return false; } }
// Reserve three logical copies for source DTO/tree, mapped tree/DTO and constructor temporary containers.
private sealed class MappingBudget {
 private readonly int copies;
 internal MappingBudget(int copies=3) { this.copies=copies; }
 private long nodes,units;
 internal void Node() { nodes=checked(nodes+copies); Units(32); if(nodes>16384) throw new QualificationCodecException("DTO mapping node budget."); }
 internal void Units(long count) { units=checked(units+count*copies); if(units>2097152) throw new QualificationCodecException("DTO mapping allocation budget."); }
 internal string Key(string text) { String(text); Units(16); return text; }
 internal V String(string text) { Node(); if(V.Utf8.GetByteCount(text)>8192) throw new QualificationCodecException("DTO string budget."); Units(text.Length*2L); return V.String(text); }
 internal V Integer(long n) { Node(); return V.Integer(n); }
 internal V Boolean(bool n) { Node(); return V.Boolean(n); }
 internal V Null() { Node(); return V.Null; }
 internal void Reserve(V v,int depth=0) {
  if(depth>32) throw new QualificationCodecException("DTO mapping depth budget.");
  if(v.Kind==K.String) { String(v.StringValue); return; } Node();
  if(v.Kind==K.Array) { Units(v.Items.Count*8L); foreach(var item in v.Items) Reserve(item,depth+1); }
  if(v.Kind==K.Object) foreach(var pair in v.Properties) { Key(pair.Key); Reserve(pair.Value,depth+1); }
 }
}
private static V Array<T>(IReadOnlyList<T> source,Func<T,V> map,MappingBudget b) { if(source.Count>1024) throw new QualificationCodecException("DTO array budget."); b.Node(); b.Units(source.Count*8L); return V.Array(source.Select(map)); }
private static V Map<T>(IReadOnlyDictionary<string,T> source,Func<KeyValuePair<string,T>,V> map,MappingBudget b) { if(source.Count>256) throw new QualificationCodecException("DTO map budget."); b.Node(); return V.Object(source.Select(p=>new KeyValuePair<string,V>(b.Key(p.Key),map(p)))); }
private static V WriteExtension(ExtensionValue v,MappingBudget b,int depth) {
 if(depth>16) throw new QualificationCodecException("DTO extension depth.");
 switch(v.Kind) {
 case ExtensionValueKind.Null:return b.Null(); case ExtensionValueKind.Boolean:return b.Boolean(v.Boolean);
 case ExtensionValueKind.String:return b.String(v.Text!);
 case ExtensionValueKind.Array:return Array(v.Items!, x=>WriteExtension(x,b,depth+1),b);
 case ExtensionValueKind.Object:return Map(v.Properties!, p=>WriteExtension(p.Value,b,depth+1),b);
 default:throw new QualificationCodecException("DTO extension kind."); } }
private static ExtensionValue ReadExtension(V v,MappingBudget b,int depth) {
 b.Node(); if(depth>16) throw new QualificationCodecException("DTO extension depth.");
 switch(v.Kind) {
 case K.Null:return ExtensionValue.Null; case K.Boolean:return ExtensionValue.FromBoolean(v.BooleanValue);
 case K.String:return ExtensionValue.FromString(v.StringValue);
 case K.Array:return ExtensionValue.FromArray(v.Items.Select(x=>ReadExtension(x,b,depth+1)).ToArray());
 case K.Object:return ExtensionValue.FromObject(v.Properties.ToDictionary(p=>p.Key,p=>ReadExtension(p.Value,b,depth+1),StringComparer.Ordinal));
 default:throw new QualificationCodecException("DTO extension kind."); } }
private static V WriteDetail(ErrorDetailValue v,MappingBudget b) { switch(v.Kind) { case ErrorDetailKind.Null:return b.Null(); case ErrorDetailKind.Boolean:return b.Boolean(v.Boolean); case ErrorDetailKind.String:return b.String(v.Text!); case ErrorDetailKind.Integer:return b.Integer(v.Integer); default:throw new QualificationCodecException("DTO detail kind."); } }
private static ErrorDetailValue ReadDetail(V v,MappingBudget b) { switch(v.Kind) { case K.Null:return ErrorDetailValue.Null; case K.Boolean:return ErrorDetailValue.FromBoolean(v.BooleanValue); case K.String:return ErrorDetailValue.FromString(v.StringValue); case K.Integer:return ErrorDetailValue.FromInteger(checked((int)v.IntegerValue)); default:throw new QualificationCodecException("DTO detail kind."); } }
} }
''']
output = '\n'.join(lines)+'\n'
path = Path(__file__).with_name('TypedQualificationCodec.g.cs')
import sys
if '--check' in sys.argv:
    if path.read_text(encoding='utf-8') != output: raise SystemExit('DTO mapper drift')
else:
    path.write_text(output, encoding='utf-8')
