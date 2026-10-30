#if NET8_0_OR_GREATER
#pragma warning disable CA2252
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
        /// <summary>Hostname lookup failure retains diagnostics before opening a QUIC connection.</summary>
        [Fact]
        public async Task HostnameFailureRetainsLookupDiagnostics() {
            var previousResolver = DnsWireResolveQuic.HostEntryResolver;
            DnsServerResolver.ResetForTests();
            try {
                DnsWireResolveQuic.HostEntryResolver = null;
                var cause = new SocketException((int)SocketError.HostNotFound);
                DnsServerResolver.ResolveHostAddressesAsync = _ => throw cause;
                var configuration = new Configuration("missing.example", DnsRequestFormat.DnsOverQuic) {
                    TimeOut = 1000, DnsServerResolutionAllowStale = false
                };
                var response = await DnsWireResolveQuic.ResolveWireFormatQuic("missing.example", 853,
                    "payload.example", DnsRecordType.A, false, false, false, configuration, default);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Equal(DnsQueryErrorCode.Network, response.ErrorCode);
                Assert.Same(cause, response.Exception);
                Assert.Equal(cause.Message, response.ServerResolution!.Error);
            } finally {
                DnsServerResolver.ResetForTests();
                DnsWireResolveQuic.HostEntryResolver = previousResolver;
            }
        }

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

        /// <summary>A real stream with completed writes fails the query write and releases both native resources.</summary>
        [NativeQuicTheory]
        [InlineData("example.com")]
        public async Task ResolveWireFormatQuic_ShouldDisposeResources_OnWriteException(string queryName) {
            Assert.True(QuicListener.IsSupported, "Native QUIC is required by this qualification.");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var certificate = new DnsDiscoveryCertificateFixture(IPAddress.Loopback);
            var protocols = new List<SslApplicationProtocol> { new("doq") };
            await using var listener = await QuicListener.ListenAsync(new QuicListenerOptions {
                ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0), ApplicationProtocols = protocols,
                ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions {
                    DefaultCloseErrorCode = 0, DefaultStreamErrorCode = 0, MaxInboundBidirectionalStreams = 1,
                    ServerAuthenticationOptions = new SslServerAuthenticationOptions {
                        ApplicationProtocols = protocols, ServerCertificate = certificate.Leaf
                    }
                })
            }, deadline.Token);
            Task<QuicConnection> accepting = listener.AcceptConnectionAsync(deadline.Token).AsTask();
            var previousFactory = DnsWireResolveQuic.QuicConnectionFactory;
            var previousStreamFactory = DnsWireResolveQuic.StreamFactory;
            var previousConnectionDisposer = DnsWireResolveQuic.ConnectionDisposer;
            var previousStreamDisposer = DnsWireResolveQuic.StreamDisposer;
            QuicConnection? connection = null, peer = null;
            QuicStream? stream = null;
            int connectionDisposes = 0, streamDisposes = 0;
            bool completedStreamReturned = false;
            try {
                DnsWireResolveQuic.QuicConnectionFactory = async (options, token) => {
                    connection = await QuicConnection.ConnectAsync(options, token);
                    peer = await accepting.WaitAsync(token);
                    return connection;
                };
                DnsWireResolveQuic.StreamFactory = async (owner, token) => {
                    stream = await owner.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token);
                    stream.CompleteWrites();
                    completedStreamReturned = true;
                    return stream;
                };
                DnsWireResolveQuic.ConnectionDisposer = async owner => {
                    connectionDisposes++;
                    await owner.DisposeAsync();
                };
                DnsWireResolveQuic.StreamDisposer = async owned => {
                    streamDisposes++;
                    await owned.DisposeAsync();
                };
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverQuic) {
                    Port = listener.LocalEndPoint.Port, TimeOut = 5000,
                    TlsTrustAnchors = new[] { certificate.Root }, TlsRevocationMode = X509RevocationMode.NoCheck
                };
                DnsResponse response = await DnsWireResolveQuic.ResolveWireFormatQuic(
                    "127.0.0.1", config.Port, queryName, DnsRecordType.A,
                    false, false, false, config, deadline.Token);
                Assert.NotNull(stream);
                Assert.True(completedStreamReturned);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.True(response.Exception is InvalidOperationException or QuicException,
                    response.Exception?.ToString() ?? response.Error);
                Assert.Equal(1, connectionDisposes);
                Assert.Equal(1, streamDisposes);
                await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                    stream!.ReadAsync(new byte[1], deadline.Token).AsTask());
                await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                    connection!.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, deadline.Token).AsTask());
            } finally {
                DnsWireResolveQuic.QuicConnectionFactory = previousFactory;
                DnsWireResolveQuic.StreamFactory = previousStreamFactory;
                DnsWireResolveQuic.ConnectionDisposer = previousConnectionDisposer;
                DnsWireResolveQuic.StreamDisposer = previousStreamDisposer;
                if (stream != null) await stream.DisposeAsync();
                if (connection != null) await connection.DisposeAsync();
                if (peer != null) await peer.DisposeAsync();
                deadline.Cancel();
                try { await accepting; }
                catch (Exception exception) when (deadline.IsCancellationRequested
                    && exception is OperationCanceledException or QuicException or ObjectDisposedException) { }
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
        public async Task RejectsTcpKeepaliveOption() {
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
            var previousFactory = DnsWireResolveQuic.QuicConnectionFactory;
            int connections = 0;
            try {
                DnsWireResolveQuic.QuicConnectionFactory = (_, _) => {
                    connections++;
                    return ValueTask.FromException<QuicConnection>(new InvalidOperationException("Must reject before dialing."));
                };
                var exception = await Assert.ThrowsAsync<DnsClientException>(() =>
                    DnsWireResolveQuic.ResolveWireFormatQuic("resolver.example", 853, "example.com",
                        DnsRecordType.A, false, false, false, configuration, CancellationToken.None));
                Assert.Contains("TCP Keepalive", exception.Message);
                Assert.Equal(0, connections);
            } finally {
                DnsWireResolveQuic.QuicConnectionFactory = previousFactory;
            }
        }
    }
}
#pragma warning restore CA2252
#endif
