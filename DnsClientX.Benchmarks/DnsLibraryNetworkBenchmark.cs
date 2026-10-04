using System.Net;
using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using DnsClient;
using ArDns = ARSoft.Tools.Net.Dns;
using KapDns = DNS.Client;
using KapProtocol = DNS.Protocol;

namespace DnsClientX.Benchmarks;

/// <summary>
/// Compares uncached UDP/TCP query paths against the same controlled resolver. Set
/// DNS_BENCHMARK_SERVER, DNS_BENCHMARK_PORT and DNS_BENCHMARK_NAME to use a lab resolver;
/// otherwise the benchmark owns a loopback responder.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 2, warmupCount: 5, iterationCount: 15, invocationCount: 1024)]
[BenchmarkCategory("LibraryComparison", "Network")]
public class DnsLibraryNetworkBenchmark {
    private ControlledDnsResolver? _server;
    private ClientX _dnsClientX = null!;
    private LookupClient _dnsClientNet = null!;
    private ArDns.DnsClient _arSoft = null!;
    private KapDns.DnsClient _kapetan = null!;
    private string _queryName = null!;
    private bool _qualify;

    /// <summary>Number of records returned by the controlled resolver.</summary>
    [ParamsSource(nameof(AnswerCounts))]
    public int AnswerCount { get; set; }

    /// <summary>The controlled payload sizes, or the lab's explicitly expected count.</summary>
    public IEnumerable<int> AnswerCounts {
        get {
            if (Environment.GetEnvironmentVariable("DNS_BENCHMARK_SERVER") == null) return new[] { 1, 16 };
            string count = Environment.GetEnvironmentVariable("DNS_BENCHMARK_ANSWERS") ?? "1";
            if (!int.TryParse(count, out int expected) || expected < 1 || expected > ushort.MaxValue)
                throw new InvalidOperationException("DNS_BENCHMARK_ANSWERS must be between 1 and 65535.");
            return new[] { expected };
        }
    }

    /// <summary>Transport requested explicitly by every comparison client.</summary>
    [Params(Transport.Udp, Transport.Tcp)]
    public Transport QueryTransport { get; set; }

    /// <summary>Creates reusable clients and a controlled loopback resolver unless a lab resolver is configured.</summary>
    [GlobalSetup]
    public void Setup() {
        _queryName = Environment.GetEnvironmentVariable("DNS_BENCHMARK_NAME") ?? "benchmark.ad.evotec.xyz";
        string? configuredServer = Environment.GetEnvironmentVariable("DNS_BENCHMARK_SERVER");
        int configuredPort = int.TryParse(Environment.GetEnvironmentVariable("DNS_BENCHMARK_PORT"), out int port)
            ? port
            : 53;

        IPAddress serverAddress;
        int serverPort;
        if (configuredServer == null) {
            _server = new ControlledDnsResolver(AnswerCount);
            serverAddress = IPAddress.Loopback;
            serverPort = _server.Port;
        } else {
            serverAddress = IPAddress.Parse(configuredServer);
            serverPort = configuredPort;
        }

        _dnsClientX = new ClientX(new Configuration(serverAddress.ToString(), DnsRequestFormatMapper.FromTransport(QueryTransport)) {
            Port = serverPort,
            TimeOut = 2000,
            UseTcpFallback = true,
            EnableTcpConnectionReuse = true
        });
        _dnsClientNet = new LookupClient(new LookupClientOptions(new NameServer(serverAddress, serverPort)) {
            UseCache = false,
            Retries = 0,
            Timeout = TimeSpan.FromSeconds(2),
            UseTcpFallback = true,
            UseTcpOnly = QueryTransport == Transport.Tcp,
            UseRandomNameServer = false
        });
        ArDns.IClientTransport[] transports = QueryTransport == Transport.Tcp
            ? new ArDns.IClientTransport[] { new ArDns.TcpClientTransport(serverPort) }
            : new ArDns.IClientTransport[] { new ArDns.UdpClientTransport(serverPort), new ArDns.TcpClientTransport(serverPort) };
        _arSoft = new ArDns.DnsClient(new[] { serverAddress }, transports, disposeTransport: true, queryTimeout: 2000) { IsResponseValidationEnabled = true };
        var endpoint = new IPEndPoint(serverAddress, serverPort);
        var tcp = new KapDns.RequestResolver.TcpRequestResolver(endpoint);
        _kapetan = QueryTransport == Transport.Tcp ? new KapDns.DnsClient(tcp)
            : new KapDns.DnsClient(new KapDns.RequestResolver.UdpRequestResolver(endpoint, tcp, 2000));

        // Exercise the actual public paths before timing. An external lab must supply the same RRset.
        _qualify = true;
        ValidateCount(DnsClientNet().GetAwaiter().GetResult());
        ValidateCount(DnsClientXCurrent().GetAwaiter().GetResult());
        ValidateCount(ArSoft().GetAwaiter().GetResult());
        ValidateCount(KapetanDns().GetAwaiter().GetResult());
        _qualify = false;
    }

