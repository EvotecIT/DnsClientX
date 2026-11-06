using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DnsClientX {
    /// <summary>
    /// Provides shared JSON serialization helpers for user-facing payloads.
    /// </summary>
    public static class DnsClientXJsonSerializer {
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Reflection-based JSON options support arbitrary caller-owned enums.")]
#endif
        private static class ReflectionOptions {
            internal static readonly JsonSerializerOptions Shared = CreateSerializerOptions();
        }

        /// <summary>
        /// Serializes a value using the shared user-facing serializer options.
        /// </summary>
        /// <typeparam name="T">Type of the value being serialized.</typeparam>
        /// <param name="value">Value to serialize.</param>
        /// <returns>Formatted JSON text.</returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Arbitrary JSON payloads require preserved metadata. Use the overload accepting JsonTypeInfo<T> for trimmed applications.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary JSON payloads may require generated serialization code. Use the overload accepting JsonTypeInfo<T> for Native AOT.")]
#endif
        public static string Serialize<T>(T value) {
            return JsonSerializer.Serialize(value, ReflectionOptions.Shared);
        }

        /// <summary>
        /// Deserializes a value using the shared user-facing serializer options.
        /// </summary>
        /// <typeparam name="T">Type of the value being deserialized.</typeparam>
        /// <param name="content">JSON content to parse.</param>
        /// <returns>Parsed value or <c>default</c> when deserialization yields no object.</returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Arbitrary JSON payloads require preserved metadata. Use the overload accepting JsonTypeInfo<T> for trimmed applications.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary JSON payloads may require generated serialization code. Use the overload accepting JsonTypeInfo<T> for Native AOT.")]
#endif
        public static T? Deserialize<T>(string content) {
            if (content == null) {
                throw new ArgumentNullException(nameof(content));
            }

            return JsonSerializer.Deserialize<T>(content, ReflectionOptions.Shared);
        }

        /// <summary>Serializes a payload using a source-generated JSON contract.</summary>
        /// <typeparam name="T">Payload type.</typeparam>
        /// <param name="value">Payload to serialize.</param>
        /// <param name="typeInfo">Generated serialization metadata.</param>
        /// <returns>Serialized JSON text.</returns>
        public static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo) {
            if (typeInfo == null) {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            return JsonSerializer.Serialize(value, typeInfo);
        }

        /// <summary>Deserializes a payload using a source-generated JSON contract.</summary>
        /// <typeparam name="T">Payload type.</typeparam>
        /// <param name="content">JSON text to parse.</param>
        /// <param name="typeInfo">Generated serialization metadata.</param>
        /// <returns>Parsed payload or null.</returns>
        public static T? Deserialize<T>(string content, JsonTypeInfo<T> typeInfo) {
            if (content == null) {
                throw new ArgumentNullException(nameof(content));
            }
            if (typeInfo == null) {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            return JsonSerializer.Deserialize(content, typeInfo);
        }

        /// <summary>
        /// Creates serializer options for user-facing JSON payloads.
        /// </summary>
        /// <returns>Configured serializer options.</returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("The general enum converter supports arbitrary caller-owned enum types. Use a source-generated JSON context for Native AOT.")]
#endif
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
