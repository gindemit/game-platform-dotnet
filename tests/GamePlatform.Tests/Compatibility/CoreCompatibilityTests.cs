using System;
using System.Collections.Generic;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Tests.Compatibility;

public sealed class CoreCompatibilityTests
{
    private static readonly Guid Issued = Guid.Parse("00112233-4455-4677-8899-aabbccddeeff");

    [Theory]
    [InlineData("pocket-bloom")]
    [InlineData("fixture/0")]
    [InlineData("race-4")]
    [InlineData("weekly-distance")]
    [InlineData("Provider_USER_1")]
    public void SemanticIdsPreserveOriginalOrdinalBytes(string value)
    {
        Assert.Equal(value, new PlatformId(value).Value);
        Assert.Equal(value, new SemanticId(value).Value);
        Assert.Equal(value, new BackendNamespace(value).Value);
        Assert.NotEqual(new PlatformId(value), new PlatformId(value.ToUpperInvariant()));
    }

    [Fact]
    public void IdBoundsAndDefaultRemainLegacyCompatible()
    {
        Assert.False(default(PlatformId).IsValid);
        Assert.False(default(SemanticId).IsValid);
        Assert.False(default(BackendNamespace).IsValid);
        Assert.Equal(string.Empty, default(PlatformId).ToString());
        Assert.Throws<ArgumentException>(() => new PlatformId(" \t"));
        Assert.Throws<ArgumentException>(() => new PlatformId(null!));
        Assert.True(new PlatformId(new string('x', 128)).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformId(new string('x', 129)));
    }

    [Theory]
    [InlineData(PlatformFailure.Offline, 1)]
    [InlineData(PlatformFailure.Unauthorized, 2)]
    [InlineData(PlatformFailure.Conflict, 3)]
    [InlineData(PlatformFailure.NotFound, 4)]
    [InlineData(PlatformFailure.RateLimited, 5)]
    [InlineData(PlatformFailure.Invalid, 6)]
    [InlineData(PlatformFailure.Unavailable, 7)]
    [InlineData(PlatformFailure.NotImplemented, 8)]
    public void FailureValuesAndDefaultsRemainCompatible(PlatformFailure failure, int number)
    {
        Assert.Equal(number, (int)failure);
        var result = PlatformResult<string>.Error(failure, null!);
        Assert.False(result.Succeeded); Assert.Null(result.Value); Assert.Equal(string.Empty, result.Diagnostic);
    }

