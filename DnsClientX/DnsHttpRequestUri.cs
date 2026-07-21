using System;

namespace DnsClientX;

/// <summary>Builds absolute HTTP request URIs from an immutable per-query configuration snapshot.</summary>
internal static class DnsHttpRequestUri {
    internal static Uri Build(Configuration configuration, string? query = null, string? path = null) {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));
        if (configuration.BaseUri is null) {
            throw new DnsClientException("An absolute base URI is required for DNS over HTTP.");
        }

        var builder = new UriBuilder(configuration.BaseUri);
        if (path != null) builder.Path = path;
        if (query != null) builder.Query = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
        return builder.Uri;
    }
}
