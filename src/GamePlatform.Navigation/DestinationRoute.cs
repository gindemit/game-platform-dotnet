namespace GamePlatform.Navigation
{

    public sealed class DestinationRoute : NavigationRoute
    {
        public DestinationRoute(string destinationKey) : base("destination")
        {
            DestinationKey = RequireKey(destinationKey, nameof(destinationKey));
        }
        public string DestinationKey { get; }
    }
}
