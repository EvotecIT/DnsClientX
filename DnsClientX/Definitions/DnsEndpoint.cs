using System;
using System.ComponentModel;
using System.Reflection;
using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Enumerates known DNS endpoints including DNS-over-HTTPS and DNSCrypt
    /// providers as well as system resolvers. DNS-over-HTTPS is defined in
    /// <a href="https://www.rfc-editor.org/rfc/rfc8484">RFC 8484</a>.
    /// </summary>
    /// <remarks>
    /// The values correlate with predefined settings that can be selected
    /// via <see cref="ClientXBuilder"/> when instantiating <see cref="ClientX"/>.
    /// </remarks>
    public enum DnsEndpoint {
        /// <summary>
        /// Use the system's default DNS resolver using UDP. When using this option, the system's default DNS resolver will be used.
        /// When UDP reaches the maximum packet size, it will automatically switch to TCP.
        /// </summary>
        [Description("Use the system's default DNS resolver using UDP. When UDP reaches the maximum packet size, it will automatically switch to TCP.")]
        System,
        /// <summary>
        /// Use the system's default DNS resolver using TCP. When using this option, the system's default DNS resolver will be used.
        /// </summary>
        [Description("Use the system's default DNS resolver using TCP.")]
        SystemTcp,
        /// <summary>
        /// Cloudflare DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("Cloudflare DNS-over-HTTPS endpoint.")]
        Cloudflare,
        /// <summary>
        /// Cloudflare's security-focused DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("Cloudflare's security-focused DNS-over-HTTPS endpoint.")]
        CloudflareSecurity,
        /// <summary>
        /// Cloudflare's family-friendly DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("Cloudflare's family-friendly DNS-over-HTTPS endpoint.")]
        CloudflareFamily,
        /// <summary>
        /// Cloudflare's DNS-over-HTTPS endpoint using wire format.
        /// </summary>
        [Description("Cloudflare's DNS-over-HTTPS endpoint using wire format.")]
        CloudflareWireFormat,
        /// <summary>
        /// Cloudflare's DNS-over-HTTPS endpoint using wire format with POST method.
        /// </summary>
        [Description("Cloudflare's DNS-over-HTTPS endpoint using wire format with POST method.")]
        CloudflareWireFormatPost,
        /// <summary>
        /// Cloudflare's DNS-over-HTTPS endpoint using JSON over POST method.
        /// </summary>
        [Description("Cloudflare's DNS-over-HTTPS endpoint using JSON over POST method.")]
        CloudflareJsonPost,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("Google's DNS-over-HTTPS endpoint.")]
        Google,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint using wire format over GET method.
        /// </summary>
        [Description("Google's DNS-over-HTTPS endpoint using wire format over GET method.")]
        GoogleWireFormat,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint using wire format over POST method.
        /// </summary>
        [Description("Google's DNS-over-HTTPS endpoint using wire format over POST method.")]
        GoogleWireFormatPost,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint using JSON over POST method.
        /// </summary>
        [Description("Google's DNS-over-HTTPS endpoint using JSON over POST method.")]
        GoogleJsonPost,
        /// <summary>
        /// Quad9's DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("Quad9's DNS-over-HTTPS endpoint.")]
        Quad9,
        /// <summary>
        /// Quad9's DNS-over-HTTPS endpoint with ECS support.
        /// </summary>
        [Description("Quad9's DNS-over-HTTPS endpoint with ECS support.")]
        Quad9ECS,
        /// <summary>
        /// Quad9's unsecured DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("Quad9's unsecured DNS-over-HTTPS endpoint.")]
        Quad9Unsecure,
        /// <summary>
        /// OpenDNS's DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("OpenDNS's DNS-over-HTTPS endpoint.")]
        OpenDNS,
        /// <summary>
        /// OpenDNS's family-friendly DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("OpenDNS's family-friendly DNS-over-HTTPS endpoint.")]
        OpenDNSFamily,
        /// <summary>
        /// Cloudflare's DNS-over-QUIC endpoint.
        /// </summary>
        [Description("Cloudflare's DNS-over-QUIC endpoint.")]
        CloudflareQuic,
        /// <summary>
        /// Google's DNS-over-QUIC endpoint.
        /// </summary>
        [Description("Google's DNS-over-QUIC endpoint.")]
        GoogleQuic,
        /// <summary>
        /// AdGuard DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("AdGuard DNS-over-HTTPS endpoint.")]
        AdGuard,
        /// <summary>
        /// AdGuard family protection DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("AdGuard family protection DNS-over-HTTPS endpoint.")]
        AdGuardFamily,
        /// <summary>
        /// AdGuard non-filtering DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("AdGuard non-filtering DNS-over-HTTPS endpoint.")]
        AdGuardNonFiltering,
        /// <summary>
        /// NextDNS DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("NextDNS DNS-over-HTTPS endpoint.")]
        NextDNS,
        /// <summary>
        /// Cloudflare DNSCrypt endpoint.
        /// </summary>
        [Description("Cloudflare DNSCrypt endpoint.")]
        DnsCryptCloudflare,
        /// <summary>
        /// Quad9 DNSCrypt endpoint.
        /// </summary>
        [Description("Quad9 DNSCrypt endpoint.")]
        DnsCryptQuad9,
        /// <summary>
        /// DNSCrypt relay server option.
        /// </summary>
        [Description("DNSCrypt relay server option.")]
        DnsCryptRelay,
        /// <summary>
        /// DNS root servers, queried iteratively starting from one of the
        /// well known A-M root server instances.
        /// </summary>
        [Description("DNS root servers, queried iteratively starting from one of the well known A-M root server instances.")]
        RootServer,
        /// <summary>
        /// Cloudflare's Oblivious DNS-over-HTTPS endpoint.
        /// </summary>
        [Description("Cloudflare's Oblivious DNS-over-HTTPS endpoint.")]
        CloudflareOdoh,
        /// <summary>
        /// Custom DNS endpoint configured via <see cref="Configuration"/>
        /// overrides.
        /// </summary>
        [Description("Custom DNS endpoint configured via overrides.")]
        Custom
    }

    /// <summary>
    /// Extension helpers for <see cref="DnsEndpoint"/>.
    /// </summary>
    public static class DnsEndpointExtensions {
        /// <summary>
        /// Gets the description associated with the specified <see cref="DnsEndpoint"/>.
        /// </summary>
        /// <param name="endpoint">Endpoint value.</param>
        /// <returns>Description text if available; otherwise the enum name.</returns>
        public static string GetDescription(this DnsEndpoint endpoint) {
            var member = typeof(DnsEndpoint).GetMember(endpoint.ToString());
            var attr = member.Length > 0 ? member[0].GetCustomAttribute<DescriptionAttribute>() : null;
            return attr?.Description ?? endpoint.ToString();
        }

        /// <summary>
        /// Returns all <see cref="DnsEndpoint"/> values with their descriptions.
        /// </summary>
        /// <returns>Sequence of endpoint and description pairs.</returns>
        public static IEnumerable<(DnsEndpoint Endpoint, string Description)> GetAllWithDescriptions() {
#if NET6_0_OR_GREATER
            foreach (DnsEndpoint value in Enum.GetValues<DnsEndpoint>()) {
#else
            foreach (DnsEndpoint value in Enum.GetValues(typeof(DnsEndpoint))) {
#endif
                yield return (value, value.GetDescription());
            }
        }
    }
}
