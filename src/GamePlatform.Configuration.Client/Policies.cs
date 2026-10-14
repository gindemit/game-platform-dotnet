using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Configuration;

namespace GamePlatform.Configuration.Client
{
    internal sealed class HostDeadline : IDisposable
    {
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly CancellationTokenSource cancelled;
        private readonly Task expiration;
        internal CancellationToken Token => cancelled.Token;
        internal HostDeadline(IConfigurationClock clock, CancellationToken parent, long deadline)
        {
            cancelled = CancellationTokenSource.CreateLinkedTokenSource(parent);
            expiration = Expire(clock, deadline);
        }
        private async Task Expire(IConfigurationClock clock, long deadline)
        {
            try
            {
                await clock.DelayAsync(TimeSpan.FromSeconds(Math.Max(0, deadline - clock.MonotonicSeconds)), stop.Token).ConfigureAwait(false);
                cancelled.Cancel();
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }
        public void Dispose()
        {
            stop.Cancel();
            stop.Dispose();
            cancelled.Dispose();
        }
    }
    public sealed class ConfigurationClientPolicy
    {
        public const int ControlBytes = 16384;
        public const int GraphFiles = 64;
        public const int GraphBytes = 8388608;
        public const int CacheBudgetBytes = 33554432;
        public const int FreshSeconds = 86400;
        public Uri NormalRoot { get; }
        public Uri ControlRoot { get; }
        public bool AllowLoopbackHttp { get; }
        public ConfigurationClientPolicy(Uri normalRoot, Uri controlRoot, bool allowLoopbackHttp = false)
        {
            NormalRoot = Root(normalRoot, allowLoopbackHttp);
            ControlRoot = Root(controlRoot, allowLoopbackHttp);
            AllowLoopbackHttp = allowLoopbackHttp;
        }
        private static Uri Root(Uri value, bool loopback)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (!value.IsAbsoluteUri || value.UserInfo.Length != 0 || value.Query.Length != 0 || value.Fragment.Length != 0 || !value.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
                || (value.Scheme != "https" && !(loopback && value.Scheme == "http" && value.IsLoopback)) || value.OriginalString.Length > 4096 || value.AbsolutePath.Contains("%") || value.AbsolutePath.Contains(".."))
                throw new ArgumentException("origin_invalid", nameof(value));
            return value;
        }
        public Uri Resolve(string path, bool control = false)
        {
            if (path != "current.json" && path != "control.json")
                Wire.Path(path);
            var root = control ? ControlRoot : NormalRoot;
            var result = new Uri(root, path);
            if (result.AbsoluteUri.Length > 4096 || result.GetLeftPart(UriPartial.Authority) != root.GetLeftPart(UriPartial.Authority) || !result.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal))
                throw new InvalidConfigurationException("path_escape");
            return result;
        }
        internal void Response(ConfigurationHttpRequest request, ConfigurationHttpResponse response)
        {
            if (response.Address != request.Address)
                throw new InvalidConfigurationException("redirect_invalid");
            if (response.Bytes.Length > request.MaximumDecodedBytes)
                throw new InvalidConfigurationException("bounds");
            if (response.Status == 200 && !response.ContentType.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidConfigurationException("mime_invalid");
            if (response.ETag != null && (response.ETag.Length > 4096 || response.ETag.Contains("\r") || response.ETag.Contains("\n")))
                throw new InvalidConfigurationException("validator_invalid");
        }
    }
}
