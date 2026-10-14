using System;
using System.Text.RegularExpressions;

namespace GamePlatform.Features.Progression
{

    internal static class ProgressionValidation
    {
        private static readonly Regex Source = new Regex("^[^\\r\\n\\0]{1,256}$", RegexOptions.CultureInvariant);
        internal static void BusinessSource(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || !Source.IsMatch(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
        private static readonly Regex SemanticPattern = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant);
        internal static void Semantic(string value, string parameter)
        {
            if (value == null || !SemanticPattern.IsMatch(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
        internal static void Timestamp(long value, string parameter)
        {
            if (value < 0 || value > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
