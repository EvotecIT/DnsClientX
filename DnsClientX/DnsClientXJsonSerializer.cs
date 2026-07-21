using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnsClientX {
    /// <summary>
    /// Provides shared JSON serialization helpers for user-facing payloads.
    /// </summary>
    public static class DnsClientXJsonSerializer {
        private static readonly JsonSerializerOptions SharedSerializerOptions = CreateSerializerOptions();

        /// <summary>
        /// Serializes a value using the shared user-facing serializer options.
        /// </summary>
        /// <typeparam name="T">Type of the value being serialized.</typeparam>
        /// <param name="value">Value to serialize.</param>
        /// <returns>Formatted JSON text.</returns>
        public static string Serialize<T>(T value) {
            return JsonSerializer.Serialize(value, SharedSerializerOptions);
        }

        /// <summary>
        /// Deserializes a value using the shared user-facing serializer options.
        /// </summary>
        /// <typeparam name="T">Type of the value being deserialized.</typeparam>
        /// <param name="content">JSON content to parse.</param>
        /// <returns>Parsed value or <c>default</c> when deserialization yields no object.</returns>
        public static T? Deserialize<T>(string content) {
            if (content == null) {
                throw new ArgumentNullException(nameof(content));
            }

            return JsonSerializer.Deserialize<T>(content, SharedSerializerOptions);
        }

        /// <summary>
        /// Creates serializer options for user-facing JSON payloads.
        /// </summary>
        /// <returns>Configured serializer options.</returns>
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
