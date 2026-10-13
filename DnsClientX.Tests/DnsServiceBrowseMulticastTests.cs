using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;
/// <summary>Exercises public continuous browse workflows through real loopback multicast sockets.</summary>
public class DnsServiceBrowseMulticastTests {
    private const string Type = "_http._tcp.local";
    private const string Instance = "site._http._tcp.local";
    /// <summary>A persistent receiver observes announcements and goodbyes even when no new snapshot query is issued.</summary>
    [Fact]
    public async Task PersistentBrowseYieldsAddedUpdatedAndRemoved() {
        using var fixture = new MulticastFixture();
        using var client = fixture.CreateClient();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = client.BrowseServicesContinuousAsync("http", "tcp", cancellationToken: stop.Token).GetAsyncEnumerator();
        Task<bool> first = stream.MoveNextAsync().AsTask();
        await fixture.ReadQuery(stop.Token);
        await fixture.Announce(DnsDiscoveryTestWire.Ptr(Type, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80), DnsDiscoveryTestWire.Txt(Instance, "path=/first"), DnsDiscoveryTestWire.Address("host.local", "192.0.2.30"));
        Assert.True(await first);
        Assert.Equal(DnsServiceBrowseChange.Added, stream.Current.Change);
        Assert.Equal("192.0.2.30", Assert.Single(stream.Current.Addresses).ToString());
        Task<bool> second = stream.MoveNextAsync().AsTask();
        await fixture.Announce(DnsDiscoveryTestWire.Txt(Instance, "path=/second"));
        Assert.True(await second);
        Assert.Equal(DnsServiceBrowseChange.Updated, stream.Current.Change);
        Assert.Equal("/second", stream.Current.Service.Metadata!["path"]);
        Task<bool> third = stream.MoveNextAsync().AsTask();
        await fixture.Announce(DnsDiscoveryTestWire.Ptr(Type, Instance, ttl: 0));
        Assert.True(await third);
        Assert.Equal(DnsServiceBrowseChange.Removed, stream.Current.Change);
        Assert.Equal(Instance, stream.Current.Service.ServiceName);
    }

