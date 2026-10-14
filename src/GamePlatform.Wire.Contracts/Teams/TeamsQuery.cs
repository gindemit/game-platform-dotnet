using System;
using System.Linq;
namespace GamePlatform.Wire.Contracts.Teams
{
    public sealed class TeamsQuery
    {
        public TeamsQuery(string view, Guid? teamId = null, string? search = null, string? cursor = null, int pageSize = 30)
        {
            if (!new[] { "discover", "mine", "team", "members", "messages", "requests", "invites", "help" }.Contains(view))
                throw new ArgumentOutOfRangeException(nameof(view));
            if (teamId == Guid.Empty || pageSize < 1 || pageSize > 50 || cursor?.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            if (search != null && !ValidSearch(search))
                throw new ArgumentOutOfRangeException(nameof(search));
            View = view;
            TeamId = teamId;
            Search = search;
            Cursor = cursor;
            PageSize = pageSize;
        }
        public string View { get; }
        public Guid? TeamId { get; }
        public string? Search { get; }
        public string? Cursor { get; }
        public int PageSize { get; }
        private static bool ValidSearch(string value)
        {
            int scalars = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current <= '\u001f' || current == '\u007f')
                    return false;
                if (char.IsHighSurrogate(current))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i]))
                        return false;
                }
                else if (char.IsLowSurrogate(current))
                    return false;
                if (++scalars > 64)
                    return false;
            }
            return true;
        }
    }
}
