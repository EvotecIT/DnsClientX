using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects failure diagnostics at the HTTP transport boundary.</summary>
public sealed class DnsTransportFailureDiagnosticsTests {
    /// <summary>Wrapped legacy HTTP timeouts retain their category and captured cause.</summary>
    [Theory]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSON)]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSONPOST)]
    [InlineData(DnsRequestFormat.DnsOverHttps)]
#if NET5_0_OR_GREATER
    [InlineData(DnsRequestFormat.DnsOverHttp2)]
    [InlineData(DnsRequestFormat.DnsOverHttp3)]
#endif
    public async Task CapturedHttpTimeoutRemainsTimeout(DnsRequestFormat format) {
        var exception = new HttpRequestException("transport deadline",
            new WebException("deadline", WebExceptionStatus.Timeout));
        using var client = new HttpClient(new FailureHandler(exception)) {
            BaseAddress = new Uri("https://resolver.example/dns-query")
        };
        var configuration = new Configuration(client.BaseAddress, format);

        DnsResponse response = await Query(client, configuration, CancellationToken.None);

        Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
        Assert.Same(exception, response.Exception);
        Assert.Equal("timeout.example", Assert.Single(response.Questions).Name);
    }

    /// <summary>Malformed JSON is classified as a response failure for GET and POST.</summary>
    [Theory]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSON)]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSONPOST)]
    public async Task MalformedJsonPreservesParseFailure(DnsRequestFormat format) {
        using var client = new HttpClient(new InvalidJsonHandler()) {
            BaseAddress = new Uri("https://resolver.example/resolve")
        };
        DnsResponse response = await Query(client, new Configuration(client.BaseAddress, format), CancellationToken.None);

        Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        Assert.Equal(DnsQueryErrorCode.InvalidResponse, response.ErrorCode);
        Assert.NotNull(response.Exception);
    }

    /// <summary>Caller cancellation is never converted into a transport timeout response.</summary>
    [Theory]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSON)]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSONPOST)]
    public async Task CallerCancellationStillPropagates(DnsRequestFormat format) {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        using var client = new HttpClient(new FailureHandler(new OperationCanceledException(cancel.Token))) {
            BaseAddress = new Uri("https://resolver.example/resolve")
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Query(client, new Configuration(client.BaseAddress, format), cancel.Token));
    }

    private static Task<DnsResponse> Query(HttpClient client, Configuration configuration, CancellationToken token) =>
        configuration.RequestFormat switch {
            DnsRequestFormat.DnsOverHttpsJSON => client.ResolveJsonFormat("timeout.example", DnsRecordType.A, false, false, false, configuration, token),
            DnsRequestFormat.DnsOverHttpsJSONPOST => client.ResolveJsonFormatPost("timeout.example", DnsRecordType.A, false, false, false, configuration, token),
            DnsRequestFormat.DnsOverHttps => client.ResolveWireFormatGet("timeout.example", DnsRecordType.A, false, false, false, configuration, token),
#if NET5_0_OR_GREATER
            DnsRequestFormat.DnsOverHttp2 => client.ResolveWireFormatHttp2("timeout.example", DnsRecordType.A, false, false, false, configuration, token),
            DnsRequestFormat.DnsOverHttp3 => client.ResolveWireFormatHttp3("timeout.example", DnsRecordType.A, false, false, false, configuration, token),
#endif
            _ => throw new ArgumentOutOfRangeException(nameof(configuration))
        };

    private sealed class FailureHandler : HttpMessageHandler {
        private readonly Exception _exception;
        internal FailureHandler(Exception exception) => _exception = exception;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromException<HttpResponseMessage>(_exception);
    }

    private sealed class InvalidJsonHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{", System.Text.Encoding.UTF8, "application/dns-json")
            });
    }
}
