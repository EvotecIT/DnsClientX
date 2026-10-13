using System;

namespace DnsClientX;

/// <summary>Builds absolute HTTP request URIs from an immutable per-query configuration snapshot.</summary>
internal static class DnsHttpRequestUri {
    internal static Uri Build(Configuration configuration, string? query = null, string? path = null) {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));
        if (configuration.BaseUri is null) {
            throw new DnsClientException("An absolute base URI is required for DNS over HTTP.");
        }

        if (configuration.DohUriTemplate != null) {
            DnsDohUriTemplate template = DnsDohUriTemplate.Parse(configuration.DohUriTemplate);
            string expanded;
            if (query == null) {
                expanded = template.ExpandPost();
            } else {
                if (!query.StartsWith("dns=", StringComparison.Ordinal)) {
                    throw new ArgumentException("A DoH URI template accepts only a DNS wire query.", nameof(query));
                }
                string encoded = query.Substring(4).Replace('-', '+').Replace('_', '/');
                encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
                expanded = template.ExpandGet(Convert.FromBase64String(encoded));
            }
            var result = new Uri(expanded, UriKind.Absolute);
            if (result.Scheme != Uri.UriSchemeHttps || result.IdnHost != configuration.BaseUri.IdnHost
                || result.Port != configuration.BaseUri.Port || result.UserInfo.Length != 0 || result.Fragment.Length != 0) {
                throw new DnsClientException("The DoH template must retain the configured authenticated HTTPS origin.");
            }
            return result;
        }

        var builder = new UriBuilder(configuration.BaseUri);
        if (path != null) builder.Path = path;
        if (query != null) builder.Query = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
        return builder.Uri;
    }
}
