using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>
/// Contains a validated DNS wire response together with the exact request and response sizes.
/// </summary>
public sealed class DnsWireQueryResult {
    internal DnsWireQueryResult(DnsResponse response, byte[] queryMessage, byte[] responseMessage) {
        Response = response;
        QueryMessage = queryMessage;
        ResponseMessage = responseMessage;
    }

    /// <summary>Gets the parsed and correlation-validated DNS response.</summary>
    public DnsResponse Response { get; }

    /// <summary>Gets a copy of the serialized DNS request.</summary>
    public byte[] QueryMessage { get; }

    /// <summary>Gets a copy of the received DNS response.</summary>
    public byte[] ResponseMessage { get; }
}

/// <summary>
/// Sends caller-constructed DNS messages over UDP or TCP while retaining DnsClientX's
/// hostname resolution, peer validation, timeout, correlation, and wire parsing rules.
/// This is intended for diagnostics such as authoritative, CHAOS-class, and EDNS probes.
/// </summary>
public static class DnsWireQueryClient {
    /// <summary>Sends a DNS message over UDP, optionally falling back to TCP when truncated.</summary>
    public static async Task<DnsWireQueryResult> QueryUdpAsync(
        string server,
        int port,
        DnsMessage query,
        int timeoutMilliseconds = Configuration.DefaultTimeout,
        bool useTcpFallback = true,
        CancellationToken cancellationToken = default) {
        if (query == null) throw new ArgumentNullException(nameof(query));
        ValidateArguments(server, port, timeoutMilliseconds);
        IPAddress address = await ResolveServerAsync(server, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        byte[] queryBytes = query.SerializeDnsWireFormat();
        byte[] responseBytes;
        using (var udp = new UdpClient(address.AddressFamily)) {
            responseBytes = await DnsWireResolveUdp.SendQueryOverUdp(
                udp, queryBytes, address, port, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        DnsResponse response = await DnsWire.DeserializeDnsWireResponse(null, false, responseBytes, query).ConfigureAwait(false);
        Transport transport = Transport.Udp;
        if (response.IsTruncated && useTcpFallback) {
            responseBytes = await DnsWireResolveTcp.SendQueryOverTcp(
                queryBytes, address.ToString(), port, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            response = await DnsWire.DeserializeDnsWireResponse(null, false, responseBytes, query).ConfigureAwait(false);
            transport = Transport.Tcp;
        }
        response.AddServerDetails(CreateConfiguration(server, port, DnsRequestFormat.DnsOverUDP, timeoutMilliseconds), transport);
        return new DnsWireQueryResult(response, queryBytes, responseBytes);
    }

    /// <summary>Sends a DNS message over TCP.</summary>
    public static async Task<DnsWireQueryResult> QueryTcpAsync(
        string server,
        int port,
        DnsMessage query,
        int timeoutMilliseconds = Configuration.DefaultTimeout,
        CancellationToken cancellationToken = default) {
        if (query == null) throw new ArgumentNullException(nameof(query));
        ValidateArguments(server, port, timeoutMilliseconds);
        IPAddress address = await ResolveServerAsync(server, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        byte[] queryBytes = query.SerializeDnsWireFormat();
        byte[] responseBytes = await DnsWireResolveTcp.SendQueryOverTcp(
            queryBytes, address.ToString(), port, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        DnsResponse response = await DnsWire.DeserializeDnsWireResponse(null, false, responseBytes, query).ConfigureAwait(false);
        response.AddServerDetails(CreateConfiguration(server, port, DnsRequestFormat.DnsOverTCP, timeoutMilliseconds), Transport.Tcp);
        return new DnsWireQueryResult(response, queryBytes, responseBytes);
    }

    private static async Task<IPAddress> ResolveServerAsync(string server, int timeoutMilliseconds, CancellationToken cancellationToken) {
        (IPAddress? address, string? error) = await DnsServerResolver.ResolveAsync(
            server, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        if (address == null) throw new DnsClientException(error ?? $"DNS server '{server}' could not be resolved.");
        return address;
    }

    private static Configuration CreateConfiguration(string server, int port, DnsRequestFormat format, int timeoutMilliseconds) {
        return new Configuration(server, format) {
            Port = port,
            TimeOut = timeoutMilliseconds
        };
    }

    private static void ValidateArguments(string server, int port, int timeoutMilliseconds) {
        if (string.IsNullOrWhiteSpace(server)) throw new ArgumentException("DNS server is required.", nameof(server));
        if (port <= 0 || port > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(port));
        if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
    }
}