    [Fact]
    public void ResultsPreserveNullSuccessButRejectInvalidFailureAndOversizedDiagnostics()
    {
        Assert.True(PlatformResult<string?>.Success(null).Succeeded);
        Assert.Throws<ArgumentException>(() => PlatformResult<int>.Error(PlatformFailure.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformResult<int>.Error((PlatformFailure)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformResult<int>.Error((PlatformFailure)(-1)));
        Assert.Equal(512, PlatformResult<int>.Error(PlatformFailure.Invalid, new string('x', 512)).Diagnostic.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformResult<int>.Error(PlatformFailure.Invalid, new string('x', 513)));
        Assert.Equal(0, PlatformResult<int>.Error(PlatformFailure.Invalid).Value);
    }

    [Fact]
    public void RfcKnownAnswerPreservesIssuedUuidAndRejectsWrongWidths()
    {
        byte[] expected = Convert.FromHexString("00112233445546778899aabbccddeeff");
        var bytes = new byte[16];
        new AppId(Issued).WriteRfcBytes(bytes);
        Assert.Equal(expected, bytes);
        Assert.Equal(Issued, AppId.FromRfcBytes(expected).Value);
        Assert.Equal(Issued, PlatformUserId.FromRfcBytes(expected).Value);
        Assert.Equal(Issued, OperationId.FromRfcBytes(expected).Value);
        Assert.Equal(Issued, ClientStreamId.FromRfcBytes(expected).Value);
        Assert.Throws<ArgumentException>(() => UuidIdentity.FromRfcBytes(new byte[15]));
        Assert.Throws<ArgumentException>(() => UuidIdentity.FromRfcBytes(new byte[17]));
        Assert.Throws<ArgumentException>(() => UuidIdentity.WriteRfcBytes(Issued, new byte[15]));
        Assert.False(default(OperationId).IsValid);
        Assert.Throws<ArgumentException>(() => new AppId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ClientStreamId(Guid.Parse("00112233-4455-9677-8899-aabbccddeeff")));
        Assert.Throws<ArgumentException>(() => new PlatformUserId(Guid.Parse("00112233-4455-4677-0899-aabbccddeeff")));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void IssuedVersionsArePreservedPerApprovedUuidSchema(int version)
    {
        Guid value = Guid.Parse($"00112233-4455-{version}677-8899-aabbccddeeff");
        Assert.Equal(value, new OperationId(value).Value);
        Assert.Equal(version == 7, UuidIdentity.IsValid(value, false));
    }

    [Fact]
    public void RfcComparisonIsLexicographicAndRolesAreNotInterchangeable()
    {
        var low = new AppId(Guid.Parse("000000ff-ffff-4fff-8fff-ffffffffffff"));
        var high = new AppId(Guid.Parse("00000100-0000-4000-8000-000000000000"));
        Assert.True(low.CompareTo(high) < 0);
        Assert.False(low.Equals((object)new PlatformUserId(low.Value)));
        Assert.Equal(low.GetHashCode(), new AppId(low.Value).GetHashCode());
    }

    [Fact]
    public void GeneratorUsesV7RfcTimestampAndClampsClockRollback()
    {
        var clock = new Clock { Value = 0x010203040506 };
        var generator = new UuidV7Generator(clock, new Entropy());
        var first = new byte[16]; UuidIdentity.WriteRfcBytes(generator.NewId(), first);
        Assert.Equal(Convert.FromHexString("010203040506"), first[..6]);
        Assert.Equal(7, first[6] >> 4); Assert.Equal(0x80, first[8] & 0xc0);
        clock.Value -= 1000;
        var second = new byte[16]; UuidIdentity.WriteRfcBytes(generator.NewId(), second);
        Assert.Equal(first[..6], second[..6]); Assert.NotEqual(first, second);
        clock.Value = -1; Assert.Throws<ArgumentOutOfRangeException>(() => generator.NewId());
        clock.Value = PlatformNumbers.MaximumUnixMilliseconds + 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.NewId());
    }

    [Fact]
    public void OwnerTupleAndViewGenerationHaveSeparateIdentity()
    {
        var owner = new OwnerScope(new BackendNamespace("backend-a"), new AppId(Issued), new PlatformUserId(Issued));
        var view = new ScopedOwnerContext(owner, new SemanticId("view/1"), 0);
        var replacement = new ScopedOwnerContext(owner, new SemanticId("view/2"), long.MaxValue);
        Assert.Equal(view.Owner, replacement.Owner); Assert.NotEqual(view, replacement);
        Assert.NotEqual(owner, new OwnerScope(new BackendNamespace("backend-b"), owner.AppId, owner.UserId));
        Assert.Throws<ArgumentException>(() => new OwnerScope(default, owner.AppId, owner.UserId));
        Assert.Throws<ArgumentException>(() => new OwnerScope(owner.Backend, default, owner.UserId));
        Assert.Throws<ArgumentException>(() => new OwnerScope(owner.Backend, owner.AppId, default));
        Assert.Throws<ArgumentException>(() => new ScopedOwnerContext(default, new SemanticId("view"), 0));
        Assert.Throws<ArgumentException>(() => new ScopedOwnerContext(owner, default, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScopedOwnerContext(owner, new SemanticId("view"), -1));
    }

    [Fact]
    public void FullWidthArithmeticIsChecked()
    {
        Assert.Equal(9007199254740993L, PlatformNumbers.Add(9007199254740992L, 1));
        Assert.Throws<OverflowException>(() => PlatformNumbers.Add(long.MaxValue, 1));
        Assert.Throws<OverflowException>(() => PlatformNumbers.Add(long.MinValue, -1));
        Assert.Equal(1, PlatformNumbers.NextPositiveSequence(0));
        Assert.Throws<OverflowException>(() => PlatformNumbers.NextPositiveSequence(long.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformNumbers.NextPositiveSequence(-1));
        Assert.Equal(0, PlatformNumbers.UnixMilliseconds(0));
        Assert.Equal(PlatformNumbers.MaximumUnixMilliseconds, PlatformNumbers.UnixMilliseconds(PlatformNumbers.MaximumUnixMilliseconds));
    }

    [Fact]
    public void OutcomeCopiesLegacyMetricsWithoutWireTruncationOrNumericLoss()
    {
        var metrics = new Dictionary<string, long> { ["moves"] = 9007199254740993L, ["negative"] = long.MinValue };
        var value = Outcome(metrics);
        metrics["moves"] = 0;
        Assert.Equal(9007199254740993L, value.Metrics["moves"]);
        Assert.Equal(long.MinValue, value.Score); Assert.Equal(long.MaxValue, value.DurationTicks);
        Assert.Equal(string.Empty, value.ValidationReference);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, long>)value.Metrics).Add("new", 1));
        var many = new Dictionary<string, long>();
        for (int i = 0; i < 1000; i++) many.Add("key" + i, i);
        Assert.Equal(1000, Outcome(many).Metrics.Count);
        many.Add("too-many", 0); Assert.Throws<ArgumentOutOfRangeException>(() => Outcome(many));
        Assert.Throws<ArgumentException>(() => Outcome(new Dictionary<string, long> { [" "] = 0 }));
        Assert.Empty(Outcome(null).Metrics);
    }

    [Fact]
    public void OutcomeAndProgressPreserveLegacyBounds()
    {
        var id = new PlatformId("id");
        Assert.Throws<ArgumentException>(() => new GameplayOutcome(default, id, id, id, true, 0, 0, 1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayOutcome(id, id, id, id, true, 0, -1, 1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayOutcome(id, id, id, id, true, 0, 0, 1000001, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayOutcome(id, id, id, id, true, 0, 0, 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayOutcome(id, id, id, id, true, 0, 0, 1, null, new string('x', 513)));
        Assert.Equal(long.MaxValue, new ProgressEvent(long.MaxValue, "counter", long.MaxValue).Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressEvent(0, "counter", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressEvent(1, "counter", -1));
    }

    [Fact]
    public void StateRecordValidatesLegacyOwnershipRevisionAndPayload()
    {
        Assert.Equal(2, (int)DataOwnership.ServerEconomy);
        Assert.Equal(9007199254740993L, new StateRecord(1, 9007199254740993L, "payload", DataOwnership.ValidatedProgress).Revision);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateRecord(0, 0, "", DataOwnership.LocalPreference));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateRecord(1000001, 0, "", DataOwnership.LocalPreference));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateRecord(1, -1, "", DataOwnership.LocalPreference));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateRecord(1, 0, "", (DataOwnership)5));
        Assert.Throws<ArgumentNullException>(() => new StateRecord(1, 0, null!, DataOwnership.LocalPreference));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateRecord(1, 0, new string('x', 1024 * 1024 + 1), DataOwnership.LocalPreference));
    }

    private static GameplayOutcome Outcome(IReadOnlyDictionary<string, long>? metrics) =>
        new(new PlatformId("session"), new PlatformId("pocket-bloom"), new PlatformId("fixture/0"), new PlatformId("normal"),
            true, long.MinValue, long.MaxValue, 1000000, metrics, null);

    private sealed class Clock : IUnixMillisecondClock { public long Value; public long GetUnixMilliseconds() => Value; }
    private sealed class Entropy : IUuidRandomSource
    {
        private byte next;
        public void Fill(Span<byte> destination) { destination.Fill(next++); }
    }
}
