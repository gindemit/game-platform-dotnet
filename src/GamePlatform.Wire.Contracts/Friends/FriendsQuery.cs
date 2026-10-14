using System;
using System.Linq;

namespace GamePlatform.Wire.Contracts.Friends
{
    public sealed class FriendsQuery
    {
        public FriendsQuery(string view, string? publicCode = null, string? cursor = null, int pageSize = 30)
        {
            FriendsValidation.View(view);
            FriendsValidation.Cursor(cursor);
            if (pageSize < 1 || pageSize > 50 || (view == "lookup") != (publicCode != null) || (view == "lookup" || view == "mine") && cursor != null)
                throw new ArgumentException("Invalid friends query.");
            if (publicCode != null)
                FriendsValidation.Code(publicCode);
            View = view;
            PublicCode = publicCode;
            Cursor = cursor;
            PageSize = pageSize;
        }
        public string View { get; }
        public string? PublicCode { get; }
        public string? Cursor { get; }
        public int PageSize { get; }
    }
}

