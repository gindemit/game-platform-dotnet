using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Configuration;
using GamePlatform.Configuration.Client;
using UnityEngine;
#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_WEBGL)
using System.Net;
using System.Net.Http;
#endif

namespace GamePlatform.Configuration.Unity
{
    /// <summary>Explicit host instance. Update drives marshaling and portable deadlines.</summary>
    [DisallowMultipleComponent]
    public sealed class UnityConfigurationHost : MonoBehaviour, IConfigurationTransport, IConfigurationStore, IConfigurationClock, IConfigurationRandom
    {
        private sealed class Pending
        {
            internal readonly TaskCompletionSource<string> Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal CancellationTokenRegistration Registration;
        }
        private sealed class Delay
        {
            internal long At;
            internal readonly TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal CancellationTokenRegistration Registration;
        }
        private readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>(StringComparer.Ordinal);
        private readonly List<Delay> delays = new List<Delay>();
        private readonly System.Random random = new System.Random();
        private readonly object sync = new object();
        private readonly string hostHandle = Guid.NewGuid().ToString("N");
        private string receiverName = string.Empty;
        private ConfigurationClientPolicy? policy;
#if !UNITY_WEBGL || UNITY_EDITOR
        private FileConfigurationStore? files;
#endif
        private long nextRequest;
        private long monotonic;
        private bool dead;
        public bool IsTrusted { get; private set; }
        public long UtcSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public long MonotonicSeconds
        {
            get
            {
                lock (sync)
                    return monotonic;
            }
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject? android;
#elif UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GPConfigInitialize(string handle, string normal, string control, int loopback);
        [DllImport("__Internal")] private static extern void GPConfigFetch(string handle, string address, string id, string target, int maximum, int immutable, string validator);
        [DllImport("__Internal")] private static extern void GPConfigStoreRead(string handle, string space, string sha, string id, string target, int maximum);
        [DllImport("__Internal")] private static extern void GPConfigStoreCommit(string handle, string space, string generation, string packed, string id, string target);
        [DllImport("__Internal")] private static extern void GPConfigCancel(string handle, string id);
        [DllImport("__Internal")] private static extern void GPConfigDispose(string handle);
#else
        private HttpClient? desktop;
#endif
        public void Initialize(ConfigurationClientPolicy clientPolicy, string nativeStoreRoot)
        {
            if (policy != null || dead)
                throw new InvalidOperationException("host_already_initialized");
            if (GetComponents<UnityConfigurationHost>().Length != 1 || GetComponents<MonoBehaviour>().Length != 1)
                throw new InvalidOperationException("dedicated_receiver_required");
            policy = clientPolicy ?? throw new ArgumentNullException(nameof(clientPolicy));
            // This explicitly owned receiver gets an independent local callback target.
            receiverName = "GPConfigurationHost_" + hostHandle;
            gameObject.name = receiverName;
            lock (sync)
                monotonic = (long)Time.realtimeSinceStartupAsDouble;
#if UNITY_ANDROID && !UNITY_EDITOR
            files = new FileConfigurationStore(nativeStoreRoot);
            android = new AndroidJavaObject("com.gindemit.configuration.ConfigurationHttp", policy.NormalRoot.AbsoluteUri, policy.ControlRoot.AbsoluteUri, policy.AllowLoopbackHttp);
#elif UNITY_WEBGL && !UNITY_EDITOR
            GPConfigInitialize(hostHandle, policy.NormalRoot.AbsoluteUri, policy.ControlRoot.AbsoluteUri, policy.AllowLoopbackHttp ? 1 : 0);
#else
            files = new FileConfigurationStore(nativeStoreRoot);
            desktop = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
#endif
        }
        public void SetTimeAnchorTrusted(bool trusted)
        {
            IsTrusted = trusted;
        }
        public double NextDouble()
        {
            lock (sync)
                return random.NextDouble();
        }
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (delay <= TimeSpan.Zero)
                return Task.CompletedTask;
            var waiting = new Delay { At = checked(MonotonicSeconds + (long)Math.Ceiling(delay.TotalSeconds)) };
            waiting.Registration = cancellationToken.Register(() => waiting.Completion.TrySetCanceled(cancellationToken));
            lock (sync)
            {
                if (dead)
                {
                    waiting.Registration.Dispose();
                    throw new ObjectDisposedException(nameof(UnityConfigurationHost));
                }
                delays.Add(waiting);
            }
            return waiting.Completion.Task;
        }
        private void Update()
        {
            lock (sync)
            {
                monotonic = (long)Time.realtimeSinceStartupAsDouble;
                for (int i = delays.Count - 1; i >= 0; i--)
                    if (delays[i].Completion.Task.IsCompleted || delays[i].At <= monotonic)
                    {
                        var waiting = delays[i];
                        delays.RemoveAt(i);
                        waiting.Completion.TrySetResult(true);
                        waiting.Registration.Dispose();
                    }
            }
            while (actions.TryDequeue(out var action))
                action();
        }
        private Task<string> Request(Action<string> dispatch, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string id;
            var request = new Pending();
            lock (sync)
            {
                if (dead || policy == null)
                    throw new InvalidOperationException("host_unavailable");
                id = checked(++nextRequest).ToString("x16", CultureInfo.InvariantCulture);
                pending.Add(id, request);
            }
            var registration = token.Register(() => { lock (sync) pending.Remove(id); request.Completion.TrySetCanceled(token); actions.Enqueue(() => Cancel(id)); });
            bool retained;
            lock (sync)
            {
                retained = !dead && pending.TryGetValue(id, out var current) && ReferenceEquals(current, request);
                if (retained)
                    request.Registration = registration;
            }
            if (!retained)
                registration.Dispose();
            actions.Enqueue(() => { if (request.Completion.Task.IsCompleted) return; try { if (gameObject.name != receiverName || GetComponents<UnityConfigurationHost>().Length != 1 || GetComponents<MonoBehaviour>().Length != 1) throw new InvalidOperationException("receiver_ownership_changed"); dispatch(id); } catch (Exception error) { lock (sync) pending.Remove(id); request.Registration.Dispose(); request.Completion.TrySetException(new IOException("host_transport_unavailable", error)); } });
            return request.Completion.Task;
        }
        private void Cancel(string id)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            android?.Call("cancel", id);
#elif UNITY_WEBGL && !UNITY_EDITOR
            GPConfigCancel(hostHandle, id);
#endif
        }
        private void Receive(string message)
        {
            if (message == null || message.Length > 1500000)
                return;
            int separator = message.IndexOf('|');
            if (separator != 16)
                return;
            string id = message.Substring(0, separator);
            Pending? request;
            lock (sync)
            {
                if (!pending.TryGetValue(id, out request))
                    return;
                pending.Remove(id);
            }
            request.Registration.Dispose();
            request.Completion.TrySetResult(message.Substring(separator + 1));
        }
        [UnityEngine.Scripting.Preserve] public void OnConfigurationHttp(string message) => Receive(message);
        [UnityEngine.Scripting.Preserve] public void OnConfigurationStore(string message) => Receive(message);
        private static byte[] Decode(string text, int maximum)
        {
            if (text.Length > ((maximum + 2) / 3) * 4)
                throw new InvalidConfigurationException("bridge_bounds");
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(text);
            }
            catch (FormatException) { throw new InvalidConfigurationException("bridge_invalid"); }
            if (bytes.Length > maximum)
                throw new InvalidConfigurationException("bridge_bounds");
            return bytes;
        }
        public async Task<ConfigurationHttpResponse> FetchAsync(ConfigurationHttpRequest request, CancellationToken cancellationToken)
        {
            if (policy == null)
                throw new InvalidOperationException("host_unavailable");
            bool allowed = false;
            foreach (bool control in new[] { false, true })
            {
                Uri root = control ? policy.ControlRoot : policy.NormalRoot;
                if (request.Address.GetLeftPart(UriPartial.Authority) == root.GetLeftPart(UriPartial.Authority) && request.Address.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal))
                {
                    string path = request.Address.AbsolutePath.Substring(root.AbsolutePath.Length);
                    if ((control && path == "control.json") || (!control && path != "control.json"))
                    {
                        try
                        {
                            allowed = policy.Resolve(path, control) == request.Address;
                        }
                        catch (InvalidConfigurationException) { }
                        if (allowed)
                            break;
                    }
                }
            }
            if (!allowed)
                throw new InvalidConfigurationException("origin_invalid");
