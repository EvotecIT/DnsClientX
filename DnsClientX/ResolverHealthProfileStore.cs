using System;
using System.IO;
using System.Text;

namespace DnsClientX {
    /// <summary>Reads and writes bounded, versioned local resolver health profiles.</summary>
    public static class ResolverHealthProfileStore {
        /// <summary>Gets the maximum UTF-8 profile size accepted by this library.</summary>
        public const int MaximumProfileBytes = 256 * 1024;
        /// <summary>Loads and validates a profile without downloading remote configuration.</summary>
        public static ResolverHealthProfile Load(string path) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            using var input = File.OpenRead(Path.GetFullPath(path));
            using var buffer = new MemoryStream();
            var block = new byte[4096];
            int read;
            while ((read = input.Read(block, 0, block.Length)) > 0) {
                if (buffer.Length + read > MaximumProfileBytes) throw new InvalidOperationException("Resolver health profile exceeds the 256 KiB limit.");
                buffer.Write(block, 0, read);
            }
            ResolverHealthProfile profile = DnsClientXJsonSerializer.Deserialize<ResolverHealthProfile>(new UTF8Encoding(false, true).GetString(buffer.ToArray())) ?? throw new InvalidOperationException("Resolver health profile is empty.");
            profile.Validate();
            return profile;
        }
        /// <summary>Validates and saves a profile as UTF-8 JSON.</summary>
        public static void Save(string path, ResolverHealthProfile profile) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Validate();
            string json = DnsClientXJsonSerializer.Serialize(profile);
            if (Encoding.UTF8.GetByteCount(json) > MaximumProfileBytes) throw new InvalidOperationException("Resolver health profile exceeds the 256 KiB limit.");
            File.WriteAllText(Path.GetFullPath(path), json, new UTF8Encoding(false));
        }
    }
}
