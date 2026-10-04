using System;
using System.Net.Http;

namespace DnsClientX;

/// <summary>Applies one HTTP negotiation policy to wire, JSON, and authority operations.</summary>
internal static class DnsHttpRequestSettings {
    internal static void Configure(HttpRequestMessage request, Configuration configuration, Version? requiredVersion = null) {
#if NET5_0_OR_GREATER
        request.Version = requiredVersion ?? configuration.HttpVersion;
        request.VersionPolicy = VersionPolicy(configuration);
#endif
    }

#if NET5_0_OR_GREATER
    internal static HttpVersionPolicy VersionPolicy(Configuration configuration) => configuration.BootstrapResolver == null
        ? HttpVersionPolicy.RequestVersionOrHigher : HttpVersionPolicy.RequestVersionExact;
#endif
}
