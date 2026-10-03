#if NET8_0_OR_GREATER
using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX.Tests;

/// <summary>Protects ownership while a pooled TCP or TLS connection is being established.</summary>
[Collection("NoParallel")]
public sealed class DnsStreamConnectionPoolLifecycleTests {
    /// <summary>A connection completing after owner disposal cannot send a query or retain its socket.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposedPoolCannotPublishAnEstablishingConnection(bool tls) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        int queriesReceived = 0;
        Task server = Task.Run(async () => {
            using TcpClient peer = await listener.AcceptTcpClientAsync(deadline.Token);
            using NetworkStream network = peer.GetStream();
            using var ssl = tls ? new SslStream(network, leaveInnerStreamOpen: true) : null;
            Stream stream = ssl ?? (Stream)network;
            if (tls) {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(deadline.Token);
                await ssl!.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                    ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, deadline.Token);
            }
            byte[] prefix = new byte[2];
            int received = await stream.ReadAsync(prefix, deadline.Token);
            if (received == 0) return;
            if (received == 1) await stream.ReadExactlyAsync(prefix.AsMemory(1), deadline.Token);
            byte[] query = new byte[BinaryPrimitives.ReadUInt16BigEndian(prefix)];
            await stream.ReadExactlyAsync(query, deadline.Token);
            Interlocked.Increment(ref queriesReceived);
            byte[] response = TestUtilities.CreateResponseFromQuery(query);
            BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)response.Length);
            await stream.WriteAsync(prefix, deadline.Token);
            await stream.WriteAsync(response, deadline.Token);
        }, deadline.Token);
        using var pool = new DnsStreamConnectionPool(connectOverride: async (client, address, selectedPort, _, token) => {
            await client.ConnectAsync(address, selectedPort, token);
            if (!tls) {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(token);
            }
        });
        byte[] queryBytes = new DnsMessage("lifetime.example", DnsRecordType.A, new DnsMessageOptions()).SerializeDnsWireFormat();
        try {
            Task<byte[]> query = tls ? pool.QueryTlsAsync(IPAddress.Loopback, port, null, "localhost", true,
                SslProtocols.Tls12 | SslProtocols.Tls13, queryBytes, 10000, 1, deadline.Token)
                : pool.QueryTcpAsync(IPAddress.Loopback, port, null, queryBytes, 10000, 1, deadline.Token);
            await entered.Task.WaitAsync(deadline.Token);
            pool.Dispose();
            release.TrySetResult(true);
            Exception? failure = await Record.ExceptionAsync(async () => await query);
            await server;
            Assert.NotNull(failure);
            Assert.Equal(0, queriesReceived);
        } finally {
            release.TrySetResult(true);
            listener.Stop();
        }
    }
}
#endif
