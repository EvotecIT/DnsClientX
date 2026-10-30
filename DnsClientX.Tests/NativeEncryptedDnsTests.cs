#if NET8_0_OR_GREATER
#pragma warning disable CA2252, CA1416
using System.Net.Quic;
using System.Security.Cryptography.X509Certificates;

namespace DnsClientX.Tests;

/// <summary>Exercises native encrypted transports through public clients without handler or connection overrides.</summary>
[Collection("NoParallel")]
public sealed class NativeEncryptedDnsTests {
    /// <summary>Native TLS rejects unknown roots and names; deadlines/cancellation preserve reuse and disposal settles active work.</summary>
    [NativeQuicTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrustDeadlinesReuseAndDrain(bool http3) {
        Assert.True(QuicListener.IsSupported, "The native qualification job requires QUIC support.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var server = new NativeEncryptedDnsFixture();
        await server.StartAsync(http3);
        Configuration Configuration(bool trust = true, string identity = "localhost", int timeout = 10000) {
            Configuration configuration = http3
                ? new(new Uri($"https://{(identity == "localhost" ? "localhost" : "127.0.0.1")}:{server.Port}/dns-query"), DnsRequestFormat.DnsOverHttp3)
                : new("127.0.0.1", DnsRequestFormat.DnsOverQuic) { Port = server.Port };
            configuration.TimeOut = timeout;
            configuration.TlsServerName = identity;
            if (!http3) configuration.ConnectionAddress = System.Net.IPAddress.Loopback;
            configuration.TlsTrustAnchors = trust ? new[] { server.Root } : null;
            configuration.TlsRevocationMode = X509RevocationMode.NoCheck;
            return configuration;
        }
        await using var client = new ClientX(Configuration(), enableCache: false);
        async Task Answer(string name, ClientX? answeringClient = null) {
            DnsResponse answer = await (answeringClient ?? client).Resolve(name, DnsRecordType.A, retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.True(answer.Status == DnsResponseCode.NoError, answer.Error + " " + answer.Exception);
            Assert.Contains(answer.Answers, record => record.Data == "192.0.2.42");
            Assert.Equal(http3 ? Transport.Doh : Transport.Quic, answer.UsedTransport);
        }
        await Answer("first.test");
        await Answer("second.test");
        Assert.Equal(1, server.ConnectionCount);
        int acceptedQueries = server.QueryCount;
        foreach (var configuration in new[] { Configuration(trust: false), Configuration(identity: "wrong.test") }) {
            await using var rejected = new ClientX(configuration, enableCache: false);
            DnsResponse response = await rejected.Resolve("rejected.test", DnsRecordType.A,
                retryOnTransient: false, cancellationToken: deadline.Token);
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
        }
        Assert.Equal(acceptedQueries, server.QueryCount);
        // Cold native TLS/HTTP/3 setup has its own budget; the deadline contract stays at one second.
        await using (var timed = new ClientX(Configuration(timeout: 1000), enableCache: false)) {
            Task<DnsResponse> stalled = timed.Resolve("stall-timeout.test", DnsRecordType.A,
                retryOnTransient: false, cancellationToken: deadline.Token);
            await server.WaitForQueryAsync("stall-timeout.test", deadline.Token);
            if (http3) {
                // Direct HTTP Resolve preserves HttpClient's deadline exception contract.
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stalled);
            } else {
                DnsResponse timeout = await stalled;
                Assert.Equal(DnsQueryErrorCode.Timeout, timeout.ErrorCode);
            }
            Assert.False(deadline.IsCancellationRequested);
            int afterTimeout = server.ConnectionCount;
            await Answer("after-timeout.test", timed);
            Assert.Equal(afterTimeout, server.ConnectionCount);
        }
        int beforeCancellation = server.ConnectionCount;
        using (var canceled = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token)) {
            Task<DnsResponse> query = client.Resolve("stall-cancel.test", DnsRecordType.A,
                retryOnTransient: false, cancellationToken: canceled.Token);
            await server.WaitForQueryAsync("stall-cancel.test", deadline.Token);
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
        }
        await Answer("after-cancel.test");
        Assert.Equal(beforeCancellation, server.ConnectionCount);
        Task<DnsResponse> draining = client.Resolve("stall-dispose.test", DnsRecordType.A,
            retryOnTransient: false, cancellationToken: deadline.Token);
        await server.WaitForQueryAsync("stall-dispose.test", deadline.Token);
        await client.DisposeAsync().AsTask().WaitAsync(deadline.Token);
        // Public queries may return a transport error or throw during teardown; neither may escape alive.
        try { Assert.NotEqual(DnsResponseCode.NoError, (await draining.WaitAsync(deadline.Token)).Status); }
        catch (Exception exception) when (exception is DnsClientException or ObjectDisposedException or OperationCanceledException) { }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.Resolve("closed.test", cancellationToken: deadline.Token));
    }
}
#endif
