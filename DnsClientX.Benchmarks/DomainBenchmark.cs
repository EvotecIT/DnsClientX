using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace DnsClientX.Benchmarks;

/// <summary>
/// Benchmark suite measuring query performance over different transports.
/// </summary>
[MemoryDiagnoser]
public class DomainBenchmark {
    private readonly string[] _domains = ["google.com", "github.com", "cloudflare.com"];

    /// <summary>Benchmark querying over UDP.</summary>
    [Benchmark]
    public async Task Udp() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverUDP);
        }
    }

    /// <summary>Benchmark querying over TCP.</summary>
    [Benchmark]
    public async Task Tcp() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverTCP);
        }
    }

    /// <summary>Benchmark querying over TLS.</summary>
    [Benchmark]
    public async Task Dot() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverTLS);
        }
    }

    /// <summary>Benchmark querying over HTTPS.</summary>
    [Benchmark]
    public async Task Doh() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverHttps);
        }
    }

    /// <summary>Benchmark querying over QUIC.</summary>
    [Benchmark]
    public async Task Doq() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverQuic);
        }
    }
}
