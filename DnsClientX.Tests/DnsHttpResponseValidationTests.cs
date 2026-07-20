using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Tests RFC 8484 HTTP-envelope validation before DNS wire parsing.</summary>
public class DnsHttpResponseValidationTests {
    private sealed class ResponseHandler : HttpMessageHandler {
        private readonly HttpStatusCode _statusCode;
        private readonly HttpContent _content;

        internal ResponseHandler(HttpStatusCode statusCode, HttpContent content) {
            _statusCode = statusCode;
            _content = content;
        }
        internal HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = _content });
        }
    }

    /// <summary>An HTTP error page is reported as HTTP failure rather than parsed as a DNS packet.</summary>
    [Fact]
    public async Task ResolveWireFormatGet_ReportsHttpErrorBeforeParsingBody() {
        using var handler = new ResponseHandler(
            HttpStatusCode.HttpVersionNotSupported,
            new StringContent("<html><body>HTTP version not supported</body></html>"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://resolver.example/dns-query") };
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttps);

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false,
                configuration, CancellationToken.None));

        Assert.Contains("HttpVersionNotSupported", exception.Message);
        Assert.Contains("HTTP version not supported", exception.Message);
        Assert.DoesNotContain("QR=0", exception.Message);
        Assert.Equal(DnsResponseCode.ServerFailure, exception.Response?.Status);
    }

    /// <summary>An explicit non-DNS media type is rejected before parsing even with a 2xx status.</summary>
    [Fact]
    public async Task ResolveWireFormatGet_RejectsExplicitNonDnsMediaType() {
        using var handler = new ResponseHandler(HttpStatusCode.OK, new StringContent("not a DNS message"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://resolver.example/dns-query") };
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttps);

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false,
                configuration, CancellationToken.None));

        Assert.Contains("is not application/dns-message", exception.Message);
    }

    /// <summary>POST uses the same HTTP-envelope validation as GET and modern HTTP versions.</summary>
    [Fact]
    public async Task ResolveWireFormatPost_ReportsHttpErrorBeforeParsingBody() {
        using var handler = new ResponseHandler(
            HttpStatusCode.ServiceUnavailable,
            new StringContent("resolver temporarily unavailable"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://resolver.example/dns-query") };
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttpsPOST);

        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(() =>
            client.ResolveWireFormatPost("example.com", DnsRecordType.A, false, false, false,
                configuration, CancellationToken.None));

        Assert.Contains("ServiceUnavailable", exception.Message);
        Assert.Contains("resolver temporarily unavailable", exception.Message);
    }

    /// <summary>A wire query overrides JSON defaults when DNSSEC validation upgrades a JSON endpoint.</summary>
    [Fact]
    public async Task WireGetAdvertisesDnsMessageOnJsonConfiguredClient() {
        using var handler = new ResponseHandler(HttpStatusCode.BadRequest, new StringContent("capture"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://resolver.example/dns-query?ct=application/dns-json") };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/dns-json");
        var configuration = new Configuration(client.BaseAddress, DnsRequestFormat.DnsOverHttpsJSON);

        await Assert.ThrowsAsync<DnsClientException>(() => client.ResolveWireFormatGet(
            "example.com", DnsRecordType.A, true, true, false, configuration,
            CancellationToken.None, useStandardDnsQueryPath: true));

        Assert.Equal("/dns-query", handler.Request?.RequestUri?.AbsolutePath);
        Assert.StartsWith("?dns=", handler.Request?.RequestUri?.Query, StringComparison.Ordinal);
        Assert.Equal("application/dns-message", Assert.Single(handler.Request!.Headers.Accept).MediaType);
    }

    /// <summary>The immutable query configuration, not a mutable client's BaseAddress, selects the resolver.</summary>
    [Fact]
    public async Task WireGetUsesAbsoluteUriFromQuerySnapshot() {
        using var handler = new ResponseHandler(HttpStatusCode.BadRequest, new StringContent("capture"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://wrong-resolver.example/dns-query") };
        var configuration = new Configuration(
            new Uri("https://snapshot-resolver.example/custom-query"),
            DnsRequestFormat.DnsOverHttps);

        await Assert.ThrowsAsync<DnsClientException>(() => client.ResolveWireFormatGet(
            "example.com", DnsRecordType.A, false, false, false, configuration, CancellationToken.None));

        Assert.Equal("snapshot-resolver.example", handler.Request?.RequestUri?.Host);
        Assert.Equal("/custom-query", handler.Request?.RequestUri?.AbsolutePath);
    }
}
