using System.Threading;
using System.Threading.Tasks;
namespace GamePlatform.Navigation.Kernel
{
    public enum NavigationAdmissionOutcome { Available, Unavailable }
    /// <summary>Prepare a shell, not remote feature data. Release provisional resources when its lifetime retires.</summary>
    public delegate Task<NavigationAdmissionOutcome> NavigationAdmission(NavigationEntry provisional, CancellationToken requestToken);
}
