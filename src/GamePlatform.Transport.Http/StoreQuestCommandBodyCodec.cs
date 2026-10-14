using System;
using GamePlatform.Features.Quests;
using GamePlatform.Features.Store;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Transport.Http
{
    /// <summary>
    /// Encodes store purchase and quest claim semantic bodies with the A06 MessagePack codec.
    /// </summary>
    public sealed class StoreQuestCommandBodyCodec : IStorePurchaseCommandCodec, IQuestClaimCommandCodec
    {
        private readonly StoreQuestMessagePackCodec codec;

        public StoreQuestCommandBodyCodec(StoreQuestMessagePackCodec codec)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public byte[] EncodePurchaseCommand(string offerId, int offerVersion, Guid purchaseKey) =>
            codec.EncodePurchaseCommand(new StoreOfferPurchaseCommand(offerId, offerVersion, purchaseKey));

        public byte[] EncodeClaimCommand(string questId, string occurrenceKey) => codec.EncodeClaimCommand(new QuestClaimCommand(questId, occurrenceKey));
    }
}
