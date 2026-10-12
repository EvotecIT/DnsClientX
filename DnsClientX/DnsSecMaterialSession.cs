using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>Stages material until the validator accepts the complete dependency chain.</summary>
internal sealed class DnsSecMaterialSession {
    private readonly DnsResponseCache? _cache;
    private readonly Func<string, DnsRecordType, string> _key;
    private readonly Func<string, DnsRecordType, CancellationToken, Task<DnsResponse>> _lookup;
    private readonly List<(string Name, DnsRecordType Type, string Key, DnsResponse Response)> _observed = new();
    internal DnsSecMaterialSession(DnsResponseCache? cache, Func<string, DnsRecordType, string> key,
        Func<string, DnsRecordType, CancellationToken, Task<DnsResponse>> lookup) {
        _cache = cache;
        _key = key;
        _lookup = lookup;
    }

    internal async Task<DnsResponse> LookupAsync(string name, DnsRecordType type, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        string key = _key(name, type);
        DnsResponse response;
        if (_cache != null && _cache.TryGet(key, out var cached)) {
            response = cached;
            response.ResponseSource = DnsResponseSource.Cache;
        } else {
            response = await _lookup(name, type, token).ConfigureAwait(false);
            response.ReceivedAtUtc ??= DateTimeOffset.UtcNow;
        }
        _observed.Add((name, type, key, response));
        return response;
    }

    internal DnsSecValidationDependency[] Complete(DnsSecValidationEngine validator, DnsSecValidationStatus status,
        TimeSpan maxCacheTtl) {
        bool accepted = status is DnsSecValidationStatus.Secure or DnsSecValidationStatus.Insecure;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var dependencies = new List<DnsSecValidationDependency>();
        foreach (var item in _observed) {
            DateTimeOffset? reusableUntil = null;
            if (accepted && validator.CacheExpiresAtUtc.HasValue) {
                validator.ApplyAuthenticatedLifetimes(item.Response);
                TimeSpan lifetime = ClientX.GetCacheTtl(item.Response, item.Name, item.Type);
                if (lifetime > maxCacheTtl) lifetime = maxCacheTtl;
                TimeSpan proofLifetime = validator.CacheExpiresAtUtc.Value - now;
                if (lifetime > proofLifetime) lifetime = proofLifetime;
                if (lifetime > TimeSpan.Zero) {
                    reusableUntil = now.Add(lifetime);
                    // A cache hit already has an absolute expiry. Do not refresh it on reuse.
                    if (item.Response.ResponseSource != DnsResponseSource.Cache)
                        _cache?.Set(item.Key, item.Response, lifetime, item.Name, item.Type);
                }
            }
            dependencies.Add(new DnsSecValidationDependency(item.Name, item.Type, item.Response, reusableUntil));
        }
        return dependencies.ToArray();
    }
}
