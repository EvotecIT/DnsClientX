using System;
using System.IO;
using System.Net;
#if NET8_0_OR_GREATER
using System.Net.Quic;
#endif
using System.Net.Security;
using System.Security.Authentication;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
#if NET8_0_OR_GREATER
    #pragma warning disable CA2252
    /// <summary>
    /// Resolves DNS queries over QUIC according to RFC 9250.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static class DnsWireResolveQuic {
        private const long DoqProtocolError = 2;
        internal static Func<string, IPHostEntry>? HostEntryResolver;
        internal static Func<QuicClientConnectionOptions, CancellationToken, ValueTask<QuicConnection>> QuicConnectionFactory { get; set; } = QuicConnection.ConnectAsync;
        internal static Func<QuicConnection, CancellationToken, ValueTask<QuicStream>> StreamFactory { get; set; } =
            static (connection, token) => connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token);
        internal static Func<QuicConnection, ValueTask> ConnectionDisposer { get; set; } = static connection => connection.DisposeAsync();
        internal static Func<QuicStream, ValueTask> StreamDisposer { get; set; } = static stream => stream.DisposeAsync();
        internal static Func<QuicConnection, long, CancellationToken, ValueTask> ConnectionCloser { get; set; } =
            static (connection, errorCode, token) => connection.CloseAsync(errorCode, token);
        internal static int ConnectionDisposeCount;
        internal static int StreamDisposeCount;

        internal static async Task<DnsResponse> ResolveWireFormatQuic(string dnsServer, int port, string name,
            DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug,
            Configuration endpointConfiguration, CancellationToken cancellationToken,
            DnsQuicConnectionPool? connectionPool = null) {
            if (string.IsNullOrWhiteSpace(dnsServer)) throw new ArgumentNullException(nameof(dnsServer));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

            DnsMessage query = CreatePaddedQuery(name, type, requestDnsSec, validateDnsSec, endpointConfiguration);
            byte[] queryBytes = query.SerializeDnsWireFormat();
            var payload = new byte[queryBytes.Length + 2];
            payload[0] = (byte)(queryBytes.Length >> 8);
            payload[1] = (byte)queryBytes.Length;
            Buffer.BlockCopy(queryBytes, 0, payload, 2, queryBytes.Length);

            if (debug) {
                Settings.Logger.WriteDebug($"DoQ query {name} {type}, {queryBytes.Length} DNS bytes.");
            }

            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) {
                return Failure(name, type, endpointConfiguration, DnsResponseCode.NotImplemented,
                    "DNS over QUIC is not supported on this platform.");
            }

            IPAddress? address;
            if (IPAddress.TryParse(dnsServer, out IPAddress? literal)) {
                address = literal;
            } else if (HostEntryResolver != null) {
                IPHostEntry entry = HostEntryResolver(dnsServer);
                address = entry.AddressList.Length == 0 ? null : entry.AddressList[0];
            } else {
                (address, string? error) = await DnsServerResolver.ResolveAsync(
                    dnsServer,
                    endpointConfiguration.TimeOut,
                    cancellationToken,
                    endpointConfiguration.DnsServerResolutionSuccessTtl,
                    endpointConfiguration.DnsServerResolutionFailureTtl,
                    endpointConfiguration.DnsServerResolutionAllowStale,
                    endpointConfiguration.DnsServerResolutionStaleTtl,
                    endpointConfiguration.DnsServerResolutionFailureBackoffEnabled,
                    endpointConfiguration.DnsServerResolutionFailureBackoffFactor,
                    endpointConfiguration.DnsServerResolutionFailureBackoffMaxTtl,
                    endpointConfiguration.PreferredAddressFamily).ConfigureAwait(false);
                if (address == null) {
                    return Failure(name, type, endpointConfiguration, DnsResponseCode.ServerFailure,
                        error ?? $"Host '{dnsServer}' resolved to no addresses.");
                }
            }

            if (address == null) {
                return Failure(name, type, endpointConfiguration, DnsResponseCode.ServerFailure,
                    $"Host '{dnsServer}' resolved to no addresses.");
            }

            string targetHost = string.IsNullOrWhiteSpace(endpointConfiguration.TlsServerName)
                ? dnsServer
                : endpointConfiguration.TlsServerName!;
            var options = new QuicClientConnectionOptions {
                RemoteEndPoint = new IPEndPoint(address, port),
                DefaultCloseErrorCode = 0,
                DefaultStreamErrorCode = 0,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions {
                    TargetHost = targetHost,
                    ApplicationProtocols = [new SslApplicationProtocol("doq")],
                    EnabledSslProtocols = SslProtocols.Tls13
                }
            };

            using var timeout = CreateTimeoutTokenSource(endpointConfiguration.TimeOut, cancellationToken);
            string poolKey = $"{targetHost}:{port}";
            try {
                if (connectionPool == null) {
                    QuicConnection connection = await QuicConnectionFactory(options, timeout.Token).ConfigureAwait(false);
                    try {
                        return await QueryConnection(connection, payload, query, debug, endpointConfiguration, timeout.Token).ConfigureAwait(false);
                    } catch (DnsClientException) {
                        await CloseForProtocolViolationAsync(connection).ConfigureAwait(false);
                        throw;
                    } finally {
                        await ConnectionDisposer(connection).ConfigureAwait(false);
                    }
                }

                for (int attempt = 0; ; attempt++) {
                    QuicConnection connection = await connectionPool.GetAsync(poolKey, options, QuicConnectionFactory, timeout.Token).ConfigureAwait(false);
                    try {
                        return await QueryConnection(connection, payload, query, debug, endpointConfiguration, timeout.Token).ConfigureAwait(false);
                    } catch (DnsClientException) {
                        await connectionPool.InvalidateAsync(poolKey, connection, CloseForProtocolViolationAsync).ConfigureAwait(false);
                        throw;
                    } catch (Exception ex) when (attempt == 0 && (ex is QuicException || ex is IOException ||
                                                                  ex is InvalidOperationException || ex is ObjectDisposedException)) {
                        await connectionPool.InvalidateAsync(poolKey, connection).ConfigureAwait(false);
                    }
                }
            } catch (PlatformNotSupportedException ex) {
                return Failure(name, type, endpointConfiguration, DnsResponseCode.NotImplemented,
                    "DNS over QUIC is not supported on this platform: " + ex.Message);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                DnsResponse response = Failure(name, type, endpointConfiguration, DnsResponseCode.ServerFailure,
                    $"The DoQ request timed out after {endpointConfiguration.TimeOut} milliseconds.");
                response.ErrorCode = DnsQueryErrorCode.Timeout;
                return response;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                DnsResponse response = Failure(name, type, endpointConfiguration, DnsResponseCode.ServerFailure,
                    $"Failed to query {name} {type} over QUIC: {ex.Message}");
                response.ErrorCode = ex is QuicException || ex is IOException
                    ? DnsQueryErrorCode.Network
                    : DnsQueryErrorCode.ServFail;
                response.Exception = ex;
                return response;
            }
        }

        internal static async ValueTask CloseForProtocolViolationAsync(QuicConnection connection) {
            try {
                await ConnectionCloser(connection, DoqProtocolError, CancellationToken.None).ConfigureAwait(false);
            } catch (Exception ex) when (ex is QuicException || ex is ObjectDisposedException || ex is InvalidOperationException) {
                // Disposal below still guarantees that a protocol-invalid connection is not reused.
            }
        }

        private static async Task<DnsResponse> QueryConnection(QuicConnection connection, byte[] payload,
            DnsMessage query, bool debug, Configuration configuration, CancellationToken cancellationToken) {
            QuicStream stream = await StreamFactory(connection, cancellationToken).ConfigureAwait(false);
            try {
                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                stream.CompleteWrites();
                var length = new byte[2];
                await DnsWire.ReadExactAsync(stream, length, 0, 2, cancellationToken).ConfigureAwait(false);
                int responseLength = (length[0] << 8) | length[1];
                if (responseLength == 0) throw new DnsClientException("DoQ response declares a zero-length DNS message.");
                var responseBytes = new byte[responseLength];
                await DnsWire.ReadExactAsync(stream, responseBytes, 0, responseLength, cancellationToken).ConfigureAwait(false);
                DnsResponse response = await DnsWire.DeserializeDnsWireResponse(null, debug, responseBytes, query).ConfigureAwait(false);
                response.AddServerDetails(configuration, Transport.Quic);
                return response;
            } finally {
                await StreamDisposer(stream).ConfigureAwait(false);
            }
        }

        private static DnsMessage CreatePaddedQuery(string name, DnsRecordType type, bool requestDnsSec,
            bool validateDnsSec, Configuration configuration) {
            DnsMessage initial = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, configuration,
                checkingDisabled: configuration.CheckingDisabled || validateDnsSec, transactionId: 0);
            byte[] bytes = initial.SerializeDnsWireFormat();
            if (HasConfiguredPadding(configuration)) return initial;
            bool alreadyHasOpt = bytes[10] != 0 || bytes[11] != 0;
            int optionOverhead = alreadyHasOpt ? 4 : 15;
            int paddingLength = (128 - ((bytes.Length + optionOverhead) % 128)) % 128;
            return DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, configuration,
                checkingDisabled: configuration.CheckingDisabled || validateDnsSec,
                transactionId: 0,
                additionalOptions: new[] { new PaddingOption(paddingLength) });
        }

        private static bool HasConfiguredPadding(Configuration configuration) {
            if (configuration.EdnsOptions?.PaddingLength > 0) return true;
            if (configuration.EdnsOptions?.Options == null) return false;
            foreach (EdnsOption option in configuration.EdnsOptions.Options) {
                if (option.Code == 12) return true;
            }
            return false;
        }

        private static DnsResponse Failure(string name, DnsRecordType type, Configuration configuration,
            DnsResponseCode status, string error) {
            var response = new DnsResponse {
                Questions = [new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverQuic, Type = type, OriginalName = name }],
                Status = status,
                Error = error
            };
            response.AddServerDetails(configuration, Transport.Quic);
            return response;
        }

        private static CancellationTokenSource CreateTimeoutTokenSource(int timeoutMilliseconds, CancellationToken cancellationToken) {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMilliseconds <= 0) linked.Cancel();
            else linked.CancelAfter(timeoutMilliseconds);
            return linked;
        }
    }
    #pragma warning restore CA2252
#endif
}
