using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX;

/// <summary>Expands built-in provider profiles into concrete resolver endpoints.</summary>
public static class DnsResolverEndpointFactory {
    /// <summary>Expands providers using their existing defaults and removes duplicate endpoints.</summary>
    public static DnsResolverEndpoint[] From(IEnumerable<DnsEndpoint> endpoints) {
        if (endpoints == null) { throw new ArgumentNullException(nameof(endpoints)); }
        return endpoints.SelectMany(From).GroupBy(candidate =>
            $"{candidate.Transport}|{candidate.RequestFormat}|{candidate.DohUrl}|{candidate.Host}|{candidate.Port}|{candidate.Family}|{candidate.TlsServerName}",
            StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
    }

    /// <summary>Expands one provider using its existing default transport.</summary>
    public static DnsResolverEndpoint[] From(DnsEndpoint endpoint) {
        if (endpoint == DnsEndpoint.Custom) { return Array.Empty<DnsResolverEndpoint>(); }
        if (endpoint == DnsEndpoint.System || endpoint == DnsEndpoint.SystemTcp || endpoint == DnsEndpoint.RootServer) {
            return From(endpoint, endpoint == DnsEndpoint.SystemTcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP);
        }
        var profile = RequireProfile(endpoint);
        return profile.Expand(profile.DefaultFormat);
    }

    /// <summary>Expands a provider using a published transport, preserving its filtering profile and TLS identity.</summary>
    /// <param name="endpoint">The built-in provider profile.</param>
    /// <param name="requestFormat">A format returned by GetSupportedRequestFormats.</param>
    /// <returns>Fresh endpoint objects for all configured addresses.</returns>
    public static DnsResolverEndpoint[] From(DnsEndpoint endpoint, DnsRequestFormat requestFormat) {
        if (endpoint == DnsEndpoint.System || endpoint == DnsEndpoint.SystemTcp || endpoint == DnsEndpoint.RootServer) {
            if (endpoint == DnsEndpoint.RootServer && requestFormat != DnsRequestFormat.DnsOverUDP) {
                throw new NotSupportedException("The RootServer profile uses iterative UDP resolution. Use an explicit TCP endpoint to query a root server directly.");
            }
            if (requestFormat != DnsRequestFormat.DnsOverUDP && requestFormat != DnsRequestFormat.DnsOverTCP) {
                throw new NotSupportedException("System and root profiles support only UDP and TCP.");
            }
            IEnumerable<string> hosts = endpoint == DnsEndpoint.RootServer ? RootServers.Servers : SystemInformation.GetDnsFromActiveNetworkCard();
            return hosts.Select(host => new DnsResolverEndpoint {
                Host = host, Port = 53, Transport = DnsRequestFormatMapper.ToTransport(requestFormat), RequestFormat = requestFormat
            }).ToArray();
        }
        return RequireProfile(endpoint).Expand(requestFormat);
    }

    /// <summary>Lists published formats for a profile, independently of the current runtime's transport capability.</summary>
    /// <remarks>Reserved legacy identifiers and Custom have no built-in transports. Use explicit endpoints for custom services.</remarks>
    public static DnsRequestFormat[] GetSupportedRequestFormats(DnsEndpoint endpoint) {
        if (endpoint == DnsEndpoint.RootServer) { return new[] { DnsRequestFormat.DnsOverUDP }; }
        if (endpoint == DnsEndpoint.System || endpoint == DnsEndpoint.SystemTcp) {
            return new[] { DnsRequestFormat.DnsOverUDP, DnsRequestFormat.DnsOverTCP };
        }
        return DnsProviderPresets.Get(endpoint)?.Formats ?? Array.Empty<DnsRequestFormat>();
    }

    internal static DnsProviderPresets.Profile RequireProfile(DnsEndpoint endpoint) {
        if (!Enum.IsDefined(typeof(DnsEndpoint), endpoint) || endpoint == DnsEndpoint.Custom) {
            throw new ArgumentException("Invalid endpoint", nameof(endpoint));
        }
        return DnsProviderPresets.Get(endpoint) ?? throw new NotSupportedException(endpoint switch {
            DnsEndpoint.CloudflareJsonPost => "Cloudflare does not publish a JSON-over-POST DNS endpoint. Use CloudflareWireFormatPost for RFC 8484 POST.",
            DnsEndpoint.GoogleJsonPost => "Google Public DNS does not publish a JSON-over-POST endpoint. Use GoogleWireFormatPost for RFC 8484 POST.",
            DnsEndpoint.CloudflareQuic => "Cloudflare does not publish a DNS-over-QUIC resolver endpoint.",
            DnsEndpoint.GoogleQuic => "Google Public DNS does not publish a DNS-over-QUIC resolver endpoint.",
            DnsEndpoint.CloudflareOdoh => "Use the optional ODoH provider with an explicit target and relay.",
            DnsEndpoint.DnsCryptCloudflare or DnsEndpoint.DnsCryptQuad9 or DnsEndpoint.DnsCryptRelay => "Use the optional DNSCrypt provider with an explicit trusted resolver stamp and optional relay.",
            _ => "Invalid endpoint."
        });
    }
}
