using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX.DnsCrypt;

namespace DnsClientX.Tests;

/// <summary>Actual native encryption, certificate discovery, framing, lifetime and core provider binding.</summary>
public class DnsCryptTransportTests {
    /// <summary>Proves both native suites and real transports admit independent concurrent exchanges.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task NativeUdpTcpDiscoveryAndConcurrentQueriesUseDistinctKeysAndNonces(int system, bool tcp) {
        using var server = new DnsCryptTestServer((ushort)system) { SendInvalidUdpFirst = true };
        using var provider = new DnsCryptClient(Options(server, tcp));
        byte[][] answers = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => provider.QueryAsync(DnsCryptVectorsTests.Query)));
        Assert.All(answers, answer => Assert.Equal(DnsCryptVectorsTests.Response, answer));
        Assert.Equal(1, server.CertificateQueries);
        Assert.Equal(12, server.ApplicationQueries);
        Assert.Equal(12, server.QueryIdentities.Distinct().Count());
        Assert.Equal(DnsRequestFormat.DnsCrypt, provider.RequestFormat);
    }

    /// <summary>Retries authenticated UDP truncation through a newly encrypted TCP exchange.</summary>
    [Fact]
    public async Task AuthenticatedUdpTruncationRetriesFreshEncryptedPacketOverTcp() {
        using var server = new DnsCryptTestServer { TruncateUdp = true };
        using var provider = new DnsCryptClient(Options(server));
        DnsWireTransportResponse exchange = await provider.QueryExchangeAsync(DnsCryptVectorsTests.Query);
        Assert.Equal(DnsCryptVectorsTests.Response, exchange.Message);
        Assert.Equal(Transport.Tcp, exchange.Transport);
        Assert.Equal(2, server.ApplicationQueries);
        Assert.Equal(2, server.QueryIdentities.Distinct().Count());
        using var client = provider.CreateClient();
        DnsResponse response = await client.Resolve("www.example.com", DnsRecordType.A);
        Assert.Equal(Transport.Tcp, response.UsedTransport);
    }

    /// <summary>Concurrent UDP successes and TCP retries retain transport evidence for their own response.</summary>
    [Fact]
    public async Task ConcurrentFallbacksReturnPerExchangeTransportEvidence() {
        using var server = new DnsCryptTestServer { TruncateEvenUdp = true };
        using var provider = new DnsCryptClient(Options(server));
        byte[][] queries = Enumerable.Range(0, 12).Select(index => {
            byte[] query = (byte[])DnsCryptVectorsTests.Query.Clone(); query[1] = (byte)index; return query;
        }).ToArray();
        DnsWireTransportResponse[] responses = await Task.WhenAll(queries.Select(query => provider.QueryExchangeAsync(query)));
        for (int i = 0; i < responses.Length; i++) {
            Assert.Equal(i % 2 == 0 ? Transport.Tcp : Transport.Udp, responses[i].Transport);
            Assert.True(DnsCryptWire.Matches(queries[i], responses[i].Message));
        }
    }

    /// <summary>Uses the optional provider through the high-level client and checks relay destination bytes.</summary>
    [Fact]
    public async Task RelayFramingAndHighLevelClientKeepProviderIdentity() {
        using var server = new DnsCryptTestServer();
        var options = Options(server); options.RelayEndpoint = server.Endpoint;
        using var provider = new DnsCryptClient(options);
        using var client = provider.CreateClient();
        DnsResponse response = await client.Resolve("www.example.com", DnsRecordType.A);
        Assert.Equal("93.184.216.34", Assert.Single(response.Answers!).Data);
        Assert.Equal(DnsRequestFormat.DnsCryptRelay, provider.RequestFormat);
        Assert.Same(provider, client.EndpointConfiguration.WireTransport);
        byte[] wrapped = DnsCryptWire.WrapRelay(new byte[] { 1 }, new IPEndPoint(IPAddress.Parse("192.0.2.1"), 443));
        Assert.Equal(DnsCryptVectorsTests.Hex("ffffffffffffffff000000000000000000000000ffffc000020101bb01"), wrapped);
    }

    /// <summary>Certificate failure never sends an application question without encryption.</summary>
    [Fact]
    public async Task InvalidCertificatesNeverPermitPlaintextApplicationFallback() {
        using var server = new DnsCryptTestServer { CorruptCertificates = true };
        using var provider = new DnsCryptClient(Options(server));
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.QueryAsync(DnsCryptVectorsTests.Query));
        Assert.Equal(0, server.ApplicationQueries);
        Assert.Equal(2, server.CertificateQueries);
    }

    /// <summary>Refreshes accepted material and preserves only a still-valid cached certificate after failure.</summary>
    [Fact]
    public async Task ExplicitRefreshReplacesCertificateAndExpiredMaterialIsNeverUsed() {
        using var server = new DnsCryptTestServer();
        using var provider = new DnsCryptClient(Options(server));
        await provider.RefreshCertificatesAsync();
        server.Certificate = DnsCryptTestServer.SignedCertificate(2, 2);
        await provider.RefreshCertificatesAsync();
        Assert.Equal(2, server.CertificateQueries);
        server.Certificate = DnsCryptTestServer.SignedCertificate(2, 3, validSeconds: -1);
        // The still-valid previously cached certificate remains valid after a bad refresh.
        await provider.RefreshCertificatesAsync();
        Assert.Equal(DnsCryptVectorsTests.Response, await provider.QueryAsync(DnsCryptVectorsTests.Query));
        using var fresh = new DnsCryptClient(Options(server));
        await Assert.ThrowsAsync<InvalidDataException>(() => fresh.QueryAsync(DnsCryptVectorsTests.Query));
    }

    /// <summary>Canceled UDP work releases its endpoint before returning and leaves another query usable.</summary>
    [Fact]
    public async Task CancellationReturnsAfterUdpSocketIsReleasedAndOtherWaiterCompletes() {
        using var server = new DnsCryptTestServer { HoldApplications = true };
        using var provider = new DnsCryptClient(Options(server));
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> pending = provider.QueryAsync(DnsCryptVectorsTests.Query, cancellation.Token);
        IPEndPoint local = await Watch(server.UdpApplication.Task);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        using var socket = new UdpClient(local);
        server.HoldApplications = false;
        Assert.Equal(DnsCryptVectorsTests.Response, await provider.QueryAsync(DnsCryptVectorsTests.Query));
    }

    /// <summary>Provider disposal closes active TCP exchanges and rejects new work.</summary>
    [Fact]
    public async Task DisposalCancelsAndDrainsTcpExchangeAndRejectsNewQueries() {
        using var server = new DnsCryptTestServer { HoldApplications = true };
        var provider = new DnsCryptClient(Options(server, true));
        Task<byte[]> pending = provider.QueryAsync(DnsCryptVectorsTests.Query);
        await Watch(server.TcpApplication.Task);
        provider.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Watch(server.TcpClosed.Task);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.QueryAsync(DnsCryptVectorsTests.Query));
        provider.Dispose();
    }

    /// <summary>Filters invalid signed keys before applying suite preference and certificate serial ordering.</summary>
    [Fact]
    public void SignedLowOrderCertificateIsIgnoredWhileAnotherValidCertificateRemainsUsable() {
        byte[] invalid = DnsCryptTestServer.SignedCertificate(2, 20, new byte[32]);
        byte[] valid = DnsCryptTestServer.SignedCertificate(2, 2);
        Assert.Null(DnsCryptCertificate.Parse(invalid, DnsCryptVectorsTests.ProviderKey, DateTimeOffset.UtcNow));
        Assert.Equal((uint)2, DnsCryptCertificate.Select(new[] { invalid, valid }, DnsCryptVectorsTests.ProviderKey, DnsCryptEncryptionPolicy.RequireXChaCha20, DateTimeOffset.UtcNow).Serial);
        byte[] newer = DnsCryptTestServer.SignedCertificate(2, 3);
        byte[] salsa = DnsCryptTestServer.SignedCertificate(1, 50);
        Assert.Equal((uint)3, DnsCryptCertificate.Select(new[] { valid, salsa, newer }, DnsCryptVectorsTests.ProviderKey, DnsCryptEncryptionPolicy.PreferXChaCha20, DateTimeOffset.UtcNow).Serial);
        Assert.Equal((uint)50, DnsCryptCertificate.Select(new[] { valid, salsa, newer }, DnsCryptVectorsTests.ProviderKey, DnsCryptEncryptionPolicy.RequireXSalsa20, DateTimeOffset.UtcNow).Serial);
    }

    private static DnsCryptOptions Options(DnsCryptTestServer server, bool tcp = false) => new(server.Endpoint, "2.dnscrypt-cert.example.com", DnsCryptVectorsTests.ProviderKey) {
        NetworkTransport = tcp ? DnsCryptNetworkTransport.Tcp : DnsCryptNetworkTransport.UdpWithTcpFallback,
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static async Task<T> Watch<T>(Task<T> task) {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))));
        return await task;
    }
}
