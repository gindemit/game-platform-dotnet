using System;
using GamePlatform.Core;

namespace GamePlatform.Navigation
{

    public sealed class PlayRoute : NavigationRoute
    {
        public PlayRoute(SemanticId contentKey, long levelScopeGeneration) : base("play")
        {
            if (!contentKey.IsValid)
                throw new ArgumentException("A content key is required.", nameof(contentKey));
            if (levelScopeGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(levelScopeGeneration));
            ContentKey = contentKey;
            LevelScopeGeneration = levelScopeGeneration;
        }
        public SemanticId ContentKey { get; }
        public long LevelScopeGeneration { get; }
    }
}
