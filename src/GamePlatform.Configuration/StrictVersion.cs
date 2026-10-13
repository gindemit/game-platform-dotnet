using System;
using System.Globalization;

namespace GamePlatform.Configuration
{
    /// <summary>Bounded strict ASCII SemVer subset, independent of culture.</summary>
    public sealed class StrictVersion : IComparable<StrictVersion>
    {
        private readonly int major;
        private readonly int minor;
        private readonly int patch;
        private readonly string[] prerelease;
        private StrictVersion(int major, int minor, int patch, string[] prerelease) { this.major = major; this.minor = minor; this.patch = patch; this.prerelease = prerelease; }
        public static StrictVersion Parse(string value) => TryParse(value, out var version) ? version! : throw new InvalidConfigurationException("version_invalid");
        public static bool TryParse(string? value, out StrictVersion? version)
        {
            version = null;
            if (value == null || value.Length == 0 || value.Length > 256) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == '-' || c == '+')) return false;
            int plus = value.IndexOf('+');
            string main = plus < 0 ? value : value.Substring(0, plus);
            if (plus >= 0 && !Identifiers(value.Substring(plus + 1), false, out _)) return false;
            int dash = main.IndexOf('-');
            string[] pre = Array.Empty<string>();
            if (dash >= 0 && !Identifiers(main.Substring(dash + 1), true, out pre)) return false;
            string[] numbers = (dash < 0 ? main : main.Substring(0, dash)).Split('.');
            if (numbers.Length != 3 || !Number(numbers[0], out int a) || !Number(numbers[1], out int b) || !Number(numbers[2], out int c0)) return false;
            version = new StrictVersion(a, b, c0, pre);
            return true;
        }
        private static bool Number(string text, out int value)
        {
            value = 0;
            if (text.Length == 0 || (text.Length > 1 && text[0] == '0')) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
        private static bool Identifiers(string text, bool numericBound, out string[] parts)
        {
            parts = text.Split('.');
            foreach (string part in parts)
            {
                if (part.Length == 0) return false;
                bool numeric = true;
                foreach (char c in part)
                {
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-')) return false;
                    if (c < '0' || c > '9') numeric = false;
                }
                if (numericBound && numeric && !Number(part, out _)) return false;
            }
            return true;
        }
        public int CompareTo(StrictVersion? other)
        {
            if (other == null) return 1;
            int result = major.CompareTo(other.major);
            if (result == 0) result = minor.CompareTo(other.minor);
            if (result == 0) result = patch.CompareTo(other.patch);
            if (result != 0) return result;
            if (prerelease.Length == 0 || other.prerelease.Length == 0) return prerelease.Length == other.prerelease.Length ? 0 : prerelease.Length == 0 ? 1 : -1;
            for (int i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
            {
                bool left = Number(prerelease[i], out int a);
                bool right = Number(other.prerelease[i], out int b);
                result = left && right ? a.CompareTo(b) : left != right ? left ? -1 : 1 : string.CompareOrdinal(prerelease[i], other.prerelease[i]);
                if (result != 0) return result;
            }
            return prerelease.Length.CompareTo(other.prerelease.Length);
        }
    }
}
