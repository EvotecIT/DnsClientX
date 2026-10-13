using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;
public partial class ClientX {
    private readonly HashSet<DnsServiceBrowseSession> _activeServiceBrowsers = new();
    /// <summary>Continuously browses a service through a persistent RFC 6762 multicast session.</summary>
    /// <param name = "service">The service label, with or without its leading underscore.</param>
    /// <param name = "protocol">The transport label, with or without its leading underscore.</param>
    /// <param name = "domain">The browsing domain, usually <c>local</c>.</param>
    /// <param name = "options">Explicit resource-record, instance, and event-buffer bounds.</param>
    /// <param name = "cancellationToken">Cancellation for the complete session.</param>
    /// <returns>Additions, endpoint/metadata/address updates, goodbyes, and expirations.</returns>
    /// <remarks>The client must use multicast DNS. The configured multicast interface and port are retained.
    /// Early enumerator disposal cancels and drains the receiver before returning. A slow consumer pauses
    /// processing at the bounded event queue; UDP packets may be lost under sustained backpressure.</remarks>
    public async IAsyncEnumerable<DnsServiceBrowseEvent> BrowseServicesContinuousAsync(string service, string protocol, string domain = "local", DnsServiceBrowseOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        string serviceType = BuildServiceType(service, protocol, domain);
        Configuration configuration = EndpointConfiguration.CreateQuerySnapshot();
        if (configuration.RequestFormat != DnsRequestFormat.Multicast) {
            throw new InvalidOperationException("Continuous DNS-SD browsing requires multicast DNS.");
        }

        options ??= new DnsServiceBrowseOptions();
        if (options.MaximumRecords < 1 || options.MaximumRecords > 65536) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumRecords));
        }

        if (options.MaximumServices < 1 || options.MaximumServices > 4096) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumServices));
        }

        if (options.EventBufferCapacity < 1 || options.EventBufferCapacity > 4096) {
            throw new ArgumentOutOfRangeException(nameof(options.EventBufferCapacity));
        }

        var session = new DnsServiceBrowseSession(configuration, serviceType, options.MaximumRecords, options.MaximumServices, options.EventBufferCapacity, Debug, cancellationToken);
        try {
            lock (_lock) {
                if (_disposed) {
                    throw new ObjectDisposedException(nameof(ClientX));
                }
                _activeServiceBrowsers.Add(session);
                session.Start();
            }
            await foreach (DnsServiceBrowseEvent change in session.ReadAsync(cancellationToken).ConfigureAwait(false)) {
                yield return change;
            }
        }
        finally {
            await session.DisposeAsync().ConfigureAwait(false);
            lock (_lock)
                _activeServiceBrowsers.Remove(session);
        }
    }

    private Task StopServiceBrowsingAsync() {
        DnsServiceBrowseSession[] sessions;
        lock (_lock)
            sessions = _activeServiceBrowsers.ToArray();
        return Task.WhenAll(sessions.Select(session => session.StopAsync()));
    }
}
