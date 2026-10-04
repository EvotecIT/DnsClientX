#if NET8_0_OR_GREATER
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace DnsClientX.Tests;

/// <summary>Exercises explicit bootstrap through real DNS packets, independent of system DNS.</summary>
[Collection("NoParallel")]
public sealed class BootstrapResolverTests : IDisposable {
    /// <summary>Isolates the shared endpoint address cache.</summary>
    public BootstrapResolverTests() => DnsServerResolver.ResetForTests();
    /// <summary>Restores the default system lookup after each contract test.</summary>
    public void Dispose() => DnsServerResolver.ResetForTests();

    /// <summary>The same core option resolves UDP/TCP bootstrap addresses and retains endpoint provenance.</summary>
    [Theory]
    [InlineData(Transport.Udp)]
    [InlineData(Transport.Tcp)]
    public async Task QueryUsesExplicitBootstrapWithoutSystemLookup(Transport bootstrapTransport) {
        int systemCalls = 0;
        DnsServerResolver.ResolveHostAddressesAsync = _ => { systemCalls++; throw new InvalidOperationException("System DNS must not be used."); };
        await using var server = new BootstrapDnsServer();
        var configuration = new Configuration("endpoint.bootstrap.invalid", DnsRequestFormat.DnsOverUDP) {
            Port = server.Port, TimeOut = 1000, BootstrapResolver = server.Endpoint(bootstrapTransport)
        };
        using var client = new ClientX(configuration);
        DnsResponse response = await client.Resolve("payload.example", retryOnTransient: false);
        Assert.Equal(DnsResponseCode.NoError, response.Status);
        Assert.Equal("192.0.2.80", Assert.Single(response.Answers).Data);
        Assert.Equal(0, systemCalls);
        Assert.Equal("endpoint.bootstrap.invalid", response.ServerResolution!.Hostname);
        Assert.Equal("127.0.0.1", response.ServerResolution.Address);
        Assert.StartsWith(bootstrapTransport.ToString().ToLowerInvariant() + "@", response.ServerResolution.BootstrapResolver);
        Assert.False(response.ServerResolution.UsedStaleAddress);
        var cached = await client.Resolve("payload.example", retryOnTransient: false);
        Assert.True(cached.ServerResolution!.ServedFromCache);
        Assert.Equal(1, server.BootstrapQueries);
    }

    /// <summary>Resolver identities partition address cache entries, and a zero wire TTL forces refresh.</summary>
    [Fact]
    public async Task BootstrapCacheHonorsResolverAndWireTtl() {
        await using var first = new BootstrapDnsServer { BootstrapAddress = IPAddress.Parse("192.0.2.1") };
        await using var second = new BootstrapDnsServer { BootstrapAddress = IPAddress.Parse("192.0.2.2"), Ttl = 0 };
        var configuration = new Configuration("endpoint.bootstrap.invalid", DnsRequestFormat.DnsOverUDP) {
            TimeOut = 1000, BootstrapResolver = first.Endpoint(), DnsServerResolutionAllowStale = false
        };
        var a = await DnsServerResolver.ResolveAsync("endpoint.bootstrap.invalid", configuration, default);
        configuration.BootstrapResolver = second.Endpoint();
        var b = await DnsServerResolver.ResolveAsync("endpoint.bootstrap.invalid", configuration, default);
        second.BootstrapAddress = IPAddress.Parse("192.0.2.3");
        var refreshed = await DnsServerResolver.ResolveAsync("endpoint.bootstrap.invalid", configuration, default);
        Assert.Equal("192.0.2.1", a.Address!.ToString());
        Assert.Equal("192.0.2.2", b.Address!.ToString());
        Assert.Equal("192.0.2.3", refreshed.Address!.ToString());
        Assert.Equal(2, second.BootstrapQueries);
    }

    /// <summary>An unrelated address preceding the requested RRset cannot become the resolver address.</summary>
    [Fact]
    public async Task BootstrapIgnoresUnrelatedAnswerOwners() {
        await using var server = new BootstrapDnsServer { IncludeUnrelatedAddress = true };
        var result = await DnsBootstrapResolver.ResolveAsync("endpoint.bootstrap.invalid", server.Endpoint(), 1000, null);
        Assert.Equal(IPAddress.Loopback, Assert.Single(result.Addresses));
    }

