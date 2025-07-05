#if NET8_0_OR_GREATER
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsWireResolveQuicTests {
        [Fact]
        public async Task ResolveWireFormatQuic_ReturnsServerFailure_WhenHostHasNoAddresses() {
            var previous = DnsWireResolveQuic.HostEntryResolver;
            try {
                DnsWireResolveQuic.HostEntryResolver = _ => new IPHostEntry { AddressList = Array.Empty<IPAddress>() };
                var config = new Configuration("dummy", DnsRequestFormat.DnsOverQuic);
                var response = await DnsWireResolveQuic.ResolveWireFormatQuic("dummy", 853, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
            } finally {
                DnsWireResolveQuic.HostEntryResolver = previous;
            }
        }
    }
}
#endif
