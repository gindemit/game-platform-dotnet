using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>
    /// In-memory bounded scheduler. Time and eligibility are supplied per claim; resolution is always explicit.
    /// No prompt is opened or completed by this type.
    /// </summary>
    public sealed class PromptScheduler
    {
        private readonly object _gate = new object();
        private readonly List<QueueItem> _items = new List<QueueItem>();
        private readonly int _capacity;
        private long _nextQueueId;
        private long _nextClaimId;

        public PromptScheduler(int capacity = 32)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int PendingCount { get { lock (_gate) return _items.Count; } }

        public PromptEnqueueOutcome Enqueue(PromptRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            lock (_gate)
            {
                if (_items.Any(item => string.Equals(item.Request.Key, request.Key, StringComparison.Ordinal)))
                    return PromptEnqueueOutcome.Duplicate;
                if (_items.Count >= _capacity)
                    return PromptEnqueueOutcome.CapacityExceeded;
                _items.Add(new QueueItem(request, checked(++_nextQueueId)));
                return PromptEnqueueOutcome.Enqueued;
            }
        }

        /// <summary>
        /// Claims the highest-priority eligible request, breaking ties by insertion order. Revalidation runs
        /// outside the scheduler lock; deferral and expiry never count as completion.
        /// A claimed prompt retains exclusive presentation ownership past expiry until release or resolution.
        /// </summary>
        public bool TryClaimNext(DateTimeOffset now, PromptSchedulingContext context,
            Func<PromptRequest, PromptSchedulingContext, bool> revalidate, out PromptClaim? claim)
        {
            if (revalidate == null)
                throw new ArgumentNullException(nameof(revalidate));
            claim = null;
            RemoveExpired(now);
            if (context.DefersPrompts)
                return false;

            QueueItem[] candidates;
            lock (_gate)
            {
                if (_items.Any(item => item.IsClaimed))
                    return false;
                candidates = _items.Where(item => !item.IsClaimed)
                    .OrderByDescending(item => item.Request.Priority).ThenBy(item => item.QueueId).ToArray();
            }

            foreach (QueueItem candidate in candidates)
            {
                // Revalidation may query a feature owner and is intentionally outside the lock.
                if (!revalidate(candidate.Request, context))
                    continue;
                lock (_gate)
                {
                    if (_items.Any(item => item.IsClaimed))
                        return false;
                    QueueItem? current = _items.FirstOrDefault(item => item.QueueId == candidate.QueueId);
                    if (current == null || current.IsClaimed || current.Request.ExpiresAt <= now)
                        continue;
                    current.IsClaimed = true;
                    current.ClaimId = checked(++_nextClaimId);
                    claim = new PromptClaim(current.Request, current.QueueId, current.ClaimId);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Explicitly removes a prompt after its owning flow has handled it.</summary>
        public bool ResolveClaim(PromptClaim claim)
        {
            if (claim == null)
                throw new ArgumentNullException(nameof(claim));
            lock (_gate)
            {
                QueueItem? item = _items.FirstOrDefault(candidate => candidate.QueueId == claim.QueueId);
                if (item == null || !item.IsClaimed || item.ClaimId != claim.ClaimId)
                    return false;
                _items.Remove(item);
                return true;
            }
        }

        /// <summary>Releases a claim so it can be revalidated and claimed later.</summary>
        public bool ReleaseClaim(PromptClaim claim)
        {
            if (claim == null)
                throw new ArgumentNullException(nameof(claim));
            lock (_gate)
            {
                QueueItem? item = _items.FirstOrDefault(candidate => candidate.QueueId == claim.QueueId);
                if (item == null || !item.IsClaimed || item.ClaimId != claim.ClaimId)
                    return false;
                item.IsClaimed = false;
                item.ClaimId = 0;
                return true;
            }
        }

        private void RemoveExpired(DateTimeOffset now)
        {
            lock (_gate)
                _items.RemoveAll(item => !item.IsClaimed && item.Request.ExpiresAt <= now);
        }

        private sealed class QueueItem
        {
            public QueueItem(PromptRequest request, long queueId) { Request = request; QueueId = queueId; }
            public PromptRequest Request { get; }
            public long QueueId { get; }
            public bool IsClaimed { get; set; }
            public long ClaimId { get; set; }
        }
    }
}
