using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Provider configuration and external-format compatibility contracts.</summary>
public sealed class ProviderTransportProfilesTests {
    /// <summary>Verifies PublishedSecureFormatsPreserveProfileAndTlsIdentity.</summary>
    [Theory]
    [InlineData(DnsEndpoint.Cloudflare, DnsRequestFormat.DnsOverTLS, "one.one.one.one", 853)]
    [InlineData(DnsEndpoint.CloudflareFamily, DnsRequestFormat.DnsOverTLS, "family.cloudflare-dns.com", 853)]
    [InlineData(DnsEndpoint.Google, DnsRequestFormat.DnsOverTLS, "dns.google", 853)]
    [InlineData(DnsEndpoint.CleanBrowsingSecurity, DnsRequestFormat.DnsOverTLS, "security-filter-dns.cleanbrowsing.org", 853)]
    [InlineData(DnsEndpoint.ControlDAds, DnsRequestFormat.DnsOverQuic, "p2.freedns.controld.com", 853)]
    [InlineData(DnsEndpoint.ControlDFamily, DnsRequestFormat.DnsOverTLS, "family.freedns.controld.com", 853)]
    [InlineData(DnsEndpoint.AdGuardFamily, DnsRequestFormat.DnsOverQuic, "family.adguard-dns.com", 853)]
    [InlineData(DnsEndpoint.Quad9ECS, DnsRequestFormat.DnsOverQuic, "dns11.quad9.net", 853)]
    public void PublishedSecureFormatsPreserveProfileAndTlsIdentity(DnsEndpoint provider, DnsRequestFormat format, string host, int port) {
        DnsResolverEndpoint endpoint = Assert.Single(DnsResolverEndpointFactory.From(provider, format));
        var configuration = new Configuration(provider, format);
        Assert.Equal(host, endpoint.Host);
        Assert.Equal(host, endpoint.TlsServerName);
        Assert.Equal(port, endpoint.Port);
        Assert.Equal(format, endpoint.RequestFormat);
        Assert.Equal(host, configuration.Hostname);
        Assert.Equal(host, configuration.TlsServerName);
        Assert.Equal(port, configuration.Port);
        Assert.Null(configuration.BaseUri);
        Assert.Contains(format, DnsResolverEndpointFactory.GetSupportedRequestFormats(provider));
    }

    /// <summary>Verifies PlainDnsAlternativesKeepFilteringAddressesAndDefaultDoh.</summary>
    [Fact]
    public void PlainDnsAlternativesKeepFilteringAddressesAndDefaultDoh() {
        var endpoints = DnsResolverEndpointFactory.From(DnsEndpoint.ControlDFamily, DnsRequestFormat.DnsOverTCP);
        Assert.Equal(new[] { "76.76.2.4", "76.76.10.4" }, endpoints.Select(endpoint => endpoint.Host));
        Assert.All(endpoints, endpoint => { Assert.Equal(Transport.Tcp, endpoint.Transport); Assert.Equal(53, endpoint.Port); });
        var defaults = new Configuration(DnsEndpoint.ControlDFamily);
        Assert.Equal(new Uri("https://freedns.controld.com/family"), defaults.BaseUri);
        Assert.Equal(DnsRequestFormat.DnsOverHttps, defaults.RequestFormat);
    }

    /// <summary>Google JSON uses its published resolve API while the default remains DNS wire format.</summary>
    [Fact]
    public void GoogleJsonAlternativeUsesTheResolvePath() {
        var configuration = new Configuration(DnsEndpoint.Google, DnsRequestFormat.DnsOverHttpsJSON);
        Assert.Equal(new Uri("https://dns.google/resolve"), configuration.BaseUri);
        Assert.Equal(DnsRequestFormat.DnsOverHttpsJSON, configuration.RequestFormat);
        Assert.Equal(new Uri("https://dns.google/dns-query"), new Configuration(DnsEndpoint.Google).BaseUri);
        Assert.Equal(new Uri("https://dns.google/resolve"), Assert.Single(DnsResolverEndpointFactory.From(DnsEndpoint.Google, DnsRequestFormat.DnsOverHttpsJSON)).DohUrl);
    }

