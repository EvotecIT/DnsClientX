using System;
using System.Net.Http;

namespace DnsClientX;

/// <summary>Applies one HTTP negotiation policy to wire, JSON, and authority operations.</summary>
internal static class DnsHttpRequestSettings {
    internal static void Configure(HttpRequestMessage request, Configuration configuration, Version? requiredVersion = null) {
#if NET5_0_OR_GREATER
        request.Version = requiredVersion ?? configuration.HttpVersion;
        request.VersionPolicy = VersionPolicy(configuration, request.Version);
#endif
    }

#if NET5_0_OR_GREATER
    internal static HttpVersionPolicy VersionPolicy(Configuration configuration, Version? requestVersion = null) {
        if (configuration.BootstrapResolver != null) {
            return HttpVersionPolicy.RequestVersionExact;
        }

        // IP-literal DoH endpoints can advertise HTTP/3 even when their QUIC TLS path
        // rejects the connection. Keep HTTP/1.1 and HTTP/2 requests on the chosen
        // version; HTTP/3 remains available when explicitly requested.
        Version version = requestVersion ?? configuration.HttpVersion;
        UriHostNameType? hostType = configuration.BaseUri?.HostNameType;
        return version.Major < 3 && hostType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            ? HttpVersionPolicy.RequestVersionExact
            : HttpVersionPolicy.RequestVersionOrHigher;
    }
#endif
}
