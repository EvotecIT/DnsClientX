using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace DnsClientX.Benchmarks;

/// <summary>
/// Benchmark suite measuring query performance over different transports.
/// </summary>
[MemoryDiagnoser]
public class DomainBenchmark {
    private readonly string[] _domains = ["google.com", "github.com", "cloudflare.com"];

    [Benchmark]
    /// <summary>Benchmark querying over UDP.</summary>
    public async Task Udp() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverUDP);
        }
    }

    [Benchmark]
    /// <summary>Benchmark querying over TCP.</summary>
    public async Task Tcp() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverTCP);
        }
    }

    [Benchmark]
    /// <summary>Benchmark querying over TLS.</summary>
    public async Task Dot() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverTLS);
        }
    }

    [Benchmark]
    /// <summary>Benchmark querying over HTTPS.</summary>
    public async Task Doh() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverHttps);
        }
    }

    [Benchmark]
    /// <summary>Benchmark querying over QUIC.</summary>
    public async Task Doq() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverQuic);
        }
    }
}
