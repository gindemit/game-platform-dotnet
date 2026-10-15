using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>Consumer-owned storage for acknowledged, immutable tutorial checkpoints.</summary>
    public interface ITutorialCheckpointStore
    {
        Task<TutorialCheckpoint?> LoadAsync(TutorialCheckpointIdentity identity, CancellationToken cancellationToken);
        Task<TutorialCheckpointSaveResult> SaveAsync(TutorialCheckpoint checkpoint, CancellationToken cancellationToken);
    }
}
