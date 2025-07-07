using BenchmarkDotNet.Running;
using DnsClientX.Benchmarks;

/// <summary>
/// Entry point for running performance benchmarks.
/// </summary>
internal class Program {
    private static void Main() {
        BenchmarkRunner.Run<DomainBenchmark>();
    }
}
