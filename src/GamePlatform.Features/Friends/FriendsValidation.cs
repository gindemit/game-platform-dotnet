using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
namespace GamePlatform.Features.Friends
{
    internal static class FriendsValidation
    {
        internal static readonly string[] Views = { "mine", "friends", "incoming", "outgoing", "blocked", "suggestions", "lookup" };
        internal static readonly string[] Actions = { "send", "accept", "decline", "cancel", "remove", "block", "unblock", "set_privacy" };
        internal static void View(string value)
        {
            if (!Views.Contains(value))
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Id(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Valid ID required.");
        }
        internal static void Operation(Guid value)
        {
            Id(value);
            if (value.ToString("D")[14] != '7')
                throw new ArgumentException("UUIDv7 required.");
        }
        internal static void Time(long value)
        {
            if (value < 0 || value > 253402300799999L)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Code(string value)
        {
            if (value == null || !Regex.IsMatch(value, "^[0-9ABCDEFGHJKMNPQRSTVWXYZ]{12}$"))
                throw new ArgumentException("Invalid public code.");
        }
        internal static void Cursor(string? value)
        {
            if (value != null && (value.Length < 16 || value.Length > 4096 || !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$")))
                throw new ArgumentException("Invalid cursor.");
        }
        internal static void Text(string value, int max, bool empty = false)
        {
            if (value == null || !empty && value.Length == 0)
                throw new ArgumentException("Invalid text.");
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c))
                    throw new ArgumentException("Invalid text.");
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i]))
                        throw new ArgumentException("Invalid text.");
                }
                else if (char.IsLowSurrogate(c))
                    throw new ArgumentException("Invalid text.");
                if (++count > max)
                    throw new ArgumentException("Invalid text.");
            }
        }
    }
}
