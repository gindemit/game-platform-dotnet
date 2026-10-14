namespace GamePlatform.Features.Contracts
{

    public interface IGameplayOutcomeSink
    {
        void Report(GameplayOutcome outcome);
    }
}
