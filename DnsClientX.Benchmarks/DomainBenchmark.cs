using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace DnsClientX.Benchmarks;

/// <summary>
/// Benchmark suite measuring query performance over different transports.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("External", "Transport")]
public class DomainBenchmark {
    private readonly string _domain = "github.com";
    private ClientX _udp = null!;
    private ClientX _tcp = null!;
    private ClientX _dot = null!;
    private ClientX _doh = null!;
    private ClientX _doq = null!;

    /// <summary>Creates one client per transport so the benchmark includes connection reuse.</summary>
    [GlobalSetup]
    public void Setup() {
        _udp = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverUDP);
        _tcp = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverTCP);
        _dot = new ClientX("one.one.one.one", DnsRequestFormat.DnsOverTLS);
        _doh = new ClientX(new Uri("https://cloudflare-dns.com/dns-query"), DnsRequestFormat.DnsOverHttps);
        _doq = new ClientX(DnsEndpoint.Quad9Quic);
    }

    /// <summary>Releases transport clients after the benchmark run.</summary>
    [GlobalCleanup]
    public void Cleanup() {
        _udp.Dispose();
        _tcp.Dispose();
        _dot.Dispose();
        _doh.Dispose();
        _doq.Dispose();
    }

    /// <summary>Benchmark querying over UDP.</summary>
    [Benchmark]
    public Task<DnsResponse> Udp() => Resolve(_udp);

    /// <summary>Benchmark querying over TCP.</summary>
    [Benchmark]
    public Task<DnsResponse> Tcp() => Resolve(_tcp);

    /// <summary>Benchmark querying over TLS.</summary>
    [Benchmark]
    public Task<DnsResponse> Dot() => Resolve(_dot);

    /// <summary>Benchmark querying over HTTPS.</summary>
    [Benchmark]
    public Task<DnsResponse> Doh() => Resolve(_doh);

    /// <summary>Benchmark querying over QUIC.</summary>
    [Benchmark]
    public Task<DnsResponse> Doq() => Resolve(_doq);

    private async Task<DnsResponse> Resolve(ClientX client) {
        DnsResponse response = await client.Resolve(_domain, DnsRecordType.A, retryOnTransient: false)
            .ConfigureAwait(false);
        if (response.Status != DnsResponseCode.NoError || response.Answers == null || response.Answers.Length == 0) {
            throw new InvalidOperationException(
                $"The {client.EndpointConfiguration.RequestFormat} benchmark query for '{_domain}' failed: " +
                $"{response.Status} {response.Error}");
        }
        return response;
    }
}
