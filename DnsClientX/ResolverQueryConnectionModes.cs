using System.Collections.Generic;
namespace DnsClientX {
    internal static class ResolverQueryConnectionModes {
        internal static ResolverQueryConnectionMode Combine(IEnumerable<ResolverQueryConnectionMode> modes) {
            ResolverQueryConnectionMode? first = null;
            foreach (var mode in modes) {
                if (first.HasValue && first.Value != mode) { return ResolverQueryConnectionMode.Mixed; }
                first = mode;
            }
            return first ?? ResolverQueryConnectionMode.Cold;
        }
    }
}
