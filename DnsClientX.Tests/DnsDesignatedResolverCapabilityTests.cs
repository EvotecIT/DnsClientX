using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects the boundary between compatible SVCB bindings and executable DDR configurations.</summary>
public class DnsDesignatedResolverCapabilityTests {
    private static readonly IPAddress AlternateAddress = IPAddress.Parse("127.0.0.2");

    /// <summary>HTTP/3 cannot substitute its URI authority to work around an unsupported alternate dial address, including POST.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Http3AlternateAddressCannotCreateConfiguration(bool usePost) {
        DnsDesignatedResolver candidate = CreateVerifiedCandidate("h3", AlternateAddress);
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => candidate.CreateConfiguration(usePost));
        Assert.Contains("HTTP/3", exception.Message);
        Assert.Contains("original", exception.Message);
    }

    /// <summary>A real UDP advertisement exposes unsupported HTTP/3 routing before attempting native verification; inspection remains unverified.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UdpDiscoveryReportsHttp3AlternateAddressCapability(bool verify) {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var registration = stop.Token.Register(server.Dispose);
        Task reply = Task.Run(async () => {
            UdpReceiveResult query = await server.ReceiveAsync();
            Assert.True(DnsWireMessageParser.TryParseQuestion(query.Buffer, 0, out DnsWireQuestionInfo question));
            Assert.Equal("_dns.resolver.arpa", question.Name.TrimEnd('.'));
            byte[] response = DnsDiscoveryTestWire.Response(query.Buffer,
                DnsDiscoveryTestWire.Svcb("_dns.resolver.arpa", 1, "resolver.example", "h3", 443, "/q{?dns}", AlternateAddress));
            await server.SendAsync(response, response.Length, query.RemoteEndPoint);
        });
        using var client = new ClientX(new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
            Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port,
            TimeOut = 2000,
            UseTcpFallback = false
        });
        DnsDesignatedResolver candidate = Assert.Single(await client.DiscoverDesignatedResolversAsync(
            new DnsDesignatedResolverDiscoveryOptions { Verify = verify, VerificationTimeout = TimeSpan.FromMilliseconds(100) }, stop.Token));
        await reply;
        Assert.Equal("h3", candidate.Binding.Alpn);
        Assert.Equal(DnsRequestFormat.DnsOverHttp3, candidate.Binding.RequestFormat);
        Assert.Equal(IPAddress.Loopback, candidate.OriginalResolverAddress);
        Assert.Equal(AlternateAddress, candidate.ConnectionAddress);
        Assert.Equal(verify ? DnsDesignatedResolverVerification.Unsupported : DnsDesignatedResolverVerification.NotAttempted, candidate.Verification);
        if (verify) {
            Assert.Contains("HTTP/3", candidate.VerificationError);
            Assert.Contains("original", candidate.VerificationError);
        } else {
            Assert.Null(candidate.VerificationError);
        }
        Assert.Throws<InvalidOperationException>(() => candidate.CreateConfiguration());
        Assert.Equal(DnsRequestFormat.DnsOverUDP, client.EndpointConfiguration.RequestFormat);
    }

    /// <summary>Same-address HTTP/3 retains its original HTTPS authority without requesting an alternate socket route.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Http3SameAddressRetainsOriginalAuthority(bool usePost) {
        Configuration configuration = CreateVerifiedCandidate("h3", IPAddress.Loopback).CreateConfiguration(usePost);
        Assert.Equal("127.0.0.1", configuration.BaseUri!.Host);
        Assert.Equal("https://127.0.0.1:443/q{?dns}", configuration.DohUriTemplate);
        Assert.Equal(new Version(3, 0), configuration.HttpVersion);
        Assert.Equal(usePost ? DnsRequestFormat.DnsOverHttpsWirePost : DnsRequestFormat.DnsOverHttp3, configuration.RequestFormat);
        Assert.Null(configuration.ConnectionAddress);
    }

    /// <summary>Supported HTTP alternate routes retain the original authority and protocol for GET and POST; legacy targets reject that route explicitly.</summary>
    [Theory]
    [InlineData("h2", false)]
    [InlineData("h2", true)]
    [InlineData("http/1.1", false)]
    [InlineData("http/1.1", true)]
    public void HttpAlternateAddressRetainsOriginalAuthority(string alpn, bool usePost) {
        DnsDesignatedResolver candidate = CreateVerifiedCandidate(alpn, AlternateAddress);
#if NET8_0_OR_GREATER
        Configuration configuration = candidate.CreateConfiguration(usePost);
        Assert.Equal("127.0.0.1", configuration.BaseUri!.Host);
        Assert.Equal("https://127.0.0.1:443/q{?dns}", configuration.DohUriTemplate);
        Assert.Equal(alpn == "h2" ? new Version(2, 0) : new Version(1, 1), configuration.HttpVersion);
        Assert.Equal(usePost ? DnsRequestFormat.DnsOverHttpsWirePost : DnsRequestFormat.DnsOverHttps, configuration.RequestFormat);
        Assert.Equal(AlternateAddress, configuration.ConnectionAddress);
#else
        Assert.Throws<NotSupportedException>(() => candidate.CreateConfiguration(usePost));
#endif
    }

    /// <summary>DoQ already separates the QUIC dial endpoint from the original TLS identity and keeps alternate addresses usable.</summary>
    [Fact]
    public void DoqAlternateAddressRetainsOriginalTlsIdentity() {
        Configuration configuration = CreateVerifiedCandidate("doq", AlternateAddress).CreateConfiguration();
        Assert.Equal(DnsRequestFormat.DnsOverQuic, configuration.RequestFormat);
        Assert.Equal("127.0.0.1", configuration.Hostname);
        Assert.Equal("127.0.0.1", configuration.TlsServerName);
        Assert.Equal(AlternateAddress, configuration.ConnectionAddress);
    }

    private static DnsDesignatedResolver CreateVerifiedCandidate(string alpn, IPAddress connectionAddress) {
        string recordText = "1 resolver.example alpn=" + alpn + (alpn == "doq" ? string.Empty : " dohpath=/q{?dns}");
        var record = Assert.IsType<SvcbRecord>(new DnsAnswer { Type = DnsRecordType.SVCB, DataRaw = recordText }.TypedRecord);
        DnsServiceBinding binding = Assert.Single(DnsServiceBindingSelection.SelectDnsResolvers("_dns.resolver.arpa", new[] { record }));
        return new DnsDesignatedResolver(IPAddress.Loopback, 53, binding, connectionAddress,
            DateTimeOffset.UtcNow.AddMinutes(1), Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency + 60,
            Array.Empty<X509Certificate2>(), X509RevocationMode.NoCheck) {
            Verification = DnsDesignatedResolverVerification.Verified
        };
    }
}
