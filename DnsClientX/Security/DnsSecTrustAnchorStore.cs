using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DnsClientX;

/// <summary>Identifies the RFC 5011 lifecycle state of a managed DNSSEC trust anchor.</summary>
public enum DnsSecTrustAnchorState {
    /// <summary>The SEP key is completing the add hold-down period.</summary>
    AddPending,
    /// <summary>The key is an active trust anchor.</summary>
    Valid,
    /// <summary>The active key was absent from the most recently authenticated DNSKEY RRset.</summary>
    Missing,
    /// <summary>The key proved its own REVOKE bit and is permanently unusable.</summary>
    Revoked,
    /// <summary>The revoked key completed the remove hold-down period.</summary>
    Removed
}

/// <summary>Describes one persisted RFC 5011 root trust-anchor key.</summary>
public sealed class DnsSecManagedTrustAnchor {
    internal DnsSecManagedTrustAnchor(Rfc5011StateKey key) {
        KeyTag = key.KeyTag;
        Flags = key.Flags;
        Protocol = key.Protocol;
        Algorithm = (DnsKeyAlgorithm)key.Algorithm;
        PublicKey = key.PublicKey;
        State = key.State;
        FirstSeenUtc = key.FirstSeenUtc;
        LastSeenUtc = key.LastSeenUtc;
        HoldDownUntilUtc = key.HoldDownUntilUtc;
        MissingSinceUtc = key.MissingSinceUtc;
        RevokedAtUtc = key.RevokedAtUtc;
    }

    /// <summary>Gets the DNSKEY key tag as last observed.</summary>
    public ushort KeyTag { get; }
    /// <summary>Gets the DNSKEY flags as last observed.</summary>
    public ushort Flags { get; }
    /// <summary>Gets the DNSKEY protocol field.</summary>
    public byte Protocol { get; }
    /// <summary>Gets the DNSSEC algorithm.</summary>
    public DnsKeyAlgorithm Algorithm { get; }
    /// <summary>Gets the Base64 DNSKEY public key.</summary>
    public string PublicKey { get; }
    /// <summary>Gets the RFC 5011 state.</summary>
    public DnsSecTrustAnchorState State { get; }
    /// <summary>Gets when the key first entered its current add lifecycle.</summary>
    public DateTimeOffset FirstSeenUtc { get; }
    /// <summary>Gets the most recent authenticated observation of the key.</summary>
    public DateTimeOffset LastSeenUtc { get; }
    /// <summary>Gets the add hold-down deadline, when applicable.</summary>
    public DateTimeOffset? HoldDownUntilUtc { get; }
    /// <summary>Gets when an active key was first observed missing.</summary>
    public DateTimeOffset? MissingSinceUtc { get; }
    /// <summary>Gets when a self-signed revocation was authenticated.</summary>
    public DateTimeOffset? RevokedAtUtc { get; }
}

/// <summary>Provides a read-only view of an RFC 5011 trust-anchor state file.</summary>
public sealed class DnsSecTrustAnchorStoreSnapshot {
    internal DnsSecTrustAnchorStoreSnapshot(Rfc5011StateFile state) {
        LastSuccessfulRefreshUtc = state.LastSuccessfulRefreshUtc;
        NextRefreshUtc = state.NextRefreshUtc;
        Anchors = Array.AsReadOnly(state.Keys.Select(key => new DnsSecManagedTrustAnchor(key)).ToArray());
    }

    /// <summary>Gets the most recent authenticated DNSKEY refresh time.</summary>
    public DateTimeOffset? LastSuccessfulRefreshUtc { get; }
    /// <summary>Gets the RFC 5011 active-refresh deadline calculated from TTL and signature lifetime.</summary>
    public DateTimeOffset? NextRefreshUtc { get; }
    /// <summary>Gets all tracked keys, including revoked and removed tombstones.</summary>
    public IReadOnlyList<DnsSecManagedTrustAnchor> Anchors { get; }
}

/// <summary>Reads persisted RFC 5011 state maintained by DNSSEC validation.</summary>
public static class DnsSecTrustAnchorStore {
    /// <summary>Loads and validates a trust-anchor state file without changing it.</summary>
    public static DnsSecTrustAnchorStoreSnapshot Load(string path) {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("RFC 5011 trust-anchor state was not found.", fullPath);
        return new DnsSecTrustAnchorStoreSnapshot(Rfc5011Store.LoadFile(fullPath));
    }
}

internal sealed class Rfc5011Store {
    private const int SchemaVersion = 1;
    private static readonly TimeSpan AddHoldDown = TimeSpan.FromDays(30);
    private static readonly TimeSpan RemoveHoldDown = TimeSpan.FromDays(30);
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly object _pathLock;

