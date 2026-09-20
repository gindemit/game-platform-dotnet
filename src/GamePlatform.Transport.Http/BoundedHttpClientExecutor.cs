#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Transport.Abstractions;

namespace GamePlatform.Transport.Http
{
    /// <summary>
    /// Bounded System.Net.Http execution for the frozen MessagePack provisioning,
    /// bootstrap, pull, push and receipt routes. The HttpClient lifetime is owned by the
    /// caller; redirects must remain disabled on its handler.
    /// </summary>
    public sealed class BoundedHttpClientExecutor : IHttpExecutor
    {
        private const int MaximumRequestBytes = 262_144;
        private const int MaximumHeaderCount = 64;
        private const int MaximumHeaderNameLength = 128;
        private const int MaximumHeaderValueLength = 16_384;
        private readonly HttpClient client;
        private readonly Uri baseUri;
        private readonly string basePath;
        private readonly int maximumResponseBytes;

        public BoundedHttpClientExecutor(HttpClient client, BackendHttpConfiguration configuration)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            baseUri = configuration.BaseUri;
            basePath = baseUri.AbsolutePath;
            maximumResponseBytes = configuration.MaximumResponseBytes;
        }

        public async Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRequest(request);

            using var message = CreateMessage(request);
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "The HTTP request failed before a response was received.", error);
            }

            using (response)
            {
                try
                {
                    ValidateFinalUri(request.Uri, response.RequestMessage?.RequestUri);
                    var headers = CopyHeaders(response);
                    var body = await ReadBoundedBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
                    return new HttpResponseData((int)response.StatusCode, headers, body);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (HttpExecutionException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The HTTP response could not be read safely.", error);
                }
            }
        }

        private void ValidateRequest(HttpRequestData request)
        {
            if (request.BodyLength > MaximumRequestBytes) NotSent("The HTTP request body exceeds the frozen bound.");
            if (!Uri.TryCreate(request.Uri, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !SameAuthority(baseUri, uri) || !uri.AbsolutePath.StartsWith(basePath, StringComparison.Ordinal))
                NotSent("The HTTP request escaped the configured HTTPS authority.");

            var relativePath = uri.AbsolutePath.Substring(basePath.Length);
            var isGet = string.Equals(request.Method, "GET", StringComparison.Ordinal);
            var isPost = string.Equals(request.Method, "POST", StringComparison.Ordinal);
            if ((!isGet && !isPost) || !IsFrozenRoute(relativePath, uri.Query, isGet))
                NotSent("The HTTP route is not available in the production composition.");
            if ((isGet && request.BodyLength != 0) || (isPost && request.BodyLength == 0))
                NotSent("The HTTP request body does not match the frozen operation.");

            if (request.Headers.Count < 2 || request.Headers.Count > 3 ||
                !request.Headers.TryGetValue("Authorization", out var authorization) ||
                authorization.Length <= 7 || authorization.Length > MaximumHeaderValueLength ||
                !authorization.StartsWith("Bearer ", StringComparison.Ordinal) ||
                !request.Headers.TryGetValue("Accept", out var accept) ||
                !string.Equals(accept, ProvisioningHttpProvider.MediaType, StringComparison.OrdinalIgnoreCase))
                NotSent("The HTTP request headers do not match the frozen authenticated MessagePack profile.");

            var hasContentType = request.Headers.TryGetValue("Content-Type", out var contentType);
            if (isPost != hasContentType || (hasContentType && !string.Equals(contentType, ProvisioningHttpProvider.MediaType, StringComparison.OrdinalIgnoreCase)))
                NotSent("The HTTP content type does not match the frozen MessagePack profile.");
            foreach (var header in request.Headers)
            {
                if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    NotSent("The HTTP request contains an unsupported header.");
            }
        }

        private static bool IsFrozenRoute(string relativePath, string query, bool isGet)
        {
            var segments = relativePath.Split('/');
            if (segments.Length < 4 || segments[0] != "v1" || segments[1] != "apps" ||
                !Guid.TryParseExact(segments[2], "D", out _)) return false;
            if (isGet)
            {
                return (segments.Length == 4 && segments[3] == "bootstrap" &&
                    query.StartsWith("?clientStreamId=", StringComparison.Ordinal) &&
                    Guid.TryParseExact(query.Substring("?clientStreamId=".Length), "D", out _)) ||
                    (query.Length == 0 && segments.Length == 6 && segments[3] == "gameplay" &&
                    segments[4] == "reward-receipts" && Guid.TryParseExact(segments[5], "D", out _));
            }
            if (query.Length != 0) return false;
            return (segments.Length == 4 && segments[3] == "provision") ||
                (segments.Length == 5 && segments[3] == "bootstrap" && segments[4] == "pages") ||
                (segments.Length == 5 && segments[3] == "sync" && segments[4] == "pull") ||
                (segments.Length == 5 && segments[3] == "sync" && segments[4] == "push") ||
                (segments.Length == 6 && segments[3] == "sync" && segments[4] == "receipts" && segments[5] == "lookup");
        }

        private static HttpRequestMessage CreateMessage(HttpRequestData request)
        {
            var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Uri);
            if (request.BodyLength != 0) message.Content = new ByteArrayContent(request.CopyBody());
            foreach (var header in request.Headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    message.Content!.Headers.TryAddWithoutValidation(header.Key, header.Value);
                else
                    message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return message;
        }

        private async Task<byte[]> ReadBoundedBodyAsync(HttpContent? content, CancellationToken cancellationToken)
        {
            if (content == null) return Array.Empty<byte>();
            if (content.Headers.ContentLength.HasValue && content.Headers.ContentLength.Value > maximumResponseBytes)
                throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The HTTP response body exceeds the configured bound.");
            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            using var buffer = new MemoryStream(Math.Min(maximumResponseBytes, 16_384));
            var chunk = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > maximumResponseBytes)
                    throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The HTTP response body exceeds the configured bound.");
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }

        private static Dictionary<string, string> CopyHeaders(HttpResponseMessage response)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddHeaders(result, response.Headers);
            if (response.Content != null) AddHeaders(result, response.Content.Headers);
            return result;
        }

        private static void AddHeaders(Dictionary<string, string> target, System.Net.Http.Headers.HttpHeaders source)
        {
            foreach (var header in source)
            {
                if (target.Count >= MaximumHeaderCount || header.Key.Length == 0 || header.Key.Length > MaximumHeaderNameLength)
                    throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The HTTP response headers exceed the configured bound.");
                var value = string.Join(",", header.Value);
                if (value.Length > MaximumHeaderValueLength || target.ContainsKey(header.Key))
                    throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The HTTP response headers are invalid or exceed the configured bound.");
                target.Add(header.Key, value);
            }
        }

        private static void ValidateFinalUri(string requested, Uri? actual)
        {
            if (actual == null || !Uri.TryCreate(requested, UriKind.Absolute, out var expected) ||
                !string.Equals(expected.AbsoluteUri, actual.AbsoluteUri, StringComparison.Ordinal))
                throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "HTTP redirects are not accepted.");
        }

        private static bool SameAuthority(Uri left, Uri right) =>
            string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port;

        private static void NotSent(string message) => throw new HttpExecutionException(HttpDeliveryCertainty.NotSent, message);
    }
}
