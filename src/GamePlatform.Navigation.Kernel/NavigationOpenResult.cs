using System.Threading.Tasks;
namespace GamePlatform.Navigation.Kernel
{
    public sealed class NavigationOpenResult<TResult>
    {
        internal NavigationOpenResult(NavigationOutcome outcome, NavigationEntry? entry, Task<NavigationChildResult<TResult>> result)
        { Outcome = outcome; Entry = entry; Result = result; }
        public NavigationOutcome Outcome { get; }
        public NavigationEntry? Entry { get; }
        public Task<NavigationChildResult<TResult>> Result { get; }
    }
}
