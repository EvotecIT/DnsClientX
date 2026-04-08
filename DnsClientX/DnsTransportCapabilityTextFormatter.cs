using System;
using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Builds human-readable lines for transport capability output.
    /// </summary>
    public static class DnsTransportCapabilityTextFormatter {
        /// <summary>
        /// Builds text lines for one capability report.
        /// </summary>
        /// <param name="capabilities">Capability entries to format.</param>
        /// <returns>Human-readable output lines.</returns>
        public static string[] BuildLines(IEnumerable<DnsTransportCapabilityInfo> capabilities) {
            if (capabilities == null) {
                throw new ArgumentNullException(nameof(capabilities));
            }

            var lines = new List<string> {
                "Transport Capabilities:"
            };

            foreach (DnsTransportCapabilityInfo capability in capabilities) {
                lines.Add($"  {capability.Name} ({capability.RequestFormat})");
                lines.Add($"    Supported: {(capability.Supported ? "yes" : "no")}");
                lines.Add($"    Package: {capability.Package}");
                lines.Add($"    Targets: {capability.TargetFrameworkScope}");
                lines.Add($"    Runtime requirement: {capability.RuntimeRequirement}");
                lines.Add($"    Notes: {capability.Notes}");
            }

            return lines.ToArray();
        }
    }
}
