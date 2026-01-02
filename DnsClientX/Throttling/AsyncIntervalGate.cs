using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Throttling;

/// <summary>
/// Provides a simple async gate that ensures callers do not proceed more often than a configured interval.
/// </summary>
/// <remarks>
/// This is useful for rate limiting DNS queries across concurrent tasks.
/// </remarks>
public sealed class AsyncIntervalGate : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private DateTime _nextUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncIntervalGate"/> class.
    /// </summary>
    /// <param name="interval">Minimum interval between permits; negative values are treated as zero.</param>
    public AsyncIntervalGate(TimeSpan interval)
    {
        _interval = interval < TimeSpan.Zero ? TimeSpan.Zero : interval;
        _nextUtc = DateTime.MinValue;
    }

    /// <summary>
    /// Waits until the next permit is available and then consumes it.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            if (_nextUtc > now)
            {
                var delay = _nextUtc - now;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                now = DateTime.UtcNow;
            }

            _nextUtc = now + _interval;
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _mutex.Dispose();
    }
}

