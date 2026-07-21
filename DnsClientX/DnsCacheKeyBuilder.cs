using System;
using System.Collections.Generic;
using System.Text;

namespace DnsClientX {
    internal static class DnsCacheKeyBuilder {
        internal static string Build(Configuration configuration, string normalizedName, DnsRecordType type,
            bool requestDnsSec, bool validateDnsSec, bool returnAllTypes, bool typedRecords, bool parseTypedTxtRecords,
            TimeSpan maxCacheTtl, bool ignoreCertificateErrors) {
            var builder = new StringBuilder(256);
            Append(builder, configuration.RequestFormat.ToString());
            Append(builder, configuration.BuiltInEndpoint?.ToString() ?? string.Empty);
            Append(builder, configuration.BaseUri?.AbsoluteUri ?? configuration.Hostname ?? string.Empty);
            Append(builder, configuration.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, configuration.TlsServerName ?? string.Empty);
            Append(builder, configuration.LocalEndPoint?.ToString() ?? string.Empty);
            Append(builder, configuration.MulticastInterfaceIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
            Append(builder, configuration.PreferredAddressFamily?.ToString() ?? string.Empty);
            Append(builder, configuration.UseTcpFallback ? "tcp-fallback" : "no-tcp-fallback");
            Append(builder, configuration.IterativeMaxHops.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, configuration.EnableQNameMinimization ? "qname-minimized" : "full-qname");
            Append(builder, configuration.Rfc5011TrustAnchorStorePath ?? string.Empty);
            Append(builder, maxCacheTtl.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, ignoreCertificateErrors ? "insecure-tls" : "validate-tls");
            Append(builder, DnsWireNameCodec.Canonical(normalizedName));
            Append(builder, ((ushort)type).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, requestDnsSec ? "do" : "no-do");
            Append(builder, configuration.CheckingDisabled || validateDnsSec ? "cd" : "no-cd");
            Append(builder, configuration.RecursionDesired ? "rd" : "no-rd");
            Append(builder, returnAllTypes ? "all" : "filtered");
            Append(builder, typedRecords ? "typed" : "raw");
            Append(builder, parseTypedTxtRecords ? "typed-txt" : "raw-txt");

            EdnsOptions? edns = configuration.EdnsOptions;
            Append(builder, (edns?.EnableEdns ?? configuration.EnableEdns) ? "edns" : "no-edns");
            Append(builder, (edns?.UdpBufferSize ?? configuration.UdpBufferSize).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, edns?.Subnet?.Subnet ?? configuration.Subnet ?? string.Empty);
            IEnumerable<EdnsOption>? options = edns?.GetEffectiveOptions();
            if (options != null) {
                foreach (EdnsOption option in options) Append(builder, Convert.ToBase64String(option.ToByteArray()));
            }
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value) {
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
