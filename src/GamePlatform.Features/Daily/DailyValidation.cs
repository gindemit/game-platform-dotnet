using System;
using System.Globalization;
namespace GamePlatform.Features.Daily
{
    internal static class DailyValidation
    {
        internal static string UtcDate(string value, string name)
        {
            if (value == null)
                throw new ArgumentNullException(name);
            if (value.Length != 10 || !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw new ArgumentException("A yyyy-MM-dd UTC date is required.", name);
            return value;
        }
        internal static int CycleDay(int value, string name) => value >= 1 && value <= 7 ? value : throw new ArgumentOutOfRangeException(name);
        internal static int PolicyVersion(int value, string name) => value >= 1 ? value : throw new ArgumentOutOfRangeException(name);
        internal static string Key(string value, string name) =>
            string.IsNullOrEmpty(value) || value.Length > 128 ? throw new ArgumentException("A semantic key is required.", name) : value;
    }
}
