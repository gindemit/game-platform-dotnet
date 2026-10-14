using System;

namespace GamePlatform.Navigation
{

    public readonly struct CallerReturnContext
    {
        public CallerReturnContext(NavigationRoute route, string callerKey)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            if (string.IsNullOrWhiteSpace(callerKey) || callerKey.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(callerKey));
            CallerKey = callerKey;
        }
        public NavigationRoute Route { get; }
        public string CallerKey { get; }
    }
}