#if UNITY_ANDROID && !UNITY_EDITOR
            string encoded = await Request(id => android!.Call("get", request.Address.AbsoluteUri, id, receiverName, request.MaximumDecodedBytes, request.ETag ?? string.Empty, request.Immutable), cancellationToken).ConfigureAwait(false);
#elif UNITY_WEBGL && !UNITY_EDITOR
            string encoded = await Request(id => GPConfigFetch(hostHandle, request.Address.AbsoluteUri, id, receiverName, request.MaximumDecodedBytes, request.Immutable ? 1 : 0, request.ETag ?? string.Empty), cancellationToken).ConfigureAwait(false);
#else
            using var message = new HttpRequestMessage(HttpMethod.Get, request.Address);
            if (request.ETag != null)
                message.Headers.TryAddWithoutValidation("If-None-Match", request.ETag);
            using var response = await desktop!.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                int count = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                    break;
                if (output.Length + count > request.MaximumDecodedBytes)
                    throw new InvalidConfigurationException("decoded_bounds");
                output.Write(buffer, 0, count);
            }
            return new ConfigurationHttpResponse((int)response.StatusCode, output.ToArray(), response.RequestMessage?.RequestUri ?? request.Address, response.Content.Headers.ContentType?.ToString() ?? string.Empty, response.Headers.ETag?.ToString(), response.Headers.RetryAfter?.Delta.HasValue == true ? (int)Math.Min(900, response.Headers.RetryAfter.Delta.Value.TotalSeconds) : null);
