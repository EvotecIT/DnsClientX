using System.Reflection;
using System.Linq;
using BenchmarkDotNet.Running;

/// <summary>
/// Entry point for running performance benchmarks.
/// </summary>
internal class Program {
    private static int Main(string[] args) {
        var summaries = BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
        return summaries.Any(summary => summary.HasCriticalValidationErrors ||
                                        summary.Reports.Any(report => !report.Success))
            ? 1
            : 0;
    }
}
