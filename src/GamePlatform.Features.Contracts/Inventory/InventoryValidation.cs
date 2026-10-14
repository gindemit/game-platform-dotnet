using System;
using System.Text.RegularExpressions;

namespace GamePlatform.Features.Contracts.Inventory
{

    internal static class InventoryValidation
    {
        private static readonly Regex Source = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);
        internal static void BusinessSource(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || !Source.IsMatch(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