    /// <summary>Verifies UnpublishedProviderFormatsFailExplicitly.</summary>
    [Theory]
    [InlineData(DnsEndpoint.Cloudflare, DnsRequestFormat.DnsOverQuic)]
    [InlineData(DnsEndpoint.Google, DnsRequestFormat.DnsOverQuic)]
    [InlineData(DnsEndpoint.DnsWatch, DnsRequestFormat.DnsOverTLS)]
    [InlineData(DnsEndpoint.ComodoSecureDns, DnsRequestFormat.DnsOverHttps)]
    [InlineData(DnsEndpoint.RootServer, DnsRequestFormat.DnsOverTCP)]
    public void UnpublishedProviderFormatsFailExplicitly(DnsEndpoint provider, DnsRequestFormat format) {
        Assert.Throws<NotSupportedException>(() => DnsResolverEndpointFactory.From(provider, format));
        Assert.Throws<NotSupportedException>(() => new Configuration(provider, format));
    }

    /// <summary>Verifies TargetExpansionUsesTheChosenTransportForEveryProvider.</summary>
    [Fact]
    public async Task TargetExpansionUsesTheChosenTransportForEveryProvider() {
        var targets = await ResolverExecutionTargetResolver.ResolveAsync(new ResolverExecutionTargetSource {
            BuiltInEndpoints = new[] { DnsEndpoint.AdGuard, DnsEndpoint.ControlDAds }, ProviderRequestFormat = DnsRequestFormat.DnsOverQuic
        });
        Assert.Equal(new[] { "dns.adguard-dns.com", "p2.freedns.controld.com" }, targets.Select(target => target.ExplicitEndpoint!.Host));
        Assert.All(targets, target => Assert.Equal(DnsRequestFormat.DnsOverQuic, target.ExplicitEndpoint!.RequestFormat));
        Assert.NotEqual(targets[0].CandidateId, targets[1].CandidateId);
    }

    /// <summary>Transport choices retain system policy and root iteration through the shared client boundary.</summary>
    [Theory]
    [InlineData(DnsEndpoint.System, DnsRequestFormat.DnsOverTCP, true, DnsEndpoint.SystemTcp)]
    [InlineData(DnsEndpoint.SystemTcp, DnsRequestFormat.DnsOverUDP, false, DnsEndpoint.System)]
    [InlineData(DnsEndpoint.RootServer, DnsRequestFormat.DnsOverUDP, true, DnsEndpoint.RootServer)]
    [InlineData(DnsEndpoint.RootServer, DnsRequestFormat.DnsOverUDP, false, DnsEndpoint.RootServer)]
    public async Task SpecialProfileTargetsPreservePolicyAndIteration(DnsEndpoint provider, DnsRequestFormat format,
        bool probe, DnsEndpoint expected) {
        var source = new ResolverExecutionTargetSource { ProviderRequestFormat = format };
        if (probe) { source.ProbeProfile = provider; }
        else { source.BuiltInEndpoints = new[] { provider }; }
        ResolverExecutionTarget target = Assert.Single(await ResolverExecutionTargetResolver.ResolveAsync(source));
        Assert.Equal(expected, target.BuiltInEndpoint);
        Assert.Null(target.ExplicitEndpoint);
        using var client = ResolverExecutionClientFactory.CreateClient(target);
        Assert.Equal(expected, client.EndpointConfiguration.BuiltInEndpoint);
        Assert.Equal(format, client.EndpointConfiguration.RequestFormat);
        if (provider != DnsEndpoint.RootServer) { Assert.NotNull(client.EndpointConfiguration.SystemDnsConfiguration); }
    }

    /// <summary>The UDP iterative engine cannot silently claim a TCP-only root benchmark.</summary>
    [Fact]
    public async Task RootTargetRejectsUnsupportedIterativeTcpOverride() {
        await Assert.ThrowsAsync<NotSupportedException>(() => ResolverExecutionTargetResolver.ResolveAsync(
            new ResolverExecutionTargetSource {
                BuiltInEndpoints = new[] { DnsEndpoint.RootServer }, ProviderRequestFormat = DnsRequestFormat.DnsOverTCP
            }));
    }

    /// <summary>Endpoint-list expansion cannot silently discard system policy or root iteration.</summary>
    [Theory]
    [InlineData(DnsEndpoint.System, false)]
    [InlineData(DnsEndpoint.SystemTcp, true)]
    [InlineData(DnsEndpoint.RootServer, true)]
    public void MultiResolverTransportOverridesRejectPolicyProfiles(DnsEndpoint provider, bool resolverList) {
        var request = new ResolveDnsRequest {
            Names = new[] { "example.com" }, ProviderRequestFormat = DnsRequestFormat.DnsOverUDP
        };
        if (resolverList) { request.ResolverDnsProviders = new[] { provider }; }
        else { request.DnsProviders = new[] { provider, DnsEndpoint.Cloudflare }; }
        Assert.Throws<NotSupportedException>(() => request.Validate());
    }
}
