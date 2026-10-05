using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>Obtains resolver addresses through the ordinary DNS engine without recursive bootstrap.</summary>
internal static class DnsBootstrapResolver {
    internal static void Validate(DnsResolverEndpoint endpoint) {
        if (!IPAddress.TryParse(endpoint.Host, out _)) throw new ArgumentException("Bootstrap resolver Host must be an IP literal.", nameof(endpoint));
        if (endpoint.Port < 1 || endpoint.Port > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(endpoint), "Bootstrap resolver port must be between 1 and 65535.");
        if ((endpoint.Transport != Transport.Udp && endpoint.Transport != Transport.Tcp)
            || endpoint.DohUrl != null
            || (endpoint.RequestFormat.HasValue && endpoint.RequestFormat != DnsRequestFormatMapper.FromTransport(endpoint.Transport))) {
            throw new NotSupportedException("Bootstrap resolvers must use UDP or TCP.");
        }
    }

    internal static string CacheKey(DnsResolverEndpoint? endpoint) => endpoint == null ? "system" :
        FormattableString.Invariant($"{endpoint.Transport}|{endpoint.Host}|{endpoint.Port}|{endpoint.Family}|{endpoint.Timeout?.Ticks}|{endpoint.AllowTcpFallback}|{endpoint.EdnsBufferSize}|{endpoint.DnsSecOk}");

    internal static async Task<(IPAddress[] Addresses, TimeSpan? Ttl)> ResolveAsync(string hostname, DnsResolverEndpoint endpoint,
        int timeoutMilliseconds, AddressFamily? preferredFamily, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(endpoint);
        int timeout = endpoint.Timeout.HasValue && endpoint.Timeout.Value > TimeSpan.Zero
            ? (int)Math.Min(int.MaxValue, Math.Max(1, endpoint.Timeout.Value.TotalMilliseconds)) : timeoutMilliseconds;
        if (timeoutMilliseconds > 0) timeout = Math.Min(timeout, timeoutMilliseconds);
        var configuration = new Configuration(endpoint.Host!, DnsRequestFormatMapper.FromTransport(endpoint.Transport)) {
            Port = endpoint.Port,
            TimeOut = timeout > 0 ? timeout : Configuration.DefaultTimeout,
            UseTcpFallback = endpoint.AllowTcpFallback,
            WaitForCanceledStreamQueryDrain = true
        };
        if (endpoint.EdnsBufferSize.HasValue) {
            configuration.EnableEdns = true;
            configuration.UdpBufferSize = endpoint.EdnsBufferSize.Value;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(configuration.TimeOut);
        using var client = new ClientX(configuration);
        DnsRecordType first = (preferredFamily ?? endpoint.Family) == AddressFamily.InterNetworkV6 ? DnsRecordType.AAAA : DnsRecordType.A;
        try {
            foreach (DnsRecordType type in new[] { first, first == DnsRecordType.A ? DnsRecordType.AAAA : DnsRecordType.A }) {
                DnsResponse response = await client.Resolve(hostname, type, requestDnsSec: endpoint.DnsSecOk == true,
                    returnAllTypes: true, retryOnTransient: false, cancellationToken: deadline.Token).ConfigureAwait(false);
                if (response.Status != DnsResponseCode.NoError || !string.IsNullOrEmpty(response.Error)) {
                    throw new DnsClientException(response.Error ?? $"Bootstrap resolver returned {response.Status} for '{hostname}'.", response);
                }
                IPAddress[] addresses = SelectAddressAnswers(response, hostname, type)
                    .Select(answer => IPAddress.TryParse(answer.Data, out var address) ? address : null)
                    .Where(address => address != null).Cast<IPAddress>().ToArray();
                if (addresses.Length > 0) {
                    // An alias may expire before its target address. Keep both within the wire TTL.
                    int ttl = response.Answers.Min(answer => Math.Max(0, answer.TTL));
                    return (addresses, TimeSpan.FromSeconds(ttl));
                }
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
            throw new TimeoutException($"Bootstrap resolution timed out after {configuration.TimeOut} milliseconds.");
        }
        return (Array.Empty<IPAddress>(), null);
    }

    private static IEnumerable<DnsAnswer> SelectAddressAnswers(DnsResponse response, string hostname, DnsRecordType type) {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string candidate = hostname.TrimEnd('.');
        while (visited.Add(candidate)) {
            DnsAnswer[] answers = response.Answers.Where(answer => answer.Type == type
                && string.Equals(answer.Name.TrimEnd('.'), candidate, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (answers.Length > 0) return answers;
            string? alias = ClientX.FindAliasTarget(response, candidate);
            if (alias == null) break;
            candidate = alias;
        }
        return Array.Empty<DnsAnswer>();
    }
}
