#if NET8_0_OR_GREATER
using System;
using System.IO;
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
    /// <summary>Protects RFC 9103 TLS 1.3, ALPN, server authentication, and mutual TLS behavior.</summary>
    [Collection("NoParallel")]
    public class ZoneTransferTlsTests {
        /// <summary>An AXFR can use TLS 1.3 with dot ALPN and mutually authenticated certificates.</summary>
        [Fact]
        public async Task ZoneTransferAsync_UsesAuthenticatedTls13AndDotAlpn() {
            using X509Certificate2 serverCertificate = Certificate("localhost");
            using X509Certificate2 clientCertificate = Certificate("zone-transfer-client");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            SslProtocols negotiatedProtocol = SslProtocols.None;
            SslApplicationProtocol negotiatedApplicationProtocol = default;
            bool observedClientCertificate = false;

            Task server = Task.Run(async () => {
                try {
                    using TcpClient connection = await listener.AcceptTcpClientAsync(timeout.Token);
                    using var tls = new SslStream(connection.GetStream(), false,
                        (_, certificate, _, _) => {
                            observedClientCertificate = certificate != null &&
                                string.Equals(certificate.GetCertHashString(), clientCertificate.GetCertHashString(),
                                    StringComparison.OrdinalIgnoreCase);
                            return observedClientCertificate;
                        });
                    var options = new SslServerAuthenticationOptions {
                        ServerCertificate = serverCertificate,
                        ClientCertificateRequired = true,
                        EnabledSslProtocols = SslProtocols.Tls13,
                        ApplicationProtocols = new() { new SslApplicationProtocol("dot") }
                    };
                    await tls.AuthenticateAsServerAsync(options, timeout.Token);
                    negotiatedProtocol = tls.SslProtocol;
                    negotiatedApplicationProtocol = tls.NegotiatedApplicationProtocol;

                    byte[] query = await ReadFrameAsync(tls, timeout.Token);
                    byte[] response = AxfrResponse(query[0], query[1]);
                    await WriteFrameAsync(tls, response, timeout.Token);
                } finally {
                    listener.Stop();
                }
            }, timeout.Token);

            var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) {
                Port = port,
                TimeOut = 5000,
                TlsServerName = "localhost",
                ZoneTransferClientCertificate = clientCertificate,
                ZoneTransferServerCertificateValidationCallback = (_, certificate, _, _) =>
                    certificate != null && string.Equals(certificate.GetCertHashString(),
                        serverCertificate.GetCertHashString(), StringComparison.OrdinalIgnoreCase)
            };
            using var client = new ClientX(configuration);

            ZoneTransferResult[] result = await client.ZoneTransferAsync("example.com",
                retryOnTransient: false, cancellationToken: timeout.Token);
            await server;

            Assert.Equal(3, result.Length);
            Assert.Equal(SslProtocols.Tls13, negotiatedProtocol);
            Assert.Equal(new SslApplicationProtocol("dot"), negotiatedApplicationProtocol);
            Assert.True(observedClientCertificate);
        }

        private static X509Certificate2 Certificate(string commonName) {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            using X509Certificate2 temporary = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            byte[] pfx = temporary.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(pfx, null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet,
                Pkcs12LoaderLimits.Defaults);
#else
            return new X509Certificate2(pfx, (string?)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
#endif
        }

        private static byte[] AxfrResponse(byte idHigh, byte idLow) {
            using var output = new MemoryStream();
            output.WriteByte(idHigh);
            output.WriteByte(idLow);
            UInt16(output, 0x8400);
            UInt16(output, 1);
            UInt16(output, 3);
            UInt16(output, 0);
            UInt16(output, 0);
            Write(output, Name("example.com"));
            UInt16(output, (ushort)DnsRecordType.AXFR);
            UInt16(output, 1);
            byte[] soa = Soa();
            Record(output, "example.com", DnsRecordType.SOA, soa);
            Record(output, "www.example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 });
            Record(output, "example.com", DnsRecordType.SOA, soa);
            return output.ToArray();
        }

        private static void Record(Stream output, string owner, DnsRecordType type, byte[] rdata) {
            Write(output, Name(owner));
            UInt16(output, (ushort)type);
            UInt16(output, 1);
            UInt32(output, 3600);
            UInt16(output, checked((ushort)rdata.Length));
            Write(output, rdata);
        }

        private static byte[] Soa() {
            using var output = new MemoryStream();
            Write(output, Name("ns1.example.com"));
            Write(output, Name("hostmaster.example.com"));
            UInt32(output, 2026072101);
            UInt32(output, 3600);
            UInt32(output, 600);
            UInt32(output, 86400);
            UInt32(output, 60);
            return output.ToArray();
        }

        private static byte[] Name(string name) => DnsWireNameCodec.ToCanonicalWire(name);

        private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken) {
            var length = new byte[2];
            await TestUtilities.ReadExactlyAsync(stream, length, length.Length, cancellationToken);
            int count = (length[0] << 8) | length[1];
            var message = new byte[count];
            await TestUtilities.ReadExactlyAsync(stream, message, count, cancellationToken);
            return message;
        }

        private static async Task WriteFrameAsync(Stream stream, byte[] message,
            CancellationToken cancellationToken) {
            byte[] length = { (byte)(message.Length >> 8), (byte)message.Length };
            await stream.WriteAsync(length, cancellationToken);
            await stream.WriteAsync(message, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static void UInt16(Stream output, ushort value) {
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void UInt32(Stream output, uint value) {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void Write(Stream output, byte[] value) => output.Write(value, 0, value.Length);
    }
}
#endif
