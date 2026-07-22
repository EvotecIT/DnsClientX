using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Implements DNS over TLS (DoT) resolution using raw wire format messages.
    /// </summary>
    internal static class DnsWireResolveDot {
        /// <summary>
        /// Sends a DNS query in wire format using DNS over TLS (DoT) and returns the response.
        /// </summary>
        /// <param name="dnsServer"></param>
        /// <param name="port"></param>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
        /// <param name="debug">if set to <c>true</c> [debug].</param>
        /// <param name="endpointConfiguration">Configuration used for server details.</param>
        /// <param name="ignoreCertificateErrors">Ignore certificate validation errors.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <param name="connectionPool">Optional persistent connection owner.</param>
        /// <returns>The DNS response.</returns>
        /// <exception cref="System.ArgumentNullException">name - Name is null or empty.</exception>
        /// <exception cref="System.Exception">
        /// Failed to read the length prefix of the response.
        /// or
        /// The stream was closed before the entire response could be read.
        /// </exception>
        internal static async Task<DnsResponse> ResolveWireFormatDoT(string dnsServer, int port, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, bool ignoreCertificateErrors, CancellationToken cancellationToken, DnsStreamConnectionPool? connectionPool = null) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name), "Name is null or empty.");

            var query = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, endpointConfiguration,
                checkingDisabled: endpointConfiguration.CheckingDisabled || validateDnsSec);
            var queryBytes = query.SerializeDnsWireFormat();

            if (debug) {
                var combinedQueryBytes = new byte[queryBytes.Length + 2];
                combinedQueryBytes[0] = (byte)(queryBytes.Length >> 8);
                combinedQueryBytes[1] = (byte)queryBytes.Length;
                Buffer.BlockCopy(queryBytes, 0, combinedQueryBytes, 2, queryBytes.Length);
                // Print the combined DNS query bytes to the logger
                Settings.Logger.WriteDebug($"Query Name: " + name + " type: " + type);
                Settings.Logger.WriteDebug($"Query before combination: {BitConverter.ToString(queryBytes)}");
                Settings.Logger.WriteDebug($"Sending combined query: {BitConverter.ToString(combinedQueryBytes)}");

                Settings.Logger.WriteDebug($"Transaction ID: {BitConverter.ToString(queryBytes, 0, 2)}");
                Settings.Logger.WriteDebug($"Flags: {BitConverter.ToString(queryBytes, 2, 2)}");
                Settings.Logger.WriteDebug($"Question count: {BitConverter.ToString(queryBytes, 4, 2)}");
                Settings.Logger.WriteDebug($"Answer count: {BitConverter.ToString(queryBytes, 6, 2)}");
                Settings.Logger.WriteDebug($"Authority records count: {BitConverter.ToString(queryBytes, 8, 2)}");
                Settings.Logger.WriteDebug($"Additional records count: {BitConverter.ToString(queryBytes, 10, 2)}");
                Settings.Logger.WriteDebug($"Question name: {BitConverter.ToString(queryBytes, 12, queryBytes.Length - 12 - 4)}");
                Settings.Logger.WriteDebug($"Question type: {BitConverter.ToString(queryBytes, queryBytes.Length - 4, 2)}");
                Settings.Logger.WriteDebug($"Question class: {BitConverter.ToString(queryBytes, queryBytes.Length - 2, 2)}");
            }

            DotFailurePhase failurePhase = DotFailurePhase.Connect;
            try {
                var (address, resolveError) = await DnsServerResolver.ResolveAsync(
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
                if (address == null) throw new DnsClientException(resolveError ?? $"Host '{dnsServer}' resolved to no addresses.");

                string targetHost = string.IsNullOrWhiteSpace(endpointConfiguration.TlsServerName)
                    ? dnsServer
                    : endpointConfiguration.TlsServerName!;
#if NET6_0_OR_GREATER
                SslProtocols protocols = SslProtocols.Tls12 | SslProtocols.Tls13;
#else
                SslProtocols protocols = SslProtocols.Tls12;
#endif
                using DnsStreamConnectionPool? ownedPool = connectionPool == null ? new DnsStreamConnectionPool() : null;
                DnsStreamConnectionPool activePool = connectionPool ?? ownedPool!;
                byte[] responseBuffer = await activePool.QueryTlsAsync(
                    address,
                    port,
                    endpointConfiguration.LocalEndPoint,
                    targetHost,
                    ignoreCertificateErrors,
                    protocols,
                    queryBytes,
                    endpointConfiguration.TimeOut,
                    endpointConfiguration.MaxTcpQueriesPerConnection,
                    cancellationToken).ConfigureAwait(false);
                failurePhase = DotFailurePhase.Exchange;

                // Deserialize the response from DNS wire format
                var response = await DnsWire.DeserializeDnsWireResponse(null, debug, responseBuffer, query).ConfigureAwait(false);
                response.AddServerDetails(endpointConfiguration);
                return response;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                var (status, errorCode) = MapFailure(ex, failurePhase);
                var failureResponse = new DnsResponse {
                    Questions = [ new DnsQuestion { Name = name, RequestFormat = DnsRequestFormat.DnsOverTLS, Type = type, OriginalName = name } ],
                    Status = status,
                    ErrorCode = errorCode,
                    Exception = ex
                };
                failureResponse.AddServerDetails(endpointConfiguration);
                failureResponse.Error = $"Failed to query type {type} of \"{name}\" => {ex.Message}";
                throw new DnsClientException(failureResponse.Error!, ex) { Response = failureResponse };
            }
        }

        private static (DnsResponseCode Status, DnsQueryErrorCode ErrorCode) MapFailure(Exception ex, DotFailurePhase phase) {
            if (ex is DnsStreamConnectionException && ex.InnerException != null) {
                return MapFailure(ex.InnerException, phase);
            }
            return ex switch {
                TimeoutException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.Timeout),
                SocketException => phase == DotFailurePhase.Connect
                    ? (DnsResponseCode.Refused, DnsQueryErrorCode.Network)
                    : (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                AuthenticationException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.ServFail),
                EndOfStreamException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                IOException ioEx when ioEx.InnerException is SocketException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                IOException => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.InvalidResponse),
                _ => (DnsResponseCode.ServerFailure, DnsQueryErrorCode.ServFail)
            };
        }

        private enum DotFailurePhase {
            Connect,
            Authenticate,
            Exchange
        }
    }
}
