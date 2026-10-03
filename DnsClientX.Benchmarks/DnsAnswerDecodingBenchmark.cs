using BenchmarkDotNet.Attributes;

namespace DnsClientX.Benchmarks;

/// <summary>Measures public TXT decoding and typed construction independently of network latency.</summary>
[MemoryDiagnoser]
[BenchmarkCategory("Parsing")]
public class DnsAnswerDecodingBenchmark {
    private DnsAnswer _answer;

    /// <summary>Number of character-strings in one TXT resource record.</summary>
    [Params(1, 12)]
    public int ChunkCount { get; set; }

    /// <summary>Prepares presentation data without measuring input generation.</summary>
    [GlobalSetup]
    public void Setup() {
        _answer = new DnsAnswer {
            Type = DnsRecordType.TXT,
            DataRaw = string.Join(" ", Enumerable.Repeat("\"v=spf1 include:mail.example -all\"", ChunkCount))
        };
        string expected = string.Concat(Enumerable.Repeat("v=spf1 include:mail.example -all", ChunkCount));
        if (_answer.Data != expected || _answer.TypedRecord is not TxtRecord typed || typed.Text != expected) {
            throw new InvalidOperationException("TXT benchmark decoding failed its record-fidelity contract.");
        }
    }

    /// <summary>Decodes one complete TXT resource record.</summary>
    [Benchmark]
    public string Decode() => _answer.Data;

    /// <summary>Constructs the typed TXT representation through the public factory.</summary>
    [Benchmark]
    public object? Typed() => _answer.TypedRecord;
}
