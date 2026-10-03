#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http;

namespace DnsClientX.Tests;

/// <summary>Protects bootstrap routing at the HTTP request negotiation boundary.</summary>
public sealed class BootstrapHttpVersionTests {
    /// <summary>Every supported HTTP query lane prevents an automatic QUIC upgrade.</summary>
    [Theory]
    [InlineData(DnsRequestFormat.DnsOverHttps)]
    [InlineData(DnsRequestFormat.DnsOverHttpsPOST)]
    [InlineData(DnsRequestFormat.DnsOverHttp2)]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSON)]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSONPOST)]
    public async Task BootstrapRequestsStayOnSpecifiedHttpVersion(DnsRequestFormat format) {
        var configuration = new Configuration(new Uri("https://endpoint.bootstrap.invalid/dns-query"), format) {
            BootstrapResolver = new DnsResolverEndpoint { Host = "127.0.0.1", Port = 53, Transport = Transport.Udp },
            HttpVersion = HttpVersion.Version20
        };
        using var handler = new CaptureHandler(format is DnsRequestFormat.DnsOverHttpsJSON or DnsRequestFormat.DnsOverHttpsJSONPOST);
        using var client = new HttpClient(handler) { BaseAddress = configuration.BaseUri };
        _ = format switch {
            DnsRequestFormat.DnsOverHttps => await client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false, configuration, default),
            DnsRequestFormat.DnsOverHttpsPOST => await client.ResolveWireFormatPost("example.com", DnsRecordType.A, false, false, false, configuration, default),
            DnsRequestFormat.DnsOverHttp2 => await client.ResolveWireFormatHttp2("example.com", DnsRecordType.A, false, false, false, configuration, default),
            DnsRequestFormat.DnsOverHttpsJSON => await client.ResolveJsonFormat("example.com", DnsRecordType.A, false, false, false, configuration, default),
            _ => await client.ResolveJsonFormatPost("example.com", DnsRecordType.A, false, false, false, configuration, default)
        };
        Assert.Equal(HttpVersion.Version20, handler.Version);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, handler.Policy);
    }

    private sealed class CaptureHandler(bool json) : HttpMessageHandler {
        internal Version? Version { get; private set; }
        internal HttpVersionPolicy Policy { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Version = request.Version;
            Policy = request.VersionPolicy;
            HttpContent content;
            if (json) content = new StringContent("{\"Status\":0}", System.Text.Encoding.UTF8, "application/dns-json");
            else {
                content = new ByteArrayContent(TestUtilities.CreateResponseFromQuery(await TestUtilities.ReadDnsQueryAsync(request)));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
#endif
