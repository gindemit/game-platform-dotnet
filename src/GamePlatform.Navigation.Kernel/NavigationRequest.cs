using System;
namespace GamePlatform.Navigation.Kernel
{
    /// <summary>Arguments must be immutable caller-owned values for the complete entry lifetime.</summary>
    public sealed class NavigationRequest<TArgs, TResult>
    {
        public NavigationRequest(NavigationDefinition<TArgs, TResult> definition, TArgs arguments, string? matchKey = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Arguments = arguments;
            MatchKey = matchKey;
            if (definition.Multiplicity == NavigationMultiplicity.ReuseMatching && string.IsNullOrEmpty(matchKey))
                throw new ArgumentException("ReuseMatching requires an explicit stable match key.", nameof(matchKey));
        }
        public NavigationDefinition<TArgs, TResult> Definition { get; }
        public TArgs Arguments { get; }
        public string? MatchKey { get; }
    }
}
