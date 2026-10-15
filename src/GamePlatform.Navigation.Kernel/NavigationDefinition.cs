using System;
namespace GamePlatform.Navigation.Kernel
{
    public enum NavigationPresentation { Screen, Modal }
    public enum NavigationMultiplicity { Multiple, ReuseMatching, SingletonPerScope }

    public sealed class NavigationDefinition<TArgs, TResult>
    {
        public NavigationDefinition(string key, NavigationPresentation presentation = NavigationPresentation.Screen,
            NavigationMultiplicity multiplicity = NavigationMultiplicity.Multiple)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Definition key is required.", nameof(key));
            if (!Enum.IsDefined(typeof(NavigationPresentation), presentation))
                throw new ArgumentOutOfRangeException(nameof(presentation));
            if (!Enum.IsDefined(typeof(NavigationMultiplicity), multiplicity))
                throw new ArgumentOutOfRangeException(nameof(multiplicity));
            Key = key;
            Presentation = presentation;
            Multiplicity = multiplicity;
        }
        public string Key { get; }
        public NavigationPresentation Presentation { get; }
        public NavigationMultiplicity Multiplicity { get; }
    }
}
