using System;
using System.Net;

namespace DnsClientX;

/// <summary>Retains endpoint lookup failures across shared flights, cache entries, and transport boundaries.</summary>
internal readonly struct DnsServerResolutionResult {
    internal DnsServerResolutionResult(IPAddress? address, string? error,
        DnsQueryErrorCode errorCode = DnsQueryErrorCode.None, Exception? exception = null) {
        Address = address;
        Error = error;
        ErrorCode = errorCode;
        Exception = exception;
    }

    internal IPAddress? Address { get; }
    internal string? Error { get; }
    internal DnsQueryErrorCode ErrorCode { get; }
    internal Exception? Exception { get; }

    internal void Deconstruct(out IPAddress? address, out string? error) {
        address = Address;
        error = Error;
    }

    /// <summary>Preserves the existing exception contract for operations that cannot return a DNS response.</summary>
    internal DnsClientException CreateException() {
        string message = Error ?? "DNS server lookup returned no address.";
        var response = new DnsResponse {
            Status = DnsResponseCode.ServerFailure,
            Error = message,
            ErrorCode = ErrorCode,
            Exception = Exception
        };
        return Exception == null ? new DnsClientException(message, response)
            : new DnsClientException(message, Exception) { Response = response };
    }
}
