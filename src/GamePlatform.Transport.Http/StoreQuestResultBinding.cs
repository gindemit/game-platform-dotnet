using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Transport.Http
{
    internal static class StoreQuestResultBinding
    {
        internal static bool IsStoreQuestResult(IPushAcceptedResult result) => result is StoreOfferPurchasedResult || result is QuestClaimedResult;

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
            return false;
        }
    }
}
