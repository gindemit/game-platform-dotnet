namespace GamePlatform.Navigation.Kernel
{
    public enum NavigationEntryState { Provisional, Active, Covered, Retired }
    public sealed class NavigationEntrySnapshot
    {
        internal NavigationEntrySnapshot(NavigationEntry entry, NavigationEntryState state, object? restorationState)
        { Entry = entry; State = state; RestorationState = restorationState; }
        public NavigationEntry Entry { get; }
        public NavigationEntryState State { get; }
        /// <summary>Opaque immutable caller-supplied value; the kernel does not serialize or deep-copy it.</summary>
        public object? RestorationState { get; }
        public bool TryGetRestorationState<T>(out T value)
        {
            if (RestorationState is T typed)
            { value = typed; return true; }
            value = default!;
            return false;
        }
    }
}
