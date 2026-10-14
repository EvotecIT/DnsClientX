#if NET8_0_OR_GREATER
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace DnsClientX.Tests;

internal sealed class IsolatedLinuxNetworkFactAttribute : FactAttribute {
    public IsolatedLinuxNetworkFactAttribute() {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DNSCLIENTX_NETWORK_MANIFEST"))) {
            Skip = "Run qualify-linux-network.py in fresh Linux network/mount namespaces.";
        }
    }
}

/// <summary>Uses real OS resolver files, virtual interfaces and sockets, without discovery-provider overrides.</summary>
[Collection("NoParallel")]
public sealed class IsolatedLinuxNetworkTests {
    /// <summary>System clients honor search ordering and IPv6 resolver discovery, then observe explicit refresh after adapter/configuration changes.</summary>
    [IsolatedLinuxNetworkFact]
    public async Task SearchIpv6RefreshAndMulticastAdapterChanges() {
        Assert.True(OperatingSystem.IsLinux());
        using var manifest = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("DNSCLIENTX_NETWORK_MANIFEST")!));
        string resolverFile = manifest.RootElement.GetProperty("resolverFile").GetString()!;
        Assert.Equal(manifest.RootElement.GetProperty("networkNamespace").GetString(), new FileInfo("/proc/self/ns/net").LinkTarget);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var initial = SystemInformation.GetDnsConfiguration(refresh: true);
        Assert.Contains("10.53.1.1", initial.DnsServers);
        Assert.Equal(new[] { "missing.search.test", "first.search.test" }, initial.SearchDomains);
        using var client = new ClientX(timeOutMilliseconds: 2000, enableCache: false);
        {
            DnsResponse answer = await client.Resolve("host", retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.Contains(answer.Answers, record => record.Data == "192.0.2.53");
            Assert.Equal("host", answer.Questions[0].OriginalName);
            Assert.Equal("host.first.search.test", answer.Questions[0].Name.TrimEnd('.'));
            DnsResponse absolute = await client.Resolve("host.second.search.test.", retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.Contains(absolute.Answers, record => record.Data == "192.0.2.54");
        }
        File.WriteAllText(resolverFile, "nameserver fd53:1::1\nsearch first.search.test\noptions ndots:1\n");
        Assert.Contains("fd53:1::1", SystemInformation.GetDnsConfiguration(refresh: true).DnsServers);
        using (var ipv6 = new ClientX(timeOutMilliseconds: 2000, enableCache: false)) {
            DnsResponse answer = await ipv6.Resolve("host", DnsRecordType.AAAA, retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.Contains(answer.Answers, record => record.Data == "2001:db8::53");
            Assert.Equal("fd53:1::1", answer.ServerAddress);
        }
        await BrowseOnInterfaceAsync("dnsqual1", "10.53.1.1", deadline.Token);
        await BrowseOnInterfaceAsync("dnsqual1", "fd53:1::1", deadline.Token);
        using (var change = Process.Start(new ProcessStartInfo("ip") {
            ArgumentList = { "link", "set", "dnsqual1", "down" }, RedirectStandardError = true
        })!) {
            await change.WaitForExitAsync(deadline.Token);
            Assert.Equal(0, change.ExitCode);
        }
        File.WriteAllText(resolverFile, "nameserver 10.53.2.1\nsearch second.search.test\noptions ndots:1\n");
        var refreshed = SystemInformation.GetDnsConfiguration(refresh: true);
        Assert.Contains("10.53.2.1", refreshed.DnsServers);
        Assert.Equal(new[] { "second.search.test" }, refreshed.SearchDomains);
        client.EndpointConfiguration.RefreshSystemDns();
        {
            DnsResponse answer = await client.Resolve("host", retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.Contains(answer.Answers, record => record.Data == "192.0.2.54");
            Assert.Equal("10.53.2.1", answer.ServerAddress);
        }
        // Browsing is explicitly restarted on the newly selected interface after a network change.
        await BrowseOnInterfaceAsync("dnsqual2", "10.53.2.1", deadline.Token);
        await BrowseOnInterfaceAsync("dnsqual2", "fd53:2::1", deadline.Token);
    }

    private static async Task BrowseOnInterfaceAsync(string interfaceName, string source, CancellationToken token) {
        NetworkInterface network = Assert.Single(NetworkInterface.GetAllNetworkInterfaces(), item => item.Name == interfaceName);
        IPAddress address = IPAddress.Parse(source);
        bool ipv6 = address.AddressFamily == AddressFamily.InterNetworkV6;
        int index = ipv6 ? network.GetIPProperties().GetIPv6Properties()!.Index : network.GetIPProperties().GetIPv4Properties()!.Index;
        IPAddress group = IPAddress.Parse(ipv6 ? "ff02::fb" : "239.255.42.99");
        using var peer = new UdpClient(address.AddressFamily);
        peer.ExclusiveAddressUse = false;
        peer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        peer.Client.Bind(new IPEndPoint(ipv6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
        int port = ((IPEndPoint)peer.Client.LocalEndPoint!).Port;
        if (ipv6) {
            peer.JoinMulticastGroup(index, group);
            peer.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, index);
            peer.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, 255);
            group.ScopeId = index;
        } else {
            peer.JoinMulticastGroup(group, address);
            peer.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, address.GetAddressBytes());
            peer.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
        }
        peer.MulticastLoopback = true;
        await using var client = new ClientX(new Configuration(group.ToString(), DnsRequestFormat.Multicast) {
            Port = port, MulticastInterfaceIndex = index
        });
        await using var browse = client.BrowseServicesContinuousAsync("http", "tcp", cancellationToken: token).GetAsyncEnumerator();
        Task<bool> pending = browse.MoveNextAsync().AsTask();
        UdpReceiveResult query = await peer.ReceiveAsync(token);
        Assert.Contains(network.GetIPProperties().UnicastAddresses, item =>
            item.Address.GetAddressBytes().SequenceEqual(query.RemoteEndPoint.Address.GetAddressBytes()));
        string instance = interfaceName + "._http._tcp.local";
        byte[] response = DnsDiscoveryTestWire.Response(null, DnsDiscoveryTestWire.Ptr("_http._tcp.local", instance),
            DnsDiscoveryTestWire.Srv(instance, "host.local", 80));
        await peer.SendAsync(response, new IPEndPoint(group, port), token);
        Assert.True(await pending.WaitAsync(token));
        Assert.Equal(instance, browse.Current.Service.ServiceName);
        await browse.DisposeAsync();
        await client.DisposeAsync();
        peer.Dispose();
        using var replacement = new UdpClient();
        replacement.ExclusiveAddressUse = true;
        replacement.Client.Bind(new IPEndPoint(IPAddress.Any, port));
    }
}
#endif
