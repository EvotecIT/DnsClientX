#if NET8_0_OR_GREATER
#pragma warning disable CA2252
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Quic;
using System.Runtime.CompilerServices;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the QUIC DNS wire resolver.
    /// </summary>
    public class DnsWireResolveQuicTests {
        /// <summary>
        /// Ensures server failure is returned when the host has no addresses.
        /// </summary>
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

        /// <summary>
        /// Confirms QUIC unsupported scenarios return a not implemented status.
        /// </summary>
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

        /// <summary>
        /// Ensures QUIC exceptions result in a server failure response.
        /// </summary>
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

        /// <summary>
        /// Verifies that connections and streams are disposed when write errors occur.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatQuic_ShouldDisposeResources_OnWriteException() {
            var prevFactory = DnsWireResolveQuic.QuicConnectionFactory;
            var prevResolver = DnsWireResolveQuic.HostEntryResolver;
            var prevStreamFactory = DnsWireResolveQuic.StreamFactory;
            var prevConnDisposer = DnsWireResolveQuic.ConnectionDisposer;
            var prevStreamDisposer = DnsWireResolveQuic.StreamDisposer;
            try {
                DnsWireResolveQuic.ConnectionDisposeCount = 0;
                DnsWireResolveQuic.StreamDisposeCount = 0;
                DnsWireResolveQuic.HostEntryResolver = _ => new IPHostEntry { AddressList = [IPAddress.Loopback] };
                DnsWireResolveQuic.QuicConnectionFactory = (_, _) => new ValueTask<QuicConnection>((QuicConnection)RuntimeHelpers.GetUninitializedObject(typeof(QuicConnection)));
                DnsWireResolveQuic.StreamFactory = (_, _) => new ValueTask<QuicStream>((QuicStream)RuntimeHelpers.GetUninitializedObject(typeof(QuicStream)));
                DnsWireResolveQuic.ConnectionDisposer = _ => { DnsWireResolveQuic.ConnectionDisposeCount++; return ValueTask.CompletedTask; };
                DnsWireResolveQuic.StreamDisposer = _ => { DnsWireResolveQuic.StreamDisposeCount++; return ValueTask.CompletedTask; };

                var config = new Configuration("dummy", DnsRequestFormat.DnsOverQuic);

                await DnsWireResolveQuic.ResolveWireFormatQuic("dummy", 853, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

                Assert.Equal(1, DnsWireResolveQuic.ConnectionDisposeCount);
                Assert.Equal(1, DnsWireResolveQuic.StreamDisposeCount);
            } finally {
                DnsWireResolveQuic.QuicConnectionFactory = prevFactory;
                DnsWireResolveQuic.HostEntryResolver = prevResolver;
                DnsWireResolveQuic.StreamFactory = prevStreamFactory;
                DnsWireResolveQuic.ConnectionDisposer = prevConnDisposer;
                DnsWireResolveQuic.StreamDisposer = prevStreamDisposer;
            }
        }
    }
}
#pragma warning restore CA2252
#endif
