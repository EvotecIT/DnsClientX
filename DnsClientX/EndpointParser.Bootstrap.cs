using System;

namespace DnsClientX;

public static partial class EndpointParser {
    /// <summary>Parses an IP-literal UDP/TCP bootstrap endpoint, such as <c>udp@1.1.1.1:53</c>.</summary>
    /// <param name="input">A single endpoint using the ordinary resolver endpoint syntax.</param>
    /// <returns>The validated bootstrap resolver.</returns>
    public static DnsResolverEndpoint ParseBootstrap(string input) {
        DnsResolverEndpoint[] endpoints = TryParseMany(new[] { input }, out var errors);
        if (errors.Count > 0 || endpoints.Length != 1) {
            throw new ArgumentException(errors.Count > 0 ? string.Join("; ", errors) : "Specify one bootstrap resolver.", nameof(input));
        }
        DnsBootstrapResolver.Validate(endpoints[0]);
        return endpoints[0];
    }
}
