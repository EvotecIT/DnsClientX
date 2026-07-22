using BenchmarkDotNet.Attributes;

namespace DnsClientX.Benchmarks;

/// <summary>Measures full DNS wire-response parsing independently of network latency.</summary>
[MemoryDiagnoser]
[BenchmarkCategory("Parsing")]
public class DnsWireParserBenchmark {
    private byte[] _response = null!;

    /// <summary>Gets or sets the number of A answers in the deterministic response.</summary>
    [Params(1, 16, 64)]
    public int AnswerCount { get; set; }

    /// <summary>Creates one immutable response payload for the selected answer count.</summary>
    [GlobalSetup]
    public void Setup() {
        var query = new DnsMessage("benchmark.example", DnsRecordType.A,
            new DnsMessageOptions(TransactionId: 0x1234));
        _response = ControlledDnsMessages.CreateAResponse(query.SerializeDnsWireFormat(), AnswerCount);
    }

    /// <summary>Parses the complete DNS response and returns the observed answer count.</summary>
    [Benchmark]
    public async Task<int> ParseResponse() {
        DnsResponse response = await DnsWire.DeserializeDnsWireFormat(null, false, _response)
            .ConfigureAwait(false);
        return response.Answers.Length;
    }
}
