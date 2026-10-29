using System;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Transport
{
    public sealed class StorefrontCodecTests
    {
        [Fact]
        public void MismatchedTerminalIdentityCannotConfirmDelivery()
        {
            var codec = new StoreQuestMessagePackCodec();
            var operation = new OperationId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001"));
            var key = Guid.Parse("0199f9a0-0000-7000-8000-000000000002");
            var body = codec.EncodePurchaseCommand(new StoreOfferPurchaseCommand("offer.one", 1, key));
            var result = new StoreOfferPurchasedResult(Guid.Parse("0199f9a0-0000-7000-8000-000000000003"), operation.Value,
                "offer.other", 1, key, new StoreOfferDebit("coins", 1), new ILiveSliceRewardLine[] { new LiveSliceCurrencyRewardLine(0, 1, "coins", 1) });
            var terminal = codec.EncodeResult(new PushAccepted(operation.Value, 1, 1, result));
            var record = new StorePurchaseRecord(operation, 1, "accepted", body, terminal, true, 1);
            Assert.Throws<FormatException>(() => new StorePurchaseObservationCodec().Decode(record));
        }

        [Fact]
        public void EmptyCatalogueUsesTheSharedWireVector()
        {
            var codec = new StorefrontMessagePackCodec();
            Assert.Equal(new byte[] { 0x80 }, codec.EncodeRequest());
            Assert.Empty(codec.DecodeResponse(new byte[] { 0x81, 0xa6, 0x6f, 0x66, 0x66, 0x65, 0x72, 0x73, 0x90 }));
        }

        [Fact]
        public void ReadsBackendGeneratedOfferFixture()
        {
            const string hex = "81a66f66666572739187a76f666665724964b5626f6f737465722e68616d6d65722e73696e676c65ac6f6666657256657273696f6ecf0000000000000001a5707269636582aa63757272656e63794964a5636f696e73a87175616e74697479cf0000000000000064a8636f6e74656e74739183a46b696e64aa6974656d5f737461636ba66974656d4964ae626f6f737465722e68616d6d6572a87175616e74697479cf0000000000000001ad70757263686173654c696d6974c0ae707572636861736564436f756e74cf0000000000000000ac617661696c6162696c697479a9617661696c61626c65";
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            var offer = Assert.Single(new StorefrontMessagePackCodec().DecodeResponse(bytes));
            Assert.Equal("booster.hammer.single", offer.OfferId);
            Assert.Equal(100, offer.Price);
            Assert.Equal("booster.hammer", Assert.Single(offer.Contents).ResourceId);
            Assert.Equal("available", offer.Availability);
        }

        [Fact]
        public void RejectsTrailingDataAndOversizedPayload()
        {
            var codec = new StorefrontMessagePackCodec();
            Assert.Throws<FormatException>(() => codec.DecodeResponse(new byte[] { 0x81, 0xa6, 0x6f, 0x66, 0x66, 0x65, 0x72, 0x73, 0x90, 0xc0 }));
            Assert.Throws<FormatException>(() => codec.DecodeResponse(new byte[262145]));
        }
    }
}