    internal Rfc5011Store(string path) {
        _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        _pathLock = PathLocks.GetOrAdd(_path, _ => new object());
    }

    internal DnsSecKey[] ActiveKeys(DateTimeOffset now) {
        lock (_pathLock) {
            Rfc5011StateFile state = LoadOrCreate(now);
            return state.Keys
                .Where(key => key.State == DnsSecTrustAnchorState.Valid || key.State == DnsSecTrustAnchorState.Missing)
                .Select(ToDnsSecKey)
                .ToArray();
        }
    }

    internal DnsSecTrustAnchorStoreSnapshot ObserveAuthenticated(
        IReadOnlyCollection<DnsSecKey> observed,
        IReadOnlyCollection<string> validatedBy,
        IReadOnlyCollection<string> verifiedRevocations,
        uint originalTtl,
        DateTimeOffset signatureExpiration,
        DateTimeOffset now) {
        lock (_pathLock) {
            Rfc5011StateFile state = LoadOrCreate(now);
            if (state.LastSuccessfulRefreshUtc.HasValue && now < state.LastSuccessfulRefreshUtc.Value) {
                throw new InvalidOperationException(
                    "System time moved backwards before the last authenticated RFC 5011 observation; trust-anchor timers were not advanced.");
            }

            var observedById = observed
                .Where(IsSepKey)
                .GroupBy(KeyIdentity, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var existingById = state.Keys.ToDictionary(key => key.Id, StringComparer.Ordinal);

            // A valid self-signature by the key itself makes revocation immediate and permanent.
            foreach (string id in verifiedRevocations) {
                if (!observedById.TryGetValue(id, out DnsSecKey revokedKey)) continue;
                if (!existingById.TryGetValue(id, out Rfc5011StateKey? tracked)) continue;
                tracked.Flags = revokedKey.Flags;
                tracked.KeyTag = revokedKey.KeyTag;
                tracked.State = DnsSecTrustAnchorState.Revoked;
                tracked.RevokedAtUtc ??= now;
                tracked.LastSeenUtc = now;
                tracked.MissingSinceUtc = null;
            }

            foreach (KeyValuePair<string, DnsSecKey> item in observedById) {
                DnsSecKey key = item.Value;
                if ((key.Flags & 0x0080) != 0) continue;
                if (!existingById.TryGetValue(item.Key, out Rfc5011StateKey? tracked)) {
                    DateTimeOffset holdDownUntil = now + (TimeSpan.FromSeconds(originalTtl) > AddHoldDown
                        ? TimeSpan.FromSeconds(originalTtl)
                        : AddHoldDown);
                    tracked = FromKey(key, DnsSecTrustAnchorState.AddPending, now);
                    tracked.HoldDownUntilUtc = holdDownUntil;
                    tracked.ValidatedBy = validatedBy.Distinct(StringComparer.Ordinal).ToArray();
                    state.Keys.Add(tracked);
                    existingById.Add(item.Key, tracked);
                    continue;
                }

                tracked.Flags = key.Flags;
                tracked.KeyTag = key.KeyTag;
                tracked.LastSeenUtc = now;
                if (tracked.State == DnsSecTrustAnchorState.Missing) {
                    tracked.State = DnsSecTrustAnchorState.Valid;
                    tracked.MissingSinceUtc = null;
                } else if (tracked.State == DnsSecTrustAnchorState.AddPending
                           && tracked.HoldDownUntilUtc.HasValue
                           && now >= tracked.HoldDownUntilUtc.Value) {
                    bool originalValidatorRemains = tracked.ValidatedBy.Length == 0 || tracked.ValidatedBy.Any(id =>
                        existingById.TryGetValue(id, out Rfc5011StateKey? validator)
                        && (validator.State == DnsSecTrustAnchorState.Valid || validator.State == DnsSecTrustAnchorState.Missing));
                    if (originalValidatorRemains) {
                        tracked.State = DnsSecTrustAnchorState.Valid;
                        tracked.HoldDownUntilUtc = null;
                    } else {
                        state.Keys.Remove(tracked);
                        existingById.Remove(item.Key);
                    }
                }
            }

            foreach (Rfc5011StateKey tracked in state.Keys.ToArray()) {
                if (observedById.ContainsKey(tracked.Id)) continue;
                if (tracked.State == DnsSecTrustAnchorState.AddPending) {
                    state.Keys.Remove(tracked);
                } else if (tracked.State == DnsSecTrustAnchorState.Valid) {
                    tracked.State = DnsSecTrustAnchorState.Missing;
                    tracked.MissingSinceUtc = now;
                } else if (tracked.State == DnsSecTrustAnchorState.Revoked
                           && tracked.RevokedAtUtc.HasValue
                           && now >= tracked.RevokedAtUtc.Value + RemoveHoldDown) {
                    tracked.State = DnsSecTrustAnchorState.Removed;
                }
            }

            state.LastSuccessfulRefreshUtc = now;
            state.NextRefreshUtc = CalculateNextRefresh(now, originalTtl, signatureExpiration);
            SaveFile(_path, state);
            return new DnsSecTrustAnchorStoreSnapshot(state);
        }
    }

    internal static string KeyIdentity(DnsSecKey key) =>
        $"{key.Protocol}:{key.Algorithm}:{Convert.ToBase64String(key.PublicKey)}";

    internal static Rfc5011StateFile LoadFile(string path) {
        string content = File.ReadAllText(path);
        Rfc5011StateFile? state;
        try {
            state = DnsClientXJsonSerializer.Deserialize<Rfc5011StateFile>(content);
        } catch (Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException) {
            throw new InvalidDataException("RFC 5011 trust-anchor state is malformed; it was not reset automatically.", ex);
        }
        if (state == null || state.SchemaVersion != SchemaVersion || state.Keys == null) {
            throw new InvalidDataException("RFC 5011 trust-anchor state has an unsupported schema or missing key collection.");
        }
        if (state.Keys.Any(key => string.IsNullOrWhiteSpace(key.Id) || string.IsNullOrWhiteSpace(key.PublicKey))) {
            throw new InvalidDataException("RFC 5011 trust-anchor state contains an invalid key entry.");
        }
        return state;
    }

    private Rfc5011StateFile LoadOrCreate(DateTimeOffset now) {
        if (File.Exists(_path)) return LoadFile(_path);
        var state = new Rfc5011StateFile();
        foreach (DnsSecKey key in RootTrustAnchors.DnsKeys) {
            state.Keys.Add(FromKey(key, DnsSecTrustAnchorState.Valid, now));
        }
        return state;
    }

    private static Rfc5011StateKey FromKey(DnsSecKey key, DnsSecTrustAnchorState state,
        DateTimeOffset now) => new() {
            Id = KeyIdentity(key),
            KeyTag = key.KeyTag,
            Flags = key.Flags,
            Protocol = key.Protocol,
            Algorithm = key.Algorithm,
            PublicKey = Convert.ToBase64String(key.PublicKey),
            State = state,
            FirstSeenUtc = now,
            LastSeenUtc = now
        };

    private static DnsSecKey ToDnsSecKey(Rfc5011StateKey key) =>
        new(".", key.Flags, key.Protocol, key.Algorithm, Convert.FromBase64String(key.PublicKey));

    private static bool IsSepKey(DnsSecKey key) =>
        key.Protocol == 3 && (key.Flags & 0x0100) != 0 && (key.Flags & 0x0001) != 0;

    private static DateTimeOffset CalculateNextRefresh(DateTimeOffset now, uint ttl,
        DateTimeOffset signatureExpiration) {
        TimeSpan ttlHalf = TimeSpan.FromSeconds(ttl / 2d);
        TimeSpan signatureHalf = TimeSpan.FromTicks(Math.Max(0, (signatureExpiration - now).Ticks / 2));
        TimeSpan interval = new[] { TimeSpan.FromDays(15), ttlHalf, signatureHalf }.Min();
        if (interval < TimeSpan.FromHours(1)) interval = TimeSpan.FromHours(1);
        return now + interval;
    }

    private static void SaveFile(string path, Rfc5011StateFile state) {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(temporary, DnsClientXJsonSerializer.Serialize(state));
            if (File.Exists(path)) {
                File.Replace(temporary, path, null);
            } else {
                File.Move(temporary, path);
            }
        } finally {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

internal sealed class Rfc5011StateFile {
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset? LastSuccessfulRefreshUtc { get; set; }
    public DateTimeOffset? NextRefreshUtc { get; set; }
    public List<Rfc5011StateKey> Keys { get; set; } = new();
}

internal sealed class Rfc5011StateKey {
    public string Id { get; set; } = string.Empty;
    public ushort KeyTag { get; set; }
    public ushort Flags { get; set; }
    public byte Protocol { get; set; }
    public byte Algorithm { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public DnsSecTrustAnchorState State { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public DateTimeOffset? HoldDownUntilUtc { get; set; }
    public DateTimeOffset? MissingSinceUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string[] ValidatedBy { get; set; } = Array.Empty<string>();
}
