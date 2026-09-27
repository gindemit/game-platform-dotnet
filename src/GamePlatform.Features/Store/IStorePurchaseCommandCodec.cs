using System;

namespace GamePlatform.Features.Store
{
    public interface IStorePurchaseCommandCodec
    {
        byte[] EncodePurchaseCommand(string offerId, int offerVersion, Guid purchaseKey);
    }
}
