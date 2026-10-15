using System;
using System.Threading;
namespace GamePlatform.Navigation.Kernel
{
    public sealed class NavigationEntry
    {
        internal NavigationEntry(EntryId id, FlowId flowId, string scopeId, long generation, EntryId? parentId,
            string definitionKey, object? arguments, Type argumentsType, Type resultType,
            NavigationPresentation presentation, string? matchKey, CancellationToken lifetimeToken)
        {
            Id = id;
            FlowId = flowId;
            ScopeId = scopeId;
            Generation = generation;
            ParentId = parentId;
            DefinitionKey = definitionKey;
            Arguments = arguments;
            ArgumentsType = argumentsType;
            ResultType = resultType;
            Presentation = presentation;
            MatchKey = matchKey;
            LifetimeToken = lifetimeToken;
        }
        public EntryId Id { get; }
        public FlowId FlowId { get; }
        public string ScopeId { get; }
        public long Generation { get; }
        public EntryId? ParentId { get; }
        public string DefinitionKey { get; }
        public object? Arguments { get; }
        public Type ArgumentsType { get; }
        public Type ResultType { get; }
        public NavigationPresentation Presentation { get; }
        public string? MatchKey { get; }
        public CancellationToken LifetimeToken { get; }
        public TArgs GetArguments<TArgs>() => ArgumentsType == typeof(TArgs) ? (TArgs)Arguments! : throw new InvalidOperationException("Argument type mismatch.");
    }
}
