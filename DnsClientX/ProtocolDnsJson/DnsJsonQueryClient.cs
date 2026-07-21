using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>
/// Sends DNS JSON queries through a caller-provided <see cref="HttpClient"/>.
/// This is suitable for browser and dependency-injected hosts where DnsClientX must not own the HTTP handler.
/// </summary>
public static class DnsJsonQueryClient {
    /// <summary>
    /// Queries a DNS JSON endpoint and returns the shared DnsClientX response model.
    /// </summary>
    /// <param name="httpClient">HTTP client owned by the caller.</param>
    /// <param name="endpoint">Absolute DNS JSON endpoint, for example <c>https://dns.google/resolve</c>.</param>
    /// <param name="name">DNS name to query.</param>
    /// <param name="type">DNS record type to query.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC records.</param>
    /// <param name="checkingDisabled">Whether to ask the recursive resolver to disable DNSSEC checking.</param>
    /// <param name="debug">Whether to log the JSON response through DnsClientX diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<DnsResponse> QueryAsync(
        HttpClient httpClient,
        Uri endpoint,
        string name,
        DnsRecordType type,
        bool requestDnsSec = false,
        bool checkingDisabled = false,
        bool debug = false,
        CancellationToken cancellationToken = default) {
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
        if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp)) {
            throw new ArgumentException("The DNS JSON endpoint must be an absolute HTTP or HTTPS URI.", nameof(endpoint));
        }
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The DNS name cannot be empty.", nameof(name));

        string normalizedName = name.Trim().TrimEnd('.');
        var uriBuilder = new UriBuilder(endpoint) { Fragment = string.Empty };
        string existingQuery = uriBuilder.Query.Length > 1 ? uriBuilder.Query.Substring(1) + "&" : string.Empty;
        uriBuilder.Query = string.Concat(
            existingQuery,
            "name=", Uri.EscapeDataString(normalizedName),
            "&type=", ((ushort)type).ToString(System.Globalization.CultureInfo.InvariantCulture),
            requestDnsSec ? "&do=1" : string.Empty,
            checkingDisabled ? "&cd=1" : string.Empty);

        using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-json"));
        using HttpResponseMessage httpResponse = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!httpResponse.IsSuccessStatusCode) {
            throw new DnsClientException($"DNS JSON endpoint '{endpoint.Host}' returned HTTP {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}).");
        }

        HttpContent? content = httpResponse.Content;
        if (content == null) {
            throw new DnsClientException($"DNS JSON endpoint '{endpoint.Host}' returned no response content.");
        }

        string? mediaType = content.Headers.ContentType?.MediaType;
        if (mediaType != null && mediaType.Length > 0 &&
            !mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("application/dns-json", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)) {
            throw new DnsClientException($"DNS JSON endpoint '{endpoint.Host}' returned unsupported media type '{mediaType}'.");
        }

        DnsResponse response = await httpResponse.DeserializeResponse(debug).ConfigureAwait(false);
        response.Questions ??= Array.Empty<DnsQuestion>();
        response.Answers ??= Array.Empty<DnsAnswer>();
        response.Authorities ??= Array.Empty<DnsAnswer>();
        response.Additional ??= Array.Empty<DnsAnswer>();
        if (response.Questions.Length == 0) {
            response.Questions = new[] {
                new DnsQuestion {
                    Name = normalizedName,
                    OriginalName = name,
                    Type = type
                }
            };
        }

        var configuration = new Configuration(endpoint, DnsRequestFormat.DnsOverHttps) {
            CheckingDisabled = checkingDisabled,
            Port = endpoint.IsDefaultPort ? 443 : endpoint.Port
        };
        response.AddServerDetails(configuration, Transport.Doh);
        return response;
    }
}
