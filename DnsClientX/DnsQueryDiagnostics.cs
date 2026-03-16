using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Shared diagnostics helpers for classifying DNS query failures and suspicious resolver responses.
    /// </summary>
    public static class DnsQueryDiagnostics {
        /// <summary>
        /// Determines whether the supplied exception represents a transient transport or resolver failure.
        /// </summary>
        /// <param name="exception">Exception to classify.</param>
        /// <returns><c>true</c> when retry may succeed; otherwise <c>false</c>.</returns>
        public static bool IsTransient(Exception exception) {
            if (exception is null) {
                throw new ArgumentNullException(nameof(exception));
            }

            if (exception is DnsClientException dnsException) {
                DnsResponse? response = null;

                if (dnsException.Response is not null) {
                    response = dnsException.Response;
                } else if (dnsException.Data.Contains("DnsResponse") && dnsException.Data["DnsResponse"] is DnsResponse typedResponse) {
                    response = typedResponse;
                }

                if (response is not null) {
                    return IsTransient(response);
                }
            }

            if (exception is HttpRequestException httpException) {
                if (HasNetworkOrSslConnectionError(httpException.Message) || HasNetworkOrSslConnectionError(httpException.InnerException?.Message)) {
                    return true;
                }
            }

            if (exception is SocketException socketException) {
                if (socketException.SocketErrorCode == SocketError.ConnectionReset ||
                    socketException.SocketErrorCode == SocketError.NetworkUnreachable) {
                    return true;
                }
            }

            return exception is DnsClientException ||
                   exception is TaskCanceledException ||
                   exception is TimeoutException ||
                   exception is HttpRequestException ||
                   exception is SocketException ||
                   exception is IOException ||
                   (exception.InnerException != null && IsTransient(exception.InnerException));
        }

        /// <summary>
        /// Determines whether the supplied DNS response should be treated as transient.
        /// </summary>
        /// <param name="response">Response to classify.</param>
        /// <returns><c>true</c> when retry may succeed; otherwise <c>false</c>.</returns>
        public static bool IsTransient(DnsResponse response) {
            if (response is null) {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.Status == DnsResponseCode.ServerFailure) {
                if (response.Answers != null && response.Answers.Length > 0) {
                    return false;
                }

                if (!string.IsNullOrEmpty(response.Error) && HasNetworkOrSslConnectionError(response.Error)) {
                    return true;
                }

                return true;
            }

            if (response.Status == DnsResponseCode.Refused ||
                response.Status == DnsResponseCode.NotImplemented) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a resolver returned any meaningful envelope data.
        /// </summary>
        /// <param name="response">Response to inspect.</param>
        /// <returns><c>true</c> when at least one response section has data; otherwise <c>false</c>.</returns>
        public static bool HasEnvelopeData(DnsResponse response) {
            if (response is null) {
                throw new ArgumentNullException(nameof(response));
            }

            return GetCount(response.Questions) > 0 ||
                   GetCount(response.Answers) > 0 ||
                   GetCount(response.Authorities) > 0 ||
                   GetCount(response.Additional) > 0;
        }

        /// <summary>
        /// Detects suspicious empty "success" responses where the resolver returned no sections and no truncation signal.
        /// </summary>
        /// <param name="response">Response to inspect.</param>
        /// <returns><c>true</c> when the response looks like an empty success envelope; otherwise <c>false</c>.</returns>
        public static bool IsSuspiciousEmptySuccess(DnsResponse response) {
            if (response is null) {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.IsTruncated) {
                return false;
            }

            return !HasEnvelopeData(response);
        }

        private static bool HasNetworkOrSslConnectionError(string? errorMessage) {
            if (string.IsNullOrEmpty(errorMessage)) {
                return false;
            }

            var message = errorMessage!;
            return message.IndexOf("ssl connection could not be established", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("connection was forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("certificate validation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("handshake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("network error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("an error occurred while sending the request", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("unexpected error occurred on a send", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("underlying connection was closed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetCount<T>(T[]? items) {
            return items?.Length ?? 0;
        }
    }
}
