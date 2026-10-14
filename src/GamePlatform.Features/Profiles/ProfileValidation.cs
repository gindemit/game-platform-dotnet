using System;
using System.Text;
using System.Text.RegularExpressions;

namespace GamePlatform.Features.Profiles
{

    internal static class ProfileValidation
    {
        internal const int MaximumExtensionBytes = 16_384;
        private static readonly Regex SemanticKey = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant);
        private static readonly Regex LocalePattern = new Regex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant);

        internal static void DisplayName(string? value, string parameter)
        {
            if (value == null || value.Length == 0 || !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                !string.Equals(value, value.Normalize(NormalizationForm.FormC), StringComparison.Ordinal) || ScalarCount(value) > 64)
                throw new ArgumentOutOfRangeException(parameter);
            ValidateUnicode(value, parameter);
        }
        internal static void AvatarKey(string? value, string parameter)
        {
            if (value != null && !SemanticKey.IsMatch(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
        internal static void Locale(string? value, string parameter)
        {
            if (value == null || !LocalePattern.IsMatch(value) || ScalarCount(value) > 35)
                throw new ArgumentOutOfRangeException(parameter);
            ValidateUnicode(value, parameter);
        }
        internal static void Timestamp(long value, string parameter)
        {
            if (value < 0 || value > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(parameter);
        }
        internal static void BusinessSource(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(parameter);
        }
        private static int ScalarCount(string value)
        {
            var count = 0;
            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                        return int.MaxValue;
                    index++;
                }
                else if (char.IsLowSurrogate(value[index]))
                    return int.MaxValue;
                count++;
            }
            return count;
        }
        private static void ValidateUnicode(string value, string parameter)
        {
            if (ScalarCount(value) == int.MaxValue || Encoding.UTF8.GetByteCount(value) > 8192)
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
