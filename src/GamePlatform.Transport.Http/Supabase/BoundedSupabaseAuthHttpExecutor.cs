#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Transport.Abstractions;

namespace GamePlatform.Transport.Http.Supabase
{
    /// <summary>Caller-owned HttpClient executor for the two Supabase Auth JSON routes only.</summary>
    public sealed class BoundedSupabaseAuthHttpExecutor : IHttpExecutor
    {
        private const int MaximumRequestBytes = 65_536;
        private const int MaximumHeaderCount = 16;
        private const int MaximumHeaderNameLength = 128;
        private const int MaximumHeaderValueLength = 16_384;
        private readonly HttpClient client;
        private readonly SupabaseAuthConfiguration configuration;
        private readonly string basePath;

        public BoundedSupabaseAuthHttpExecutor(HttpClient client, SupabaseAuthConfiguration configuration)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            basePath = configuration.AuthBaseUri.AbsolutePath;
        }

        public async Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRequest(request);
            using var message = CreateMessage(request);
            HttpResponseMessage response;
            try { response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "The Supabase Auth request failed before a response was received.", error); }

            using (response)
            {
                try
                {
                    ValidateFinalUri(request.Uri, response.RequestMessage?.RequestUri);
                    if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                        throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "Supabase Auth redirects are not accepted.");
                    var headers = CopyHeaders(response);
                    var body = await ReadBoundedBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
                    return new HttpResponseData((int)response.StatusCode, headers, body);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (HttpExecutionException) { throw; }
                catch (Exception error) { throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The Supabase Auth response could not be read safely.", error); }
            }
        }

        private void ValidateRequest(HttpRequestData request)
        {
            if (!string.Equals(request.Method, "POST", StringComparison.Ordinal) || request.BodyLength == 0 || request.BodyLength > MaximumRequestBytes)
                NotSent("The Supabase Auth request shape is invalid.");
            if (!Uri.TryCreate(request.Uri, UriKind.Absolute, out var uri) || !SameAuthority(configuration.AuthBaseUri, uri) || !uri.AbsolutePath.StartsWith(basePath, StringComparison.Ordinal))
                NotSent("The Supabase Auth request escaped the configured HTTPS authority.");
            var relativePath = uri.AbsolutePath.Substring(basePath.Length);
            if (!((relativePath == "signup" && uri.Query.Length == 0) ||
                  (relativePath == "token" && string.Equals(uri.Query, "?grant_type=refresh_token", StringComparison.Ordinal))))
                NotSent("The Supabase Auth route is not available.");
            if (request.Headers.Count != 3 || !request.Headers.TryGetValue("apikey", out var apiKey) ||
                !string.Equals(apiKey, configuration.PublishableApiKey, StringComparison.Ordinal) ||
                !request.Headers.TryGetValue("Accept", out var accept) || !IsJson(accept) ||
                !request.Headers.TryGetValue("Content-Type", out var contentType) || !IsJson(contentType))
                NotSent("The Supabase Auth headers are invalid.");
            foreach (var header in request.Headers)
            {
                if (header.Key.Length > MaximumHeaderNameLength || header.Value.Length > MaximumHeaderValueLength ||
                    (!string.Equals(header.Key, "apikey", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)))
                    NotSent("The Supabase Auth headers are invalid.");
            }
        }

        private static HttpRequestMessage CreateMessage(HttpRequestData request)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, request.Uri) { Content = new ByteArrayContent(request.CopyBody()) };
            foreach (var header in request.Headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)) message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                else message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return message;
        }

        private async Task<byte[]> ReadBoundedBodyAsync(HttpContent? content, CancellationToken cancellationToken)
        {
            if (content == null) return Array.Empty<byte>();
            if (content.Headers.ContentLength.HasValue && content.Headers.ContentLength.Value > configuration.MaximumResponseBytes)
                throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The Supabase Auth response body exceeds the configured bound.");
            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            using var buffer = new MemoryStream(Math.Min(configuration.MaximumResponseBytes, 16_384));
            var chunk = new byte[8_192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > configuration.MaximumResponseBytes)
                    throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The Supabase Auth response body exceeds the configured bound.");
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }

        private static Dictionary<string, string> CopyHeaders(HttpResponseMessage response)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddHeaders(result, response.Headers); if (response.Content != null) AddHeaders(result, response.Content.Headers); return result;
        }
        private static void AddHeaders(Dictionary<string, string> target, System.Net.Http.Headers.HttpHeaders source)
        {
            foreach (var header in source)
            {
                var value = string.Join(",", header.Value);
                if (target.Count >= MaximumHeaderCount || header.Key.Length == 0 || header.Key.Length > MaximumHeaderNameLength || value.Length > MaximumHeaderValueLength || target.ContainsKey(header.Key))
                    throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "The Supabase Auth response headers are invalid or exceed the configured bound.");
                target.Add(header.Key, value);
            }
        }
        private static bool IsJson(string value) => string.Equals(value, "application/json", StringComparison.OrdinalIgnoreCase);
        private static void ValidateFinalUri(string requested, Uri? actual)
        {
            if (actual == null || !Uri.TryCreate(requested, UriKind.Absolute, out var expected) || !string.Equals(expected.AbsoluteUri, actual.AbsoluteUri, StringComparison.Ordinal))
                throw new HttpExecutionException(HttpDeliveryCertainty.ResponseReceived, "Supabase Auth redirects are not accepted.");
        }
        private static bool SameAuthority(Uri left, Uri right) => string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port;
        private static void NotSent(string message) => throw new HttpExecutionException(HttpDeliveryCertainty.NotSent, message);
    }
}
