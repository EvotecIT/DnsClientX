#if NET8_0_OR_GREATER
#pragma warning disable CA2252, CA1416
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace DnsClientX.Tests;

/// <summary>Owns native loopback DoQ or HTTP/3 peers and ephemeral software certificates.</summary>
internal sealed class NativeEncryptedDnsFixture : IAsyncDisposable {
    private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(60));
    private readonly ConcurrentBag<Task> _peers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _queries = new();
    private readonly ConcurrentDictionary<string, byte> _connections = new();
    private readonly RSA _rootKey = RSA.Create(2048);
    private readonly RSA _leafKey = RSA.Create(2048);
    private readonly X509Certificate2 _certificate;
    private QuicListener? _listener;
    private WebApplication? _application;
    private Task? _accepting;
    private int _queryCount;

    internal NativeEncryptedDnsFixture() {
        var rootRequest = new CertificateRequest("CN=DnsClientX native test CA", _rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        Root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var request = new CertificateRequest("CN=localhost", _leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var issued = request.Create(Root, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
        using var withKey = issued.CopyWithPrivateKey(_leafKey);
        _certificate = TestUtilities.ImportForServer(withKey);
        Assert.True(DnsTlsCertificateTrust.Validate(_certificate, null, SslPolicyErrors.RemoteCertificateChainErrors,
            "localhost", new[] { Root }, X509RevocationMode.NoCheck), "Generated native fixture must satisfy the isolated trust policy.");
    }

    internal X509Certificate2 Root { get; }
    internal int Port { get; private set; }
    internal int ConnectionCount => _connections.Count;
    internal int QueryCount => Volatile.Read(ref _queryCount);

    internal async Task StartAsync(bool http3) {
        if (http3) {
            Port = TestUtilities.GetFreeUdpPort();
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseQuic();
            builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(Port, endpoint => {
                endpoint.Protocols = HttpProtocols.Http3;
                endpoint.UseHttps(_certificate);
            }));
            _application = builder.Build();
            _application.Map("/dns-query", async context => {
                Assert.Equal("HTTP/3", context.Request.Protocol);
                Assert.Equal("localhost", context.Request.Host.Host);
                _connections.TryAdd(context.Connection.Id, 0);
                byte[] query;
                if (context.Request.Method == "POST") {
                    using var body = new MemoryStream();
                    await context.Request.Body.CopyToAsync(body, context.RequestAborted);
                    query = body.ToArray();
                } else {
                    string encoded = context.Request.Query["dns"].ToString().Replace('-', '+').Replace('_', '/');
                    query = Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
                }
                byte[] response = await RespondAsync(query, context.RequestAborted);
                context.Response.ContentType = "application/dns-message";
                await context.Response.Body.WriteAsync(response, context.RequestAborted);
            });
            await _application.StartAsync(_lifetime.Token);
        } else {
            var protocols = new List<SslApplicationProtocol> { new("doq") };
            _listener = await QuicListener.ListenAsync(new QuicListenerOptions {
                ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0), ApplicationProtocols = protocols,
                ConnectionOptionsCallback = (_, hello, _) => {
                    return ValueTask.FromResult(new QuicServerConnectionOptions {
                        DefaultCloseErrorCode = 0, DefaultStreamErrorCode = 0, MaxInboundBidirectionalStreams = 32,
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions {
                            ApplicationProtocols = protocols, ServerCertificate = _certificate
                        }
                    });
                }
            }, _lifetime.Token);
            Port = _listener.LocalEndPoint.Port;
            _accepting = AcceptAsync();
        }
    }

    internal Task WaitForQueryAsync(string name, CancellationToken token) => _queries.GetOrAdd(name,
        _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(token);

    private async Task AcceptAsync() {
        while (!_lifetime.IsCancellationRequested) {
            try {
                QuicConnection connection = await _listener!.AcceptConnectionAsync(_lifetime.Token);
                _connections.TryAdd(connection.RemoteEndPoint.ToString(), 0);
                _peers.Add(ServeConnectionAsync(connection));
            } catch (Exception exception) when (!_lifetime.IsCancellationRequested &&
                exception is QuicException or System.Security.Authentication.AuthenticationException) {
                // Rejected certificate handshakes are expected negative qualification inputs.
            } catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) {
                break;
            }
        }
    }

    private async Task ServeConnectionAsync(QuicConnection connection) {
        await using (connection) {
            try {
                while (!_lifetime.IsCancellationRequested) {
                    QuicStream stream = await connection.AcceptInboundStreamAsync(_lifetime.Token);
                    _peers.Add(ServeStreamAsync(stream));
                }
            } catch (QuicException) { }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        }
    }

    private async Task ServeStreamAsync(QuicStream stream) {
        await using (stream) {
            try {
                byte[] prefix = new byte[2];
                await stream.ReadExactlyAsync(prefix, _lifetime.Token);
                byte[] query = new byte[(prefix[0] << 8) | prefix[1]];
                await stream.ReadExactlyAsync(query, _lifetime.Token);
                Assert.Equal(0, query[0] | query[1]);
                byte[] response = await RespondAsync(query, _lifetime.Token);
                prefix[0] = (byte)(response.Length >> 8);
                prefix[1] = (byte)response.Length;
                await stream.WriteAsync(prefix, _lifetime.Token);
                await stream.WriteAsync(response, _lifetime.Token);
                stream.CompleteWrites();
            } catch (QuicException) { }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        }
    }

    private async Task<byte[]> RespondAsync(byte[] query, CancellationToken cancellationToken) {
        string name = new DnsWireReader(query, 12).ReadName().TrimEnd('.');
        Interlocked.Increment(ref _queryCount);
        _queries.GetOrAdd(name, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(true);
        if (name.StartsWith("stall-", StringComparison.Ordinal)) {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            await Task.Delay(Timeout.Infinite, linked.Token);
        }
        byte[] response = TestUtilities.CreateResponseFromQuery(query);
        response[7] = 1;
        return response.Concat(new byte[] { 0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 30, 0, 4, 192, 0, 2, 42 }).ToArray();
    }

    public async ValueTask DisposeAsync() {
        _lifetime.Cancel();
        if (_application != null) await _application.DisposeAsync();
        if (_accepting != null) await _accepting;
        if (_listener != null) await _listener.DisposeAsync();
        // Accepting has stopped, so no connection tasks can add new stream tasks after they settle.
        await Task.WhenAll(_peers.ToArray());
        await Task.WhenAll(_peers.ToArray());
        _certificate.Dispose();
        Root.Dispose();
        _rootKey.Dispose();
        _leafKey.Dispose();
        _lifetime.Dispose();
    }
}
#endif
