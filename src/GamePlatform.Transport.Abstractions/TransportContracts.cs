using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Transport.Abstractions
{
    public enum HttpDeliveryCertainty { NotSent, Uncertain, ResponseReceived }

    public sealed class HttpRequestData
    {
        private readonly byte[] body;
        public HttpRequestData(string method, string uri, IReadOnlyDictionary<string, string> headers, byte[] body)
        {
            if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("An HTTP method is required.", nameof(method));
            if (!System.Uri.TryCreate(uri, UriKind.Absolute, out _)) throw new ArgumentException("An absolute URI is required.", nameof(uri));
            Method = method;
            Uri = uri;
            Headers = CopyHeaders(headers);
            this.body = body == null ? Array.Empty<byte>() : (byte[])body.Clone();
        }
        public string Method { get; }
        public string Uri { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public int BodyLength => body.Length;
        public byte[] CopyBody() => (byte[])body.Clone();

        private static IReadOnlyDictionary<string, string> CopyHeaders(IReadOnlyDictionary<string, string> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                    throw new ArgumentException("An HTTP header name is invalid.", nameof(source));
                if (pair.Value == null || pair.Value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                    throw new ArgumentException("An HTTP header value is invalid.", nameof(source));
                if (copy.ContainsKey(pair.Key)) throw new ArgumentException("Duplicate HTTP headers are not allowed.", nameof(source));
                copy.Add(pair.Key, pair.Value);
            }
            return new ReadOnlyDictionary<string, string>(copy);
        }
    }

    public sealed class HttpResponseData
    {
        private readonly byte[] body;
        public HttpResponseData(int statusCode, IReadOnlyDictionary<string, string> headers, byte[] body)
        {
            if (statusCode < 100 || statusCode > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
            StatusCode = statusCode;
            Headers = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(headers ?? throw new ArgumentNullException(nameof(headers)), StringComparer.OrdinalIgnoreCase));
            this.body = body == null ? Array.Empty<byte>() : (byte[])body.Clone();
        }
        public int StatusCode { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public int BodyLength => body.Length;
        public byte[] CopyBody() => (byte[])body.Clone();
        public bool TryGetHeader(string name, out string value) => Headers.TryGetValue(name, out value!);
    }

    public sealed class HttpExecutionException : Exception
    {
        public HttpExecutionException(HttpDeliveryCertainty certainty, string message, Exception? innerException = null) : base(message, innerException) { Certainty = certainty; }
        public HttpDeliveryCertainty Certainty { get; }
    }

    public interface IHttpExecutor { Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken); }
    public interface IWireCodec { byte[] Encode<T>(T value); T Decode<T>(byte[] payload); }
}
