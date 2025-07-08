#if NET8_0_OR_GREATER
#pragma warning disable CA2252
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Quic;
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

        [Fact]
        public async Task ResolveWireFormatQuic_ReturnsNotImplemented_WhenQuicUnsupported() {
            var previousFactory = DnsWireResolveQuic.QuicConnectionFactory;
            var previousResolver = DnsWireResolveQuic.HostEntryResolver;
            try {
                DnsWireResolveQuic.HostEntryResolver = _ => new IPHostEntry { AddressList = [IPAddress.Loopback] };
                DnsWireResolveQuic.QuicConnectionFactory = (_, _) => ValueTask.FromException<QuicConnection>(new PlatformNotSupportedException("QUIC unsupported"));
                var config = new Configuration("dummy", DnsRequestFormat.DnsOverQuic);
                var response = await DnsWireResolveQuic.ResolveWireFormatQuic("dummy", 853, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);
                Assert.Equal(DnsResponseCode.NotImplemented, response.Status);
                Assert.Contains("not supported", response.Error, StringComparison.OrdinalIgnoreCase);
            } finally {
                DnsWireResolveQuic.QuicConnectionFactory = previousFactory;
                DnsWireResolveQuic.HostEntryResolver = previousResolver;
            }
        }

        [Fact]
        public async Task ResolveWireFormatQuic_ReturnsServerFailure_WhenQuicExceptionThrown() {
            var previousFactory = DnsWireResolveQuic.QuicConnectionFactory;
            var previousResolver = DnsWireResolveQuic.HostEntryResolver;
            try {
                DnsWireResolveQuic.HostEntryResolver = _ => new IPHostEntry { AddressList = [IPAddress.Loopback] };
                DnsWireResolveQuic.QuicConnectionFactory = (_, _) => ValueTask.FromException<QuicConnection>(new QuicException(QuicError.InternalError, null, "connection failed"));
                var config = new Configuration("dummy", DnsRequestFormat.DnsOverQuic);
                var response = await DnsWireResolveQuic.ResolveWireFormatQuic("dummy", 853, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Contains("connection failed", response.Error, StringComparison.OrdinalIgnoreCase);
            } finally {
                DnsWireResolveQuic.QuicConnectionFactory = previousFactory;
                DnsWireResolveQuic.HostEntryResolver = previousResolver;
            }
        }
    }
}
#pragma warning restore CA2252
#endif