    /// <summary>Explicit bootstrap failures remain failures instead of consulting the operating system.</summary>
    [Fact]
    public async Task BootstrapFailureDoesNotFallBackToSystemDns() {
        int systemCalls = 0;
        DnsServerResolver.ResolveHostAddressesAsync = _ => { systemCalls++; return Task.FromResult(new[] { IPAddress.Loopback }); };
        await using var server = new BootstrapDnsServer { FailBootstrap = true };
        var configuration = new Configuration("endpoint.bootstrap.invalid", DnsRequestFormat.DnsOverUDP) {
            TimeOut = 1000, BootstrapResolver = server.Endpoint(), DnsServerResolutionAllowStale = false
        };
        var result = await DnsServerResolver.ResolveAsync("endpoint.bootstrap.invalid", configuration, default);
        Assert.Null(result.Address);
        Assert.NotEmpty(result.Error!);
        Assert.Equal(0, systemCalls);
        Assert.Equal(result.Error, configuration.ServerResolution!.Error);
        using var client = new ClientX(new Configuration(new Uri("https://endpoint.bootstrap.invalid/dns-query"), DnsRequestFormat.DnsOverHttps) {
            TimeOut = 1000, BootstrapResolver = server.Endpoint(), DnsServerResolutionAllowStale = false
        });
        var response = await client.Resolve("payload.example", retryOnTransient: false);
        Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        Assert.Equal(result.Error, response.Error);
        Assert.NotNull(response.ServerResolution);
        Assert.NotEqual(DnsQueryErrorCode.None, result.ErrorCode);
        Assert.Equal(result.ErrorCode, response.ErrorCode);
        Assert.Same(result.Exception, response.Exception);
    }

