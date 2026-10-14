using System;
using GamePlatform.Core;

namespace GamePlatform.Navigation
{

    public sealed class ResultRoute : NavigationRoute
    {
        public ResultRoute(SemanticId contentKey, CompletionPresentationState state) : base("result")
        {
            if (!contentKey.IsValid)
                throw new ArgumentException("A content key is required.", nameof(contentKey));
            ContentKey = contentKey;
            State = state;
        }
        public SemanticId ContentKey { get; }
        public CompletionPresentationState State { get; }
    }
}
