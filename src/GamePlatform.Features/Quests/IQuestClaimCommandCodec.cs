namespace GamePlatform.Features.Quests
{
    public interface IQuestClaimCommandCodec
    {
        byte[] EncodeClaimCommand(string questId, string occurrenceKey);
    }
}
