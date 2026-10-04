using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects HTTP DNS callers from response bodies that exceed their protocol limits.</summary>
public sealed class DnsHttpResponseBoundsTests {
    /// <summary>Both JSON query methods reject an HTTP error carrying a successful-looking DNS payload.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonQueryRejectsHttpFailure(bool post) {
        using var client = new HttpClient(new ResponseHandler(
            new StringContent("{\"Status\":0}"), HttpStatusCode.InternalServerError)) {
            BaseAddress = new Uri("https://resolver.example/dns-query")
        };
        var configuration = new Configuration(client.BaseAddress, post
            ? DnsRequestFormat.DnsOverHttpsJSONPOST
            : DnsRequestFormat.DnsOverHttpsJSON);

        DnsResponse response = post
            ? await client.ResolveJsonFormatPost("example.com", DnsRecordType.A, false, false, false, configuration, default)
            : await client.ResolveJsonFormat("example.com", DnsRecordType.A, false, false, false, configuration, default);

        Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        Assert.Contains("HTTP 500", response.Error, StringComparison.Ordinal);
    }

    /// <summary>An oversized wire response is rejected from headers without reading its body.</summary>
    [Fact]
    public async Task WireGetRejectsDeclaredOversizeBeforeReadingBody() {
        using var client = new HttpClient(new ResponseHandler(new HeaderOnlyContent(ushort.MaxValue + 1))) {
            BaseAddress = new Uri("https://resolver.example/dns-query")
        };
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttps);

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false,
                configuration, CancellationToken.None));

        Assert.Contains("65535", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The public JSON path applies its own bounded response policy before buffering.</summary>
    [Fact]
    public async Task PublicJsonQueryRejectsDeclaredOversizeBeforeReadingBody() {
        using var client = new HttpClient(new ResponseHandler(new HeaderOnlyContent(1024 * 1024 + 1, "application/dns-json")));

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            DnsJsonQueryClient.QueryAsync(client, new Uri("https://resolver.example/resolve"),
                "example.com", DnsRecordType.A));

        Assert.Contains("1048576", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Unknown-length wire bodies stop at the DNS message limit rather than draining forever.</summary>
    [Fact]
    public async Task WireGetStopsChunkedBodyAtProtocolLimit() {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var stream = new PrefixThenWaitStream(ushort.MaxValue + 1);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message");
        using var client = new HttpClient(new ResponseHandler(content)) {
            BaseAddress = new Uri("https://resolver.example/dns-query")
        };
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttps);

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false,
                configuration, cts.Token));

        Assert.Contains("65535", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Unknown-length JSON bodies cannot stream past their documented response cap.</summary>
    [Fact]
    public async Task PublicJsonQueryStopsUnknownLengthBodyAtLimit() {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var stream = new PrefixThenWaitStream(1024 * 1024 + 1);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-json");
        using var client = new HttpClient(new ResponseHandler(content));

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            DnsJsonQueryClient.QueryAsync(client, new Uri("https://resolver.example/resolve"),
                "example.com", DnsRecordType.A, cancellationToken: cts.Token));

        Assert.Contains("1048576", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>HTTP failure diagnostics read only a short body prefix.</summary>
    [Fact]
    public async Task WireGetCapsHttpErrorPreview() {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var stream = new PrefixThenWaitStream(513);
        using var content = new StreamContent(stream);
        using var client = new HttpClient(new ResponseHandler(content, HttpStatusCode.BadGateway)) {
            BaseAddress = new Uri("https://resolver.example/dns-query")
        };
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttps);

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false,
                configuration, cts.Token));

        Assert.Contains("BadGateway", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Body: ", exception.Message, StringComparison.Ordinal);
        Assert.True(exception.Message.Length < 800);
    }

    /// <summary>A body read that ignores its token is stopped by closing the response stream.</summary>
    [Fact]
    public async Task WireGetTimeoutClosesStalledResponseStream() {
        using var stream = new DisposeToUnblockStream();
        using var content = new StreamContent(stream);
        using var client = new HttpClient(new ResponseHandler(content)) {
            BaseAddress = new Uri("https://resolver.example/dns-query")
        };
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttps) {
            TimeOut = 150
        };

        Task<DnsResponse> query = client.ResolveWireFormatGet("example.com", DnsRecordType.A,
            false, false, false, configuration, CancellationToken.None);
        Task winner = await Task.WhenAny(query, Task.Delay(TimeSpan.FromSeconds(2)));
        if (winner != query) {
            stream.Dispose();
            await Assert.ThrowsAnyAsync<Exception>(() => query);
            Assert.Fail("The configured timeout did not stop the response body read.");
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
        Assert.True(stream.WasDisposed);
    }

    private sealed class ResponseHandler(HttpContent content, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
    }

    private sealed class HeaderOnlyContent : HttpContent {
        internal HeaderOnlyContent(int length, string mediaType = "application/dns-message") {
            Headers.ContentLength = length;
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("The body was buffered before its declared length was checked.");

        protected override bool TryComputeLength(out long length) {
            length = Headers.ContentLength.GetValueOrDefault();
            return true;
        }
    }

    private sealed class PrefixThenWaitStream(int prefixBytes) : Stream {
        private int _remaining = prefixBytes;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            if (_remaining == 0) {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }
            int copied = Math.Min(_remaining, count);
            for (int index = offset; index < offset + copied; index++) {
                buffer[index] = (byte)'x';
            }
            _remaining -= copied;
            return copied;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DisposeToUnblockStream : Stream {
        private readonly TaskCompletionSource<bool> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool WasDisposed => _closed.Task.IsCompleted;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            await _closed.Task;
            throw new IOException("The response stream was closed.");
        }

        protected override void Dispose(bool disposing) {
            _closed.TrySetResult(true);
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
