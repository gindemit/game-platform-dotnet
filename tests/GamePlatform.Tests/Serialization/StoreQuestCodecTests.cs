using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Serialization;

public sealed class StoreQuestCodecTests
{
    private const string Schema = "a06/store-quest.schema.json#/$defs/";
    private static readonly JsonDocument Fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "contracts/a06/fixtures.json")));
    private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
    private readonly StoreQuestMessagePackCodec storeQuest = new StoreQuestMessagePackCodec();

    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var item in Fixtures.RootElement.GetProperty("cases").EnumerateArray()) data.Add(item.GetProperty("id").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void EveryApprovedFixtureValidatesExactlyAsDeclared(string id)
    {
        var fixture = Case(id);
        var peer = fixture.TryGetProperty("schema", out var named) ? named.GetString() : null;
        var prefix = peer == "v1-push" ? "push.schema.json#/$defs/" : peer == "v1-recovery" ? "recovery.schema.json#/$defs/" : Schema;
        var schema = prefix + fixture.GetProperty("definition").GetString();
        var value = QualificationCodecTests.Parse(fixture.GetProperty("value"));

        if (fixture.GetProperty("valid").GetBoolean())
            Assert.Equal(codec.Encode(schema, value), codec.Encode(schema, codec.Decode(schema, codec.Encode(schema, value))));
        else
            Assert.Throws<QualificationCodecException>(() => codec.ValidateDiagnostic(schema, value));
    }

    [Theory]
    [InlineData("accepted-purchase")]
    [InlineData("accepted-purchase-reused-by-second-operation")]
    [InlineData("accepted-claim")]
    [InlineData("accepted-purchase-max-quantity")]
    [InlineData("legacy-gameplay-accepted-still-valid")]
    [InlineData("terminal-insufficient-balance")]
    [InlineData("terminal-quest-not-claimable")]
    public void TypedResultsRoundTripToIdenticalBytes(string id)
    {
        var bytes = FixtureBytes(id);

        Assert.Equal(bytes, storeQuest.EncodeResult(storeQuest.DecodeResult(bytes)));
    }

    [Theory]
    [InlineData("push-request-purchase-and-claim", "request")]
    [InlineData("push-response", "response")]
    [InlineData("receipt-found-purchase", "receiptResponse")]
    [InlineData("receipt-found-claim", "receiptResponse")]
    [InlineData("receipt-found-legacy-gameplay", "receiptResponse")]
    [InlineData("receipt-found-store-rejection", "receiptResponse")]
    [InlineData("receipt-not-found", "receiptResponse")]
    public void TypedEnvelopesRoundTripToIdenticalBytes(string id, string definition)
    {
        var bytes = FixtureBytes(id);

        var again = definition == "request" ? storeQuest.EncodePushRequest(storeQuest.DecodePushRequest(bytes))
            : definition == "response" ? storeQuest.EncodePushResponse(storeQuest.DecodePushResponse(bytes))
            : storeQuest.EncodeReceiptResponse(storeQuest.DecodeReceiptResponse(bytes));

        Assert.Equal(bytes, again);
    }

    [Fact]
    public void ReceiptLookupDecodesAPurchasedResult()
    {
        var receipt = storeQuest.DecodeReceiptResponse(FixtureBytes("receipt-found-purchase"));

        Assert.True(receipt.Found);
        var accepted = Assert.IsType<PushAccepted>(receipt.Result);
        Assert.Equal(42, accepted.FeedRevision);
        var purchase = Assert.IsType<StoreOfferPurchasedResult>(accepted.Result);
        Assert.Equal("test.offer.starter", purchase.OfferId);
        Assert.Equal(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-1020304050c0"), purchase.PurchaseKey);
        Assert.Equal("test.coin", purchase.Debit.CurrencyId);
        Assert.Equal(10, purchase.Debit.Quantity);
        Assert.Equal("test.item.hat", Assert.IsType<LiveSliceStackRewardLine>(purchase.Lines[0]).ItemId);
        Assert.Equal(5, Assert.IsType<LiveSliceCurrencyRewardLine>(purchase.Lines[1]).Quantity);
    }

    [Fact]
    public void ReceiptLookupDecodesAClaimedResult()
    {
        var receipt = storeQuest.DecodeReceiptResponse(FixtureBytes("receipt-found-claim"));

        var claim = Assert.IsType<QuestClaimedResult>(Assert.IsType<PushAccepted>(receipt.Result).Result);
        Assert.NotEmpty(claim.OccurrenceKey);
        Assert.NotEmpty(claim.Lines);
    }

    [Fact]
    public void LegacyAndRejectedReceiptsKeepTheirFrozenTypes()
    {
        Assert.IsType<PushGameplayCompletionRecordedResult>(Assert.IsType<PushAccepted>(storeQuest.DecodeReceiptResponse(FixtureBytes("receipt-found-legacy-gameplay")).Result).Result);
        Assert.IsType<PushTerminalRejected>(storeQuest.DecodeReceiptResponse(FixtureBytes("receipt-found-store-rejection")).Result);
        Assert.Null(storeQuest.DecodeReceiptResponse(FixtureBytes("receipt-not-found")).Result);
    }

    [Fact]
    public void PayloadCodecsRoundTripAndRejectMalformedBodies()
    {
        var key = Guid.Parse("01890f3e-7a6b-7c8d-9e0f-1020304050c0");
        var purchase = storeQuest.DecodePurchaseCommand(storeQuest.EncodePurchaseCommand(new StoreOfferPurchaseCommand("test.offer.starter", 1, key)));
        var claim = storeQuest.DecodeClaimCommand(storeQuest.EncodeClaimCommand(new QuestClaimCommand("test.quest", "test.quest:occ-1")));

        Assert.Equal(("test.offer.starter", 1, key), (purchase.OfferId, purchase.OfferVersion, purchase.PurchaseKey));
        Assert.Equal(("test.quest", "test.quest:occ-1"), (claim.QuestId, claim.OccurrenceKey));
        Assert.Throws<QualificationCodecException>(() => storeQuest.EncodePurchaseCommand(new StoreOfferPurchaseCommand("test.offer", 0, key)));
        Assert.Throws<QualificationCodecException>(() => storeQuest.EncodePurchaseCommand(new StoreOfferPurchaseCommand("test.offer", 1, Guid.Parse("01890f3e-7a6b-4c8d-9e0f-1020304050c0"))));
        Assert.Throws<QualificationCodecException>(() => storeQuest.DecodeClaimCommand(storeQuest.EncodePurchaseCommand(new StoreOfferPurchaseCommand("test.offer", 1, key))));
        Assert.Throws<QualificationCodecException>(() => storeQuest.DecodePurchaseCommand(new byte[] { 0xc0 }));
    }

    private byte[] FixtureBytes(string id)
    {
        var fixture = Case(id);
        return codec.Encode(Schema + fixture.GetProperty("definition").GetString(), QualificationCodecTests.Parse(fixture.GetProperty("value")));
    }

    private static JsonElement Case(string id) => Fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(value => value.GetProperty("id").GetString() == id);

    private static string Root
    {
        get { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory != null && !File.Exists(Path.Combine(directory.FullName, "GamePlatform.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException(); }
    }
}