    /// <summary>A real one-second PTR lifetime yields an expiry when the announcer goes silent.</summary>
    [Fact]
    public async Task PersistentBrowseExpiresWithoutFurtherReplies() {
        using var fixture = new MulticastFixture();
        using var client = fixture.CreateClient();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var stream = client.BrowseServicesContinuousAsync("http", "tcp", cancellationToken: stop.Token).GetAsyncEnumerator();
        Task<bool> first = stream.MoveNextAsync().AsTask();
        await fixture.ReadQuery(stop.Token);
        await fixture.Announce(DnsDiscoveryTestWire.Ptr(Type, Instance, ttl: 1), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80));
        Assert.True(await first);
        Assert.Equal(DnsServiceBrowseChange.Added, stream.Current.Change);
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(DnsServiceBrowseChange.Expired, stream.Current.Change);
    }

    /// <summary>Early disposal drains a receiver stalled by a bounded one-event buffer and releases its UDP binding.</summary>
    [Fact]
    public async Task EarlyDisposalDrainsBackpressureAndReleasesSocket() {
        var fixture = new MulticastFixture();
        int port = fixture.Port;
        using var client = fixture.CreateClient();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var stream = client.BrowseServicesContinuousAsync("http", "tcp", options: new DnsServiceBrowseOptions { EventBufferCapacity = 1 }, cancellationToken: stop.Token).GetAsyncEnumerator();
        try {
            Task<bool> first = stream.MoveNextAsync().AsTask();
            await fixture.ReadQuery(stop.Token);
            await fixture.Announce(DnsDiscoveryTestWire.Ptr(Type, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80), DnsDiscoveryTestWire.Txt(Instance, "step=0"));
            Assert.True(await first);
            for (int index = 1; index <= 8; index++) {
                await fixture.Announce(DnsDiscoveryTestWire.Txt(Instance, "step=" + index));
            }

            await Task.Delay(150, stop.Token);
            Task disposal = stream.DisposeAsync().AsTask();
            Assert.Same(disposal, await Task.WhenAny(disposal, Task.Delay(3000, stop.Token)));
            await disposal;
            fixture.Dispose();
            using var replacement = new UdpClient();
            replacement.Client.ExclusiveAddressUse = true;
            replacement.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        finally {
            await stream.DisposeAsync();
            fixture.Dispose();
        }
    }

    /// <summary>Client disposal cancels its owned persistent receiver and completes an idle reader after the socket has drained.</summary>
    [Fact]
    public async Task ClientDisposalEndsIdleBrowseAndReleasesSocket() {
        var fixture = new MulticastFixture();
        int port = fixture.Port;
        var client = fixture.CreateClient();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = client.BrowseServicesContinuousAsync("http", "tcp", cancellationToken: stop.Token).GetAsyncEnumerator();
        try {
            Task<bool> pending = stream.MoveNextAsync().AsTask();
            await fixture.ReadQuery(stop.Token);
            await client.DisposeAsync();
            Assert.False(await pending);
            fixture.Dispose();
            using var replacement = new UdpClient();
            replacement.Client.ExclusiveAddressUse = true;
            replacement.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        finally {
            client.Dispose();
            fixture.Dispose();
        }
    }

    /// <summary>The public receiver surfaces a real related-record overflow instead of swallowing it as a malformed packet.</summary>
    [Fact]
    public async Task PublicBrowseFailsExplicitlyAtRecordBound() {
        using var fixture = new MulticastFixture();
        using var client = fixture.CreateClient();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var stream = client.BrowseServicesContinuousAsync("http", "tcp", options: new DnsServiceBrowseOptions { MaximumRecords = 2 }, cancellationToken: stop.Token).GetAsyncEnumerator();
        Task<bool> pending = stream.MoveNextAsync().AsTask();
        await fixture.ReadQuery(stop.Token);
        await fixture.Announce(DnsDiscoveryTestWire.Ptr(Type, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80), DnsDiscoveryTestWire.Txt(Instance, "path=/"));
        DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(async () => await pending);
        Assert.Contains("MaximumRecords", exception.Message);
    }

    private sealed class MulticastFixture : IDisposable {
        private readonly UdpClient _socket = new(AddressFamily.InterNetwork);
        private readonly IPAddress _group = IPAddress.Parse("239.255.42.99");
        private readonly int _interfaceIndex;
        internal int Port { get; }

        internal MulticastFixture() {
            NetworkInterface loopback = NetworkInterface.GetAllNetworkInterfaces().First(item => item.NetworkInterfaceType == NetworkInterfaceType.Loopback);
            _interfaceIndex = loopback.GetIPProperties().GetIPv4Properties()!.Index;
            _socket.ExclusiveAddressUse = false;
            _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            Port = ((IPEndPoint)_socket.Client.LocalEndPoint!).Port;
            _socket.JoinMulticastGroup(_group, IPAddress.Loopback);
            _socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.Loopback.GetAddressBytes());
            _socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
            _socket.MulticastLoopback = true;
        }

        internal ClientX CreateClient() => new(new Configuration(_group.ToString(), DnsRequestFormat.Multicast) { Port = Port, MulticastInterfaceIndex = _interfaceIndex });
        internal async Task ReadQuery(CancellationToken token) {
            using CancellationTokenRegistration cancel = token.Register(_socket.Dispose);
            while (true) {
                UdpReceiveResult received = await _socket.ReceiveAsync();
                if (received.Buffer.Length >= 12 && (received.Buffer[2] & 0x80) == 0) {
                    return;
                }
            }
        }

        internal Task Announce(params DnsDiscoveryTestWire.Record[] records) {
            byte[] response = DnsDiscoveryTestWire.Response(null, records);
            return _socket.SendAsync(response, response.Length, new IPEndPoint(_group, Port));
        }

        public void Dispose() => _socket.Dispose();
    }
}