    /// <summary>Queries through DnsClient.NET 1.8.0 as the comparison baseline.</summary>
    [Benchmark(Baseline = true)]
    public async Task<int> DnsClientNet() {
        IDnsQueryResponse response = await _dnsClientNet.QueryAsync(_queryName, QueryType.A).ConfigureAwait(false);
        if (_qualify) ValidatePayload(response.Answers.OfType<DnsClient.Protocol.ARecord>().Select(answer => answer.Address.ToString()));
        return ValidateCount(response.Answers.Count);
    }

    /// <summary>Queries through the current DnsClientX source with equivalent cache and retry settings.</summary>
    [Benchmark]
    public async Task<int> DnsClientXCurrent() {
        DnsResponse response = await _dnsClientX.Resolve(
            _queryName,
            DnsRecordType.A,
            retryOnTransient: false).ConfigureAwait(false);
        if (response.Status != DnsResponseCode.NoError) {
            throw new InvalidOperationException($"DnsClientX returned {response.Status}: {response.Error}");
        }
        if (_qualify) ValidatePayload(response.Answers.Select(answer => answer.Data));
        return ValidateCount(response.Answers.Length);
    }

    /// <summary>Queries through ARSoft.Tools.Net 3.6.1 without a resolver cache.</summary>
    [Benchmark]
    public async Task<int> ArSoft() {
        ArDns.DnsMessage? response = await _arSoft.ResolveAsync(
            ARSoft.Tools.Net.DomainName.Parse(_queryName), ArDns.RecordType.A).ConfigureAwait(false);
        if (response == null || response.ReturnCode != ArDns.ReturnCode.NoError) {
            throw new InvalidOperationException("ARSoft.Tools.Net did not return a successful response.");
        }
        if (_qualify) ValidatePayload(response.AnswerRecords.OfType<ArDns.ARecord>().Select(answer => answer.Address.ToString()));
        return ValidateCount(response.AnswerRecords.Count);
    }

    /// <summary>Queries through the DNS 7.0.0 client with UDP and TCP fallback.</summary>
    [Benchmark]
    public async Task<int> KapetanDns() {
        KapDns.ClientRequest request = _kapetan.Create();
        request.RecursionDesired = true;
        request.Questions.Add(new KapProtocol.Question(
            KapProtocol.Domain.FromString(_queryName), KapProtocol.RecordType.A));
        // This package has no TCP timeout setting. Bound its public operation explicitly.
        KapProtocol.IResponse response = await request.Resolve().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        if (response.ResponseCode != KapProtocol.ResponseCode.NoError) {
            throw new InvalidOperationException("DNS did not return a successful response.");
        }
        if (_qualify) ValidatePayload(response.AnswerRecords.OfType<KapProtocol.ResourceRecords.IPAddressResourceRecord>().Select(answer => answer.IPAddress.ToString()));
        return ValidateCount(response.AnswerRecords.Count);
    }

    private int ValidateCount(int observed) {
        if (observed != AnswerCount) throw new InvalidOperationException($"Expected {AnswerCount} answers, received {observed}.");
        return observed;
    }

    private void ValidatePayload(IEnumerable<string> addresses) {
        if (_server == null) {
            ValidateCount(addresses.Count());
            return; // The lab controls its payload; count remains the required contract.
        }
        string[] expected = Enumerable.Range(1, AnswerCount).Select(index => $"192.0.2.{index}").ToArray();
        if (!addresses.SequenceEqual(expected)) throw new InvalidOperationException("Comparison client returned different A record data.");
    }

    /// <summary>Releases clients and the controlled responder.</summary>
    [GlobalCleanup]
    public async Task Cleanup() {
        _dnsClientX.Dispose();
        ((IDisposable)_arSoft).Dispose();
        if (_server != null) await _server.DisposeAsync().ConfigureAwait(false);
    }
}
