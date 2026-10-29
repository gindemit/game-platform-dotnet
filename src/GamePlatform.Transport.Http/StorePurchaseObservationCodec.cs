using System;
using GamePlatform.Features.Store;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Wire.Contracts;
namespace GamePlatform.Transport.Http
{
    public sealed class StorePurchaseObservationCodec : IStorePurchaseObservationCodec
    {
        private readonly StoreQuestMessagePackCodec codec = new StoreQuestMessagePackCodec();
        public StorePurchaseObservation Decode(StorePurchaseRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            var command = codec.DecodePurchaseCommand(record.CopyBody());
            string status;
            string? rejection = null;
            var terminal = record.CopyTerminal();
            if (record.DeliveryState == "pending" || record.DeliveryState == "in_flight")
            {
                if (terminal != null)
                    throw new FormatException("Pending purchase has a terminal result.");
                status = "Pending";
            }
            else if (record.DeliveryState == "accepted" && terminal != null &&
                codec.DecodeResult(terminal) is PushAccepted accepted &&
                accepted.OperationId == record.OperationId.Value && accepted.Sequence == record.Sequence &&
                accepted.FeedRevision > 0 && accepted.Result is StoreOfferPurchasedResult purchase &&
                purchase.PurchaseKey == command.PurchaseKey && purchase.OfferId == command.OfferId &&
                purchase.OfferVersion == command.OfferVersion)
                status = record.CompatibleCheckpoint && record.CommittedThrough >= accepted.FeedRevision ? "Delivered" : "AcceptedAwaitingProjection";
            else if (record.DeliveryState == "terminal_rejected" && terminal != null &&
                codec.DecodeResult(terminal) is PushTerminalRejected rejected &&
                rejected.OperationId == record.OperationId.Value && rejected.Sequence == record.Sequence && !rejected.Error.Retryable)
            {
                status = "Rejected";
                rejection = rejected.Error.Code;
            }
            else
                throw new FormatException("Invalid Store terminal result.");
            return new StorePurchaseObservation(record.OperationId, command.PurchaseKey, command.OfferId, command.OfferVersion, status, rejection);
        }
    }
}
