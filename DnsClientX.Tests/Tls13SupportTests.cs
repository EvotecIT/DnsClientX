#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Runs native TLS 1.3 server proof on Linux and supported Windows releases.</summary>
    internal sealed class Tls13ServerFactAttribute : FactAttribute {
        /// <summary>Excludes only known platform versions without native TLS 1.3 server support.</summary>
        public Tls13ServerFactAttribute() {
            if (OperatingSystem.IsMacOS()) {
                Skip = "The in-process test requires server-side TLS 1.3, which this .NET macOS backend does not provide.";
            } else if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)) {
                Skip = "TLS 1.3 requires Windows 11 or Windows Server 2022 or later.";
            }
        }
    }

    /// <summary>
    /// Tests verifying TLS 1.3 support for DNS over TLS.
    /// </summary>
    public class Tls13SupportTests {
        private static async Task<(SslProtocols Protocol, string? ServerName, SslApplicationProtocol Alpn)> RunTls13ServerAsync(X509Certificate2 cert,
            TcpListener listener, CancellationToken token) {
            try {
#if NET8_0_OR_GREATER
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#else
                using TcpClient client = await listener.AcceptTcpClientAsync();
#endif
                using var sslStream = new SslStream(client.GetStream(), false);
                string? serverName = null;
                await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                    ServerCertificateSelectionCallback = (_, name) => { serverName = name; return cert; },
                    EnabledSslProtocols = SslProtocols.Tls13,
                    ApplicationProtocols = new List<SslApplicationProtocol> { new("dot") }
                }, token);
                var length = new byte[2];
                await TestUtilities.ReadExactlyAsync(sslStream, length, length.Length, token);
                int queryLength = (length[0] << 8) | length[1];
                Assert.InRange(queryLength, 17, ushort.MaxValue);
                var query = new byte[queryLength];
                await TestUtilities.ReadExactlyAsync(sslStream, query, query.Length, token);
                Assert.True(DnsWireMessageParser.TryParseQuestion(query, 0, out DnsWireQuestionInfo question));
                Assert.Equal("tls13.example", question.Name.TrimEnd('.'));
                Assert.Equal((ushort)DnsRecordType.A, question.Type);
                byte[] response = DnsDiscoveryTestWire.Response(query,
                    DnsDiscoveryTestWire.Address("tls13.example", "192.0.2.13"));
                await sslStream.WriteAsync(new byte[] { (byte)(response.Length >> 8), (byte)response.Length }, token);
                await sslStream.WriteAsync(response, token);
                await sslStream.FlushAsync(token);
                return (sslStream.SslProtocol, serverName, sslStream.NegotiatedApplicationProtocol);
            } finally {
                listener.Stop();
            }
        }

        /// <summary>
        /// An authenticated ordinary DoT client receives a real DNS answer from a TLS 1.3-only server.
        /// </summary>
        [Tls13ServerFact]
        public async Task ResolveWireFormatDoT_UsesTls13WhenAvailable() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            // Use a generated TLS signing certificate with explicit server usage and DNS/IP identities.
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
            using X509Certificate2 cert = TestUtilities.CreateTlsCertificate(request);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var serverTask = RunTls13ServerAsync(cert, listener, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) {
                Port = port,
                TlsServerName = "localhost",
                TimeOut = 5000,
                TlsTrustAnchors = new[] { cert },
                TlsRevocationMode = X509RevocationMode.NoCheck,
                UseTcpFallback = false
            };
            try {
                using var client = new ClientX(config);
                Assert.False(client.IgnoreCertificateErrors);
                DnsResponse response = await client.Resolve("tls13.example", DnsRecordType.A,
                    retryOnTransient: false, cancellationToken: cts.Token);
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Equal("192.0.2.13", Assert.Single(response.Answers).Data);
                var handshake = await serverTask;
                Assert.Equal(SslProtocols.Tls13, handshake.Protocol);
                Assert.Equal("localhost", handshake.ServerName);
                Assert.Equal(new SslApplicationProtocol("dot"), handshake.Alpn);
            } finally {
                cts.Cancel();
                listener.Stop();
                try { await serverTask; }
                catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
                catch (SocketException) when (cts.IsCancellationRequested) { }
            }
        }
    }
}
#endif
