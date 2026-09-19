using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;

namespace GamePlatform.Serialization.MessagePack
{
    internal static class QualificationSchemaValidator
    {
        internal enum Direction { Diagnostic, ToWire, FromWire }
        internal static V Validate(string reference, V value, Direction direction = Direction.Diagnostic)
        {
            return Reference(reference, "", value, direction);
        }
        private static V Reference(string reference, string file, V value, Direction direction)
        {
            var split = reference.Split('#');
            if (split[0].Length != 0) file = split[0];
            if (!QualificationSchemas.Documents.TryGetValue(file, out var schema) || split.Length != 2 || !split[1].StartsWith("/$defs/", StringComparison.Ordinal)) Fail();
            schema = QualificationSchemas.Documents[file];
            foreach (var part in split[1].Substring(1).Split('/')) schema = schema.Properties[part];
            var primitive = file == "common.schema.json" ? split[1].Substring(7) : "";
            bool uuid = primitive == "uuid" || primitive == "uuidV7";
            bool wide = primitive == "signedInt64" || primitive == "nonNegativeInt64" || primitive == "positiveInt64";
            if (direction == Direction.FromWire && uuid)
            {
                if (value.Kind != K.Binary || value.BinaryBytes.Length != 16) Fail();
                var hex = BitConverter.ToString(value.BinaryBytes).Replace("-", "").ToLowerInvariant();
                value = V.String(hex.Substring(0,8)+"-"+hex.Substring(8,4)+"-"+hex.Substring(12,4)+"-"+hex.Substring(16,4)+"-"+hex.Substring(20,12));
            }
            if (direction == Direction.FromWire && wide)
            {
                if (value.Kind != K.Integer) Fail();
                value = V.String(value.IntegerValue.ToString(CultureInfo.InvariantCulture));
            }
            value = Evaluate(schema!, file, value, direction);
            if (direction == Direction.ToWire && uuid)
            {
                var hex = value.StringValue.Replace("-", ""); var bytes = new byte[16];
                for (int i = 0; i < 16; i++) bytes[i] = byte.Parse(hex.Substring(i*2,2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return V.Binary(bytes);
            }
            if (direction == Direction.ToWire && wide) return V.Integer(long.Parse(value.StringValue, CultureInfo.InvariantCulture));
            return value;
        }
        private static bool Matches(V schema, string file, V value)
        {
            try { Evaluate(schema,file,value,Direction.Diagnostic); return true; } catch (QualificationCodecException) { return false; }
        }
        private static V Evaluate(V schema, string file, V input, Direction direction)
        {
            if (schema.Kind == K.Boolean) { if (!schema.BooleanValue) Fail(); return input; }
            var s = schema.Properties;
            if (s.TryGetValue("$ref", out var reference)) return Reference(reference.StringValue,file,input,direction);
            if (s.TryGetValue("oneOf", out var oneOf))
            {
                V? selected = null; int matches = 0;
                foreach (var branch in oneOf.Items) { try { var result = Evaluate(branch,file,input,direction); selected = result; matches++; } catch (QualificationCodecException) { } }
                if (matches != 1) Fail(); return selected!;
            }
            if (s.TryGetValue("type",out var type))
            {
                var expected = type.StringValue;
                if (!(expected == "object" && input.Kind == K.Object || expected == "array" && input.Kind == K.Array || expected == "integer" && input.Kind == K.Integer || expected == "string" && input.Kind == K.String || expected == "boolean" && input.Kind == K.Boolean || expected == "null" && input.Kind == K.Null)) Fail();
            }
            if (s.TryGetValue("const",out var constant) && !Equal(input,constant)) Fail();
            if (s.TryGetValue("enum",out var enums) && !enums.Items.Any(v => Equal(v,input))) Fail();
            V resultValue = input;
            if (input.Kind == K.String)
            {
                var text = input.StringValue;
                int scalars = 0; for (int i=0;i<text.Length;i++,scalars++) if (char.IsHighSurrogate(text[i])) i++;
                Bound(s,"minLength",scalars,true); Bound(s,"maxLength",scalars,false);
                if (s.TryGetValue("pattern",out var pattern) && !Regex.IsMatch(text,pattern.StringValue,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))) Fail();
                if (s.ContainsKey("x-maximumDecimal") || s.ContainsKey("x-minimumDecimal"))
                {
                    if (!long.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n)) Fail();
                    if (s.TryGetValue("x-maximumDecimal",out var max) && n > long.Parse(max.StringValue,CultureInfo.InvariantCulture)) Fail();
                    if (s.TryGetValue("x-minimumDecimal",out var min) && n < long.Parse(min.StringValue,CultureInfo.InvariantCulture)) Fail();
                }
                if (s.ContainsKey("x-canonicalBase64Url"))
                {
                    try { var b = Convert.FromBase64String(text.Replace('-','+').Replace('_','/') + new string('=',(4-text.Length%4)%4)); if (Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_') != text) Fail(); } catch (FormatException) { Fail(); }
                }
            }
            if (input.Kind == K.Integer) { Bound(s,"minimum",input.IntegerValue,true); Bound(s,"maximum",input.IntegerValue,false); }
            if (input.Kind == K.Array)
            {
                Bound(s,"minItems",input.Items.Count,true); Bound(s,"maxItems",input.Items.Count,false);
                if (s.TryGetValue("items",out var itemSchema)) resultValue = V.Array(input.Items.Select(v => Evaluate(itemSchema,file,v,direction)));
                if (s.ContainsKey("uniqueItems")) for (int i=0;i<input.Items.Count;i++) for(int j=0;j<i;j++) if(Equal(input.Items[i],input.Items[j])) Fail();
                if (s.TryGetValue("contains",out var contains) && !resultValue.Items.Any(v=>Matches(contains,file,v))) Fail();
            }
            if (input.Kind == K.Object)
            {
                Bound(s,"minProperties",input.Properties.Count,true); Bound(s,"maxProperties",input.Properties.Count,false);
                if (s.TryGetValue("required",out var required)) foreach(var name in required.Items) if(!input.Properties.ContainsKey(name.StringValue)) Fail();
                var output = new Dictionary<string,V>(StringComparer.Ordinal);
                foreach(var pair in input.Properties)
                {
                    if(s.TryGetValue("propertyNames",out var names)) Evaluate(names,file,V.String(pair.Key),Direction.Diagnostic);
                    if(s.TryGetValue("properties",out var properties) && properties.Properties.TryGetValue(pair.Key,out var propertySchema)) output.Add(pair.Key,Evaluate(propertySchema,file,pair.Value,direction));
                    else if(s.TryGetValue("additionalProperties",out var additional)) output.Add(pair.Key,Evaluate(additional,file,pair.Value,direction));
                    else output.Add(pair.Key,pair.Value);
                }
                resultValue = V.Object(output);
            }
            var diagnostic = direction == Direction.ToWire ? input : resultValue;
            if(s.TryGetValue("allOf",out var all)) foreach(var branch in all.Items)
            {
                var basis=direction==Direction.ToWire ? input : resultValue;
                var branchResult=Evaluate(branch,file,basis,direction);
                resultValue=MergeChanges(basis,branchResult,resultValue);
            }
            diagnostic = direction == Direction.ToWire ? input : resultValue;
            if(s.TryGetValue("if",out var condition))
            {
                string branch = Matches(condition,file,diagnostic) ? "then" : "else";
                if(s.TryGetValue(branch,out var consequence))
                {
                    var basis=direction==Direction.ToWire ? input : resultValue;
                    resultValue=MergeChanges(basis,Evaluate(consequence,file,basis,direction),resultValue);
                }
            }
            if(s.ContainsKey("x-recursiveScalars")) Extension(diagnostic,1);
            if(s.TryGetValue("x-maximumDepth",out var depth) && Depth(diagnostic)>depth.IntegerValue) Fail();
            if(s.ContainsKey("x-streamSequence"))
            {
                long finalized=long.Parse(diagnostic.Properties["finalizedThrough"].StringValue,CultureInfo.InvariantCulture);
                var next=diagnostic.Properties["nextSequence"];
                if(finalized==long.MaxValue ? next.Kind!=K.Null : next.Kind!=K.String || long.Parse(next.StringValue,CultureInfo.InvariantCulture)!=checked(finalized+1)) Fail();
            }
            if(s.TryGetValue("x-distinctProperties",out var distinct))
                for(int i=0;i<distinct.Items.Count;i++) for(int j=0;j<i;j++) if(Equal(diagnostic.Properties[distinct.Items[i].StringValue],diagnostic.Properties[distinct.Items[j].StringValue])) Fail();
            return resultValue;
        }
        private static void Extension(V value,int depth)
        {
            if(depth>16 || value.Kind==K.Integer || value.Kind==K.Binary) Fail();
            if(value.Kind==K.Array) foreach(var v in value.Items) Extension(v,depth+1);
            if(value.Kind==K.Object) foreach(var v in value.Properties.Values) Extension(v,depth+1);
        }
        private static V MergeChanges(V original,V changed,V target)
        {
            if(Equal(original,changed)) return target;
            if(original.Kind==K.Object && changed.Kind==K.Object && target.Kind==K.Object)
                return V.Object(target.Properties.Select(p=>new KeyValuePair<string,V>(p.Key,MergeChanges(original.Properties[p.Key],changed.Properties[p.Key],p.Value))));
            return changed;
        }
        private static int Depth(V v) => v.Kind==K.Array ? 1+(v.Items.Count==0?0:v.Items.Max(Depth)) : v.Kind==K.Object ? 1+(v.Properties.Count==0?0:v.Properties.Values.Max(Depth)) : 1;
        private static void Bound(IReadOnlyDictionary<string,V> s,string key,long value,bool minimum) { if(s.TryGetValue(key,out var n) && (minimum ? value<n.IntegerValue : value>n.IntegerValue)) Fail(); }
        internal static bool Equal(V a,V b)
        {
            if(a.Kind!=b.Kind) return false;
            switch(a.Kind) {
                case K.Null:return true; case K.Boolean:return a.BooleanValue==b.BooleanValue; case K.Integer:return a.IntegerValue==b.IntegerValue;
                case K.String:return a.StringValue==b.StringValue; case K.Binary:return a.BinaryBytes.SequenceEqual(b.BinaryBytes);
                case K.Array:return a.Items.Count==b.Items.Count && a.Items.Zip(b.Items,Equal).All(v=>v);
                default:return a.Properties.Count==b.Properties.Count && a.Properties.All(p=>b.Properties.TryGetValue(p.Key,out var v)&&Equal(p.Value,v));
            }
        }
        private static void Fail() => throw new QualificationCodecException("Value violates the reviewed core schema.");
    }
}
