using System;
using GamePlatform.Core;

namespace GamePlatform.Navigation
{

    public sealed class ModalRoute : NavigationRoute
    {
        public ModalRoute(string modalKey) : base("modal")
        {
            ModalKey = RequireKey(modalKey, nameof(modalKey));
        }
        public string ModalKey { get; }
    }
}
