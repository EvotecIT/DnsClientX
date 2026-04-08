using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            ResolverScoreSnapshot? snapshot = JsonSerializer.Deserialize<ResolverScoreSnapshot>(content, CreateSerializerOptions());
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

            File.WriteAllText(fullPath, JsonSerializer.Serialize(snapshot, CreateSerializerOptions()));
        }

        /// <summary>
        /// Creates serializer options for persisted resolver score snapshots.
        /// </summary>
        public static JsonSerializerOptions CreateSerializerOptions() {
            var serializerOptions = new JsonSerializerOptions {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            serializerOptions.Converters.Add(new JsonStringEnumConverter());
            return serializerOptions;
        }
    }
}
