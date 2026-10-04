using System.Reflection;
using System.Linq;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;

/// <summary>
/// Entry point for running performance benchmarks.
/// </summary>
internal class Program {
    private static int Main(string[] args) {
        // DNS 7.0.0 ships a non-optimized assembly. Measure the published package as supplied;
        // record that limitation with results rather than silently substituting a custom build.
        var config = DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator);
        var summaries = BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args, config).ToArray();
        bool informationOnly = args.Any(arg => arg is "--list" or "-l" or "--help" or "-h" or "--version");
        return (summaries.Length == 0 && !informationOnly) ||
               summaries.Any(summary => summary.HasCriticalValidationErrors ||
                                        summary.Reports.Any(report => !report.Success))
            ? 1
            : 0;
    }
}
