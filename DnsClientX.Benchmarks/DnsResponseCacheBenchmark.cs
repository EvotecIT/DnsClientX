using BenchmarkDotNet.Attributes;

namespace DnsClientX.Benchmarks;

/// <summary>Measures isolated in-memory cache hit and miss paths.</summary>
[MemoryDiagnoser]
[BenchmarkCategory("Cache")]
public class DnsResponseCacheBenchmark {
    private const string HitKey = "benchmark.example|A";
    private DnsResponseCache _cache = null!;

    /// <summary>Creates a cache containing one stable response.</summary>
    [GlobalSetup]
    public void Setup() {
        _cache = new DnsResponseCache();
        _cache.Set(HitKey, new DnsResponse {
            Status = DnsResponseCode.NoError,
            Questions = new[] { new DnsQuestion { Name = "benchmark.example", Type = DnsRecordType.A } },
            Answers = new[] {
                new DnsAnswer {
                    Name = "benchmark.example",
                    Type = DnsRecordType.A,
                    TTL = 60,
                    DataRaw = "192.0.2.1"
                }
            }
        }, TimeSpan.FromHours(1));
    }

    /// <summary>Retrieves and defensively clones a cached response.</summary>
    [Benchmark]
    public int CacheHit() => _cache.TryGet(HitKey, out DnsResponse? response)
        ? response.Answers.Length
        : -1;

    /// <summary>Checks a key that is not present.</summary>
    [Benchmark]
    public bool CacheMiss() => _cache.TryGet("missing.example|A", out _);

    /// <summary>Releases the cache cleanup timer.</summary>
    [GlobalCleanup]
    public void Cleanup() => _cache.Dispose();
}
