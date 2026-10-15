using System;
using System.Linq;
using GamePlatform.Features.Daily;
using GamePlatform.Wire.Contracts;
using W = GamePlatform.Wire.Contracts.Daily;
namespace GamePlatform.Transport.Http
{
    internal static class DailyModelMapper
    {
        internal static DailyStatus ToDomain(W.DailyStatusResponse value) => new DailyStatus(value.UtcDate, value.NextUtcDayAt, value.Eligible, value.CycleDay, value.PolicyVersion,
            value.Reward.Select(line => new DailyPlannedLine(line.Kind, line.ResourceId, line.Quantity)).ToArray(), value.History.Select(ToDomain).ToArray());
        internal static W.DailyStatusResponse ToWire(DailyStatus value) => new W.DailyStatusResponse(value.UtcDate, value.NextUtcDayAtMilliseconds, value.Eligible, value.CycleDay, value.PolicyVersion,
            value.Reward.Select(line => new W.DailyPlannedLine(line.Kind, line.ResourceId, line.Quantity)).ToArray(),
            value.History.Select(entry => new W.DailyHistoryEntry(entry.UtcDate, entry.CycleDay, entry.ClaimId, entry.PolicyVersion, entry.Lines.Select(ToWire).ToArray())).ToArray());
        internal static DailyClaimEntry ToDomain(W.DailyHistoryEntry value) => new DailyClaimEntry(value.UtcDate, value.CycleDay, value.ClaimId, value.PolicyVersion, value.Lines.Select(ToDomain).ToArray());
        internal static DailyClaimEntry ToDomain(W.DailyClaimedResult value) => new DailyClaimEntry(value.UtcDate, value.CycleDay, value.ClaimId, value.PolicyVersion, value.Lines.Select(ToDomain).ToArray());
        private static DailyReceiptLine ToDomain(ILiveSliceRewardLine line) => line switch
        {
            LiveSliceCurrencyRewardLine currency => new DailyReceiptLine(currency.LineIndex, currency.DefinitionVersion, "currency", currency.CurrencyId, currency.Quantity),
            LiveSliceStackRewardLine stack => new DailyReceiptLine(stack.LineIndex, stack.DefinitionVersion, "item_stack", stack.ItemId, stack.Quantity),
            _ => throw new ArgumentException("Daily receipts carry currency or item stack lines.", nameof(line))
        };
        private static ILiveSliceRewardLine ToWire(DailyReceiptLine line) => line.Kind == "currency"
            ? new LiveSliceCurrencyRewardLine(line.LineIndex, line.DefinitionVersion, line.ResourceId, line.Quantity)
            : new LiveSliceStackRewardLine(line.LineIndex, line.DefinitionVersion, line.ResourceId, line.Quantity);
    }
}
