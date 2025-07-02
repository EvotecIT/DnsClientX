using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace DnsClientX.Benchmarks;

[MemoryDiagnoser]
public class DomainBenchmark {
    private readonly string[] _domains = ["google.com", "github.com", "cloudflare.com"];

    [Benchmark]
    public async Task Udp() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverUDP);
        }
    }

    [Benchmark]
    public async Task Tcp() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverTCP);
        }
    }

    [Benchmark]
    public async Task Dot() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverTLS);
        }
    }

    [Benchmark]
    public async Task Doh() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverHttps);
        }
    }

    [Benchmark]
    public async Task Doq() {
        foreach (var domain in _domains) {
            await ClientX.QueryDns(domain, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverQuic);
        }
    }
}
