namespace DnsClientX {
    internal readonly struct DnsSecValidationResult {
        internal DnsSecValidationResult(DnsSecValidationStatus status, string message) {
            Status = status;
            Message = message;
        }

        internal DnsSecValidationStatus Status { get; }
        internal string Message { get; }

        internal static DnsSecValidationResult Secure(string message) => new(DnsSecValidationStatus.Secure, message);
        internal static DnsSecValidationResult Insecure(string message) => new(DnsSecValidationStatus.Insecure, message);
        internal static DnsSecValidationResult Bogus(string message) => new(DnsSecValidationStatus.Bogus, message);
        internal static DnsSecValidationResult Indeterminate(string message) => new(DnsSecValidationStatus.Indeterminate, message);
    }
}
