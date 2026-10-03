using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

public partial class ClientX {
    private static async Task<DnsResponse?> PrepareHttpBootstrapAsync(Configuration configuration, string name,
        DnsRecordType type, CancellationToken cancellationToken) {
        if (!UsesHttpBootstrap(configuration)) return null;
        if (configuration.HttpVersion.Major >= 3 || configuration.RequestFormat == DnsRequestFormat.DnsOverHttp3) {
            throw new NotSupportedException("Explicit HTTP bootstrap supports HTTP/1.1 and HTTP/2; HTTP/3 has an independent QUIC dial path.");
        }
        var (address, error) = await DnsServerResolver.ResolveAsync(configuration.BaseUri!.IdnHost,
            configuration, cancellationToken).ConfigureAwait(false);
        if (address != null) return null;
        var response = new DnsResponse {
            Questions = [new DnsQuestion { Name = name, Type = type }],
            Status = DnsResponseCode.ServerFailure,
            Error = error ?? "HTTP bootstrap returned no address.",
            ErrorCode = DnsQueryErrorCode.Network
        };
        response.AddServerDetails(configuration);
        return response;
    }

    private static bool UsesHttpBootstrap(Configuration configuration) => configuration.BootstrapResolver != null
        && configuration.BaseUri != null && !IPAddress.TryParse(configuration.BaseUri.IdnHost, out _);

    private static string HttpBootstrapKey(Configuration configuration) => !UsesHttpBootstrap(configuration) ? string.Empty :
        $"{DnsBootstrapResolver.CacheKey(configuration.BootstrapResolver)}|{configuration.ServerResolution?.Hostname}|{configuration.ServerResolution?.Address}";

    private HttpMessageHandler CreateBootstrapHttpHandler(Configuration configuration, bool ignoreCertificateErrors) {
#if NET8_0_OR_GREATER
        if (_webProxy != null) throw new NotSupportedException("Explicit HTTP bootstrap cannot be combined with an HTTP proxy.");
        DnsServerResolutionInfo resolution = configuration.ServerResolution
            ?? throw new InvalidOperationException("HTTP bootstrap must resolve the endpoint before selecting a connection pool.");
        if (!IPAddress.TryParse(resolution.Address, out var address)) throw new DnsClientException(resolution.Error ?? "HTTP bootstrap returned no address.");
        var sockets = new SocketsHttpHandler {
            UseProxy = false,
            UseCookies = false,
            MaxConnectionsPerServer = configuration.MaxConnectionsPerServer,
            AllowAutoRedirect = false,
            SslOptions = new SslClientAuthenticationOptions { EnabledSslProtocols = (SslProtocols)_securityProtocol }
        };
        if (ignoreCertificateErrors) sockets.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        sockets.ConnectCallback = async (context, cancellationToken) => {
            // Keep the original URL authority for HTTP Host, SNI, and certificate validation.
            if (!string.Equals(context.DnsEndPoint.Host.TrimEnd('.'), resolution.Hostname.TrimEnd('.'), StringComparison.OrdinalIgnoreCase)) {
                throw new DnsClientException("HTTP connection authority does not match the bootstrapped endpoint.");
            }
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken).ConfigureAwait(false);
                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            } catch {
                socket.Dispose();
                throw;
            }
        };
        return sockets;
#else
        throw new NotSupportedException("Explicit HTTP bootstrap requires .NET 8 or later. UDP, TCP, and TLS bootstrap remain available.");
#endif
    }
}
