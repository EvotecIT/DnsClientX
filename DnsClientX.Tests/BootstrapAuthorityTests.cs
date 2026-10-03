#if NET8_0_OR_GREATER
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace DnsClientX.Tests;

/// <summary>Exercises the configured bootstrap path for authoritative operations using real local packets.</summary>
[Collection("NoParallel")]
public sealed class BootstrapAuthorityTests : IDisposable {
    /// <summary>Isolates the shared address cache and makes an accidental system lookup observable.</summary>
    public BootstrapAuthorityTests() {
        DnsServerResolver.ResetForTests();
        DnsServerResolver.ResolveHostAddressesAsync = _ => throw new InvalidOperationException("An explicit bootstrap operation cannot consult system DNS.");
    }
    /// <summary>Restores system lookup after each contract.</summary>
    public void Dispose() => DnsServerResolver.ResetForTests();

    /// <summary>Add, RRset delete, and exact-value delete all resolve the configured named authority.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task WireUpdatesHonorBootstrapAndProvenance(int operation) {
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer();
        await using var authority = new AuthorityServer();
        using var client = new ClientX(Configuration(authority.Port, bootstrap.Endpoint()));
        DnsResponse response = operation switch {
            0 => await client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "192.0.2.1"),
            1 => await client.DeleteRecordAsync("example.com", "www.example.com", DnsRecordType.A),
            _ => await client.DeleteRecordValueAsync("example.com", "www.example.com", DnsRecordType.A, "192.0.2.1")
        };
        Assert.Equal(DnsResponseCode.NoError, response.Status);
        Assert.Equal("127.0.0.1", response.ServerResolution!.Address);
        Assert.Equal(1, bootstrap.BootstrapQueries);
        await authority.Completion;
    }

    /// <summary>Both transfer forms resolve the authority on the caller's bootstrap route.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZoneTransfersHonorBootstrap(bool incremental) {
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer();
        await using var authority = new AuthorityServer();
        using var client = new ClientX(Configuration(authority.Port, bootstrap.Endpoint()));
        if (incremental) Assert.Equal(IncrementalZoneTransferKind.NoChange, (await client.IncrementalZoneTransferAsync("example.com", 7)).Kind);
        else Assert.True((await client.ZoneTransferAsync("example.com", retryOnTransient: false)).Last().IsClosing);
        Assert.Equal(1, bootstrap.BootstrapQueries);
        await authority.Completion;
    }

    private static Configuration Configuration(int port, DnsResolverEndpoint bootstrap) => new("endpoint.bootstrap.invalid", DnsRequestFormat.DnsOverTCP) {
        Port = port, TimeOut = 3000, BootstrapResolver = bootstrap
    };

    private sealed class AuthorityServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(10));
        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        internal Task Completion { get; }
        internal AuthorityServer() { _listener.Start(); Completion = ServeAsync(); }
        private async Task ServeAsync() {
            try {
                using TcpClient peer = await _listener.AcceptTcpClientAsync(_stop.Token);
                using NetworkStream stream = peer.GetStream();
                var prefix = new byte[2];
                await stream.ReadExactlyAsync(prefix, _stop.Token);
                var query = new byte[BinaryPrimitives.ReadUInt16BigEndian(prefix)];
                await stream.ReadExactlyAsync(query, _stop.Token);
                var reader = new DnsWireReader(query, 12);
                _ = reader.ReadName();
                DnsRecordType type = (DnsRecordType)reader.ReadUInt16();
                bool update = (query[2] & 0x78) == 0x28;
                byte[] header = TestUtilities.CreateResponseFromQuery(query, update ? (ushort)0xa800 : (ushort)0x8400);
                using var message = new MemoryStream();
                if (!update) header[7] = (byte)(type == DnsRecordType.AXFR ? 2 : 1);
                message.Write(header);
                if (!update) {
                    // Uncompressed root MNAME/RNAME, serial 7, and four zero timing values.
                    byte[] soa = new byte[22];
                    BinaryPrimitives.WriteUInt32BigEndian(soa.AsSpan(2), 7);
                    for (int index = 0; index < header[7]; index++) {
                        message.Write(new byte[] { 0xc0, 0x0c, 0, 6, 0, 1, 0, 0, 0, 60, 0, 22 });
                        message.Write(soa);
                    }
                }
                byte[] response = message.ToArray();
                BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)response.Length);
                await stream.WriteAsync(prefix, _stop.Token);
                await stream.WriteAsync(response, _stop.Token);
            } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
              catch (SocketException) when (_stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync() {
            _stop.Cancel(); _listener.Stop();
            await Completion;
            _stop.Dispose();
        }
    }
}
#endif
