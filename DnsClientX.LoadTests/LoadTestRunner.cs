using System.Collections.Concurrent;
using System.Diagnostics;
using DnsClientX;

namespace DnsClientX.LoadTests;

internal static class LoadTestRunner {
    internal static async Task<LoadTestReport> RunAsync(LoadTestOptions options, CancellationToken cancellationToken) {
        var reports = new List<LoadScenarioReport>();
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

        foreach (int concurrency in options.Concurrency) {
            cancellationToken.ThrowIfCancellationRequested();
            using ClientX client = CreateClient(options);
            for (int warmup = 0; warmup < options.Warmup; warmup++) {
                DnsResponse response = await ResolveAsync(client, options, cancellationToken).ConfigureAwait(false);
                string? failure = ClassifyFailure(response);
                if (failure != null) {
                    throw new InvalidOperationException($"Load-test warmup failed: {failure}.");
                }
            }
            reports.Add(await RunScenarioAsync(client, options, concurrency, cancellationToken).ConfigureAwait(false));
        }

        return new LoadTestReport {
            StartedAtUtc = startedAtUtc,
            Server = options.Server,
            Port = options.Port,
            Name = options.Name,
            Type = options.Type,
            Transport = options.Format,
            RequestsPerScenario = options.Requests,
            WarmupRequests = options.Warmup,
            Scenarios = reports
        };
    }

    private static async Task<LoadScenarioReport> RunScenarioAsync(
        ClientX client,
        LoadTestOptions options,
        int concurrency,
        CancellationToken cancellationToken) {
        var elapsedTicks = new long[options.Requests];
        var failures = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        int nextRequest = -1;
        int succeeded = 0;
        long scenarioStart = Stopwatch.GetTimestamp();

        Task[] workers = Enumerable.Range(0, Math.Min(concurrency, options.Requests))
            .Select(_ => WorkerAsync())
            .ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
        TimeSpan scenarioElapsed = Stopwatch.GetElapsedTime(scenarioStart);

        double[] milliseconds = elapsedTicks
            .Select(ticks => TimeSpan.FromTicks(ticks).TotalMilliseconds)
            .OrderBy(value => value)
            .ToArray();
        int failed = options.Requests - succeeded;
        return new LoadScenarioReport {
            Concurrency = concurrency,
            Requests = options.Requests,
            Succeeded = succeeded,
            Failed = failed,
            DurationMilliseconds = scenarioElapsed.TotalMilliseconds,
            ThroughputPerSecond = options.Requests / scenarioElapsed.TotalSeconds,
            P50Milliseconds = Percentile(milliseconds, 0.50),
            P95Milliseconds = Percentile(milliseconds, 0.95),
            P99Milliseconds = Percentile(milliseconds, 0.99),
            Failures = new SortedDictionary<string, int>(failures, StringComparer.Ordinal)
        };

        async Task WorkerAsync() {
            while (true) {
                int requestIndex = Interlocked.Increment(ref nextRequest);
                if (requestIndex >= options.Requests) {
                    return;
                }

                long queryStart = Stopwatch.GetTimestamp();
                try {
                    DnsResponse response = await ResolveAsync(client, options, cancellationToken).ConfigureAwait(false);
                    string? failure = ClassifyFailure(response);
                    if (failure == null) {
                        Interlocked.Increment(ref succeeded);
                    } else {
                        failures.AddOrUpdate(failure, 1, (_, count) => count + 1);
                    }
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    throw;
                } catch (Exception exception) {
                    failures.AddOrUpdate(exception.GetType().Name, 1, (_, count) => count + 1);
                } finally {
                    elapsedTicks[requestIndex] = Stopwatch.GetElapsedTime(queryStart).Ticks;
                }
            }
        }
    }

    private static Task<DnsResponse> ResolveAsync(ClientX client, LoadTestOptions options, CancellationToken cancellationToken) =>
        client.Resolve(options.Name, options.Type, retryOnTransient: false, cancellationToken: cancellationToken);

    internal static string? ClassifyFailure(DnsResponse response) {
        if (response == null) throw new ArgumentNullException(nameof(response));
        if (response.Status != DnsResponseCode.NoError) return $"DNS:{response.Status}";
        if (!string.IsNullOrWhiteSpace(response.Error)) return "ResponseError";
        if (!response.RequestedAnswerPresent) return "MissingRequestedAnswer";
        return null;
    }

    private static ClientX CreateClient(LoadTestOptions options) {
        Configuration configuration;
        if (options.Format == DnsRequestFormat.DnsOverHttps) {
            Uri endpoint = Uri.TryCreate(options.Server, UriKind.Absolute, out Uri? uri)
                ? uri
                : new UriBuilder(Uri.UriSchemeHttps, options.Server, options.Port, "dns-query").Uri;
            configuration = new Configuration(endpoint, options.Format);
        } else {
            configuration = new Configuration(options.Server, options.Format) {
                Port = options.Port,
                TlsServerName = options.TlsServerName
            };
        }
        configuration.TimeOut = options.TimeoutMs;
        configuration.MaxConcurrency = null;
        return new ClientX(configuration, enableCache: false);
    }

    private static double Percentile(double[] sortedValues, double percentile) {
        if (sortedValues.Length == 0) {
            return 0;
        }
        int index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }
}
