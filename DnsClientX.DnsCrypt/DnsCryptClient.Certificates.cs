using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.DnsCrypt;

public sealed partial class DnsCryptClient {
    private async Task<DnsCryptCertificate> GetCertificateAsync(bool force, CancellationToken token) {
        CertificateState? existing = _certificate;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!force && existing != null && existing.Certificate.IsValidAt(now) && now < existing.RefreshAt) return existing.Certificate;
        await _refreshGate.WaitAsync(token).ConfigureAwait(false);
        try {
            existing = _certificate; now = DateTimeOffset.UtcNow;
            if (!force && existing != null && existing.Certificate.IsValidAt(now) && now < existing.RefreshAt) return existing.Certificate;
            try {
                byte[] query = DnsCryptWire.CertificateQuery(_options.ProviderName, _options.RelayEndpoint == null ? 512 : 4096);
                bool tcp = _options.NetworkTransport == DnsCryptNetworkTransport.Tcp;
                byte[] response;
                if (tcp) response = await ExchangeAsync(query, true, received => DnsCryptWire.Matches(query, received), token).ConfigureAwait(false);
                else {
                    try {
                        response = await ExchangeAsync(query, false, received => DnsCryptWire.Matches(query, received), token).ConfigureAwait(false);
                        if (DnsWireMessageParser.TryParseHeader(response, out var header) && header.IsTruncated) tcp = true;
                    } catch (Exception exception) when (IsExchangeFailure(exception)) { response = Array.Empty<byte>(); tcp = true; }
                    if (tcp) response = await ExchangeAsync(query, true, received => DnsCryptWire.Matches(query, received), token).ConfigureAwait(false);
                }
                DnsCryptCertificate selected;
                try { selected = SelectCertificate(query, response); }
                catch (InvalidDataException) when (!tcp) {
                    response = await ExchangeAsync(query, true, received => DnsCryptWire.Matches(query, received), token).ConfigureAwait(false);
                    selected = SelectCertificate(query, response);
                }
                DateTimeOffset refresh = DateTimeOffset.UtcNow + _options.CertificateRefreshInterval;
                // Refresh before expiry when practical, and never extend signed validity.
                DateTimeOffset early = selected.ValidUntil - TimeSpan.FromSeconds(30);
                if (early < refresh) refresh = early;
                _certificate = new CertificateState(selected, refresh);
                return selected;
            } catch (Exception exception) when (IsExchangeFailure(exception)) {
                now = DateTimeOffset.UtcNow;
                if (existing == null || !existing.Certificate.IsValidAt(now)) throw;
                _certificate = new CertificateState(existing.Certificate, now + TimeSpan.FromSeconds(30));
                return existing.Certificate;
            }
        } finally { _refreshGate.Release(); }
    }

    private DnsCryptCertificate SelectCertificate(byte[] query, byte[] response) => DnsCryptCertificate.Select(
        DnsCryptWire.CertificateRecords(query, response), _options.ProviderPublicKey, _options.EncryptionPolicy, DateTimeOffset.UtcNow);
}
