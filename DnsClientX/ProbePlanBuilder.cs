using System;

namespace DnsClientX {
    /// <summary>
    /// Expands a built-in resolver profile into the probe candidates that should be tested together.
    /// </summary>
    public static class ProbePlanBuilder {
        /// <summary>
        /// Builds the effective probe candidate list for the provided built-in resolver profile.
        /// </summary>
        public static DnsEndpoint[] BuildPlan(DnsEndpoint endpoint) {
            return endpoint switch {
                DnsEndpoint.System or DnsEndpoint.SystemTcp => new[] {
                    DnsEndpoint.System,
                    DnsEndpoint.SystemTcp
                },
                DnsEndpoint.Cloudflare or
                DnsEndpoint.CloudflareWireFormat or
                DnsEndpoint.CloudflareWireFormatPost or
                DnsEndpoint.CloudflareJsonPost or
                DnsEndpoint.CloudflareQuic or
                DnsEndpoint.CloudflareOdoh => new[] {
                    DnsEndpoint.Cloudflare,
                    DnsEndpoint.CloudflareWireFormat,
                    DnsEndpoint.CloudflareWireFormatPost,
                    DnsEndpoint.CloudflareJsonPost,
                    DnsEndpoint.CloudflareQuic,
                    DnsEndpoint.CloudflareOdoh
                },
                DnsEndpoint.Google or
                DnsEndpoint.GoogleWireFormat or
                DnsEndpoint.GoogleWireFormatPost or
                DnsEndpoint.GoogleJsonPost or
                DnsEndpoint.GoogleQuic => new[] {
                    DnsEndpoint.Google,
                    DnsEndpoint.GoogleWireFormat,
                    DnsEndpoint.GoogleWireFormatPost,
                    DnsEndpoint.GoogleJsonPost,
                    DnsEndpoint.GoogleQuic
                },
                DnsEndpoint.AdGuard or
                DnsEndpoint.AdGuardFamily or
                DnsEndpoint.AdGuardNonFiltering => new[] {
                    DnsEndpoint.AdGuard,
                    DnsEndpoint.AdGuardFamily,
                    DnsEndpoint.AdGuardNonFiltering
                },
                DnsEndpoint.Quad9 or
                DnsEndpoint.Quad9Http3 or
                DnsEndpoint.Quad9Quic or
                DnsEndpoint.Quad9ECS or
                DnsEndpoint.Quad9Unsecure => new[] {
                    DnsEndpoint.Quad9,
                    DnsEndpoint.Quad9Http3,
                    DnsEndpoint.Quad9Quic,
                    DnsEndpoint.Quad9ECS,
                    DnsEndpoint.Quad9Unsecure
                },
                DnsEndpoint.OpenDNS or
                DnsEndpoint.OpenDNSFamily => new[] {
                    DnsEndpoint.OpenDNS,
                    DnsEndpoint.OpenDNSFamily
                },
                DnsEndpoint.DnsCryptCloudflare or
                DnsEndpoint.DnsCryptQuad9 or
                DnsEndpoint.DnsCryptRelay => new[] {
                    DnsEndpoint.DnsCryptCloudflare,
                    DnsEndpoint.DnsCryptQuad9,
                    DnsEndpoint.DnsCryptRelay
                },
                _ => new[] { endpoint }
            };
        }
    }
}
