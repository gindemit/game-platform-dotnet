using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts
{

    internal static class CompatibilityGuard
    {
        internal static void Id(PlatformId value, string parameter)
        {
            if (!value.IsValid)
                throw new ArgumentException("A valid platform ID is required.", parameter);
        }
        internal static string Required(string value, string parameter, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A value is required.", parameter);
            if (value.Length > maximum)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }
        internal static string Optional(string? value, string parameter, int maximum = 512)
        {
            value ??= string.Empty;
            if (value.Length > maximum)
                throw new ArgumentOutOfRangeException(parameter);
            return value;
        }
    }
}
