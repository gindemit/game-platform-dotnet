using System;
using System.Collections.Generic;

namespace GamePlatform.Features.Contracts.Inventory
{

    public interface IInventoryStateCodec
    {
        byte[] EncodeConfirmed(InventoryConfirmedSnapshot snapshot);
        InventoryConfirmedSnapshot DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        byte[] EncodePending(IReadOnlyList<InventoryIntent> intents);
        IReadOnlyList<InventoryIntent> DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodeIntentCommand(InventoryIntent intent);
        void ValidateInstanceExtensions(ReadOnlySpan<byte> extensions);
    }
}
