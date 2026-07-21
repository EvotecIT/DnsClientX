#if NET8_0_OR_GREATER
#pragma warning disable CA2252
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Quic;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Runtime.CompilerServices;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the QUIC DNS wire resolver.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [Collection("NoParallel")]
    public class DnsWireResolveQuicTests {
        private sealed class KeepaliveOption : EdnsOption {
            internal KeepaliveOption() : base(11) { }
            protected override byte[] GetData() => Array.Empty<byte>();
        }
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

        /// <summary>
        /// Ensures the configured endpoint timeout is honored when QUIC connection establishment stalls.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatQuic_ReturnsTimeout_WhenConnectStalls() {
            var previousFactory = DnsWireResolveQuic.QuicConnectionFactory;
            var previousResolver = DnsWireResolveQuic.HostEntryResolver;
            try {
                DnsWireResolveQuic.HostEntryResolver = _ => new IPHostEntry { AddressList = [IPAddress.Loopback] };
                DnsWireResolveQuic.QuicConnectionFactory = async (_, token) => {
                    await Task.Delay(Timeout.Infinite, token);
                    return (QuicConnection)RuntimeHelpers.GetUninitializedObject(typeof(QuicConnection));
                };

                var config = new Configuration("dummy", DnsRequestFormat.DnsOverQuic) {
                    TimeOut = 100
                };

                var response = await DnsWireResolveQuic.ResolveWireFormatQuic("dummy", 853, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
                Assert.Contains("timed out", response.Error, StringComparison.OrdinalIgnoreCase);
            } finally {
                DnsWireResolveQuic.QuicConnectionFactory = previousFactory;
                DnsWireResolveQuic.HostEntryResolver = previousResolver;
            }
        }

        /// <summary>
        /// Ensures DoQ uses RFC error code zero and the configured DNS identity for TLS SNI.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatQuic_ConfiguresRfcCloseCodesAndTlsIdentity() {
            var previousFactory = DnsWireResolveQuic.QuicConnectionFactory;
            var previousResolver = DnsWireResolveQuic.HostEntryResolver;
            QuicClientConnectionOptions? captured = null;
            try {
                DnsWireResolveQuic.HostEntryResolver = _ => new IPHostEntry { AddressList = [IPAddress.Loopback] };
                DnsWireResolveQuic.QuicConnectionFactory = (options, _) => {
                    captured = options;
                    return ValueTask.FromException<QuicConnection>(new PlatformNotSupportedException("capture only"));
                };
                var config = new Configuration("192.0.2.1", DnsRequestFormat.DnsOverQuic) {
                    TlsServerName = "resolver.example",
                    LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
                };

                await DnsWireResolveQuic.ResolveWireFormatQuic("resolver.example", 853, "example.com",
                    DnsRecordType.A, false, false, false, config, CancellationToken.None);

                Assert.NotNull(captured);
                Assert.Equal(0, captured!.DefaultCloseErrorCode);
                Assert.Equal(0, captured.DefaultStreamErrorCode);
                Assert.Equal("resolver.example", captured.ClientAuthenticationOptions.TargetHost);
                Assert.Equal(config.LocalEndPoint, captured.LocalEndPoint);
                Assert.Contains(captured.ClientAuthenticationOptions.ApplicationProtocols!,
                    protocol => protocol.Protocol.Span.SequenceEqual("doq"u8));
            } finally {
                DnsWireResolveQuic.QuicConnectionFactory = previousFactory;
                DnsWireResolveQuic.HostEntryResolver = previousResolver;
            }
        }

        /// <summary>Malformed DoQ payloads close the connection with DOQ_PROTOCOL_ERROR.</summary>
        [Fact]
        public async Task ProtocolViolationUsesRfcErrorCodeTwo() {
            var previousCloser = DnsWireResolveQuic.ConnectionCloser;
            long? captured = null;
            try {
                DnsWireResolveQuic.ConnectionCloser = (_, errorCode, _) => {
                    captured = errorCode;
                    return ValueTask.CompletedTask;
                };
                var connection = (QuicConnection)RuntimeHelpers.GetUninitializedObject(typeof(QuicConnection));

                await DnsWireResolveQuic.CloseForProtocolViolationAsync(connection);

                Assert.Equal(2, captured);
            } finally {
                DnsWireResolveQuic.ConnectionCloser = previousCloser;
            }
        }

        /// <summary>DoQ rejects EDNS TCP Keepalive and the safe wire parser detects it.</summary>
        [Fact]
        public void RejectsTcpKeepaliveOption() {
            var configuration = new Configuration("resolver.example", DnsRequestFormat.DnsOverQuic) {
                EdnsOptions = new EdnsOptions()
            };
            configuration.EdnsOptions.Options.Add(new KeepaliveOption());
            var message = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(EnableEdns: true, Options: configuration.EdnsOptions.Options));
            byte[] wire = message.SerializeDnsWireFormat();

            Assert.True(DnsWireResolveQuic.HasConfiguredOption(configuration, 11));
            Assert.True(DnsWireMessageParser.TryContainsEdnsOption(wire, 11, out bool contains));
            Assert.True(contains);
        }
    }
}
#pragma warning restore CA2252
#endif
