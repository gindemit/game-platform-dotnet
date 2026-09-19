using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>Explicit desktop qualification surface. Production UnavailableMessagePackCodec remains unavailable pending peer/AOT acceptance.</summary>
    public sealed class QualificationMessagePackCodec
    {
        public byte[] Encode(string schemaRef, V diagnosticValue)
        {
            ValidateDiagnostic(schemaRef,diagnosticValue);
            var budget=new Budget(); budget.Visit(diagnosticValue,1); budget.Visit(diagnosticValue,1); budget.Visit(diagnosticValue,1);
            var wire=QualificationSchemaValidator.Validate(schemaRef,diagnosticValue,QualificationSchemaValidator.Direction.ToWire);
            var buffer=new BoundedBuffer(); var writer=new MessagePackWriter(buffer);
            Write(ref writer,wire); writer.Flush();
            if(buffer.WrittenCount>262144) throw new QualificationCodecException("Encoded body exceeds limit.");
            return buffer.WrittenSpan.ToArray();
        }
        public V Decode(string schemaRef,byte[] payload)
        {
            if(payload==null) throw new ArgumentNullException(nameof(payload));
            if(payload.Length>262144) throw new QualificationCodecException("Encoded body exceeds limit.");
            try
            {
                var reader=new MessagePackReader(new ReadOnlyMemory<byte>(payload)); var budget=new Budget();
                var wire=Read(ref reader,budget,1);
                if(!reader.End) throw new QualificationCodecException("Trailing input.");
                // Reserve output and one intermediate schema-branch copy before normalization.
                // Include integer/UUID text and base64 conversion buffers before normalization.
                budget.Visit(wire,1,true);
                budget.Visit(wire,1,true);
                var diagnostic=QualificationSchemaValidator.Validate(schemaRef,wire,QualificationSchemaValidator.Direction.FromWire);
                ValidateDiagnostic(schemaRef,diagnostic);
                return diagnostic;
            }
            catch(QualificationCodecException) { throw; }
            catch(Exception ex) when(ex is ArgumentException || ex is OverflowException || ex is InvalidOperationException || ex is EndOfStreamException || ex is MessagePackSerializationException)
            { throw new QualificationCodecException("Malformed MessagePack input.",ex); }
        }
        public void ValidateDiagnostic(string schemaRef,V value)
        {
            if(value==null) throw new ArgumentNullException(nameof(value));
            new Budget().Visit(value,1);
            QualificationSchemaValidator.Validate(schemaRef,value);
        }
        public byte[] CanonicalBytes(V input)
        {
            if(input==null || input.Kind!=K.Object) throw new QualificationCodecException("Fingerprint input must be an object.");
            var retained=new Budget(); for(int i=0;i<6;i++) retained.Visit(input,1);
            var required=new[]{"backendNamespace","authenticatedAppId","authenticatedPlatformUserId","clientStreamId","operationId","installationId","sequence","type","schemaVersion","clientCreatedAt","payload"};
            var allowed=new HashSet<string>(required.Concat(new[]{"correlationId","requestAttemptId","transportRepresentation"}),StringComparer.Ordinal);
            if(input.Properties.Keys.Any(k=>!allowed.Contains(k)) || required.Any(k=>!input.Properties.ContainsKey(k))) throw new QualificationCodecException("Invalid fingerprint scope.");
            var values=new Dictionary<string,V>(StringComparer.Ordinal);
            foreach(var key in required) values.Add(key,input.Properties[key]);
            if(values["backendNamespace"].Kind!=K.String || values["backendNamespace"].StringValue.Length==0) throw new QualificationCodecException("Invalid namespace.");
            foreach(var key in new[]{"authenticatedAppId","authenticatedPlatformUserId","clientStreamId"}) QualificationSchemaValidator.Validate("common.schema.json#/$defs/uuid",values[key]);
            var command=V.Object(values.Where(p=>p.Key!="backendNamespace" && p.Key!="authenticatedAppId" && p.Key!="authenticatedPlatformUserId" && p.Key!="clientStreamId"));
            ValidateDiagnostic("push.schema.json#/$defs/operation",command);
            var normalized=QualificationSchemaValidator.Validate("push.schema.json#/$defs/operation",command,QualificationSchemaValidator.Direction.ToWire);
            // Semantic UUIDs are text; schema-declared int64 values alone change to integer tags.
            var canonicalCommand=NormalizeCanonical(command,normalized);
            foreach(var p in canonicalCommand.Properties) values[p.Key]=p.Value;
            using(var stream=new MemoryStream()) { Put(stream,"gsc1"); Canonical(stream,V.Object(values)); return stream.ToArray(); }
        }
        public string Fingerprint(V input)
        {
            using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(CanonicalBytes(input))).Replace("-","").ToLowerInvariant();
        }
        private static V NormalizeCanonical(V original,V wire)
        {
            if(wire.Kind==K.Binary) return original;
            if(wire.Kind==K.Object) return V.Object(wire.Properties.Select(p=>new KeyValuePair<string,V>(p.Key,NormalizeCanonical(original.Properties[p.Key],p.Value))));
            if(wire.Kind==K.Array) return V.Array(wire.Items.Select((v,i)=>NormalizeCanonical(original.Items[i],v)));
            return wire;
        }
        private static void Put(Stream stream,string text) { var bytes=V.Utf8.GetBytes(text); stream.Write(bytes,0,bytes.Length); }
        private static void Canonical(Stream stream,V value)
        {
            switch(value.Kind)
            {
                case K.Null: Put(stream,"n"); break;
                case K.Boolean: Put(stream,value.BooleanValue?"t":"f"); break;
                case K.Integer: Put(stream,"i"+value.IntegerValue.ToString(CultureInfo.InvariantCulture)+";"); break;
                case K.String: Put(stream,"s"+V.Utf8.GetByteCount(value.StringValue).ToString(CultureInfo.InvariantCulture)+":"+value.StringValue); break;
                case K.Array: Put(stream,"a"+value.Items.Count.ToString(CultureInfo.InvariantCulture)+":["); foreach(var child in value.Items) Canonical(stream,child); Put(stream,"]"); break;
                case K.Object:
                    Put(stream,"o"+value.Properties.Count.ToString(CultureInfo.InvariantCulture)+":{");
                    foreach(var pair in value.Properties.OrderBy(p=>V.Utf8.GetBytes(p.Key),ByteComparer.Instance)) { Canonical(stream,V.String(pair.Key)); Put(stream,","); Canonical(stream,pair.Value); }
                    Put(stream,"}"); break;
                default: throw new QualificationCodecException("Binary has no canonical semantic tag.");
            }
        }
        private sealed class ByteComparer : IComparer<byte[]>
        {
            internal static readonly ByteComparer Instance=new ByteComparer();
            public int Compare(byte[]? a,byte[]? b) { if(a==null||b==null) throw new ArgumentNullException(); for(int i=0;i<Math.Min(a.Length,b.Length);i++) if(a[i]!=b[i]) return a[i].CompareTo(b[i]); return a.Length.CompareTo(b.Length); }
        }
        private static V Read(ref MessagePackReader reader,Budget budget,int depth)
        {
            long start=reader.Consumed;
            var result=ReadValue(ref reader,budget,depth);
            if(result.Kind==K.Object) result.EncodedByteLength=reader.Consumed-start;
            return result;
        }
        private static V ReadValue(ref MessagePackReader reader,Budget budget,int depth)
        {
            budget.Node(depth,reader.NextMessagePackType==MessagePackType.Array || reader.NextMessagePackType==MessagePackType.Map);
            switch(reader.NextMessagePackType)
            {
                case MessagePackType.Nil: reader.ReadNil(); return V.Null;
                case MessagePackType.Boolean: return V.Boolean(reader.ReadBoolean());
                case MessagePackType.Integer: return V.Integer(reader.ReadInt64());
                case MessagePackType.String:
                    var sequence=reader.ReadStringSequence()!.Value;
                    if(sequence.Length>8192) throw new QualificationCodecException("String exceeds limit.");
                    var bytes=sequence.ToArray();
                    int chars=V.Utf8.GetCharCount(bytes); budget.Units(checked(chars*2));
                    return V.String(V.Utf8.GetString(bytes));
                case MessagePackType.Binary:
                    var binary=reader.ReadBytes()!.Value;
                    if(binary.Length>262144) throw new QualificationCodecException("Binary exceeds limit.");
                    budget.Units(binary.Length); return V.OwnedBinary(binary.ToArray());
                case MessagePackType.Array:
                    int count=reader.ReadArrayHeader(); if(count>1024) throw new QualificationCodecException("Array exceeds limit.");
                    budget.Units(checked(count*8L)); var list=new V[count];
                    for(int i=0;i<count;i++) list[i]=Read(ref reader,budget,depth+1);
                    return V.OwnedArray(list);
                case MessagePackType.Map:
                    int entries=reader.ReadMapHeader(); if(entries>256) throw new QualificationCodecException("Map exceeds limit.");
                    budget.Units(checked(entries*16L)); var map=new Dictionary<string,V>(StringComparer.Ordinal);
                    for(int i=0;i<entries;i++)
                    {
                        var key=Read(ref reader,budget,depth+1);
                        if(key.Kind!=K.String || map.ContainsKey(key.StringValue)) throw new QualificationCodecException("Non-string or duplicate map key.");
                        map.Add(key.StringValue,Read(ref reader,budget,depth+1));
                    }
                    return V.OwnedObject(map);
                default: throw new QualificationCodecException("Floating-point and extension tokens are forbidden.");
            }
        }
        private static void Write(ref MessagePackWriter writer,V value)
        {
            switch(value.Kind)
            {
                case K.Null: writer.WriteNil();break; case K.Boolean:writer.Write(value.BooleanValue);break; case K.Integer:writer.Write(value.IntegerValue);break;
                case K.String:writer.WriteString(V.Utf8.GetBytes(value.StringValue));break; case K.Binary:writer.Write(value.BinaryBytes);break;
                case K.Array:writer.WriteArrayHeader(value.Items.Count);foreach(var v in value.Items) Write(ref writer,v);break;
                case K.Object:writer.WriteMapHeader(value.Properties.Count);foreach(var p in value.Properties) { writer.WriteString(V.Utf8.GetBytes(p.Key));Write(ref writer,p.Value); }break;
            }
        }
        internal static long EncodedSize(V value)
        {
            switch(value.Kind)
            {
                case K.Null:case K.Boolean:return 1;
                case K.Integer:
                    long n=value.IntegerValue;
                    return n>=0 ? n<=127?1:n<=255?2:n<=65535?3:n<=uint.MaxValue?5:9 : n>=-32?1:n>=sbyte.MinValue?2:n>=short.MinValue?3:n>=int.MinValue?5:9;
                case K.String:
                    int bytes=V.Utf8.GetByteCount(value.StringValue);return bytes+(bytes<=31?1:bytes<=255?2:bytes<=65535?3:5);
                case K.Binary:int length=value.BinaryBytes.Length;return length+(length<=255?2:length<=65535?3:5);
                case K.Array:return (value.Items.Count<=15?1:value.Items.Count<=65535?3:5)+value.Items.Sum(EncodedSize);
                default:return (value.Properties.Count<=15?1:value.Properties.Count<=65535?3:5)+value.Properties.Sum(p=>EncodedSize(V.String(p.Key))+EncodedSize(p.Value));
            }
        }
        private sealed class Budget
        {
            private long nodes; private long units;
            internal void Node(int depth,bool container=false) { if(depth>33 || (container && depth>32) || ++nodes>16384) throw new QualificationCodecException("Decoded node/depth budget exceeded."); Units(32); }
            internal void Units(long count) { units=checked(units+count); if(units>2097152) throw new QualificationCodecException("Decoded allocation budget exceeded."); }
            internal void Visit(V value,int depth,bool reserveDiagnosticExpansion=false)
            {
                Node(depth,value.Kind==K.Array || value.Kind==K.Object);
                if(reserveDiagnosticExpansion && value.Kind==K.Integer) Units(value.IntegerValue.ToString(CultureInfo.InvariantCulture).Length*2L);
                if(value.Kind==K.String) { if(V.Utf8.GetByteCount(value.StringValue)>8192) throw new QualificationCodecException("String exceeds limit."); Units(value.StringValue.Length*2L); }
                if(value.Kind==K.Binary) { if(value.BinaryBytes.Length>262144) throw new QualificationCodecException("Binary exceeds limit."); Units(reserveDiagnosticExpansion?Math.Max(72, checked(value.BinaryBytes.Length + 8L*((value.BinaryBytes.Length+2)/3)*2)):value.BinaryBytes.Length); }
                if(value.Kind==K.Array) { if(value.Items.Count>1024) throw new QualificationCodecException("Array exceeds limit."); Units(value.Items.Count*8L); foreach(var v in value.Items) Visit(v,depth+1,reserveDiagnosticExpansion); }
                if(value.Kind==K.Object) { if(value.Properties.Count>256) throw new QualificationCodecException("Map exceeds limit."); Units(value.Properties.Count*16L); foreach(var p in value.Properties) { Visit(V.String(p.Key),depth+1);Visit(p.Value,depth+1,reserveDiagnosticExpansion); } }
            }
        }
        private sealed class BoundedBuffer : IBufferWriter<byte>
        {
            private readonly byte[] bytes=new byte[262144];
            internal int WrittenCount { get; private set; }
            internal ReadOnlySpan<byte> WrittenSpan=>bytes.AsSpan(0,WrittenCount);
            public void Advance(int count) { if(count<0 || count>bytes.Length-WrittenCount) throw new QualificationCodecException("Encoded buffer limit exceeded."); WrittenCount+=count; }
            public Memory<byte> GetMemory(int sizeHint=0) { Check(sizeHint);return bytes.AsMemory(WrittenCount); }
            public Span<byte> GetSpan(int sizeHint=0) { Check(sizeHint);return bytes.AsSpan(WrittenCount); }
            private void Check(int sizeHint) { if(sizeHint<0 || Math.Max(1,sizeHint)>bytes.Length-WrittenCount) throw new QualificationCodecException("Encoded buffer limit exceeded."); }
        }
    }
}
