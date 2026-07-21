using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class containing shared retry behavior.
    /// </summary>
    public partial class ClientX {
        // Generate jitter in a thread-safe manner across all supported frameworks.
#if NET6_0_OR_GREATER
        private static int GetJitter(int max) => max <= 0 ? 0 : Random.Shared.Next(max);
#else
        private static readonly object _randLock = new();
        private static readonly Random _rand = new();
        private static int GetJitter(int max) {
            if (max <= 0) return 0;
            lock (_randLock) {
                return _rand.Next(max);
            }
        }
#endif

        private static async Task<T> RetryAsync<T>(
            Func<Task<T>> action,
            int maxRetries = 3,
            int delayMs = 100,
            Action? beforeRetry = null,
            Action<string>? onRetry = null,
            bool useJitter = true,
            CancellationToken cancellationToken = default) {
            if (maxRetries == 0) {
                T result = await action().ConfigureAwait(false);
                if (result is DnsResponse dns) dns.RetryCount = 0;
                return result;
            }

            Exception? lastException = null;
            DnsClientException? lastDnsClientException = null;
            T? lastResult = default;

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    T result = await action().ConfigureAwait(false);
                    if (result is DnsResponse response && IsTransientResponse(response)) {
                        lastResult = result;
                        if (attempt == maxRetries) break;

                        onRetry?.Invoke(DescribeRetryReason(response));
                        beforeRetry?.Invoke();
                        await DelayBeforeRetry(attempt, delayMs, useJitter, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (result is DnsResponse success) success.RetryCount = attempt - 1;
                    return result;
                } catch (Exception ex) when (IsTransient(ex)) {
                    lastException = ex;
                    if (ex is DnsClientException dnsEx) lastDnsClientException = dnsEx;
                    if (attempt == maxRetries) break;

                    onRetry?.Invoke($"transient exception: {ex.GetType().Name}");
                    beforeRetry?.Invoke();
                    await DelayBeforeRetry(attempt, delayMs, useJitter, cancellationToken).ConfigureAwait(false);
                }
            }

            if (lastDnsClientException != null) throw lastDnsClientException;
            if (lastException != null) throw lastException;
            if (lastResult is DnsResponse lastResponse && IsTransientResponse(lastResponse)) {
                lastResponse.RetryCount = maxRetries - 1;
                throw new DnsClientException("Transient DNS response after maximum retries.", lastResponse);
            }
            if (lastResult is DnsResponse finalResponse) finalResponse.RetryCount = maxRetries - 1;
            return lastResult!;
        }

        private static Task DelayBeforeRetry(
            int attempt,
            int delayMs,
            bool useJitter,
            CancellationToken cancellationToken) {
            int exponentialDelay = delayMs <= 0
                ? 0
                : (int)Math.Min((long)delayMs << (attempt - 1), int.MaxValue);
            int jitter = useJitter ? GetJitter(delayMs) : 0;
            return Task.Delay(exponentialDelay + jitter, cancellationToken);
        }

        private static bool IsTransient(Exception ex) => DnsQueryDiagnostics.IsTransient(ex);

        private static bool IsTransientResponse(DnsResponse response) => DnsQueryDiagnostics.IsTransient(response);

        private static string DescribeRetryReason(DnsResponse response) {
            if (response.IsTruncated) return "transient response: truncated";
            if (response.ErrorCode != DnsQueryErrorCode.None) return $"transient response: {response.ErrorCode}";
            return $"transient response: {response.Status}";
        }
    }
}
