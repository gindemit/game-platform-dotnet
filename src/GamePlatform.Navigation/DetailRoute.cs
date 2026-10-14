using System;
using GamePlatform.Core;

namespace GamePlatform.Navigation
{

    public sealed class DetailRoute : NavigationRoute
    {
        public DetailRoute(string detailKey, CallerReturnContext returnContext) : base("detail")
        {
            DetailKey = RequireKey(detailKey, nameof(detailKey));
            ReturnContext = returnContext;
        }
        public string DetailKey { get; }
        public CallerReturnContext ReturnContext { get; }
    }
}
