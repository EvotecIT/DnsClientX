using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX.DnsCrypt;
using Xunit.Abstractions;

namespace DnsClientX.Tests;

/// <summary>Provider operation deadlines and the high-level client's timeout contract.</summary>
public class DnsCryptDeadlineTests {
    private readonly ITestOutputHelper _output;

    /// <summary>Records reached network stages without requiring them to beat the deadline.</summary>
    public DnsCryptDeadlineTests(ITestOutputHelper output) => _output = output;

    /// <summary>The default high-level deadline permits a timed-out UDP query to complete over TCP.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultHighLevelDeadlinePermitsUdpTimeoutAndTcpSuccess(bool useConfiguration) {
        using var server = new DnsCryptTestServer { DropUdpApplications = true };
        var options = new DnsCryptOptions(server.Endpoint, "2.dnscrypt-cert.example.com", DnsCryptVectorsTests.ProviderKey);
        using var provider = new DnsCryptClient(options);
        await provider.RefreshCertificatesAsync();
        using var client = useConfiguration ? new ClientX(provider.CreateConfiguration()) : provider.CreateClient();

        DnsResponse response = await client.Resolve("www.example.com", DnsRecordType.A, retryOnTransient: false);

        Assert.Equal("93.184.216.34", Assert.Single(response.Answers!).Data);
        Assert.Equal(Transport.Tcp, response.UsedTransport);
        Assert.Equal(TaskStatus.RanToCompletion, server.UdpApplication.Task.Status);
        Assert.Equal(TaskStatus.RanToCompletion, server.TcpApplication.Task.Status);
    }

    /// <summary>Discovery and application fallback share the deadline, which may expire in either stage under load.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveryCannotRestartTheOverallQueryDeadline(bool useByteArrayApi) {
        using var server = new DnsCryptTestServer { DropUdpCertificates = true, DropUdpApplications = true };
        var options = Options(server);
        options.Timeout = TimeSpan.FromMilliseconds(250);
        options.OverallTimeout = TimeSpan.FromMilliseconds(400);
        using var provider = new DnsCryptClient(options);

        Task pending = useByteArrayApi ? provider.QueryAsync(DnsCryptVectorsTests.Query) : provider.QueryExchangeAsync(DnsCryptVectorsTests.Query);
        // Completion may occur during discovery when the host is busy. Reaching the
        // application socket before the deadline is not part of the contract.
        await Task.WhenAny(pending, server.UdpApplication.Task);
        TimeoutException failure = await Watch(Assert.ThrowsAsync<TimeoutException>(() => pending));
        Assert.Contains("overall deadline", failure.Message);
        _output.WriteLine("Deadline stages: UDP discovery {0}; TCP discovery {1}; UDP application {2}; TCP application {3}.",
            server.UdpCertificate.Task.Status, server.TcpCertificate.Task.Status,
            server.UdpApplication.Task.Status, server.TcpApplication.Task.Status);

        Assert.False(server.TcpApplication.Task.IsCompleted);
        if (server.UdpApplication.Task.Status == TaskStatus.RanToCompletion) {
            using var released = new UdpClient(await server.UdpApplication.Task);
        }
    }

    /// <summary>Discovery, explicit refresh and refresh-gate waiters all honor their own overall deadline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverallDeadlineBoundsDiscoveryAndRefreshWaiters(bool refreshWaiter) {
        using var server = new DnsCryptTestServer { HoldCertificates = true };
        var options = Options(server); options.OverallTimeout = TimeSpan.FromMilliseconds(250);
        using var provider = new DnsCryptClient(options);
        Task refresh = provider.RefreshCertificatesAsync();
        IPEndPoint local = await Watch(server.UdpCertificate.Task);
        Task waiter = refreshWaiter ? provider.RefreshCertificatesAsync() : provider.QueryAsync(DnsCryptVectorsTests.Query);

        await Watch(Assert.ThrowsAsync<TimeoutException>(() => refresh));
        await Watch(Assert.ThrowsAsync<TimeoutException>(() => waiter));

        Assert.False(server.TcpCertificate.Task.IsCompleted);
        Assert.False(server.UdpApplication.Task.IsCompleted);
        using var released = new UdpClient(local);
    }

    /// <summary>Explicit high-level timeout and caller cancellation stop UDP without starting TCP fallback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerDeadlineAndCancellationTakePrecedence(bool cancelCaller) {
        using var server = new DnsCryptTestServer { DropUdpApplications = true };
        using var provider = new DnsCryptClient(Options(server));
        await provider.RefreshCertificatesAsync();
        Configuration configuration = provider.CreateConfiguration();
        if (!cancelCaller) configuration.TimeOut = 150;
        using var client = new ClientX(configuration);
        using var cancellation = new CancellationTokenSource();
        Task<DnsResponse> pending = client.Resolve("www.example.com", DnsRecordType.A, retryOnTransient: false, cancellationToken: cancellation.Token);
        if (cancelCaller) {
            await Watch(server.UdpApplication.Task);
            cancellation.Cancel();
        }

        await Watch(Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending));

        Assert.False(server.TcpApplication.Task.IsCompleted);
        if (server.UdpApplication.Task.Status == TaskStatus.RanToCompletion) {
            using var released = new UdpClient(await server.UdpApplication.Task);
        }
    }

    /// <summary>Overall budget snapshots, transport-derived defaults and millisecond limits are safe.</summary>
    [Fact]
    public void OverallTimeoutIsSnapshottedAndConversionIsBounded() {
        using var server = new DnsCryptTestServer();
        var options = Options(server);
        using (var udp = new DnsCryptClient(options)) Assert.Equal(40000, udp.CreateConfiguration().TimeOut);
        options.NetworkTransport = DnsCryptNetworkTransport.Tcp;
        using (var tcp = new DnsCryptClient(options)) Assert.Equal(20000, tcp.CreateConfiguration().TimeOut);
        options.Timeout = TimeSpan.FromMilliseconds(int.MaxValue);
        using (var maximum = new DnsCryptClient(options)) Assert.Equal(int.MaxValue, maximum.CreateConfiguration().TimeOut);
        options.OverallTimeout = TimeSpan.FromTicks(15001);
        using (var snapshot = new DnsCryptClient(options)) {
            options.OverallTimeout = TimeSpan.Zero;
            Assert.Equal(2, snapshot.CreateConfiguration().TimeOut);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new DnsCryptClient(options));
        options.OverallTimeout = TimeSpan.FromMilliseconds((long)int.MaxValue + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new DnsCryptClient(options));
    }

    private static DnsCryptOptions Options(DnsCryptTestServer server) => new(server.Endpoint,
        "2.dnscrypt-cert.example.com", DnsCryptVectorsTests.ProviderKey);

    private static async Task<T> Watch<T>(Task<T> task) {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(3))));
        return await task;
    }
}
