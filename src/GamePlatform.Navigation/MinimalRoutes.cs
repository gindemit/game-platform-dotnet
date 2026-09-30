using System;
using GamePlatform.Core;

namespace GamePlatform.Navigation
{
    public abstract class NavigationRoute
    {
        internal NavigationRoute(string kind) { Kind = kind; }
        public string Kind { get; }

        internal static string RequireKey(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public sealed class HomeRoute : NavigationRoute
    {
        public HomeRoute() : base("home") { }
    }

    public sealed class PlayRoute : NavigationRoute
    {
        public PlayRoute(SemanticId contentKey, long levelScopeGeneration) : base("play")
        {
            if (!contentKey.IsValid) throw new ArgumentException("A content key is required.", nameof(contentKey));
            if (levelScopeGeneration < 0) throw new ArgumentOutOfRangeException(nameof(levelScopeGeneration));
            ContentKey = contentKey;
            LevelScopeGeneration = levelScopeGeneration;
        }
        public SemanticId ContentKey { get; }
        public long LevelScopeGeneration { get; }
    }

    public enum CompletionPresentationState { Pending, Confirmed, SaveFailed }

    public sealed class ResultRoute : NavigationRoute
    {
        public ResultRoute(SemanticId contentKey, CompletionPresentationState state) : base("result")
        {
            if (!contentKey.IsValid) throw new ArgumentException("A content key is required.", nameof(contentKey));
            ContentKey = contentKey;
            State = state;
        }
        public SemanticId ContentKey { get; }
        public CompletionPresentationState State { get; }
    }

    public readonly struct CallerReturnContext
    {
        public CallerReturnContext(NavigationRoute route, string callerKey)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            if (string.IsNullOrWhiteSpace(callerKey) || callerKey.Length > 128) throw new ArgumentOutOfRangeException(nameof(callerKey));
            CallerKey = callerKey;
        }
        public NavigationRoute Route { get; }
        public string CallerKey { get; }
    }

    public sealed class ProfileRoute : NavigationRoute
    {
        public ProfileRoute(CallerReturnContext returnContext) : base("profile") { ReturnContext = returnContext; }
        public CallerReturnContext ReturnContext { get; }
    }

    /// <summary>
    /// Root of a game-configured destination such as a shell tab; the key vocabulary belongs to the game.
    /// </summary>
    public sealed class DestinationRoute : NavigationRoute
    {
        public DestinationRoute(string destinationKey) : base("destination")
        {
            DestinationKey = RequireKey(destinationKey, nameof(destinationKey));
        }
        public string DestinationKey { get; }
    }

    /// <summary>
    /// Generic detail screen that returns to the caller that opened it.
    /// </summary>
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

    public sealed class ModalRoute : NavigationRoute
    {
        public ModalRoute(string modalKey) : base("modal")
        {
            if (string.IsNullOrWhiteSpace(modalKey) || modalKey.Length > 128) throw new ArgumentOutOfRangeException(nameof(modalKey));
            ModalKey = modalKey;
        }
        public string ModalKey { get; }
    }
}
