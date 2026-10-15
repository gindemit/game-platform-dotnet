using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;
using GamePlatform.Wire.Contracts.Daily;

namespace GamePlatform.Transport.Http
{
    internal static class StoreQuestResultBinding
    {
        /// <summary>
        /// The A06 superset family: store purchase, quest claim and daily claim.
        /// </summary>
        internal static bool IsStoreQuestResult(IPushAcceptedResult result) => result is StoreOfferPurchasedResult || result is QuestClaimedResult || result is DailyClaimedResult;

        internal static bool Matches(StoreQuestMessagePackCodec codec, RemoteCommand command, IPushAcceptedResult result)
        {
            if (command.OperationKind == "store.offer.purchase")
            {
                var sent = codec.DecodePurchaseCommand(command.SemanticBody);
                return result is StoreOfferPurchasedResult purchase && purchase.OfferId == sent.OfferId &&
                    purchase.OfferVersion == sent.OfferVersion && purchase.PurchaseKey == sent.PurchaseKey;
            }
            if (command.OperationKind == "quest.claim")
            {
                var sent = codec.DecodeClaimCommand(command.SemanticBody);
                return result is QuestClaimedResult claim && claim.QuestId == sent.QuestId && claim.OccurrenceKey == sent.OccurrenceKey;
            }
            if (command.OperationKind == "daily.claim")
            {
                var sent = codec.DecodeDailyClaimCommand(command.SemanticBody);
                return result is DailyClaimedResult claim && claim.UtcDate == sent.UtcDate;
            }
            return false;
        }
    }
}
