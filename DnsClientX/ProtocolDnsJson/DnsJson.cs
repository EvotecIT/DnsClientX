using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Provides JSON serialization helpers used by DNS over HTTPS implementations.
    /// </summary>
    internal static class DnsJson {
        internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        /// <summary>
        /// Encode URL
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        internal static string UrlEncode(this string value) => WebUtility.UrlEncode(value);

        /// <summary>
        /// Serializes the specified value using pre-configured JSON options.
        /// </summary>
        /// <typeparam name="T">Type of the value to serialize.</typeparam>
        /// <param name="value">Value to serialize.</param>
        /// <returns>Serialized JSON string.</returns>
        internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

        /// <summary>
        /// Deserialize a JSON HTTP response into a given type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize into.</typeparam>
        /// <param name="response">The HTTP response message with JSON as a body.</param>
        /// <param name="debug">Whether to print the JSON data to the console.</param>
        internal static async Task<T> Deserialize<T>(this HttpResponseMessage response, bool debug = false) {
            if (response.Content.Headers.ContentLength.GetValueOrDefault() == 0)
                throw new DnsClientException("Response content is empty, can't parse as JSON.");
            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            try {
                if (debug) {
                    // Read the stream as a string
                    using StreamReader reader = new StreamReader(stream);
                    string json = await reader.ReadToEndAsync().ConfigureAwait(false);
                    // Write the JSON data using logger
                    Settings.Logger.WriteDebug(json);
                    // Deserialize the JSON data
                    return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
                }
                return JsonSerializer.Deserialize<T>(stream, JsonOptions)!;
            } catch (JsonException jsonEx) {
                throw new DnsClientException($"Failed to parse JSON due to a JsonException: {jsonEx.Message}");
            } catch (IOException ioEx) {
                throw new DnsClientException($"Failed to read the response stream due to an IOException: {ioEx.Message}");
            } catch (Exception ex) {
                throw new DnsClientException($"Unexpected exception while parsing JSON: {ex.GetType().Name} => {ex.Message}");
            }
        }
    }
}
