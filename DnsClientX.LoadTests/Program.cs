using System.Text.Json;
using DnsClientX.LoadTests;

try {
    LoadTestOptions options = LoadTestOptions.Parse(args);
    if (options.ShowHelp) {
        PrintHelp();
        return 0;
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    LoadTestReport report = await LoadTestRunner.RunAsync(options, cancellation.Token).ConfigureAwait(false);
    PrintReport(report);
    if (!string.IsNullOrWhiteSpace(options.JsonPath)) {
        string fullPath = Path.GetFullPath(options.JsonPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions {
            WriteIndented = true
        }), cancellation.Token).ConfigureAwait(false);
        Console.WriteLine($"JSON: {fullPath}");
    }
    return report.Scenarios.Any(scenario => scenario.Failed > 0) ? 1 : 0;
} catch (OperationCanceledException) {
    Console.Error.WriteLine("Load test canceled.");
    return 2;
} catch (Exception exception) {
    Console.Error.WriteLine(exception.Message);
    return 2;
}

static void PrintReport(LoadTestReport report) {
    Console.WriteLine($"{report.Transport} {report.Server}:{report.Port} {report.Name} {report.Type}");
    Console.WriteLine("Concurrency  Requests  Success  Failed  Req/s       P50 ms   P95 ms   P99 ms");
    foreach (LoadScenarioReport scenario in report.Scenarios) {
        Console.WriteLine($"{scenario.Concurrency,11}  {scenario.Requests,8}  {scenario.Succeeded,7}  " +
                          $"{scenario.Failed,6}  {scenario.ThroughputPerSecond,10:F1}  " +
                          $"{scenario.P50Milliseconds,7:F2}  {scenario.P95Milliseconds,7:F2}  {scenario.P99Milliseconds,7:F2}");
        foreach ((string failure, int count) in scenario.Failures) {
            Console.WriteLine($"  failure {failure}: {count}");
        }
    }
}

static void PrintHelp() {
    Console.WriteLine("DnsClientX controlled load runner");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --server <host-or-DoH-URI>  Resolver address (default 127.0.0.1)");
    Console.WriteLine("  --port <1-65535>            Resolver port (transport default)");
    Console.WriteLine("  --name <domain>             Query name (default example.com)");
    Console.WriteLine("  --type <record-type>        DNS type (default A)");
    Console.WriteLine("  --transport <value>         udp, tcp, dot, doh, or doq (default udp)");
    Console.WriteLine("  --concurrency <list>        Comma-separated levels (default 1,32,128)");
    Console.WriteLine("  --requests <count>          Total requests per level (default 256)");
    Console.WriteLine("  --warmup <count>            Warm-up requests per level (default 8)");
    Console.WriteLine("  --timeout <milliseconds>    Whole query timeout (default 2000)");
    Console.WriteLine("  --tls-server-name <name>    Certificate name for DoT/DoQ by IP");
    Console.WriteLine("  --json <path>               Optional machine-readable report");
}
