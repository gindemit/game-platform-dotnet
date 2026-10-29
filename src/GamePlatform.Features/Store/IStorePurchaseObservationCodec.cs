using GamePlatform.Storage.Abstractions.Outbox;
namespace GamePlatform.Features.Store
{
    public interface IStorePurchaseObservationCodec
    {
        StorePurchaseObservation Decode(StorePurchaseRecord record);
    }
}
