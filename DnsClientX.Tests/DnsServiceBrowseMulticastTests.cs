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

    /// <summary>A loopback-scoped browser rejects unicast data from another interface while retaining real multicast announcements.</summary>
    [CrossInterfaceFact]
    public async Task SelectedInterfaceRejectsOffLinkUnicastAnnouncements() {
        IPAddress otherAddress = FindOtherInterfaceAddress()!;
        using var fixture = new MulticastFixture();
        using var client = fixture.CreateClient();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = client.BrowseServicesContinuousAsync("http", "tcp", cancellationToken: stop.Token).GetAsyncEnumerator();
        Task<bool> first = stream.MoveNextAsync().AsTask();
        try {
            await fixture.ReadQuery(stop.Token);
            // Leave the browse socket as the sole wildcard listener so unicast cannot be consumed by the fixture.
            fixture.Dispose();
            using var offLink = new UdpClient(AddressFamily.InterNetwork);
            offLink.ExclusiveAddressUse = false;
            offLink.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            offLink.Client.Bind(new IPEndPoint(otherAddress, fixture.Port));
            const string injectedInstance = "off-link._http._tcp.local";
            byte[] injected = DnsDiscoveryTestWire.Response(null,
                DnsDiscoveryTestWire.Ptr(Type, injectedInstance), DnsDiscoveryTestWire.Srv(injectedInstance, "off-link.local", 80));
            // The browser is wildcard-bound. Target the sender's real local interface
            // so strict host routing does not reject a non-loopback source to loopback.
            await offLink.SendAsync(injected, injected.Length, new IPEndPoint(otherAddress, fixture.Port));
            Assert.NotSame(first, await Task.WhenAny(first, Task.Delay(500, stop.Token)));

            using var local = new UdpClient(AddressFamily.InterNetwork);
            local.ExclusiveAddressUse = false;
            local.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            local.Client.Bind(new IPEndPoint(IPAddress.Loopback, fixture.Port));
            local.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.Loopback.GetAddressBytes());
            byte[] announcement = DnsDiscoveryTestWire.Response(null,
                DnsDiscoveryTestWire.Ptr(Type, Instance), DnsDiscoveryTestWire.Srv(Instance, "host.local", 80));
            await local.SendAsync(announcement, announcement.Length, new IPEndPoint(fixture.Group, fixture.Port));
            Assert.True(await first);
            Assert.Equal(Instance, stream.Current.Service.ServiceName);
        }
        finally {
            stop.Cancel();
            try { await first; } catch (OperationCanceledException) { }
        }
    }

    /// <summary>The snapshot accepts an on-link unicast reply and drains its binding at the deadline on every host.</summary>
    [Fact]
    public async Task SnapshotRetainsOnLinkReplyAndDrainsAtDeadline() {
        using var fixture = new MulticastFixture();
        using var client = fixture.CreateClient();
        client.EndpointConfiguration.TimeOut = 1000;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task<DnsResponse[]> query = client.ResolveMulticastAllAsync(Type, DnsRecordType.PTR, stop.Token);
        IPEndPoint destination = await fixture.ReadQuery(stop.Token);
        await fixture.Reply(destination, DnsDiscoveryTestWire.Ptr(Type, Instance));

        DnsResponse response = Assert.Single(await query);

        Assert.Equal(Instance, Assert.Single(response.Answers).Data);
        using var replacement = new UdpClient();
        replacement.Client.ExclusiveAddressUse = true;
        replacement.Client.Bind(destination);
    }

    /// <summary>A route-capable host delivers an off-link source to the snapshot socket, which must reject it.</summary>
    [CrossInterfaceRouteFact]
    public async Task SnapshotRejectsOffLinkUnicastAndRetainsOnLinkReply() {
        using var fixture = new MulticastFixture();
        using var client = fixture.CreateClient();
        client.EndpointConfiguration.TimeOut = 1000;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task<DnsResponse[]> query = client.ResolveMulticastAllAsync(Type, DnsRecordType.PTR, stop.Token);
        IPEndPoint destination = await fixture.ReadQuery(stop.Token);
        using var offLink = new UdpClient(AddressFamily.InterNetwork);
        offLink.ExclusiveAddressUse = false;
        offLink.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        offLink.Client.Bind(new IPEndPoint(FindOtherInterfaceAddress()!, fixture.Port));
        byte[] injected = DnsDiscoveryTestWire.Response(null, DnsDiscoveryTestWire.Ptr(Type, "off-link._http._tcp.local"));
        await offLink.SendAsync(injected, injected.Length, destination);
        await fixture.Reply(destination, DnsDiscoveryTestWire.Ptr(Type, Instance));
        DnsResponse response = Assert.Single(await query);
        Assert.Equal(Instance, Assert.Single(response.Answers).Data);
    }

    /// <summary>Local multicast is valid across address prefixes for both families, but the selected interface still bounds admission.</summary>
    [Theory]
    [InlineData("224.0.0.251", "192.0.2.10")]
    [InlineData("ff02::fb", "2001:db8::10")]
    public void ReceiveBoundaryPreservesOverlayMulticast(string groupText, string sourceText) {
        IPAddress group = IPAddress.Parse(groupText);
        var packet = new DnsWireResolveMulticast.ReceivedPacket(Array.Empty<byte>(),
            new IPEndPoint(IPAddress.Parse(sourceText), 5353), group, 17);
        Assert.True(DnsWireResolveMulticast.IsLocalResponse(packet, group, 5353, 17));
        Assert.False(DnsWireResolveMulticast.IsLocalResponse(packet, group, 5353, 18));
    }

    /// <summary>Unicast admission checks the source against the receive interface's configured subnet.</summary>
    [Theory]
    [InlineData("127.0.0.2", true)]
    [InlineData("192.0.2.10", false)]
    public void ReceiveBoundaryChecksUnicastSourceSubnet(string sourceText, bool expected) {
        int loopback = NetworkInterface.GetAllNetworkInterfaces().First(item => item.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            .GetIPProperties().GetIPv4Properties()!.Index;
        var packet = new DnsWireResolveMulticast.ReceivedPacket(Array.Empty<byte>(),
            new IPEndPoint(IPAddress.Parse(sourceText), 5353), IPAddress.Loopback, loopback);
        Assert.Equal(expected, DnsWireResolveMulticast.IsLocalResponse(packet, IPAddress.Parse("224.0.0.251"), 5353, loopback));
    }

    private static IPAddress? FindOtherInterfaceAddress() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(item => item.NetworkInterfaceType != NetworkInterfaceType.Loopback && item.OperationalStatus == OperationalStatus.Up
            && item.Supports(NetworkInterfaceComponent.IPv4))
        .SelectMany(item => item.GetIPProperties().UnicastAddresses)
        .Select(item => item.Address)
        .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));

    private sealed class CrossInterfaceFactAttribute : FactAttribute {
        public CrossInterfaceFactAttribute() {
            if (FindOtherInterfaceAddress() == null) {
                Skip = "Cross-interface admission proof requires an existing non-loopback IPv4 interface.";
            }
        }
    }

    private sealed class CrossInterfaceRouteFactAttribute : FactAttribute {
        public CrossInterfaceRouteFactAttribute() {
            IPAddress? source = FindOtherInterfaceAddress();
            if (source == null) {
                Skip = "Cross-interface admission proof requires an existing non-loopback IPv4 interface.";
                return;
            }

            using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            using var sender = new UdpClient(new IPEndPoint(source, 0));
            receiver.Client.ReceiveTimeout = 250;
            var destination = (IPEndPoint)receiver.Client.LocalEndPoint!;
            try {
                sender.Send(new byte[] { 1 }, 1, destination);
                IPEndPoint remote = new(IPAddress.Any, 0);
                receiver.Receive(ref remote);
                Assert.Equal(source, remote.Address);
            } catch (SocketException exception) when (exception.SocketErrorCode is SocketError.AddressNotAvailable
                or SocketError.TimedOut or SocketError.WouldBlock) {
                Skip = $"Host UDP route {source} -> {destination.Address} cannot deliver the off-link fixture: {exception.SocketErrorCode} ({exception.ErrorCode}).";
            }
        }
    }

    private sealed class MulticastFixture : IDisposable {
        private readonly UdpClient _socket = new(AddressFamily.InterNetwork);
        private readonly IPAddress _group = IPAddress.Parse("239.255.42.99");
        private readonly int _interfaceIndex;
        internal int Port { get; }
        internal IPAddress Group => _group;

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
        internal async Task<IPEndPoint> ReadQuery(CancellationToken token) {
            using CancellationTokenRegistration cancel = token.Register(_socket.Dispose);
            while (true) {
                UdpReceiveResult received = await _socket.ReceiveAsync();
                if (received.Buffer.Length >= 12 && (received.Buffer[2] & 0x80) == 0) {
                    return received.RemoteEndPoint;
                }
            }
        }

        internal Task Announce(params DnsDiscoveryTestWire.Record[] records) {
            byte[] response = DnsDiscoveryTestWire.Response(null, records);
            return _socket.SendAsync(response, response.Length, new IPEndPoint(_group, Port));
        }

        internal Task Reply(IPEndPoint destination, params DnsDiscoveryTestWire.Record[] records) {
            byte[] response = DnsDiscoveryTestWire.Response(null, records);
            return _socket.SendAsync(response, response.Length, destination);
        }

        public void Dispose() => _socket.Dispose();
    }
}
