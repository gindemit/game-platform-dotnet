using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Features.Purchases
{
    public sealed class NativeProtectedPage<T>
    {
        public NativeProtectedPage(IReadOnlyList<T> items, string? nextCursor, long highWatermark, long afterSequence, long lastSequence)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (items.Count > 32 || items.Any(item => item == null) || highWatermark < 0 || afterSequence < 0 ||
                afterSequence > highWatermark || lastSequence < afterSequence || lastSequence > highWatermark ||
                (items.Count > 0 && lastSequence <= afterSequence) ||
                (nextCursor == null && lastSequence != highWatermark) ||
                (nextCursor != null && (nextCursor.Length < 1 || nextCursor.Length > 4096 || lastSequence <= afterSequence || lastSequence >= highWatermark)))
                throw new ArgumentException("Invalid protected-store page.");
            Items = Array.AsReadOnly(items.ToArray());
            NextCursor = nextCursor;
            HighWatermark = highWatermark;
            AfterSequence = afterSequence;
            LastSequence = lastSequence;
        }
        public IReadOnlyList<T> Items { get; }
        public string? NextCursor { get; }
        public long HighWatermark { get; }
        public long AfterSequence { get; }
        public long LastSequence { get; }
    }
}
