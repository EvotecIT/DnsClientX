using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
#if NET8_0_OR_GREATER
    internal static class DnsWireResolveOdoh {
        internal static Task<DnsResponse> ResolveWireFormatOdoh(this HttpClient client, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            throw new NotSupportedException("System.Security.Cryptography.Hpke is not available in this environment.");
        }
    }
#else
    internal static class DnsWireResolveOdoh {
        internal static Task<DnsResponse> ResolveWireFormatOdoh(this HttpClient client, string name, DnsRecordType type, bool requestDnsSec, bool validateDnsSec, bool debug, Configuration endpointConfiguration, CancellationToken cancellationToken) {
            throw new NotSupportedException("Oblivious DNS over HTTPS is not supported on this framework.");
        }
    }
#endif
}
