namespace GamePlatform.Navigation
{

    public sealed class ProfileRoute : NavigationRoute
    {
        public ProfileRoute(CallerReturnContext returnContext) : base("profile") { ReturnContext = returnContext; }
        public CallerReturnContext ReturnContext { get; }
    }
}
