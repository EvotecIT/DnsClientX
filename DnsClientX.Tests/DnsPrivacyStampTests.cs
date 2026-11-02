using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DnsClientX.DnsCrypt;
using DnsClientX.Odoh;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Provider configuration and external-format compatibility contracts.</summary>
public sealed class DnsPrivacyStampTests {
    // Independently encoded from the DNS stamps specification, not produced by DnsStamp.
    private const string Resolver = "sdns://AQUAAAAAAAAADjEyNy4wLjAuMTo1NDQzIAABAgMEBQYHCAkKCwwNDg8QERITFBUWFxgZGhscHR4fHDIuZG5zY3J5cHQtY2VydC5leGFtcGxlLnRlc3Q";
    private const string Relay = "sdns://gRJbMjAwMTpkYjg6OjFdOjU0NDM";
    private const string Target = "sdns://BQUAAAAAAAAADnRhcmdldC5leGFtcGxlCi9kbnMtcXVlcnk";
    private const string OdohRelay = "sdns://hQAAAAAAAAAAAAAScmVsYXkuZXhhbXBsZTo4NDQzBi9wcm94eQ";

    /// <summary>Verifies DnsCryptStampRetainsTrustedIdentityAndSeparateRelayDestination.</summary>
    [Fact]
    public void DnsCryptStampRetainsTrustedIdentityAndSeparateRelayDestination() {
        DnsPrivacyStamp parsed = DnsStamp.ParsePrivacy(Resolver);
        Assert.Equal(DnsPrivacyStampKind.DnsCrypt, parsed.Kind);
        Assert.Equal(5UL, parsed.Properties);
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, 5443), parsed.Address);
        Assert.Equal("2.dnscrypt-cert.example.test", parsed.ProviderName);
        Assert.Equal(Enumerable.Range(0, 32).Select(value => (byte)value), parsed.ProviderPublicKey);
        byte[] copy = parsed.ProviderPublicKey; copy[0] = 255;
        Assert.Equal(0, parsed.ProviderPublicKey[0]);
        var options = DnsCryptOptions.FromStamp(Resolver, Relay);
        Assert.Equal(new IPEndPoint(IPAddress.Parse("2001:db8::1"), 5443), options.RelayEndpoint);
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, 5443), options.ResolverEndpoint);
        Assert.Equal(parsed.ProviderPublicKey, options.ProviderPublicKey);
    }

    /// <summary>Verifies OdohStampPairPreservesTargetAndRelayRoles.</summary>
    [Fact]
    public void OdohStampPairPreservesTargetAndRelayRoles() {
        var options = OdohClientOptions.FromEndpoints(OdohRelay, Target);
        Assert.Equal(new Uri("https://relay.example:8443/proxy"), options.ProxyUri);
        Assert.Equal(new Uri("https://target.example/dns-query"), options.TargetUri);
        Assert.Throws<ArgumentException>(() => OdohClientOptions.FromEndpoints(Target, OdohRelay));
        Assert.Throws<ArgumentException>(() => DnsCryptOptions.FromStamp(Relay));
        Assert.Throws<ArgumentException>(() => DnsCryptOptions.FromStamp(Resolver, Target));
        Assert.Throws<ArgumentException>(() => new OdohTransportFactory().Create(Target));
    }

    /// <summary>The selected provider factory supplies a configuration that borrows the created provider.</summary>
    [Fact]
    public void ExplicitProviderLoaderCreatesBorrowedConfiguration() {
        using var transport = DnsWireTransportProviderLoader.Load(typeof(OdohClient).Assembly.Location, Target, OdohRelay);
        Assert.IsType<OdohClient>(transport);
        Configuration configuration = transport.CreateConfiguration();
        Assert.Same(transport, configuration.WireTransport);
        Assert.Equal(DnsRequestFormat.ObliviousDnsOverHttps, configuration.RequestFormat);
    }

    /// <summary>Verifies MalformedPrivacyStampsFailBeforeProviderCreation.</summary>
    [Theory]
    [InlineData("sdns://AQ")]
    [InlineData("sdns://gQsxMjcuMC4wLjE6MA")]
    [InlineData("sdns://BQUAAAAAAAAADnRhcmdldC5leGFtcGxlCi9kbnMtcXVlcnkA")]
    [InlineData("sdns://BQAAAAAAAAAAAf8KL2Rucy1xdWVyeQ")]
    [InlineData("sdns://AQAAAAAAAAAACTEyNy4wLjAuMR8AAQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHDIuZG5zY3J5cHQtY2VydC5leGFtcGxlLnRlc3Q")]
    public void MalformedPrivacyStampsFailBeforeProviderCreation(string stamp) {
        Assert.ThrowsAny<FormatException>(() => DnsStamp.ParsePrivacy(stamp));
    }

    /// <summary>Verifies OdohEndpointOptionsRejectInsecureOrAmbiguousRoutes.</summary>
    [Theory]
    [InlineData("http://relay.example/dns-query")]
    [InlineData("https://user:password@relay.example/dns-query")]
    [InlineData("https://relay.example/dns-query?target=other")]
    public void OdohEndpointOptionsRejectInsecureOrAmbiguousRoutes(string relay) {
        Assert.Throws<ArgumentException>(() => OdohClientOptions.FromEndpoints(relay, "https://target.example/dns-query"));
    }

    /// <summary>A relay certificate pin is rejected instead of becoming an unpinned HTTPS connection.</summary>
    [Fact]
    public void OdohRelayStampCannotSilentlyDiscardCertificatePins() {
        const string pinned = "sdns://hQAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA1yZWxheS5leGFtcGxlBi9wcm94eQ";
        Assert.True(DnsStamp.IsPrivacyStamp(pinned));
        Assert.Throws<NotSupportedException>(() => OdohClientOptions.FromEndpoints(pinned, Target));
    }

    /// <summary>The shared configured workflow accepts real ODoH exchanges and leaves its borrowed provider alive.</summary>
    [Fact]
    public async Task ConfiguredOdohWorkflowRetainsProviderOwnership() {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        using var transport = new OdohClient(OdohClientOptions.FromEndpoints("https://relay.example/dns-query", "https://target.example:8443/dns-query"), http);
        var result = await ResolverSingleOperationWorkflow.QueryConfiguredAsync(transport.CreateConfiguration(), "example.com", DnsRecordType.A,
            clientOptions: new ResolverExecutionClientOptions { EnableAudit = true });
        Assert.Equal("1.2.3.4", Assert.Single(result.Response.Answers).Data);
        Assert.Equal(DnsRequestFormat.ObliviousDnsOverHttps, result.RequestFormat);
        Assert.NotEmpty(result.AuditTrail);
        await transport.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query));
        Assert.Equal(2, target.QueryRequests);
    }
}
