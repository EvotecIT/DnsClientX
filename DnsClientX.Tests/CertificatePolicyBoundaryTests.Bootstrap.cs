#if NET8_0_OR_GREATER
using System.Net;
using System.Reflection;

namespace DnsClientX.Tests;

public sealed partial class CertificatePolicyBoundaryTests {
    /// <summary>The certificate decision belongs to the complete query, including retries.</summary>
    [Fact]
    public async Task RetryingQueryRetainsOriginalCertificatePolicy() {
        await using var server = new UntrustedHttpsServer(gateHandshake: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new ClientX(server.Configuration());
        Task<DnsResponse> query = client.Resolve("tls-policy.example", maxRetries: 2, retryDelayMs: 0, cancellationToken: deadline.Token);
        await server.HandshakeEntered.WaitAsync(deadline.Token);
        client.IgnoreCertificateErrors = true;
        server.ReleaseHandshake();
        Assert.Equal(DnsResponseCode.ServerFailure, (await query).Status);
        Assert.Equal(0, server.RequestCount);
    }

    /// <summary>Bootstrap completion cannot create a new transport after either disposal route.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalDuringBootstrapDoesNotRecreateHttpTransport(bool asynchronously) {
        DnsServerResolver.ResetForTests();
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer { GateBootstrap = true };
        await using var server = new UntrustedHttpsServer();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new ClientX(server.Configuration("endpoint.bootstrap.invalid", bootstrap.Endpoint()), ignoreCertificateErrors: true);
        Task<DnsResponse> query = client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        await bootstrap.Entered.Task.WaitAsync(deadline.Token);
        if (asynchronously) await client.DisposeAsync(); else client.Dispose();
        bootstrap.Release.TrySetResult(true);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => query);
        Assert.Equal(0, ManagedClientCount(client));
        Assert.Equal(0, server.RequestCount);
        DnsServerResolver.ResetForTests();
    }

    /// <summary>Unicode authorities dial their equivalent ASCII name without changing HTTP Host or SNI.</summary>
    [Theory]
    [InlineData("endpoint.bootstrap.bücher.invalid")]
    [InlineData("endpoint.bootstrap.xn--bcher-kva.invalid")]
    public async Task InternationalBootstrapAuthorityConnects(string hostname) {
        DnsServerResolver.ResetForTests();
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer();
        await using var server = new UntrustedHttpsServer();
        Configuration configuration = server.Configuration(hostname, bootstrap.Endpoint());
        using var client = new ClientX(configuration, ignoreCertificateErrors: true);
        var response = await client.Resolve("tls-policy.example", retryOnTransient: false);
        Assert.Equal(DnsResponseCode.NoError, response.Status);
        Assert.Equal(DnsWireNameCodec.Canonical(configuration.BaseUri!.IdnHost), DnsWireNameCodec.Canonical(server.ServerName!));
        Assert.StartsWith(configuration.BaseUri.IdnHost + ":", server.HttpHost);
        DnsServerResolver.ResetForTests();
    }

    /// <summary>The provider-specific JSON update/delete lane uses the same explicit bootstrap path.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonAuthorityOperationPreparesBootstrap(bool delete) {
        DnsServerResolver.ResetForTests();
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer();
        await using var server = new UntrustedHttpsServer();
        Configuration configuration = server.Configuration("endpoint.bootstrap.invalid", bootstrap.Endpoint());
        configuration.RequestFormat = DnsRequestFormat.DnsOverHttpsJSONPOST;
        using var client = new ClientX(configuration, ignoreCertificateErrors: true);
        var response = delete ? await client.DeleteRecordAsync("example.com", "www.example.com", DnsRecordType.A)
            : await client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "192.0.2.1");
        Assert.Equal(DnsResponseCode.NoError, response.Status);
        Assert.Equal("127.0.0.1", response.ServerResolution!.Address);
        Assert.Equal(1, bootstrap.BootstrapQueries);
        Assert.Equal(1, server.RequestCount);
        DnsServerResolver.ResetForTests();
    }

    /// <summary>Rotating bootstrap addresses cannot retain unlimited idle pools or close a leased request.</summary>
    [Fact]
    public async Task BootstrapAddressChurnBoundsIdleClientsWithoutRetiringActiveQuery() {
        DnsServerResolver.ResetForTests();
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer { Ttl = 0 };
        await using var server = new UntrustedHttpsServer(gated: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var client = new ClientX(server.Configuration("endpoint.bootstrap.invalid", bootstrap.Endpoint()), ignoreCertificateErrors: true);
        client.EndpointConfiguration.TimeOut = 10000;
        client.EndpointConfiguration.DnsServerResolutionAllowStale = false;
        Task<DnsResponse> original = client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        await server.Entered.WaitAsync(deadline.Token);
        try {
            for (int index = 2; index <= 14; index++) {
                bootstrap.BootstrapAddress = IPAddress.Parse($"127.0.0.{index}");
                Configuration snapshot = client.EndpointConfiguration.CreateQuerySnapshot();
                var address = await DnsServerResolver.ResolveAsync(snapshot.BaseUri!.IdnHost, snapshot, deadline.Token);
                Assert.Equal(bootstrap.BootstrapAddress, address.Address);
                // Stop at pool selection so a blackholed test address does not add dial latency.
                typeof(ClientX).GetMethod("GetClient", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(client, new object?[] { snapshot, true });
            }
            Assert.InRange(ManagedClientCount(client), 1, 9);
        } finally { server.Release(); }
        Assert.Equal(DnsResponseCode.NoError, (await original).Status);
        Assert.InRange(ManagedClientCount(client), 1, 8);
        DnsServerResolver.ResetForTests();
    }

    /// <summary>A configured HTTP version cannot use QUIC outside the selected bootstrap path.</summary>
    [Fact]
    public async Task ExplicitHttp3VersionRejectsBootstrapBeforeDialing() {
        DnsServerResolver.ResetForTests();
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer();
        await using var server = new UntrustedHttpsServer();
        Configuration configuration = server.Configuration("endpoint.bootstrap.invalid", bootstrap.Endpoint());
        configuration.HttpVersion = HttpVersion.Version30;
        using var client = new ClientX(configuration, ignoreCertificateErrors: true);
        await Assert.ThrowsAsync<NotSupportedException>(() => client.Resolve("tls-policy.example", retryOnTransient: false));
        Assert.Equal(0, bootstrap.BootstrapQueries);
        Assert.Equal(0, server.RequestCount);
        DnsServerResolver.ResetForTests();
    }

    private static int ManagedClientCount(ClientX client) => ((IReadOnlyCollection<System.Net.Http.HttpClient>)typeof(ClientX)
        .GetField("_managedClients", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!).Count;
}
#endif
