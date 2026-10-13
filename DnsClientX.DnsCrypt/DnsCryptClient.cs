using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.DnsCrypt;

/// <summary>Authenticates a trusted provider's resolver certificates and exchanges encrypted DNSCrypt v2 packets.</summary>
/// <remarks>The caller owns this provider even when its configuration is used by a ClientX instance.
/// Each query uses a fresh ephemeral key pair and owns its sockets. Disposal cancels active exchanges.
/// The only unencrypted DNS message sent is the configured provider's certificate TXT query.</remarks>
public sealed partial class DnsCryptClient : IDnsWireTransport, IDisposable {
    private readonly DnsCryptOptions _options;
    private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private readonly object _lifetimeLock = new object();
    private volatile CertificateState? _certificate;
    private bool _disposed;
    private int _active;

    /// <summary>Creates a provider with snapshotted options and verifies the native cryptographic backend.</summary>
    public DnsCryptClient(DnsCryptOptions options) {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Snapshot();
        _ = DnsCryptSodium.Version;
    }

    /// <summary>Gets DNSCrypt or DNSCryptRelay according to the configured endpoint path.</summary>
    public DnsRequestFormat RequestFormat => _options.RelayEndpoint == null ? DnsRequestFormat.DnsCrypt : DnsRequestFormat.DnsCryptRelay;

    /// <summary>Gets the loaded native libsodium version; failure indicates an unsupported runtime asset.</summary>
    public static string NativeLibraryVersion => DnsCryptSodium.Version;

    /// <summary>Creates a DnsClientX configuration using this provider. The caller retains provider ownership.</summary>
    public Configuration CreateConfiguration() {
        var endpoint = _options.RelayEndpoint ?? _options.ResolverEndpoint;
        return new Configuration(endpoint.Address.ToString(), RequestFormat) { Port = endpoint.Port, WireTransport = this };
    }

    /// <summary>Creates a high-level client using this provider; dispose both objects when finished.</summary>
    /// <param name="enableCache">Whether the client may cache DNS responses.</param>
    public ClientX CreateClient(bool enableCache = false) => new ClientX(CreateConfiguration(), enableCache: enableCache);

    /// <summary>Returns an authenticated DNS wire response, retrying only through encrypted DNSCrypt transports.</summary>
    public async Task<byte[]> QueryAsync(byte[] query, CancellationToken cancellationToken = default) =>
        (await QueryExchangeAsync(query, cancellationToken).ConfigureAwait(false)).Message;

    Task<DnsWireTransportResponse> IDnsWireTransport.QueryAsync(byte[] query, CancellationToken cancellationToken) => QueryExchangeAsync(query, cancellationToken);

    /// <summary>Returns an authenticated DNS response and the physical transport used by that exchange.</summary>
    public async Task<DnsWireTransportResponse> QueryExchangeAsync(byte[] query, CancellationToken cancellationToken = default) {
        DnsCryptWire.ValidateQuery(query);
        byte[] snapshot = (byte[])query.Clone();
        using var linked = Enter(cancellationToken);
        try {
            linked.Token.ThrowIfCancellationRequested();
            DnsCryptCertificate certificate = await GetCertificateAsync(false, linked.Token).ConfigureAwait(false);
            try { return await QueryWithCertificateAsync(snapshot, certificate, linked.Token).ConfigureAwait(false); }
            catch (Exception exception) when (IsExchangeFailure(exception)) {
                // A resolver may withdraw a key before expiry. Refresh once for recovery,
                // preserving a still-valid certificate when discovery itself is unavailable.
                certificate = await GetCertificateAsync(true, linked.Token).ConfigureAwait(false);
                return await QueryWithCertificateAsync(snapshot, certificate, linked.Token).ConfigureAwait(false);
            }
        } finally { Exit(); }
    }

    /// <summary>Retrieves and validates currently advertised certificates without sending an application query.</summary>
    public async Task RefreshCertificatesAsync(CancellationToken cancellationToken = default) {
        using var linked = Enter(cancellationToken);
        try { await GetCertificateAsync(true, linked.Token).ConfigureAwait(false); }
        finally { Exit(); }
    }

