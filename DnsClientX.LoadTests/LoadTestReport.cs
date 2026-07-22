using DnsClientX;

namespace DnsClientX.LoadTests;

internal sealed class LoadTestReport {
    public DateTimeOffset StartedAtUtc { get; init; }
    public string Server { get; init; } = string.Empty;
    public int Port { get; init; }
    public string Name { get; init; } = string.Empty;
    public DnsRecordType Type { get; init; }
    public DnsRequestFormat Transport { get; init; }
    public int RequestsPerScenario { get; init; }
    public int WarmupRequests { get; init; }
    public IReadOnlyList<LoadScenarioReport> Scenarios { get; init; } = Array.Empty<LoadScenarioReport>();
}

internal sealed class LoadScenarioReport {
    public int Concurrency { get; init; }
    public int Requests { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public double DurationMilliseconds { get; init; }
    public double ThroughputPerSecond { get; init; }
    public double P50Milliseconds { get; init; }
    public double P95Milliseconds { get; init; }
    public double P99Milliseconds { get; init; }
    public IReadOnlyDictionary<string, int> Failures { get; init; } = new Dictionary<string, int>();
}
