using System;

namespace GamePlatform.Navigation
{
    public abstract class NavigationRoute
    {
        internal NavigationRoute(string kind)
        {
            Kind = kind;
        }
        public string Kind { get; }

        internal static string RequireKey(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }
}
