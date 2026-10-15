using GamePlatform.Features.Daily;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;
using GamePlatform.Wire.Contracts.Daily;
namespace GamePlatform.Transport.Http
{
    /// <summary>Durable status cache and daily.claim body codec over the pinned daily schema.</summary>
    public sealed class DailyFeatureCodec : IDailyStateCodec, IDailyClaimCommandCodec
    {
        private readonly DailyMessagePackCodec codec = new DailyMessagePackCodec();
        private readonly StoreQuestMessagePackCodec terminals = new StoreQuestMessagePackCodec();
        public byte[] EncodeStatus(DailyStatus value) => codec.EncodeStatusResponse(DailyModelMapper.ToWire(value));
        public DailyStatus DecodeStatus(byte[] bytes) => DailyModelMapper.ToDomain(codec.DecodeStatusResponse(bytes));
        public byte[] EncodeClaimCommand(string utcDate) => codec.EncodeClaimCommand(new DailyClaimCommand(utcDate));
        public DailyClaimEntry DecodeClaimResult(byte[] bytes) => DailyModelMapper.ToDomain(codec.DecodeClaimResult(bytes));
        public DailyClaimEntry DecodeAcceptedResult(byte[] terminalResult) =>
            DailyModelMapper.ToDomain((DailyClaimedResult)((PushAccepted)terminals.DecodeResult(terminalResult)).Result);
    }
}
