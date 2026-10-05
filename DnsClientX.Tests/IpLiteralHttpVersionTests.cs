#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http;

namespace DnsClientX.Tests;

/// <summary>Protects IP-literal DoH from unrequested QUIC transport upgrades.</summary>
public sealed class IpLiteralHttpVersionTests {
    /// <summary>Wire and JSON requests keep the configured HTTP/2 transport on literal endpoints.</summary>
    [Theory]
    [InlineData(DnsRequestFormat.DnsOverHttps, "https://1.1.1.1/dns-query")]
    [InlineData(DnsRequestFormat.DnsOverHttpsPOST, "https://1.1.1.1/dns-query")]
    [InlineData(DnsRequestFormat.DnsOverHttp2, "https://1.1.1.1/dns-query")]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSON, "https://1.1.1.1/dns-query")]
    [InlineData(DnsRequestFormat.DnsOverHttpsJSONPOST, "https://1.1.1.1/dns-query")]
    [InlineData(DnsRequestFormat.DnsOverHttps, "https://[2606:4700:4700::1111]/dns-query")]
    public async Task LiteralRequestsKeepHttp2(DnsRequestFormat format, string uri) {
        var configuration = new Configuration(new Uri(uri), format);
        using var handler = new CaptureHandler(format is DnsRequestFormat.DnsOverHttpsJSON or DnsRequestFormat.DnsOverHttpsJSONPOST);
        using var client = new HttpClient(handler) { BaseAddress = configuration.BaseUri };
        DnsResponse result = format switch {
            DnsRequestFormat.DnsOverHttps => await client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false, configuration, default),
            DnsRequestFormat.DnsOverHttpsPOST => await client.ResolveWireFormatPost("example.com", DnsRecordType.A, false, false, false, configuration, default),
            DnsRequestFormat.DnsOverHttp2 => await client.ResolveWireFormatHttp2("example.com", DnsRecordType.A, false, false, false, configuration, default),
            DnsRequestFormat.DnsOverHttpsJSON => await client.ResolveJsonFormat("example.com", DnsRecordType.A, false, false, false, configuration, default),
            _ => await client.ResolveJsonFormatPost("example.com", DnsRecordType.A, false, false, false, configuration, default)
        };
        Assert.Equal(DnsResponseCode.NoError, result.Status);
        Assert.Equal(HttpVersion.Version20, handler.Version);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, handler.Policy);
    }

    /// <summary>Explicit versions remain usable, while hostname negotiation retains its existing policy.</summary>
    [Theory]
    [InlineData("https://1.1.1.1/dns-query", 1, HttpVersionPolicy.RequestVersionExact)]
    [InlineData("https://1.1.1.1/dns-query", 3, HttpVersionPolicy.RequestVersionOrHigher)]
    [InlineData("https://dns.example/dns-query", 2, HttpVersionPolicy.RequestVersionOrHigher)]
    public async Task ExplicitVersionsAndHostnamePolicyArePreserved(string uri, int major, HttpVersionPolicy policy) {
        var configuration = new Configuration(new Uri(uri), DnsRequestFormat.DnsOverHttps) {
            HttpVersion = new Version(major, major == 1 ? 1 : 0)
        };
        using var handler = new CaptureHandler(false);
        using var client = new HttpClient(handler) { BaseAddress = configuration.BaseUri };
        _ = await client.ResolveWireFormatGet("example.com", DnsRecordType.A, false, false, false, configuration, default);
        Assert.Equal(configuration.HttpVersion, handler.Version);
        Assert.Equal(policy, handler.Policy);
    }

    /// <summary>The HTTP/2 lane uses its actual request version when configuration prefers HTTP/3.</summary>
    [Fact]
    public async Task Http2LaneKeepsHttp2WithHttp3Configuration() {
        var configuration = new Configuration(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttp2) {
            HttpVersion = HttpVersion.Version30
        };
        using var handler = new CaptureHandler(false);
        using var client = new HttpClient(handler) { BaseAddress = configuration.BaseUri };
        _ = await client.ResolveWireFormatHttp2("example.com", DnsRecordType.A, false, false, false, configuration, default);
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
            if (json) {
                content = new StringContent("{\"Status\":0}", System.Text.Encoding.UTF8, "application/dns-json");
            } else {
                content = new ByteArrayContent(TestUtilities.CreateResponseFromQuery(await TestUtilities.ReadDnsQueryAsync(request)));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
#endif
