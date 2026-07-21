using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal sealed class DnsZoneTransferConnection : IAsyncDisposable {
        private readonly TcpClient tcpClient;

        private DnsZoneTransferConnection(TcpClient tcpClient, Stream stream) {
            this.tcpClient = tcpClient;
            Stream = stream;
        }

        internal Stream Stream { get; }

        internal static async Task<DnsZoneTransferConnection> ConnectAsync(
            string dnsServer,
            int port,
            int timeoutMilliseconds,
            Configuration configuration,
            bool ignoreCertificateErrors,
            CancellationToken cancellationToken) {
            (IPAddress? address, string? resolveError) = await DnsServerResolver.ResolveAsync(
                dnsServer,
                timeoutMilliseconds,
                cancellationToken,
                configuration.DnsServerResolutionSuccessTtl,
                configuration.DnsServerResolutionFailureTtl,
                configuration.DnsServerResolutionAllowStale,
                configuration.DnsServerResolutionStaleTtl,
                configuration.DnsServerResolutionFailureBackoffEnabled,
                configuration.DnsServerResolutionFailureBackoffFactor,
                configuration.DnsServerResolutionFailureBackoffMaxTtl,
                configuration.PreferredAddressFamily).ConfigureAwait(false);
            if (address == null) throw new DnsClientException(resolveError ?? $"DNS server '{dnsServer}' could not be resolved.");

            TcpClient tcpClient = DnsWireResolveTcp.TcpClientFactory(address.AddressFamily);
            try {
                SocketBinding.Bind(tcpClient.Client, configuration.LocalEndPoint, address.AddressFamily);
                await ConnectTcpAsync(tcpClient, address.ToString(), port, timeoutMilliseconds, cancellationToken)
                    .ConfigureAwait(false);
                NetworkStream networkStream = tcpClient.GetStream();
                if (configuration.RequestFormat == DnsRequestFormat.DnsOverTCP) {
                    return new DnsZoneTransferConnection(tcpClient, networkStream);
                }
                if (configuration.RequestFormat != DnsRequestFormat.DnsOverTLS) {
                    throw new NotSupportedException(
                        $"Zone transfers require DnsOverTCP or DnsOverTLS, not {configuration.RequestFormat}.");
                }
                if (ignoreCertificateErrors) {
                    throw new InvalidOperationException("XFR-over-TLS requires strict server authentication; IgnoreCertificateErrors cannot be enabled.");
                }

#if NET8_0_OR_GREATER
                string authenticationName = configuration.TlsServerName
                    ?? (Uri.CheckHostName(dnsServer) == UriHostNameType.Dns
                        ? dnsServer
                        : throw new InvalidOperationException(
                            "XFR-over-TLS configured by IP address requires Configuration.TlsServerName for strict authentication."));
                var sslStream = new SslStream(
                    networkStream,
                    leaveInnerStreamOpen: false,
                    configuration.ZoneTransferServerCertificateValidationCallback);
                var dotProtocol = new SslApplicationProtocol("dot");
                var authenticationOptions = new SslClientAuthenticationOptions {
                    TargetHost = authenticationName,
                    EnabledSslProtocols = SslProtocols.Tls13,
                    ApplicationProtocols = new System.Collections.Generic.List<SslApplicationProtocol> {
                        dotProtocol
                    }
                };
                if (configuration.ZoneTransferClientCertificate != null) {
                    authenticationOptions.ClientCertificates = new X509CertificateCollection {
                        configuration.ZoneTransferClientCertificate
                    };
                }

                using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                handshakeCts.CancelAfter(timeoutMilliseconds);
                try {
                    await sslStream.AuthenticateAsClientAsync(authenticationOptions, handshakeCts.Token)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException(
                        $"TLS handshake with {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                }
                if (sslStream.NegotiatedApplicationProtocol != dotProtocol) {
                    throw new AuthenticationException("XFR-over-TLS requires the 'dot' ALPN protocol.");
                }
                return new DnsZoneTransferConnection(tcpClient, sslStream);
#else
                throw new PlatformNotSupportedException(
                    "RFC 9103 XFR-over-TLS requires the net8.0 or newer target for TLS 1.3 and 'dot' ALPN enforcement.");
#endif
            } catch {
                tcpClient.Dispose();
                throw;
            }
        }

        public ValueTask DisposeAsync() {
            try {
                Stream.Dispose();
            } finally {
                tcpClient.Dispose();
            }
            return default;
        }

        private static async Task ConnectTcpAsync(TcpClient tcpClient, string host, int port,
            int timeoutMilliseconds, CancellationToken cancellationToken) {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMilliseconds <= 0) linkedCts.Cancel();
            else linkedCts.CancelAfter(timeoutMilliseconds);
#if NET5_0_OR_GREATER
            try {
                await tcpClient.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
#else
            Task connectTask = tcpClient.ConnectAsync(host, port);
            Task completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);
            if (completed != connectTask) {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
            await connectTask.ConfigureAwait(false);
#endif
        }
    }
}
