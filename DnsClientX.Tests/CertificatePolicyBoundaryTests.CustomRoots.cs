#if NET8_0_OR_GREATER
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX.Tests;

public sealed partial class CertificatePolicyBoundaryTests {
    /// <summary>An HTTPS redirect must authenticate the redirected authority before sending its DNS query.</summary>
    [Fact]
    public async Task CustomRootsAuthenticateRedirectedAuthority() {
        using var fixture = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
        await using var server = new UntrustedHttpsServer(certificate: fixture.Leaf) { RedirectHostname = "localhost" };
        Configuration configuration = server.Configuration("127.0.0.1");
        configuration.TlsTrustAnchors = new[] { fixture.Root };
        configuration.TlsRevocationMode = X509RevocationMode.NoCheck;
        using var client = new ClientX(configuration);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        DnsResponse response = await client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        Assert.Empty(response.Answers);
        Assert.Equal(1, server.RequestCount);
    }

    /// <summary>A pooled custom-root handler authenticates each request's actual authority, including endpoint changes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomRootsCannotAuthenticateAnotherAuthorityWithFirstEndpointsCertificate(bool explicitAddress) {
        using var fixture = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
        await using var server = new UntrustedHttpsServer(certificate: fixture.Leaf);
        Configuration configuration = server.Configuration("127.0.0.1");
        if (explicitAddress) configuration.ConnectionAddress = IPAddress.Loopback;
        using var client = new ClientX(configuration);
        // Assigning trust after construction makes the first query create its policy pool from a query snapshot.
        configuration.TlsTrustAnchors = new[] { fixture.Root };
        configuration.TlsRevocationMode = X509RevocationMode.NoCheck;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        DnsResponse first = await client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.NoError, first.Status);
        Assert.Equal(1, server.RequestCount);

        Uri original = configuration.BaseUri!;
        configuration.BaseUri = new UriBuilder(original) { Host = "localhost" }.Uri;
        DnsResponse otherAuthority = await client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.ServerFailure, otherAuthority.Status);
        Assert.Empty(otherAuthority.Answers);
        Assert.Equal(1, server.RequestCount);

        configuration.BaseUri = original;
        Assert.Equal(DnsResponseCode.NoError, (await client.Resolve("tls-policy.example", retryOnTransient: false,
            cancellationToken: deadline.Token)).Status);
        Assert.Equal(2, server.RequestCount);
    }

    /// <summary>Changing caller-owned trust inputs cannot change an eager handler's in-flight authentication policy.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomRootHandshakeRetainsCapturedTrustPolicy(bool explicitAddress) {
        using var fixture = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
        using var other = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
        await using var server = new UntrustedHttpsServer(gateHandshake: true, certificate: fixture.Leaf);
        var roots = new[] { fixture.Root };
        Configuration configuration = server.Configuration("127.0.0.1");
        configuration.TlsTrustAnchors = roots;
        configuration.TlsRevocationMode = X509RevocationMode.NoCheck;
        if (explicitAddress) configuration.ConnectionAddress = IPAddress.Loopback;
        using var client = new ClientX(configuration);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Task<DnsResponse> query = client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        await server.HandshakeEntered.WaitAsync(deadline.Token);
        roots[0] = other.Root;
        configuration.TlsRevocationMode = X509RevocationMode.Online;
        server.ReleaseHandshake();
        Assert.Equal(DnsResponseCode.NoError, (await query).Status);
        Assert.Equal(1, server.RequestCount);

        DnsResponse subsequent = await client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.ServerFailure, subsequent.Status);
        Assert.Empty(subsequent.Answers);
        Assert.Equal(1, server.RequestCount);
    }
}
#endif
