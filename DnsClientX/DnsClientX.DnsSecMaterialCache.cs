using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

public partial class ClientX {
    private readonly Lazy<DnsResponseCache> _dnsSecMaterialCache = new(() => new DnsResponseCache(cleanupThreshold: 256));

    /// <summary>Gets or sets whether authenticated DNSSEC material can be reused by this client.</summary>
    /// <remarks>Material is revalidated on every query and bounded by TTL and authenticated proof expiry.
    /// This cache serves recursive-resolver validation; iterative root traversal retains its own validation session.</remarks>
    public bool DnsSecMaterialCacheEnabled { get; set; } = true;

    private DnsResponseCache? GetDnsSecMaterialCache() {
        lock (_lock) {
            ThrowIfDisposed();
            return DnsSecMaterialCacheEnabled ? _dnsSecMaterialCache.Value : null;
        }
    }

    /// <summary>Removes material retained by this client without changing the shared response cache.</summary>
    public int ClearDnsSecMaterialCache() {
        ThrowIfDisposed();
        return _dnsSecMaterialCache.IsValueCreated ? _dnsSecMaterialCache.Value.Clear() : 0;
    }
    private async Task ValidateDnsSecResponseAsync(DnsResponse response, string name, DnsRecordType type,
        Configuration queryConfiguration, int retryDelayMs, bool ignoreCertificateErrors, CancellationToken cancellationToken) {
        var materialSession = new DnsSecMaterialSession(
            GetDnsSecMaterialCache(),
            (materialName, materialType) => DnsCacheKeyBuilder.Build(queryConfiguration,
                ConvertToPunycode(materialName), materialType, true, true, true, false, false,
                MaxCacheTtl, ignoreCertificateErrors),
            (materialName, materialType, token) =>
            ResolveInternal(materialName, materialType, requestDnsSec: true, validateDnsSec: false,
                returnAllTypes: true, maxRetries: 1, retryDelayMs: retryDelayMs,
                typedRecords: false, parseTypedTxtRecords: false, cancellationToken: token,
                dnsSecMaterialQuery: true,
                queryConfigurationOverride: queryConfiguration.CreateQuerySnapshot(materialName),
                certificatePolicy: ignoreCertificateErrors));
        var validator = new DnsSecValidationEngine(materialSession.LookupAsync,
            trustAnchorStorePath: queryConfiguration.Rfc5011TrustAnchorStorePath,
            signatureVerifier: queryConfiguration.DnsSecSignatureVerifier,
            zoneTrustAnchors: queryConfiguration.DnsSecTrustAnchors);
        DnsSecValidationResult validation = await validator.ValidateAsync(response, name, type, cancellationToken).ConfigureAwait(false);
        response.DnsSecValidationStatus = validation.Status;
        response.DnsSecValidationMessage = validation.Message;
        response.DnsSecValidationExpiresUtc = validator.CacheExpiresAtUtc;
        response.DnsSecValidationDependencies = materialSession.Complete(validator, validation.Status, MaxCacheTtl);
        if (validation.Status == DnsSecValidationStatus.Bogus || validation.Status == DnsSecValidationStatus.Indeterminate) {
            string validationError = $"DNSSEC {validation.Status.ToString().ToLowerInvariant()}: {validation.Message}";
            response.Error = string.IsNullOrEmpty(response.Error) ? validationError : $"{response.Error} {validationError}";
        }
    }

}
