#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;
using GamePlatform.Wire.Contracts.Teams;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>Additive Teams pull envelope. Every existing entity remains decoded by the frozen typed mapper.</summary>
    public sealed class TeamsSupportingWireCodec : IWireCodec
    {
        private readonly MessagePackWireCodec frozen = new MessagePackWireCodec();
        private readonly TypedQualificationCodec typed = new TypedQualificationCodec();
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        public byte[] Encode<T>(T value) => frozen.Encode(value);
        public T Decode<T>(byte[] payload)
        {
            if (typeof(T) != typeof(IPullResponse)) return frozen.Decode<T>(payload);
            var value = codec.Decode("teams.schema.json#/$defs/pullResponse", payload);
            var p = value.Properties;
            if (p["resetRequired"].BooleanValue) return (T)(object)typed.FromDiagnostic<IPullResponse>(value);
            var groups = p["changes"].Items.Select(group =>
            {
                var g = group.Properties;
                var changes = g["changes"].Items.Select(change =>
                {
                    var c = change.Properties;
                    return c["entityType"].StringValue == "teams"
                        ? (IProjectionChange)new TeamsProjectionInvalidation(Guid.Parse(c["entityKey"].Properties["teamId"].StringValue), Wide(c["revision"]))
                        : typed.FromDiagnostic<IProjectionChange>(change);
                }).ToArray();
                return new PullGroup(Wide(g["feedRevision"]), changes);
            }).ToArray();
            return (T)(object)new PullPage(groups, p["nextCursor"].StringValue, p["hasMore"].BooleanValue, Wide(p["committedThrough"]), p["serverTime"].IntegerValue);
        }
        private static long Wide(V value) => long.Parse(value.StringValue, CultureInfo.InvariantCulture);
    }
}
