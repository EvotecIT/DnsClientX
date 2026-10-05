#if NET8_0_OR_GREATER
#pragma warning disable CA2252, CA1416
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX.Tests;

internal sealed class NativeQuicFactAttribute : FactAttribute {
    public NativeQuicFactAttribute() {
        if (!QuicListener.IsSupported && Environment.GetEnvironmentVariable("DNSCLIENTX_REQUIRE_QUIC") != "1")
            Skip = "Native QUIC is unavailable. The dedicated Linux/Windows qualification requires it.";
    }
}

/// <summary>Checks publication and resource ownership using real native QUIC connections.</summary>
public class DnsQuicConnectionPoolNativeTests {
    /// <summary>A connection completed after shutdown is disposed instead of escaping the closed pool.</summary>
    [NativeQuicFact]
    public async Task LateNativeConnectionIsDisposedBeforeShutdownCompletes() {
        Assert.True(QuicListener.IsSupported, "Native QUIC is required by this qualification job.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Export/import gives the Windows TLS stack a persistent certificate key association.
        #if NET9_0_OR_GREATER
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
#else
        using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
#endif
        var protocols = new List<SslApplicationProtocol> { new("doq") };
        await using var listener = await QuicListener.ListenAsync(new QuicListenerOptions {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0), ApplicationProtocols = protocols,
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions {
                DefaultCloseErrorCode = 0, DefaultStreamErrorCode = 0, MaxInboundBidirectionalStreams = 1,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions { ApplicationProtocols = protocols, ServerCertificate = certificate }
            })
        }, timeout.Token);
        Task<QuicConnection> accepting = listener.AcceptConnectionAsync(timeout.Token).AsTask();
        var pool = new DnsQuicConnectionPool();
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        QuicConnection? created = null;
        Task pending = pool.GetAsync("loopback", new QuicClientConnectionOptions {
            RemoteEndPoint = listener.LocalEndPoint, DefaultCloseErrorCode = 0, DefaultStreamErrorCode = 0,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions {
                TargetHost = "localhost", ApplicationProtocols = protocols, RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        }, async (options, token) => {
            created = await QuicConnection.ConnectAsync(options, token);
            using var registration = token.Register(() => canceled.TrySetResult(true));
            connected.TrySetResult(true);
            // Model a platform connection completing while shutdown wins the publication gate.
            await release.Task.WaitAsync(timeout.Token);
            return created;
        }, timeout.Token).AsTask();
        try {
            await connected.Task.WaitAsync(timeout.Token);
            await using var peer = await accepting;
            Task closing = pool.DisposeAsync().AsTask();
            Assert.Same(closing, pool.DisposeAsync().AsTask());
            await canceled.Task.WaitAsync(timeout.Token);
            Assert.False(closing.IsCompleted);
            release.TrySetResult(true);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            await closing.WaitAsync(timeout.Token);
            Assert.NotNull(created);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => created!.OpenOutboundStreamAsync(QuicStreamType.Bidirectional).AsTask());
        } finally {
            release.TrySetResult(true);
            try { await pending; } catch (Exception) { }
            await pool.DisposeAsync();
            if (created != null) await created.DisposeAsync();
            if (accepting.IsCompletedSuccessfully) await accepting.Result.DisposeAsync();
            else {
                timeout.Cancel();
                try { await accepting; } catch (Exception) { }
            }
        }
    }
}
#endif