    private async Task<DnsWireTransportResponse> QueryWithCertificateAsync(byte[] query, DnsCryptCertificate certificate, CancellationToken token) {
        bool tcp = _options.NetworkTransport == DnsCryptNetworkTransport.Tcp;
        if (tcp) return await EncryptedExchangeAsync(query, certificate, true, token).ConfigureAwait(false);
        try {
            DnsWireTransportResponse response = await EncryptedExchangeAsync(query, certificate, false, token).ConfigureAwait(false);
            if (!DnsWireMessageParser.TryParseHeader(response.Message, out var header) || !header.IsTruncated) return response;
        } catch (Exception exception) when (IsExchangeFailure(exception)) { }
        return await EncryptedExchangeAsync(query, certificate, true, token).ConfigureAwait(false);
    }

    private async Task<DnsWireTransportResponse> EncryptedExchangeAsync(byte[] query, DnsCryptCertificate certificate, bool tcp, CancellationToken token) {
        if (!certificate.IsValidAt(DateTimeOffset.UtcNow)) throw new InvalidDataException("The DNSCrypt resolver certificate expired before query encryption.");
        var publicKey = new byte[32]; var secretKey = new byte[32]; byte[]? shared = null;
        try {
            DnsCryptSodium.KeyPair(publicKey, secretKey);
            shared = DnsCryptSodium.SharedKey(certificate.EncryptionSystem, certificate.ResolverPublicKey, secretKey);
            byte[] nonce = DnsCryptSodium.Random(12);
            bool udpBudget = !tcp || _options.RelayEndpoint != null;
            int target = udpBudget ? Math.Max(query.Length + 1, _options.UdpQuerySize - 68) : query.Length + 1 + DnsCryptSodium.Random(1)[0];
            int paddedLength = Math.Min(4028, ((target + 63) / 64) * 64);
            byte[] packet = DnsCryptWire.EncryptQuery(certificate, query, publicKey, shared, nonce, paddedLength);
            byte[]? decrypted = null;
            await ExchangeAsync(packet, tcp, received => {
                if (!certificate.IsValidAt(DateTimeOffset.UtcNow) || (udpBudget && received.Length > packet.Length)) return false;
                try { decrypted = DnsCryptWire.DecryptResponse(certificate, query, received, shared, nonce); return true; }
                catch (System.Security.Cryptography.CryptographicException) { return false; }
                catch (InvalidDataException) { return false; }
            }, token).ConfigureAwait(false);
            return new DnsWireTransportResponse(decrypted!, tcp ? Transport.Tcp : Transport.Udp);
        } finally { DnsCryptSodium.Zero(secretKey); if (shared != null) DnsCryptSodium.Zero(shared); }
    }

    private CancellationTokenSource Enter(CancellationToken token) {
        lock (_lifetimeLock) {
            if (_disposed) throw new ObjectDisposedException(nameof(DnsCryptClient));
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
            _active++; return linked;
        }
    }

    private void Exit() {
        lock (_lifetimeLock) {
            _active--;
            if (_disposed && _active == 0) { _lifetime.Dispose(); _refreshGate.Dispose(); }
        }
    }

    /// <summary>Cancels all outstanding exchanges and rejects future queries.</summary>
    public void Dispose() {
        lock (_lifetimeLock) {
            if (_disposed) return;
            _disposed = true; _lifetime.Cancel();
            if (_active == 0) { _lifetime.Dispose(); _refreshGate.Dispose(); }
        }
    }

    private static bool IsExchangeFailure(Exception exception) => exception is TimeoutException || exception is SocketException || exception is IOException || exception is InvalidDataException;

    private sealed class CertificateState {
        internal CertificateState(DnsCryptCertificate certificate, DateTimeOffset refreshAt) { Certificate = certificate; RefreshAt = refreshAt; }
        internal DnsCryptCertificate Certificate { get; }
        internal DateTimeOffset RefreshAt { get; }
    }
}
