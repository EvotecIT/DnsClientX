using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Error scenarios for DnsMultiResolver.
    /// </summary>
    public class DnsMultiResolverErrorHandlingTests {
        [Fact]
        public async Task Error_Network_Sets_ErrorCode_Network() {
            try {
                var eps = new[] { new DnsResolverEndpoint { Host="n1", Port=53, Transport=Transport.Udp } };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.SequentialAll };
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => throw new SocketException((int)SocketError.NetworkUnreachable);
                var mr = new DnsMultiResolver(eps, opts);
                var res = await mr.QueryAsync("x.com", DnsRecordType.A);
                Assert.Equal(DnsQueryErrorCode.Network, res.ErrorCode);
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }

        [Fact]
        public async Task Error_InvalidResponse_Sets_ErrorCode_InvalidResponse() {
            try {
                var eps = new[] { new DnsResolverEndpoint { Host="n1", Port=53, Transport=Transport.Udp } };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.SequentialAll };
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => throw new DnsClientException("bad response");
                var mr = new DnsMultiResolver(eps, opts);
                var res = await mr.QueryAsync("x.com", DnsRecordType.A);
                Assert.Equal(DnsQueryErrorCode.InvalidResponse, res.ErrorCode);
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }
    }
}

