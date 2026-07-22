using DnsClientX;

namespace DnsClientX.LoadTests;

internal sealed class LoadTestOptions {
    internal string Server { get; private set; } = "127.0.0.1";
    internal int Port { get; private set; } = 53;
    internal string Name { get; private set; } = "example.com";
    internal DnsRecordType Type { get; private set; } = DnsRecordType.A;
    internal DnsRequestFormat Format { get; private set; } = DnsRequestFormat.DnsOverUDP;
    internal IReadOnlyList<int> Concurrency { get; private set; } = new[] { 1, 32, 128 };
    internal int Requests { get; private set; } = 256;
    internal int Warmup { get; private set; } = 8;
    internal int TimeoutMs { get; private set; } = 2000;
    internal string? TlsServerName { get; private set; }
    internal string? JsonPath { get; private set; }
    internal bool ShowHelp { get; private set; }

    internal static LoadTestOptions Parse(string[] args) {
        var options = new LoadTestOptions();
        bool portSpecified = false;

        for (int index = 0; index < args.Length; index++) {
            string argument = args[index];
            switch (argument.ToLowerInvariant()) {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    return options;
                case "--server":
                    options.Server = ReadValue(args, ref index, argument);
                    break;
                case "--port":
                    options.Port = ParsePositiveInt(ReadValue(args, ref index, argument), argument, ushort.MaxValue);
                    portSpecified = true;
                    break;
                case "--name":
                    options.Name = ReadValue(args, ref index, argument);
                    break;
                case "--type":
                    string type = ReadValue(args, ref index, argument);
                    if (!Enum.TryParse(type, true, out DnsRecordType recordType)) {
                        throw new ArgumentException($"Unknown DNS record type '{type}'.");
                    }
                    options.Type = recordType;
                    break;
                case "--transport":
                    options.Format = ParseTransport(ReadValue(args, ref index, argument));
                    break;
                case "--concurrency":
                    options.Concurrency = ParseConcurrency(ReadValue(args, ref index, argument));
                    break;
                case "--requests":
                    options.Requests = ParsePositiveInt(ReadValue(args, ref index, argument), argument, 10_000_000);
                    break;
                case "--warmup":
                    options.Warmup = ParseNonNegativeInt(ReadValue(args, ref index, argument), argument, 1_000_000);
                    break;
                case "--timeout":
                    options.TimeoutMs = ParsePositiveInt(ReadValue(args, ref index, argument), argument, 600_000);
                    break;
                case "--tls-server-name":
                    options.TlsServerName = ReadValue(args, ref index, argument);
                    break;
                case "--json":
                    options.JsonPath = ReadValue(args, ref index, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{argument}'. Use --help for supported options.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.Server)) {
            throw new ArgumentException("--server must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(options.Name)) {
            throw new ArgumentException("--name must not be empty.");
        }
        if (!portSpecified) {
            options.Port = options.Format switch {
                DnsRequestFormat.DnsOverTLS or DnsRequestFormat.DnsOverQuic => 853,
                DnsRequestFormat.DnsOverHttps or DnsRequestFormat.DnsOverHttpsPOST => 443,
                _ => 53
            };
        }
        return options;
    }

    private static string ReadValue(string[] args, ref int index, string argument) {
        if (++index >= args.Length) {
            throw new ArgumentException($"{argument} requires a value.");
        }
        return args[index];
    }

    private static int ParsePositiveInt(string value, string argument, int maximum) {
        int result = ParseNonNegativeInt(value, argument, maximum);
        if (result == 0) {
            throw new ArgumentOutOfRangeException(argument, $"{argument} must be greater than zero.");
        }
        return result;
    }

    private static int ParseNonNegativeInt(string value, string argument, int maximum) {
        if (!int.TryParse(value, out int result) || result < 0 || result > maximum) {
            throw new ArgumentOutOfRangeException(argument, $"{argument} must be between 0 and {maximum}.");
        }
        return result;
    }

    private static IReadOnlyList<int> ParseConcurrency(string value) {
        int[] values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => ParsePositiveInt(item, "--concurrency", 4096))
            .Distinct()
            .ToArray();
        if (values.Length == 0) {
            throw new ArgumentException("--concurrency requires at least one positive integer.");
        }
        return values;
    }

    private static DnsRequestFormat ParseTransport(string value) => value.ToLowerInvariant() switch {
        "udp" => DnsRequestFormat.DnsOverUDP,
        "tcp" => DnsRequestFormat.DnsOverTCP,
        "dot" or "tls" => DnsRequestFormat.DnsOverTLS,
        "doh" or "https" => DnsRequestFormat.DnsOverHttps,
        "doq" or "quic" => DnsRequestFormat.DnsOverQuic,
        _ => throw new ArgumentException($"Unknown transport '{value}'. Use udp, tcp, dot, doh, or doq.")
    };
}