#endif
#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            string[] pieces = encoded.Split('|');
            if (pieces.Length != 5 || !int.TryParse(pieces[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int status))
                throw new IOException("bridge_invalid");
            if (status < 0)
                throw new IOException(status == -5 ? "transport_timeout" : "transport_unavailable");
            string etag = Encoding.UTF8.GetString(Decode(pieces[1], 4096));
            string mime = Encoding.UTF8.GetString(Decode(pieces[3], 4096));
            int? retry = pieces[2].Length == 0 ? null : int.Parse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture);
            if (retry < 0 || retry > 900)
                throw new IOException("bridge_invalid");
            return new ConfigurationHttpResponse(status, Decode(pieces[4], request.MaximumDecodedBytes), request.Address, mime, etag.Length == 0 ? null : etag, retry);
#endif
        }
        public async Task<StoreSnapshot?> LoadAsync(CacheNamespace space, CancellationToken cancellationToken)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            byte[]? metadata = await BrowserRead(space.Key, string.Empty, 1048576, cancellationToken).ConfigureAwait(false);
            if (metadata == null)
                return null;
            var value = CanonicalJson.Parse(metadata);
            return new StoreSnapshot(ProtocolReader.Sequence(value["storeGeneration"]), metadata);
#else
            return await files!.LoadAsync(space, cancellationToken).ConfigureAwait(false);
#endif
        }
        public async Task<byte[]?> ReadObjectAsync(CacheNamespace space, string representationSha, int maximumDecodedBytes, CancellationToken cancellationToken)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return await BrowserRead(space.Key, representationSha, maximumDecodedBytes, cancellationToken).ConfigureAwait(false);
#else
            return await files!.ReadObjectAsync(space, representationSha, maximumDecodedBytes, cancellationToken).ConfigureAwait(false);
#endif
        }
        public async Task<StoreCommitOutcome> CommitAsync(CacheNamespace space, long expectedGeneration, byte[] metadata, IReadOnlyDictionary<string, byte[]> objects, CancellationToken cancellationToken)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var packed = new StringBuilder(Convert.ToBase64String(metadata));
            foreach (var item in objects)
                packed.Append('~').Append(item.Key).Append(':').Append(Convert.ToBase64String(item.Value));
            string result = await Request(id => GPConfigStoreCommit(hostHandle, space.Key, expectedGeneration.ToString(CultureInfo.InvariantCulture), packed.ToString(), id, receiverName), cancellationToken).ConfigureAwait(false);
            int separator = result.IndexOf('|');
            if (separator < 1)
                return StoreCommitOutcome.Unknown;
            string status = result.Substring(0, separator);
            return status == "201" ? StoreCommitOutcome.Committed : status == "409" ? StoreCommitOutcome.Conflict : status == "507" ? StoreCommitOutcome.Quota : status == "202" ? StoreCommitOutcome.Unknown : StoreCommitOutcome.Unavailable;
#else
            return await files!.CommitAsync(space, expectedGeneration, metadata, objects, cancellationToken).ConfigureAwait(false);
#endif
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        private async Task<byte[]?> BrowserRead(string space, string sha, int maximum, CancellationToken token)
        {
            string result = await Request(id => GPConfigStoreRead(hostHandle, space, sha, id, receiverName, maximum), token).ConfigureAwait(false);
            int separator = result.IndexOf('|');
            if (separator < 0)
                throw new IOException("bridge_invalid");
            string status = result.Substring(0, separator);
            if (status == "204")
                return null;
            if (status != "200")
                throw new IOException("storage_unavailable");
            return Decode(result.Substring(separator + 1), maximum);
        }
#endif
        private void OnDestroy()
        {
            KeyValuePair<string, Pending>[] requests;
            Delay[] waiting;
            lock (sync)
            {
                dead = true;
                requests = new List<KeyValuePair<string, Pending>>(pending).ToArray();
                pending.Clear();
                waiting = delays.ToArray();
                delays.Clear();
            }
            foreach (var pair in requests)
            {
                pair.Value.Registration.Dispose();
                pair.Value.Completion.TrySetCanceled();
                Cancel(pair.Key);
            }
            foreach (var item in waiting)
            {
                item.Registration.Dispose();
                item.Completion.TrySetCanceled();
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            android?.Call("dispose");
            android?.Dispose();
            android = null;
#elif UNITY_WEBGL && !UNITY_EDITOR
            GPConfigDispose(hostHandle);
#elif !UNITY_WEBGL || UNITY_EDITOR
            desktop?.Dispose();
            desktop = null;
#endif
        }
    }
}
