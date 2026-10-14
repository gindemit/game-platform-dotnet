using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GamePlatform.Transport.Abstractions
{

    public sealed class HttpResponseData
    {
        private readonly byte[] body;
        public HttpResponseData(int statusCode, IReadOnlyDictionary<string, string> headers, byte[] body)
        {
            if (statusCode < 100 || statusCode > 599)
                throw new ArgumentOutOfRangeException(nameof(statusCode));
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
}
