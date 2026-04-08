using System;
using System.IO;

namespace DnsClientX {
    /// <summary>
    /// Loads and saves persisted resolver score snapshots.
    /// </summary>
    public static class ResolverScoreStore {
        /// <summary>
        /// Loads a resolver score snapshot from disk.
        /// </summary>
        public static ResolverScoreSnapshot Load(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentNullException(nameof(path));
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) {
                throw new FileNotFoundException($"Resolver score snapshot not found: {path}", path);
            }

            string content = File.ReadAllText(fullPath);
            ResolverScoreSnapshot? snapshot = DnsClientXJsonSerializer.Deserialize<ResolverScoreSnapshot>(content);
            if (snapshot == null) {
                throw new InvalidOperationException($"Resolver score snapshot could not be read: {path}");
            }

            return snapshot;
        }

        /// <summary>
        /// Saves a resolver score snapshot to disk.
        /// </summary>
        public static void Save(string path, ResolverScoreSnapshot snapshot) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentNullException(nameof(path));
            }

            if (snapshot == null) {
                throw new ArgumentNullException(nameof(snapshot));
            }

            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, DnsClientXJsonSerializer.Serialize(snapshot));
        }
    }
}
