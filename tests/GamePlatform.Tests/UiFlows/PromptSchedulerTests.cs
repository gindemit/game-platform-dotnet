using System;
using GamePlatform.Navigation.Kernel.Flows;

namespace GamePlatform.Tests.UiFlows
{
    public sealed class PromptSchedulerTests
    {
        [Fact]
        public void HigherPriorityWinsAndClaimMustBeExplicitlyResolvedOrReleased()
        {
            var scheduler = new PromptScheduler();
            DateTimeOffset now = DateTimeOffset.UnixEpoch;
            Assert.Equal(PromptEnqueueOutcome.Enqueued, scheduler.Enqueue(new PromptRequest("daily", 2, now.AddHours(1))));
            Assert.Equal(PromptEnqueueOutcome.Enqueued, scheduler.Enqueue(new PromptRequest("tutorial", 7, now.AddHours(1))));

            Assert.True(scheduler.TryClaimNext(now, default, (_, _) => true, out PromptClaim? first));
            Assert.Equal("tutorial", first!.Request.Key);
            Assert.False(scheduler.TryClaimNext(now, default, (_, _) => true, out _));
            Assert.True(scheduler.ReleaseClaim(first));
            Assert.True(scheduler.TryClaimNext(now, default, (_, _) => true, out PromptClaim? again));
            Assert.Equal("tutorial", again!.Request.Key);
            Assert.True(scheduler.ResolveClaim(again));
            Assert.False(scheduler.ResolveClaim(again));
            Assert.True(scheduler.TryClaimNext(now, default, (_, _) => true, out PromptClaim? next));
            Assert.Equal("daily", next!.Request.Key);
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void ManualTutorialAndRecoveryContextsDeferPrompts(bool manual, bool tutorial, bool recovery)
        {
            var scheduler = new PromptScheduler();
            DateTimeOffset now = DateTimeOffset.UnixEpoch;
            scheduler.Enqueue(new PromptRequest("prompt", 1, now.AddHours(1)));
            var context = new PromptSchedulingContext(manual, tutorial, recovery);

            Assert.False(scheduler.TryClaimNext(now, context, (_, _) => true, out _));
            Assert.Equal(1, scheduler.PendingCount);
        }

        [Fact]
        public void ExpiredItemsAreRemovedAndIneligibleHigherPriorityDoesNotBlockLowerPriority()
        {
            var scheduler = new PromptScheduler();
            DateTimeOffset now = DateTimeOffset.UnixEpoch;
            scheduler.Enqueue(new PromptRequest("expired", 100, now));
            scheduler.Enqueue(new PromptRequest("stale", 10, now.AddHours(1)));
            scheduler.Enqueue(new PromptRequest("eligible", 1, now.AddHours(1)));

            Assert.True(scheduler.TryClaimNext(now, default,
                (request, _) => request.Key == "eligible", out PromptClaim? claim));
            Assert.Equal("eligible", claim!.Request.Key);
            Assert.Equal(2, scheduler.PendingCount);
        }

        [Fact]
        public void DuplicateKeysAndCapacityAreBoundedWithoutReplacingExistingRequest()
        {
            var scheduler = new PromptScheduler(capacity: 1);
            DateTimeOffset now = DateTimeOffset.UnixEpoch;
            var original = new PromptRequest("same", 1, now.AddHours(1));
            Assert.Equal(PromptEnqueueOutcome.Enqueued, scheduler.Enqueue(original));
            Assert.Equal(PromptEnqueueOutcome.Duplicate, scheduler.Enqueue(new PromptRequest("same", 99, now.AddDays(1))));
            Assert.Equal(PromptEnqueueOutcome.CapacityExceeded, scheduler.Enqueue(new PromptRequest("other", 1, now.AddHours(1))));
            Assert.True(scheduler.TryClaimNext(now, default, (_, _) => true, out PromptClaim? claim));
            Assert.Equal(original.Priority, claim!.Request.Priority);
        }

        [Fact]
        public void RevalidationRunsOnEveryClaimAttempt()
        {
            var scheduler = new PromptScheduler();
            DateTimeOffset now = DateTimeOffset.UnixEpoch;
            scheduler.Enqueue(new PromptRequest("prompt", 1, now.AddHours(1)));
            int checks = 0;

            Assert.False(scheduler.TryClaimNext(now, default, (_, _) => ++checks == 2, out _));
            Assert.True(scheduler.TryClaimNext(now, default, (_, _) => ++checks == 2, out _));
            Assert.Equal(2, checks);
        }
    }
}
