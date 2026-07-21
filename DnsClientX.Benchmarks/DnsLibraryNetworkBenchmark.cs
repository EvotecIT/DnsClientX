using System.Net;
using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using DnsClient;

namespace DnsClientX.Benchmarks;

/// <summary>
/// Compares uncached UDP query paths against the same controlled resolver. Set
/// DNS_BENCHMARK_SERVER, DNS_BENCHMARK_PORT and DNS_BENCHMARK_NAME to use a lab resolver;
/// otherwise the benchmark owns a loopback responder.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 5, iterationCount: 20, invocationCount: 1)]
[BenchmarkCategory("LibraryComparison", "Network")]
public class DnsLibraryNetworkBenchmark {
    private CancellationTokenSource? _serverCancellation;
    private UdpClient? _server;
    private Task? _serverTask;
    private ClientX _dnsClientX = null!;
    private LookupClient _dnsClientNet = null!;
    private string _queryName = null!;

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
            _serverCancellation = new CancellationTokenSource();
            _server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            serverAddress = IPAddress.Loopback;
            serverPort = ((IPEndPoint)_server.Client.LocalEndPoint!).Port;
            _serverTask = RunControlledResolver(_server, _serverCancellation.Token);
        } else {
            serverAddress = IPAddress.Parse(configuredServer);
            serverPort = configuredPort;
        }

        _dnsClientX = new ClientX(new Configuration(serverAddress.ToString(), DnsRequestFormat.DnsOverUDP) {
            Port = serverPort,
            TimeOut = 2000,
            UseTcpFallback = true
        });
        _dnsClientNet = new LookupClient(new LookupClientOptions(new NameServer(serverAddress, serverPort)) {
            UseCache = false,
            Retries = 0,
            Timeout = TimeSpan.FromSeconds(2),
            UseTcpFallback = true,
            UseRandomNameServer = false
        });
    }

    /// <summary>Queries through DnsClient.NET 1.8.0 as the comparison baseline.</summary>
    [Benchmark(Baseline = true)]
    public async Task<int> DnsClientNet() {
        IDnsQueryResponse response = await _dnsClientNet.QueryAsync(_queryName, QueryType.A).ConfigureAwait(false);
        return response.Answers.Count;
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
        return response.Answers.Length;
    }

    /// <summary>Releases clients and the controlled responder.</summary>
    [GlobalCleanup]
    public async Task Cleanup() {
        _dnsClientX.Dispose();
        _serverCancellation?.Cancel();
        _server?.Dispose();
        if (_serverTask != null) {
            try {
                await _serverTask.ConfigureAwait(false);
            } catch (OperationCanceledException) {
                // Expected during benchmark cleanup.
            } catch (ObjectDisposedException) {
                // Expected when ReceiveAsync is released by socket disposal.
            } catch (SocketException) {
                // Expected when ReceiveAsync is released by socket disposal.
            }
        }
        _serverCancellation?.Dispose();
    }

    private static async Task RunControlledResolver(UdpClient server, CancellationToken cancellationToken) {
        using CancellationTokenRegistration registration = cancellationToken.Register(server.Dispose);
        while (!cancellationToken.IsCancellationRequested) {
            UdpReceiveResult request = await server.ReceiveAsync().ConfigureAwait(false);
            byte[] response = CreateAResponse(request.Buffer);
            await server.SendAsync(response, response.Length, request.RemoteEndPoint).ConfigureAwait(false);
        }
    }

    private static byte[] CreateAResponse(byte[] query) {
        int questionEnd = 12;
        while (questionEnd < query.Length && query[questionEnd] != 0) {
            int labelLength = query[questionEnd];
            if (labelLength > 63 || questionEnd + labelLength >= query.Length) {
                throw new InvalidOperationException("The controlled resolver received an invalid question name.");
            }
            questionEnd += labelLength + 1;
        }
        questionEnd += 5; // root label plus QTYPE and QCLASS
        if (questionEnd > query.Length) throw new InvalidOperationException("The controlled resolver received a truncated question.");

        var response = new byte[questionEnd + 16];
        Buffer.BlockCopy(query, 0, response, 0, questionEnd);
        response[2] = (byte)(response[2] | 0x80);
        response[3] = (byte)(response[3] | 0x80);
        response[6] = 0;
        response[7] = 1;
        response[8] = 0;
        response[9] = 0;
        response[10] = 0;
        response[11] = 0;
        int offset = questionEnd;
        response[offset++] = 0xc0;
        response[offset++] = 0x0c;
        response[offset++] = 0;
        response[offset++] = 1;
        response[offset++] = 0;
        response[offset++] = 1;
        response[offset++] = 0;
        response[offset++] = 0;
        response[offset++] = 0;
        response[offset++] = 60;
        response[offset++] = 0;
        response[offset++] = 4;
        response[offset++] = 192;
        response[offset++] = 0;
        response[offset++] = 2;
        response[offset] = 10;
        return response;
    }
}
