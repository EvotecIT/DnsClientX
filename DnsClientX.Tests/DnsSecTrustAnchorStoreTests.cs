using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Protects RFC 5011 hold-down, revocation, rollback, and persistence contracts.</summary>
    public sealed class DnsSecTrustAnchorStoreTests : IDisposable {
        private readonly string _directory = Path.Combine(Path.GetTempPath(),
            "DnsClientX-Rfc5011-" + Guid.NewGuid().ToString("N"));
        private string PathName => Path.Combine(_directory, "root-anchors.json");

        /// <summary>A new SEP key needs a post-deadline authenticated observation before acceptance.</summary>
        [Fact]
        public void ObserveAuthenticated_EnforcesAddHoldDownAndSecondObservation() {
            var store = new Rfc5011Store(PathName);
            DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DnsSecKey[] existing = store.ActiveKeys(start);
            DnsSecKey candidate = Key(15, 42);
            string[] validators = existing.Select(Rfc5011Store.KeyIdentity).ToArray();

            DnsSecTrustAnchorStoreSnapshot first = store.ObserveAuthenticated(
                existing.Append(candidate).ToArray(), validators, Array.Empty<string>(),
                originalTtl: 172800, start.AddDays(60), start);
            DnsSecManagedTrustAnchor pending = Assert.Single(first.Anchors,
                anchor => anchor.PublicKey == Convert.ToBase64String(candidate.PublicKey));
            Assert.Equal(DnsSecTrustAnchorState.AddPending, pending.State);

            DnsSecTrustAnchorStoreSnapshot beforeDeadline = store.ObserveAuthenticated(
                existing.Append(candidate).ToArray(), validators, Array.Empty<string>(),
                172800, start.AddDays(60), start.AddDays(29));
            Assert.Equal(DnsSecTrustAnchorState.AddPending, Assert.Single(beforeDeadline.Anchors,
                anchor => anchor.PublicKey == Convert.ToBase64String(candidate.PublicKey)).State);

            DnsSecTrustAnchorStoreSnapshot accepted = store.ObserveAuthenticated(
                existing.Append(candidate).ToArray(), validators, Array.Empty<string>(),
                172800, start.AddDays(90), start.AddDays(31));
            Assert.Equal(DnsSecTrustAnchorState.Valid, Assert.Single(accepted.Anchors,
                anchor => anchor.PublicKey == Convert.ToBase64String(candidate.PublicKey)).State);
            Assert.True(File.Exists(PathName));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }

        /// <summary>Removing a pending key from any authenticated observation resets its add timer.</summary>
        [Fact]
        public void ObserveAuthenticated_RemovesAbsentPendingKey() {
            var store = new Rfc5011Store(PathName);
            DateTimeOffset start = DateTimeOffset.UtcNow;
            DnsSecKey[] existing = store.ActiveKeys(start);
            DnsSecKey candidate = Key(15, 43);
            string[] validators = existing.Select(Rfc5011Store.KeyIdentity).ToArray();
            store.ObserveAuthenticated(existing.Append(candidate).ToArray(), validators,
                Array.Empty<string>(), 3600, start.AddDays(10), start);

            DnsSecTrustAnchorStoreSnapshot reset = store.ObserveAuthenticated(existing, validators,
                Array.Empty<string>(), 3600, start.AddDays(10), start.AddDays(1));

            Assert.DoesNotContain(reset.Anchors,
                anchor => anchor.PublicKey == Convert.ToBase64String(candidate.PublicKey));
        }

        /// <summary>A proven self-revocation is immediate, permanent, and later becomes a tombstone.</summary>
        [Fact]
        public void ObserveAuthenticated_RevokesImmediatelyAndRemovesAfterHoldDown() {
            var store = new Rfc5011Store(PathName);
            DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DnsSecKey[] active = store.ActiveKeys(start);
            DnsSecKey original = active[0];
            var revoked = new DnsSecKey(".", (ushort)(original.Flags | 0x0080), original.Protocol,
                original.Algorithm, original.PublicKey);
            string id = Rfc5011Store.KeyIdentity(original);
            DnsSecKey[] observed = active.Skip(1).Append(revoked).ToArray();

            DnsSecTrustAnchorStoreSnapshot revokedSnapshot = store.ObserveAuthenticated(
                observed, active.Select(Rfc5011Store.KeyIdentity).ToArray(), new[] { id },
                3600, start.AddDays(10), start);
            Assert.Equal(DnsSecTrustAnchorState.Revoked,
                Assert.Single(revokedSnapshot.Anchors, anchor => anchor.PublicKey == Convert.ToBase64String(original.PublicKey)).State);
            Assert.DoesNotContain(store.ActiveKeys(start.AddDays(1)),
                key => Rfc5011Store.KeyIdentity(key) == id);

            DnsSecTrustAnchorStoreSnapshot removed = store.ObserveAuthenticated(
                active.Skip(1).ToArray(), active.Skip(1).Select(Rfc5011Store.KeyIdentity).ToArray(),
                Array.Empty<string>(), 3600, start.AddDays(60), start.AddDays(31));
            Assert.Equal(DnsSecTrustAnchorState.Removed,
                Assert.Single(removed.Anchors, anchor => anchor.PublicKey == Convert.ToBase64String(original.PublicKey)).State);
        }

        /// <summary>Wall-clock rollback cannot shorten security hold-down periods.</summary>
        [Fact]
        public void ObserveAuthenticated_RejectsClockRollback() {
            var store = new Rfc5011Store(PathName);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DnsSecKey[] active = store.ActiveKeys(now);
            string[] validators = active.Select(Rfc5011Store.KeyIdentity).ToArray();
            store.ObserveAuthenticated(active, validators, Array.Empty<string>(), 3600, now.AddDays(5), now);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                store.ObserveAuthenticated(active, validators, Array.Empty<string>(), 3600,
                    now.AddDays(5), now.AddMinutes(-1)));

            Assert.Contains("backwards", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Malformed state fails closed instead of silently re-bootstraping old anchors.</summary>
        [Fact]
        public void Load_RejectsCorruptState() {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(PathName, "{ not-json");

            Assert.Throws<InvalidDataException>(() => DnsSecTrustAnchorStore.Load(PathName));
        }

        private static DnsSecKey Key(byte algorithm, byte marker) =>
            new(".", 257, 3, algorithm, Enumerable.Repeat(marker, 32).ToArray());

        /// <summary>Deletes the isolated state directory created for a test.</summary>
        public void Dispose() {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }
}
