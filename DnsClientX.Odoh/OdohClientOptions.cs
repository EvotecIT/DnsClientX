using System;

namespace DnsClientX.Odoh;

/// <summary>Specifies the HTTPS relay and target used by an Oblivious DNS client.</summary>
public sealed class OdohClientOptions {
    /// <summary>Creates options for an explicitly chosen relay and target.</summary>
    /// <param name="proxyUri">HTTPS relay endpoint, without URI-template variables.</param>
    /// <param name="targetUri">HTTPS target endpoint, without a query or fragment.</param>
    public OdohClientOptions(Uri proxyUri, Uri targetUri) {
        ProxyUri = proxyUri ?? throw new ArgumentNullException(nameof(proxyUri));
        TargetUri = targetUri ?? throw new ArgumentNullException(nameof(targetUri));
    }

    /// <summary>Gets the relay endpoint. DNS queries are sent only to this endpoint.</summary>
    public Uri ProxyUri { get; }
    /// <summary>Gets the target endpoint advertised to the relay.</summary>
    public Uri TargetUri { get; }
    /// <summary>Gets or sets the authenticated HTTPS configuration endpoint.
    /// The default is the target origin's /.well-known/odohconfigs resource.</summary>
    public Uri? ConfigurationUri { get; set; }
    /// <summary>Gets or sets an already acquired configuration. Expired configurations are refreshed over HTTPS.</summary>
    public OdohTargetConfiguration? InitialConfiguration { get; set; }
    /// <summary>Gets or sets the query plaintext padding block size. Zero disables padding; the default is 128 bytes.</summary>
    public int PaddingBlockSize { get; set; } = 128;
    /// <summary>Gets or sets an upper bound on configuration caching. The default is one day.</summary>
    public TimeSpan MaximumConfigurationAge { get; set; } = TimeSpan.FromDays(1);
    /// <summary>Gets or sets the lifetime used when the configuration HTTP response supplies no freshness metadata.
    /// The default is one hour and is bounded by <see cref="MaximumConfigurationAge"/>.</summary>
    public TimeSpan DefaultConfigurationAge { get; set; } = TimeSpan.FromHours(1);
}
