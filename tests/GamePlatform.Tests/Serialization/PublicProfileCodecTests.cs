using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Profiles;
using GamePlatform.Transport.Http;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Tests.Serialization
{
    public sealed class PublicProfileCodecTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void StateCodecRoundTripsExactlyWithAndWithoutProfile(bool available)
        {
            var codec = new PublicProfileStateCodec();
            var bytes = codec.EncodeResponse(PublicProfileServiceTests.Response(available: available));
            var decoded = codec.DecodeResponse(bytes);
            Assert.Equal(PublicProfileServiceTests.Subject, decoded.PlayerId);
            Assert.Equal(1_790_000_000_000, decoded.ServerTimeMilliseconds);
            Assert.Equal(available, decoded.Profile != null);
            if (available)
            {
                Assert.Equal(9_007_199_254_740_993, decoded.Profile!.Revision);
                Assert.Equal("avatar.daisy", decoded.Profile.AvatarKey);
                Assert.Equal("Pocket Garden", decoded.Profile.Team!.Name);
            }
            Assert.Equal(bytes, codec.EncodeResponse(decoded));
            Assert.Throws<QualificationCodecException>(() => codec.DecodeResponse(bytes.Concat(new byte[] { 0 }).ToArray()));
        }

        [Fact]
        public void TeamWithoutProfileIsRejected()
        {
            var wire = new PublicProfileMessagePackCodec().EncodeResponse(new GamePlatform.Wire.Contracts.Profiles.PublicProfileResponse(
                GamePlatform.Tests.Features.Teams.TeamsServiceTests.App, PublicProfileServiceTests.Subject, 0, null,
                new GamePlatform.Wire.Contracts.Profiles.PublicProfileTeam(GamePlatform.Tests.Features.Teams.TeamsServiceTests.Team, "Pocket Garden", "daisy")));
            Assert.Throws<ArgumentException>(() => new PublicProfileStateCodec().DecodeResponse(wire));
        }

        [Fact]
        public void QueryCarriesOnlyTheSubjectAndClosedSchemaRejectsExtraFields()
        {
            var codec = new PublicProfileMessagePackCodec();
            Assert.Equal(PublicProfileServiceTests.Subject, codec.DecodeQuery(codec.EncodeQuery(PublicProfileServiceTests.Subject)));
            Assert.Throws<QualificationCodecException>(() => new QualificationMessagePackCodec().Encode("profiles.schema.json#/$defs/publicQueryRequest", V.Object(new[] {
                new KeyValuePair<string, V>("protocolVersion", V.Integer(1)), new KeyValuePair<string, V>("playerId", V.String(PublicProfileServiceTests.Subject.ToString("D"))),
                new KeyValuePair<string, V>("accountId", V.String(PublicProfileServiceTests.Subject.ToString("D"))) })));
        }
    }
}
