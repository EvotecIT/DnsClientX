#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DnsClientX.Tests;

/// <summary>Exercises certificate policy through real TLS connections and the shared response cache.</summary>
[Collection("NoParallel")]
public sealed partial class CertificatePolicyBoundaryTests {
    /// <summary>Policy changes must govern subsequent requests, including previously pooled connections.</summary>
    [Fact]
    public async Task ChangingPolicyCannotPopulateOrReadStrictCacheWithUnauthenticatedData() {
        ClientX.ResetResponseCacheForTests();
        await using var server = new UntrustedHttpsServer();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new ClientX(server.Configuration(), ignoreCertificateErrors: true, enableCache: true);
        client.IgnoreCertificateErrors = false;
        var rejected = await client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.ServerFailure, rejected.Status);
        Assert.Empty(rejected.Answers);

        using var strictReader = new ClientX(server.Configuration(), enableCache: true);
        var separate = await strictReader.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.ServerFailure, separate.Status);
        Assert.False(separate.ServedFromCache);
        Assert.Equal(0, server.RequestCount);

        client.IgnoreCertificateErrors = true;
        var accepted = await client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.NoError, accepted.Status);
        Assert.Equal("192.0.2.99", Assert.Single(accepted.Answers).Data);
        Assert.Equal(1, server.RequestCount);
        client.IgnoreCertificateErrors = false;
        var afterConnectionReuse = await client.Resolve("tls-policy.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.ServerFailure, afterConnectionReuse.Status);
        Assert.False(afterConnectionReuse.ServedFromCache);
        Assert.Equal(1, server.RequestCount);
        ClientX.ResetResponseCacheForTests();
    }

    /// <summary>A strict query cannot join a permissive flight, and changing policy cannot retire an active request.</summary>
    [Fact]
    public async Task InflightOperationRetainsItsPolicyWithoutSharingAcrossPolicies() {
        ClientX.ResetResponseCacheForTests();
        await using var server = new UntrustedHttpsServer(gated: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new ClientX(server.Configuration(), ignoreCertificateErrors: true, enableCache: true);
        Task<DnsResponse> permissive = client.Resolve("tls-flight.example", retryOnTransient: false, cancellationToken: deadline.Token);
        await server.Entered.WaitAsync(deadline.Token);
        client.IgnoreCertificateErrors = false;
        DnsResponse strict;
        try {
            strict = await client.Resolve("tls-flight.example", retryOnTransient: false, cancellationToken: deadline.Token);
        } finally {
            server.Release();
        }
        Assert.Equal(DnsResponseCode.ServerFailure, strict.Status);
        Assert.Equal(DnsResponseCode.NoError, (await permissive).Status);
        using var reader = new ClientX(server.Configuration(), enableCache: true);
        var cached = await reader.Resolve("tls-flight.example", retryOnTransient: false, cancellationToken: deadline.Token);
        Assert.Equal(DnsResponseCode.ServerFailure, cached.Status);
        Assert.False(cached.ServedFromCache);
        Assert.Equal(1, server.RequestCount);
        ClientX.ResetResponseCacheForTests();
    }

    /// <summary>Bootstrap pins the connection address while retaining Host, SNI, and certificate-policy isolation.</summary>
    [Fact]
    public async Task BootstrappedHttpsRetainsAuthorityAndStrictCertificatePolicy() {
        ClientX.ResetResponseCacheForTests();
        await using var bootstrap = new BootstrapResolverTests.BootstrapDnsServer();
        await using var server = new UntrustedHttpsServer();
        const string hostname = "endpoint.bootstrap.invalid";
        using var client = new ClientX(server.Configuration(hostname, bootstrap.Endpoint()), ignoreCertificateErrors: true, enableCache: true);
        var accepted = await client.Resolve("tls-policy.example", retryOnTransient: false);
        Assert.Equal(DnsResponseCode.NoError, accepted.Status);
        Assert.Equal(hostname, server.ServerName);
        Assert.StartsWith(hostname + ":", server.HttpHost);
        Assert.Equal("127.0.0.1", accepted.ServerResolution!.Address);

        client.IgnoreCertificateErrors = false;
        var rejected = await client.Resolve("tls-policy.example", retryOnTransient: false);
        Assert.Equal(DnsResponseCode.ServerFailure, rejected.Status);
        Assert.False(rejected.ServedFromCache);
        using var strict = new ClientX(server.Configuration(hostname, bootstrap.Endpoint()), enableCache: true);
        var separate = await strict.Resolve("tls-policy.example", retryOnTransient: false);
        Assert.Equal(DnsResponseCode.ServerFailure, separate.Status);
        Assert.False(separate.ServedFromCache);
        Assert.Equal(1, server.RequestCount);
        using var literal = new ClientX(server.Configuration("127.0.0.1", bootstrap.Endpoint()), ignoreCertificateErrors: true);
        var literalResponse = await literal.Resolve("tls-policy.example", retryOnTransient: false);
        Assert.Equal(DnsResponseCode.NoError, literalResponse.Status);
        Assert.Null(literalResponse.ServerResolution);
        Assert.Equal(1, bootstrap.BootstrapQueries);
        ClientX.ResetResponseCacheForTests();
    }

    private sealed class UntrustedHttpsServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly RSA _key = RSA.Create(2048);
        private readonly X509Certificate2 _certificate;
        private readonly List<Task> _connections = new();
        private readonly Task _accept;
        private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _handshakeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _handshakeRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        internal UntrustedHttpsServer(bool gated = false, bool gateHandshake = false) {
            var request = new CertificateRequest("CN=untrusted.invalid", _key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _certificate = TestUtilities.CreateTlsCertificate(request);
            _listener.Start();
            if (!gated) Release();
            if (!gateHandshake) ReleaseHandshake();
            _accept = AcceptAsync();
        }

        internal int RequestCount => Volatile.Read(ref _requestCount);
        internal string? ServerName { get; private set; }
        internal string? HttpHost { get; private set; }
        internal Task Entered => _entered.Task;
        internal void Release() => _release.TrySetResult(true);
        internal Task HandshakeEntered => _handshakeEntered.Task;
        internal void ReleaseHandshake() => _handshakeRelease.TrySetResult(true);
        internal Configuration Configuration(string hostname = "localhost", DnsResolverEndpoint? bootstrap = null) => new(new Uri($"https://{hostname}:{((IPEndPoint)_listener.LocalEndpoint).Port}/resolve"), DnsRequestFormat.DnsOverHttpsJSON) {
            HttpVersion = HttpVersion.Version11, TimeOut = 3000, BootstrapResolver = bootstrap
        };

        private async Task AcceptAsync() {
            try {
                while (!_stop.IsCancellationRequested) {
                    TcpClient peer = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _connections.Add(RespondAsync(peer));
                }
            } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
              catch (SocketException) when (_stop.IsCancellationRequested) { }
        }

        private async Task RespondAsync(TcpClient peer) {
            using (peer)
            using (var tls = new SslStream(peer.GetStream())) {
                try {
                    _handshakeEntered.TrySetResult(true);
                    await _handshakeRelease.Task.WaitAsync(_stop.Token);
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                        ServerCertificateSelectionCallback = (_, name) => { ServerName = name; return _certificate; },
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, _stop.Token);
                    using var reader = new StreamReader(tls, Encoding.ASCII, false, 1024, leaveOpen: true);
                    while (!_stop.IsCancellationRequested) {
                        string? request = await reader.ReadLineAsync(_stop.Token);
                        if (request == null) return;
                        string? header;
                        int contentLength = 0;
                        while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync(_stop.Token))) {
                            if (header.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) HttpHost = header.Substring(5).Trim();
                            if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) contentLength = int.Parse(header.Substring(15).Trim());
                        }
                        if (contentLength > 0) await reader.ReadBlockAsync(new char[contentLength], _stop.Token);
                        Interlocked.Increment(ref _requestCount);
                        _entered.TrySetResult(true);
                        await _release.Task.WaitAsync(_stop.Token);
                        const string json = "{\"Status\":0,\"Answer\":[{\"name\":\"tls-policy.example.\",\"type\":1,\"TTL\":60,\"data\":\"192.0.2.99\"}]}";
                        byte[] body = Encoding.UTF8.GetBytes(json);
                        byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-json\r\nContent-Length: {body.Length}\r\n\r\n");
                        await tls.WriteAsync(headerBytes, _stop.Token);
                        await tls.WriteAsync(body, _stop.Token);
                        await tls.FlushAsync(_stop.Token);
                    }
                } catch (AuthenticationException) { }
                  catch (IOException) { }
                  catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            }
        }

        public async ValueTask DisposeAsync() {
            _stop.Cancel();
            _listener.Stop();
            await _accept;
            await Task.WhenAll(_connections);
            _certificate.Dispose();
            _key.Dispose();
            _stop.Dispose();
        }
    }
}
#endif
