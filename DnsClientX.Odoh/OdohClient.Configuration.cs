using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Odoh;

public sealed partial class OdohClient {
    private async Task<OdohTargetConfiguration> GetConfigurationAsync(bool forceRefresh, CancellationToken cancellationToken) {
        OdohTargetConfiguration? cached = Volatile.Read(ref _configuration);
        if (!forceRefresh && cached != null && cached.ExpiresAt > DateTimeOffset.UtcNow) {
            return cached;
        }
        await _configurationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            cached = Volatile.Read(ref _configuration);
            if (!forceRefresh && cached != null && cached.ExpiresAt > DateTimeOffset.UtcNow) {
                return cached;
            }
            ValidateDefaultHeaders();
            using (var request = new HttpRequestMessage(HttpMethod.Get, _configurationUri)) {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                using (HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false)) {
                    ValidateResponseDestination(response, _configurationUri);
                    response.EnsureSuccessStatusCode();
                    byte[] encoded = await ReadBoundedAsync(response.Content, 65537, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    cached = OdohTargetConfiguration.Parse(encoded, GetConfigurationExpiry(response, DateTimeOffset.UtcNow));
                    Volatile.Write(ref _configuration, cached);
                    return cached;
                }
            }
        } finally {
            _configurationGate.Release();
        }
    }

    private DateTimeOffset GetConfigurationExpiry(HttpResponseMessage response, DateTimeOffset now) {
        CacheControlHeaderValue? cache = response.Headers.CacheControl;
        if (cache?.NoStore == true || cache?.NoCache == true) {
            return now;
        }
        TimeSpan age = response.Headers.Age ?? TimeSpan.Zero;
        if (response.Headers.Date.HasValue) {
            TimeSpan apparentAge = now - response.Headers.Date.Value;
            if (apparentAge > age) {
                age = apparentAge;
            }
        }
        if (age < TimeSpan.Zero) {
            age = TimeSpan.Zero;
        }
        TimeSpan lifetime = cache?.MaxAge ?? _defaultConfigurationAge;
        if (cache?.MaxAge == null && response.Content.Headers.Expires.HasValue) {
            lifetime = response.Content.Headers.Expires.Value - (response.Headers.Date ?? now);
        }
        TimeSpan remaining = lifetime - age;
        if (remaining < TimeSpan.Zero) {
            remaining = TimeSpan.Zero;
        }
        if (remaining > _maximumConfigurationAge) {
            remaining = _maximumConfigurationAge;
        }
        // DateTimeOffset.Add fails clearly for a caller-supplied lifetime beyond its range.
        return now.Add(remaining);
    }
}
