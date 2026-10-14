using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace GamePlatform.Configuration.Client.Tests
{
    public sealed class SynchronizationContextTests
    {
        private const string Pin = "0199e584-6400-7000-8000-000000000021";
        private readonly ITestOutputHelper output;

        public SynchronizationContextTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public void Incomplete_host_callbacks_keep_cold_refresh_activation_pin_control_and_lifecycle_on_the_pump()
        {
            using var pump = new SingleThreadPump();
            var fixture = new Fixture();
            var clock = new PumpClock(pump);
            var transport = new PumpTransport(pump, fixture);
            transport.RetryCurrentOnce = true;
            PumpTransport? resumedTransport = null;
            var store = new PumpStore(pump);
            using var client = CreateClient(fixture, transport, store, clock);

            pump.Run(async () =>
            {
                var activated = await client.GetFeatureAsync("progression");
                Assert.Equal("available", activated.Status);
                Assert.NotNull(activated.Feature);
                Assert.True(activated.Durable);

                var pinned = await client.PinAsync(activated.Feature!, Pin);
                Assert.Equal("available", pinned.Status);
                Assert.True(pinned.Durable);
                var restored = await client.GetPinnedAsync(Pin);
                Assert.Equal("available", restored.Status);
                Assert.Same(activated.Feature, restored.Feature);
                client.Dispose();

                resumedTransport = new PumpTransport(pump, fixture);
                using var resumed = CreateClient(fixture, resumedTransport, store, clock);
                var restore = await resumed.RestoreAsync();
                Assert.Equal("restored", restore.Reason);
                Assert.True(restore.Durable);
                var resumedPin = await resumed.GetPinnedAsync(Pin);
                Assert.Equal("available", resumedPin.Status);
                Assert.NotNull(resumedPin.Feature);
                Assert.Equal(activated.Feature!.Selection.Choice.Variant, resumedPin.Feature!.Selection.Choice.Variant);
                Assert.True(resumedPin.Durable);
                Assert.True(await resumed.ReleasePinAsync(Pin));

                var controls = await resumed.RefreshControlsAsync();
                Assert.Equal("available", controls.Status);
                Assert.True(resumed.Restrictions.Durable);
                Assert.True(resumed.Restrictions.Sequence > 0);

                resumed.Retire();
                await Assert.ThrowsAsync<InvalidConfigurationException>(() => resumed.RefreshAsync());
            });

            Assert.True(transport.Requests.Count >= 3);
            Assert.Contains("current.json", transport.Requests);
            Assert.Contains("control.json", resumedTransport!.Requests);
            Assert.Contains("store.load", store.Calls);
            Assert.Contains("store.read", store.Calls);
            Assert.Contains("store.commit", store.Calls);
            Assert.Contains("clock.delay", clock.Calls);
            Assert.Contains("clock.delay.release", clock.Calls);
            pump.AssertAllVisitsStayedOnPump(output);
        }

        [Fact]
        public void Deadline_expiry_cancels_an_incomplete_transport_without_leaving_the_host_context()
        {
            using var pump = new SingleThreadPump();
            var fixture = new Fixture();
            var clock = new PumpClock(pump);
            var transport = new PumpTransport(pump, fixture)
            {
                HoldFirstRequest = true,
                AfterFirstRequest = () => pump.Post(_ => clock.ExpireWaveDeadline(), null)
            };
            using var client = CreateClient(fixture, transport, new PumpStore(pump), clock);

            var result = pump.Run(() => client.GetFeatureAsync("progression"));

            Assert.Equal("bundled", result.Status);
            Assert.Equal("cancelled", result.Reason);
            Assert.Single(transport.Requests);
            Assert.Contains("clock.delay", clock.Calls);
            Assert.True(clock.WaveExpired);
            pump.AssertAllVisitsStayedOnPump(output);
        }

        [Fact]
        public void Caller_cancellation_is_distinct_from_deadline_and_preserves_pump_affinity()
        {
            using var pump = new SingleThreadPump();
            var fixture = new Fixture();
            var clock = new PumpClock(pump);
            using var caller = new CancellationTokenSource();
            var transport = new PumpTransport(pump, fixture)
            {
                HoldFirstRequest = true,
                AfterFirstRequest = caller.Cancel
            };
            var store = new PumpStore(pump);
            using var client = CreateClient(fixture, transport, store, clock);

            var result = pump.Run(() => client.GetFeatureAsync("progression", cancellationToken: caller.Token));

            Assert.Equal("bundled", result.Status);
            Assert.Equal("cancelled", result.Reason);
            Assert.Single(transport.Requests);
            Assert.True(caller.IsCancellationRequested);
            Assert.False(clock.WaveExpired);
            Assert.DoesNotContain("store.commit", store.Calls);
            pump.AssertAllVisitsStayedOnPump(output);
        }

        private static ConfigurationClient CreateClient(Fixture fixture, IConfigurationTransport transport, IConfigurationStore store, PumpClock clock)
        {
            var origin = new Uri("http://127.0.0.1:1/");
            return new ConfigurationClient(fixture.Scope, fixture.Owner,
                new ConfigurationClientPolicy(origin, origin, true), transport, store, clock, clock,
                fixture.Bundled, fixture.ControlMemory);
        }

        private sealed class SingleThreadPump : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new BlockingCollection<(SendOrPostCallback, object?)>();
            private readonly ConcurrentQueue<Visit> visits = new ConcurrentQueue<Visit>();
            private bool disposed;
            internal int ThreadId { get; private set; }

            public override void Post(SendOrPostCallback callback, object? state) => queue.Add((callback, state));

            internal void Record(string name)
            {
                visits.Enqueue(new Visit(name, Thread.CurrentThread.ManagedThreadId, Current));
            }

            internal Task<T> CompleteLater<T>(string name, Func<T> value, CancellationToken token = default)
            {
                Record(name);
                var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationTokenRegistration registration = default;
                if (token.CanBeCanceled)
                    registration = token.Register(() => completion.TrySetCanceled(token));
                Post(_ =>
                {
                    try
                    {
                        if (token.IsCancellationRequested)
                            completion.TrySetCanceled(token);
                        else
                            completion.TrySetResult(value());
                    }
                    catch (Exception error)
                    {
                        completion.TrySetException(error);
                    }
                    finally
                    {
                        registration.Dispose();
                    }
                }, null);
                return completion.Task;
            }

            internal void AssertAllVisitsStayedOnPump(ITestOutputHelper output)
            {
                var observed = visits.ToArray();
                Assert.NotEmpty(observed);
                foreach (var visit in observed)
                    output.WriteLine($"host_callback={visit.Name} thread={visit.ThreadId} context={visit.Context?.GetType().Name ?? "<null>"}");
                var violations = observed.Where(visit => visit.ThreadId != ThreadId || !ReferenceEquals(this, visit.Context))
                    .Select(visit => $"{visit.Name}: thread={visit.ThreadId}, context={visit.Context?.GetType().Name ?? "<null>"}")
                    .ToArray();
                Assert.True(violations.Length == 0, "Host callbacks left the captured pump:\n" + string.Join("\n", violations));
            }

            internal T Run<T>(Func<Task<T>> operation)
            {
                if (ThreadId != 0)
                    throw new InvalidOperationException("pump_already_used");
                ThreadId = Thread.CurrentThread.ManagedThreadId;
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    Task<T> task = operation();
                    while (!task.IsCompleted)
                    {
                        if (!queue.TryTake(out var work, TimeSpan.FromSeconds(10)))
                            throw new TimeoutException("pump_waited_without_a_host_callback");
                        work.Callback(work.State);
                    }
                    while (queue.TryTake(out var remaining))
                        remaining.Callback(remaining.State);
                    return task.GetAwaiter().GetResult();
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            }

            internal void Run(Func<Task> operation) => Run(async () => { await operation(); return true; });

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                queue.CompleteAdding();
                queue.Dispose();
            }

            private sealed class Visit
            {
                internal string Name { get; }
                internal int ThreadId { get; }
                internal SynchronizationContext? Context { get; }
                internal Visit(string name, int threadId, SynchronizationContext? context)
                {
                    Name = name;
                    ThreadId = threadId;
                    Context = context;
                }
            }
        }

        private sealed class PumpClock : IConfigurationClock, IConfigurationRandom
        {
            private readonly SingleThreadPump pump;
            private readonly ConcurrentQueue<string> calls = new ConcurrentQueue<string>();
            private readonly ConcurrentBag<TaskCompletionSource<bool>> waveDelays = new ConcurrentBag<TaskCompletionSource<bool>>();
            internal IReadOnlyCollection<string> Calls => calls.ToArray();
            internal bool WaveExpired { get; private set; }
            public long UtcSeconds { get { Visit("clock.utc"); return 1791972002; } }
            public long MonotonicSeconds { get { Visit("clock.monotonic"); return 1; } }
            public bool IsTrusted { get { Visit("clock.trusted"); return true; } }

            internal PumpClock(SingleThreadPump pump) => this.pump = pump;

            public double NextDouble()
            {
                Visit("clock.random");
                return .5;
            }

            public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            {
                Visit("clock.delay");
                if (delay.TotalSeconds >= 19)
                {
                    var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    waveDelays.Add(pending);
                    var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                    return AwaitAndDispose(pending.Task, registration);
                }
                if (delay.TotalSeconds < 4.9)
                {
                    calls.Enqueue("clock.delay.release");
                    return pump.CompleteLater("clock.delay.release", () => true, cancellationToken);
                }
                var localDeadline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var localRegistration = cancellationToken.Register(() => localDeadline.TrySetCanceled(cancellationToken));
                return AwaitAndDispose(localDeadline.Task, localRegistration);
            }

            internal void ExpireWaveDeadline()
            {
                WaveExpired = true;
                foreach (var pending in waveDelays)
                    pending.TrySetResult(true);
            }

            private static async Task AwaitAndDispose(Task<bool> task, CancellationTokenRegistration registration)
            {
                try
                {
                    await task;
                }
                finally
                {
                    registration.Dispose();
                }
            }

            private void Visit(string name)
            {
                calls.Enqueue(name);
                pump.Record(name);
            }
        }

        private sealed class PumpTransport : IConfigurationTransport
        {
            private readonly SingleThreadPump pump;
            private readonly Fixture fixture;
            private bool first = true;
            internal readonly ConcurrentQueue<string> Requests = new ConcurrentQueue<string>();
            internal bool HoldFirstRequest;
            internal bool RetryCurrentOnce;
            internal Action? AfterFirstRequest;
            private bool retriedCurrent;

            internal PumpTransport(SingleThreadPump pump, Fixture fixture)
            {
                this.pump = pump;
                this.fixture = fixture;
            }

            public Task<ConfigurationHttpResponse> FetchAsync(ConfigurationHttpRequest request, CancellationToken cancellationToken)
            {
                string path = request.Address.AbsolutePath.TrimStart('/');
                Requests.Enqueue(path);
                pump.Record("transport." + path);
                bool isFirst = first;
                first = false;
                if (isFirst)
                    AfterFirstRequest?.Invoke();
                if (isFirst && HoldFirstRequest)
                {
                    var pending = new TaskCompletionSource<ConfigurationHttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                    return AwaitAndDispose(pending.Task, registration);
                }
                return pump.CompleteLater("transport.release." + path, () =>
                {
                    if (path == "current.json" && RetryCurrentOnce && !retriedCurrent)
                    {
                        retriedCurrent = true;
                        return new ConfigurationHttpResponse(503, Array.Empty<byte>(), request.Address, retryAfterSeconds: 1);
                    }
                    return new ConfigurationHttpResponse(fixture.Documents.TryGetValue(path, out var bytes) ? 200 : 404,
                        bytes ?? Array.Empty<byte>(), request.Address, etag: "\"pump-" + path.Replace('/', '-') + "\"");
                }, cancellationToken);
            }

            private static async Task<T> AwaitAndDispose<T>(Task<T> task, CancellationTokenRegistration registration)
            {
                try
                {
                    return await task;
                }
                finally
                {
                    registration.Dispose();
                }
            }
        }

        private sealed class PumpStore : IConfigurationStore
        {
            private readonly SingleThreadPump pump;
            private readonly Dictionary<string, StoreSnapshot> snapshots = new Dictionary<string, StoreSnapshot>(StringComparer.Ordinal);
            private readonly Dictionary<string, Dictionary<string, byte[]>> objects = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
            private readonly ConcurrentQueue<string> calls = new ConcurrentQueue<string>();
            internal IReadOnlyCollection<string> Calls => calls.ToArray();
            internal PumpStore(SingleThreadPump pump) => this.pump = pump;

            public Task<StoreSnapshot?> LoadAsync(CacheNamespace space, CancellationToken cancellationToken)
                => Later("store.load", () => snapshots.TryGetValue(space.Key, out var snapshot) ? new StoreSnapshot(snapshot.Generation, snapshot.Metadata) : null, cancellationToken);

            public Task<byte[]?> ReadObjectAsync(CacheNamespace space, string representationSha, int maximumDecodedBytes, CancellationToken cancellationToken)
                => Later("store.read", () => objects.TryGetValue(space.Key, out var items) && items.TryGetValue(representationSha, out var bytes) ? (byte[]?)bytes.ToArray() : null, cancellationToken);

            public Task<StoreCommitOutcome> CommitAsync(CacheNamespace space, long expectedGeneration, byte[] metadata, IReadOnlyDictionary<string, byte[]> added, CancellationToken cancellationToken)
                => Later("store.commit", () =>
                {
                    long current = snapshots.TryGetValue(space.Key, out var snapshot) ? snapshot.Generation : 0;
                    if (current != expectedGeneration)
                        return StoreCommitOutcome.Conflict;
                    snapshots[space.Key] = new StoreSnapshot(current + 1, metadata);
                    if (!objects.TryGetValue(space.Key, out var items))
                        objects.Add(space.Key, items = new Dictionary<string, byte[]>(StringComparer.Ordinal));
                    foreach (var pair in added)
                        items[pair.Key] = pair.Value.ToArray();
                    return StoreCommitOutcome.Committed;
                }, cancellationToken);

            private Task<T> Later<T>(string name, Func<T> value, CancellationToken token)
            {
                calls.Enqueue(name);
                return pump.CompleteLater(name, value, token);
            }
        }
    }
}