    /// <summary>A caller can cancel its wait while another caller receives the shared bootstrap result.</summary>
    [Fact]
    public async Task BootstrapCancellationDoesNotCancelAnotherWaiter() {
        await using var server = new BootstrapDnsServer { GateBootstrap = true };
        var configuration = new Configuration("endpoint.bootstrap.invalid", DnsRequestFormat.DnsOverUDP) { TimeOut = 1000, BootstrapResolver = server.Endpoint() };
        using var cancel = new CancellationTokenSource();
        var canceled = DnsServerResolver.ResolveAsync("endpoint.bootstrap.invalid", configuration, cancel.Token);
        await server.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var remaining = DnsServerResolver.ResolveAsync("endpoint.bootstrap.invalid", configuration, default);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await canceled);
        server.Release.TrySetResult(true);
        Assert.Equal(IPAddress.Loopback, (await remaining).Address);
        Assert.Equal(1, server.BootstrapQueries);
    }

    /// <summary>NSID is requested on the wire and returned as lossless bytes and hexadecimal metadata.</summary>
    [Theory]
    [InlineData(false, "6E6F64652D31", "node-1")]
    [InlineData(true, "00FF80", null)]
    public async Task NsidRequestAndResponsePreserveOpaqueBytes(bool binary, string hex, string? text) {
        await using var server = new BootstrapDnsServer { Nsid = binary ? new byte[] { 0, 255, 128 } : System.Text.Encoding.ASCII.GetBytes("node-1") };
        var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
            Port = server.Port, TimeOut = 1000, EdnsOptions = new EdnsOptions { RequestNsid = true }
        };
        using var client = new ClientX(configuration);
        var response = await client.Resolve("payload.example", retryOnTransient: false);
        Assert.True(server.SawNsidRequest);
        Assert.Equal(server.Nsid, response.EdnsNsid);
        Assert.Equal(hex, response.EdnsNsidHex);
        Assert.Equal(text, response.EdnsNsidText);
        string json = DnsClientXJsonSerializer.Serialize(response);
        Assert.Contains($"\"edns_nsid_hex\": \"{hex}\"", json);
    }

    internal sealed class BootstrapDnsServer : IAsyncDisposable {
        private readonly UdpClient _udp;
        private readonly TcpListener _tcp;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _udpLoop;
        private readonly Task _tcpLoop;
        internal int Port => ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        internal IPAddress BootstrapAddress { get; set; } = IPAddress.Loopback;
        internal int Ttl { get; set; } = 60;
        internal int BootstrapQueries { get; private set; }
        internal bool FailBootstrap { get; set; }
        internal bool GateBootstrap { get; set; }
        internal bool IncludeUnrelatedAddress { get; set; }
        internal byte[]? Nsid { get; set; }
        internal bool SawNsidRequest { get; private set; }
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal BootstrapDnsServer() {
            (_udp, _tcp) = BindServer();
            _udpLoop = UdpLoopAsync();
            _tcpLoop = TcpLoopAsync();
        }

        private static (UdpClient Udp, TcpListener Tcp) BindServer() {
            // A TCP-selected ephemeral port need not be available to UDP. Reserve UDP first,
            // retaining rejected candidates so the allocator cannot repeatedly select the same port.
            var rejected = new List<UdpClient>();
            SocketException? lastFailure = null;
            try {
                for (int attempt = 0; attempt < 16; attempt++) {
                    var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                    int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
                    var tcp = new TcpListener(IPAddress.Loopback, port);
                    try {
                        tcp.Start();
                        return (udp, tcp);
                    } catch (SocketException exception) when (exception.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied) {
                        rejected.Add(udp);
                        lastFailure = exception;
                        tcp.Stop();
                    } catch {
                        udp.Dispose();
                        tcp.Stop();
                        throw;
                    }
                }
                throw new IOException("Could not reserve a local DNS port for both UDP and TCP.", lastFailure);
            } finally {
                foreach (var udp in rejected) {
                    udp.Dispose();
                }
            }
        }

        internal DnsResolverEndpoint Endpoint(Transport transport = Transport.Udp) => new() { Host = "127.0.0.1", Port = Port, Transport = transport };

        private async Task<byte[]> RespondAsync(byte[] query) {
            var reader = new DnsWireReader(query, 12);
            string name = reader.ReadName();
            ushort type = reader.ReadUInt16();
            bool bootstrap = name.StartsWith("endpoint.bootstrap.", StringComparison.Ordinal);
            if (bootstrap) {
                BootstrapQueries++;
                Entered.TrySetResult(true);
                if (GateBootstrap) await Release.Task.WaitAsync(_stop.Token);
            }
            byte[] question = TestUtilities.CreateResponseFromQuery(query, bootstrap && FailBootstrap ? (ushort)0x8183 : (ushort)0x8180);
            if (bootstrap && FailBootstrap) return question;
            byte[] address = (bootstrap ? BootstrapAddress : IPAddress.Parse("192.0.2.80")).GetAddressBytes();
            using var output = new MemoryStream();
            question[7] = (byte)(bootstrap && IncludeUnrelatedAddress ? 2 : 1);
            if (Nsid != null) question[11] = 1;
            output.Write(question);
            if (bootstrap && IncludeUnrelatedAddress) {
                byte[] owner = new byte[] { 5, (byte)'o', (byte)'t', (byte)'h', (byte)'e', (byte)'r', 7, (byte)'i', (byte)'n', (byte)'v', (byte)'a', (byte)'l', (byte)'i', (byte)'d', 0 };
                WriteAddress(output, owner, type, IPAddress.Parse("192.0.2.250").GetAddressBytes());
            }
            WriteAddress(output, new byte[] { 0xc0, 0x0c }, type, address);
            if (Nsid != null) {
                // The request OPT ends with the zero-length NSID option (code 3).
                SawNsidRequest = query.Length >= question.Length + 15 && query[^4] == 0 && query[^3] == 3 && query[^2] == 0 && query[^1] == 0;
                output.Write(new byte[] { 0, 0, 41, 16, 0, 0, 0, 0, 0, 0, (byte)(Nsid.Length + 4), 0, 3, 0, (byte)Nsid.Length });
                output.Write(Nsid);
            }
            return output.ToArray();
        }

        private void WriteAddress(Stream output, byte[] owner, ushort type, byte[] address) {
            output.Write(owner); output.Write(new byte[] { 0, (byte)type, 0, 1 });
            var ttl = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(ttl, Ttl); output.Write(ttl);
            output.Write(new byte[] { 0, (byte)address.Length }); output.Write(address);
        }

        private async Task UdpLoopAsync() {
            try {
                while (!_stop.IsCancellationRequested) {
                    var query = await _udp.ReceiveAsync(_stop.Token);
                    byte[] response = await RespondAsync(query.Buffer);
                    await _udp.SendAsync(response, query.RemoteEndPoint, _stop.Token);
                }
            } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }

        private async Task TcpLoopAsync() {
            try {
                while (!_stop.IsCancellationRequested) {
                    using var peer = await _tcp.AcceptTcpClientAsync(_stop.Token);
                    using var stream = peer.GetStream();
                    var length = new byte[2]; await stream.ReadExactlyAsync(length, _stop.Token);
                    var query = new byte[BinaryPrimitives.ReadUInt16BigEndian(length)]; await stream.ReadExactlyAsync(query, _stop.Token);
                    byte[] response = await RespondAsync(query);
                    BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)response.Length);
                    await stream.WriteAsync(length, _stop.Token); await stream.WriteAsync(response, _stop.Token);
                }
            } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
              catch (SocketException) when (_stop.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync() {
            _stop.Cancel();
            _tcp.Stop();
            await Task.WhenAll(_udpLoop, _tcpLoop);
            _udp.Dispose(); _stop.Dispose();
        }
    }
}
#endif
