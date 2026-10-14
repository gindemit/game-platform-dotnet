using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Configuration;
using GamePlatform.Configuration.Client;

namespace GamePlatform.Configuration.Client.Tests
{
    internal sealed class TestClock : IConfigurationClock, IConfigurationRandom
    {
        public long UtcSeconds { get; set; } = 1791972002;
        public long MonotonicSeconds { get; set; } = 1;
        public bool IsTrusted { get; set; } = true;
        private readonly List<(long At, TaskCompletionSource<bool> Ready)> waits = new List<(long, TaskCompletionSource<bool>)>();
        internal readonly TaskCompletionSource<bool> ShortDelayEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public double NextDouble() => .5;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (delay.TotalSeconds <= 0)
                return Task.CompletedTask;
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (waits)
                waits.Add((MonotonicSeconds + (long)delay.TotalSeconds, ready));
            if (delay.TotalSeconds < 5)
                ShortDelayEntered.TrySetResult(true);
            return Wait(ready, cancellationToken);
        }
        private static async Task Wait(TaskCompletionSource<bool> ready, CancellationToken token)
        {
            using var registration = token.Register(() => ready.TrySetCanceled(token));
            await ready.Task;
        }
        public void Advance(long seconds)
        {
            UtcSeconds += seconds;
            MonotonicSeconds += seconds;
            lock (waits)
            {
                foreach (var wait in waits)
                    if (wait.At <= MonotonicSeconds)
                        wait.Ready.TrySetResult(true);
                waits.RemoveAll(wait => wait.Ready.Task.IsCompleted);
            }
        }
    }
    internal sealed class OwnedDirectory : IDisposable
    {
        private readonly string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "owned-client-tests"));
        internal string Path { get; }
        internal OwnedDirectory()
        {
            Path = System.IO.Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            if (!System.IO.Path.GetFullPath(Path).StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("test_cleanup_escape");
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
    internal sealed class FileFaults : IFileConfigurationFaults
    {
        internal int Remaining;
        internal string Boundary = "before_commit";
        internal Action<string>? Observe;
        public void At(string boundary)
        {
            Observe?.Invoke(boundary);
            if (boundary == Boundary && Remaining > 0)
            {
                Remaining--;
                throw new IOException("injected_owned_file_failure");
            }
        }
    }
    internal sealed class Fixture
    {
        internal JsonValue Corpus { get; } = CanonicalJson.Parse(File.ReadAllBytes(System.IO.Path.Combine(AppContext.BaseDirectory, "corpus/service-vectors-v1.json")));
        internal ConfigurationScope Scope { get; } = new ConfigurationScope("synthetic", "local");
        internal ConfigurationOwner Owner { get; }
        internal Dictionary<string, byte[]> Documents { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        internal CompiledConfiguration Bundled { get; }
        internal ConfigurationControlMemory ControlMemory { get; }
        internal Fixture()
        {
            ControlMemory = new ConfigurationControlMemory(Scope);
            Owner = new ConfigurationOwner("synthetic-owner", Corpus["fixtureContext"]["subjectId"].AsString(), 1);
            JsonValue current = Corpus["examples"]["current"], manifest = Corpus["examples"]["manifest"], selector = Corpus["fixtureDocuments"]["selector"];
            Documents["current.json"] = Encoding.UTF8.GetBytes(Corpus["fixtureRawObservations"]["pointer"].AsString());
            Documents["control.json"] = Encoding.UTF8.GetBytes(Corpus["fixtureRawObservations"]["control"].AsString());
            Documents[manifest["selector"]["path"].AsString()] = CanonicalJson.Serialize(selector);
            Documents[current["manifest"]["path"].AsString()] = CanonicalJson.Serialize(manifest);
            foreach (var pair in Corpus["fixtureDocuments"]["assets"].AsObject())
            {
                var asset = pair.Value;
                string path = "assets/" + CanonicalJson.SemanticSha256(JsonValue.Array(new[] { asset["key"], asset["revision"] })) + ".json";
                Documents[path] = CanonicalJson.Serialize(asset);
            }
            Bundled = ConfigurationReader.Read(CanonicalJson.Serialize(Corpus["fixtureDocuments"]["nonexperimentSelector"]), Scope.App, Scope.Environment);
        }
        internal static JsonValue Set(JsonValue value, string key, JsonValue replacement)
        {
            var values = value.AsObject().Select(p => new KeyValuePair<string, JsonValue>(p.Key, p.Key == key ? replacement : p.Value));
            return JsonValue.Object(values);
        }
        internal static JsonValue Object(params (string Key, JsonValue Value)[] fields) => JsonValue.Object(fields.Select(v => new KeyValuePair<string, JsonValue>(v.Key, v.Value)));
        internal JsonValue Example(string name) => Corpus["examples"][name];
        internal byte[] Metadata(string name, long generation) => CanonicalJson.Serialize(Set(Example(name), "storeGeneration", JsonValue.String(generation.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        internal Dictionary<string, byte[]> Objects(string name)
        {
            var all = Documents.Values.ToDictionary(CanonicalJson.Sha256, b => b, StringComparer.Ordinal);
            var result = new Dictionary<string, byte[]>();
            foreach (var item in Example(name)["objects"].AsArray())
                result[item["representationSha"].AsString()] = all[item["representationSha"].AsString()];
            return result;
        }
        internal void Control(long sequence, bool deny)
        {
            var value = Example("control");
            value = Set(value, "sequence", JsonValue.String(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            value = Set(value, "restrictions", deny ? value["restrictions"] : JsonValue.Array(Array.Empty<JsonValue>()));
            Documents["control.json"] = CanonicalJson.Serialize(value);
        }
        internal void AdmissionControl(long sequence, string key)
        {
            var value = Set(Example("control"), "sequence", JsonValue.String(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var restriction = Object(("key", JsonValue.String(key)), ("effect", JsonValue.String("deny")), ("ratePermille", JsonValue.Null));
            value = Set(value, "restrictions", JsonValue.Array(new[] { restriction }));
            Documents["control.json"] = CanonicalJson.Serialize(value);
        }
        internal void AdvanceRelease()
        {
            var selector = Corpus["fixtureDocuments"]["nonexperimentSelector"];
            selector = Set(selector, "release", JsonValue.String("release-8"));
            byte[] selectorBytes = CanonicalJson.Serialize(selector);
            string selectorSha = CanonicalJson.Sha256(selectorBytes);
            var selectorRef = Object(("path", JsonValue.String("selectors/" + selectorSha + ".json")), ("representationSha", JsonValue.String(selectorSha)), ("semanticSha", JsonValue.String(selectorSha)), ("decodedBytes", JsonValue.Integer(selectorBytes.Length)));
            var manifest = Set(Set(Example("manifest"), "release", JsonValue.String("release-8")), "selector", selectorRef);
            byte[] manifestBytes = CanonicalJson.Serialize(manifest);
            string manifestSha = CanonicalJson.Sha256(manifestBytes);
            var manifestRef = Object(("path", JsonValue.String("manifests/" + manifestSha + ".json")), ("representationSha", JsonValue.String(manifestSha)), ("semanticSha", JsonValue.String(manifestSha)), ("decodedBytes", JsonValue.Integer(manifestBytes.Length)));
            var pointer = Set(Set(Set(Example("current"), "sequence", JsonValue.String("8")), "release", JsonValue.String("release-8")), "manifest", manifestRef);
            Documents[selectorRef["path"].AsString()] = selectorBytes;
            Documents[manifestRef["path"].AsString()] = manifestBytes;
            Documents["current.json"] = CanonicalJson.Serialize(pointer);
        }
        internal ConfigurationClient Client(IConfigurationTransport transport, IConfigurationStore store, TestClock clock, ConfigurationOwner? owner = null, Uri? root = null)
        {
            var origin = root ?? new Uri("http://127.0.0.1:1/");
            return new ConfigurationClient(Scope, owner ?? Owner, new ConfigurationClientPolicy(origin, origin, true), transport, store, clock, clock, Bundled, ControlMemory);
        }
    }
    internal sealed class FixtureTransport : IConfigurationTransport
    {
        internal readonly Fixture Fixture;
        internal readonly List<string> Requests = new List<string>();
        internal Func<ConfigurationHttpRequest, CancellationToken, Task<ConfigurationHttpResponse>>? Override;
        internal FixtureTransport(Fixture fixture)
        {
            Fixture = fixture;
        }
        public Task<ConfigurationHttpResponse> FetchAsync(ConfigurationHttpRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = request.Address.AbsolutePath.TrimStart('/');
            lock (Requests)
                Requests.Add(path);
            if (Override != null)
                return Override(request, cancellationToken);
            return Task.FromResult(new ConfigurationHttpResponse(Fixture.Documents.TryGetValue(path, out var bytes) ? 200 : 404, bytes ?? Array.Empty<byte>(), request.Address, etag: "\"fixture-" + path.Replace('/', '-') + "\""));
        }
    }
    internal sealed class BlockingLoadStore : IConfigurationStore
    {
        private readonly IConfigurationStore inner;
        internal bool Armed;
        internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal BlockingLoadStore(IConfigurationStore inner)
        {
            this.inner = inner;
        }
        public async Task<StoreSnapshot?> LoadAsync(CacheNamespace space, CancellationToken cancellationToken)
        {
            if (Armed)
            {
                Armed = false;
                Entered.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
            }
            return await inner.LoadAsync(space, cancellationToken);
        }
        public Task<byte[]?> ReadObjectAsync(CacheNamespace space, string sha, int maximum, CancellationToken token) => inner.ReadObjectAsync(space, sha, maximum, token);
        public Task<StoreCommitOutcome> CommitAsync(CacheNamespace space, long expected, byte[] metadata, IReadOnlyDictionary<string, byte[]> added, CancellationToken token) => inner.CommitAsync(space, expected, metadata, added, token);
    }
    internal sealed class BeforeCommitStore : IConfigurationStore
    {
        private readonly IConfigurationStore inner;
        internal Func<Task>? BeforeNextCommit;
        internal int Conflicts;
        internal BeforeCommitStore(IConfigurationStore inner)
        {
            this.inner = inner;
        }
        public Task<StoreSnapshot?> LoadAsync(CacheNamespace space, CancellationToken token) => inner.LoadAsync(space, token);
        public Task<byte[]?> ReadObjectAsync(CacheNamespace space, string sha, int maximum, CancellationToken token) => inner.ReadObjectAsync(space, sha, maximum, token);
        public async Task<StoreCommitOutcome> CommitAsync(CacheNamespace space, long expected, byte[] metadata, IReadOnlyDictionary<string, byte[]> added, CancellationToken token)
        {
            var before = BeforeNextCommit;
            BeforeNextCommit = null;
            if (before != null)
                await before();
            var result = await inner.CommitAsync(space, expected, metadata, added, token);
            if (result == StoreCommitOutcome.Conflict)
                Conflicts++;
            return result;
        }
    }
    internal sealed class LocalHttpTransport : IConfigurationTransport, IDisposable
    {
        private readonly HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
        public async Task<ConfigurationHttpResponse> FetchAsync(ConfigurationHttpRequest request, CancellationToken cancellationToken)
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, request.Address);
            if (request.ETag != null)
                message.Headers.TryAddWithoutValidation("If-None-Match", request.ETag);
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                int count = await input.ReadAsync(buffer, cancellationToken);
                if (count == 0)
                    break;
                if (output.Length + count > request.MaximumDecodedBytes)
                    throw new InvalidConfigurationException("decoded_bounds");
                output.Write(buffer, 0, count);
            }
            return new ConfigurationHttpResponse((int)response.StatusCode, output.ToArray(), request.Address, response.Content.Headers.ContentType?.ToString() ?? string.Empty, response.Headers.ETag?.ToString());
        }
        public void Dispose() => client.Dispose();
    }
    internal sealed class LocalHttpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Task loop;
        private readonly List<Task> handlers = new List<Task>();
        private readonly Fixture fixture;
        internal readonly List<string> Requests = new List<string>();
        internal Uri Root { get; }
        internal bool Gzip;
        internal LocalHttpServer(Fixture fixture)
        {
            this.fixture = fixture;
            listener.Start();
            Root = new Uri("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/");
            Console.WriteLine("owned_loopback_lease=" + Root);
            loop = Serve();
        }
        private async Task Serve()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(lifetime.Token);
                    handlers.Add(Handle(client));
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (lifetime.IsCancellationRequested) { }
        }
        private async Task Handle(TcpClient client)
        {
            using (client)
            {
                try
                {
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 8192, true);
                    string? line = await reader.ReadLineAsync(lifetime.Token);
                    if (line == null)
                        return;
                    string path = line.Split(' ')[1].TrimStart('/');
                    lock (Requests)
                        Requests.Add(path);
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync(lifetime.Token)))
                    {
                    }
                    bool found = fixture.Documents.TryGetValue(path, out var bytes);
                    byte[] body = bytes ?? Array.Empty<byte>();
                    if (Gzip && found)
                    {
                        using var encoded = new MemoryStream();
                        using (var gzip = new GZipStream(encoded, CompressionLevel.SmallestSize, true))
                            gzip.Write(body, 0, body.Length);
                        body = encoded.ToArray();
                    }
                    string headers = "HTTP/1.1 " + (found ? "200 OK" : "404 Not Found") + "\r\nContent-Type: application/json\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\nETag: \"fixture\"\r\n" + (Gzip && found ? "Content-Encoding: gzip\r\n" : string.Empty) + "\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), lifetime.Token);
                    await stream.WriteAsync(body, lifetime.Token);
                }
                catch (IOException) { }
                catch (OperationCanceledException) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            lifetime.Cancel();
            listener.Stop();
            await loop;
            await Task.WhenAll(handlers);
            lifetime.Dispose();
        }
    }
}
