using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamePlatform.Wire.Contracts.Leaderboards
{

    internal static class LeaderboardsValidation
    {
        internal static void Board(string value)
        {
            if (value == null || !Regex.IsMatch(value, "^[a-z][a-z0-9-]{0,63}$"))
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Kind(string value)
        {
            if (value != "player" && value != "team")
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Country(string? value, bool nullable)
        {
            if (value == null ? !nullable : !Regex.IsMatch(value, "^[A-Z]{2}$"))
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Scope(string scope, string? country)
        {
            if (scope != "global" && scope != "country" || (scope == "country") != (country != null))
                throw new ArgumentException("Invalid scope country binding.");
            Country(country, true);
        }
        internal static void Cursor(string? value)
        {
            if (value != null)
                Text(value, 4096, nameof(value));
            if (value != null && (value.Length < 16 || !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$")))
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Time(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Text(string value, int maximum, string name, bool emptyAllowed = false)
        {
            if (value == null || !emptyAllowed && value.Length == 0)
                throw new ArgumentOutOfRangeException(name);
            int scalars = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c))
                    throw new ArgumentOutOfRangeException(name);
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i]))
                        throw new ArgumentOutOfRangeException(name);
                }
                else if (char.IsLowSurrogate(c))
                    throw new ArgumentOutOfRangeException(name);
                if (++scalars > maximum)
                    throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
