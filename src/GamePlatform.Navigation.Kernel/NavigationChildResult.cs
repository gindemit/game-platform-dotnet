namespace GamePlatform.Navigation.Kernel
{
    public enum NavigationChildStatus { Completed, Cancelled, OwnerRetired }
    public sealed class NavigationChildResult<TResult>
    {
        internal NavigationChildResult(NavigationChildStatus status, TResult value) { Status = status; Value = value; }
        public NavigationChildStatus Status { get; }
        public bool HasValue => Status == NavigationChildStatus.Completed;
        public TResult Value { get; }
    }
}
