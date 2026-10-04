using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Provides JSON serialization helpers used by DNS over HTTPS implementations.
    /// </summary>
    internal static class DnsJson {
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
        /// <param name="typeInfo">Source generated metadata for the payload type.</param>
        /// <returns>Serialized JSON string.</returns>
        internal static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo) =>
            JsonSerializer.Serialize(value, typeInfo);

        /// <summary>
        /// Deserialize a JSON HTTP response into a given type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize into.</typeparam>
        /// <param name="response">The HTTP response message with JSON as a body.</param>
        /// <param name="debug">Whether to print the JSON data to the console.</param>
        /// <param name="typeInfo">Source generated metadata for the target type.</param>
        /// <param name="cancellationToken">Cancels reading the bounded response body.</param>
        internal static async Task<T> Deserialize<T>(this HttpResponseMessage response, JsonTypeInfo<T> typeInfo,
            bool debug = false, CancellationToken cancellationToken = default) {
            if (!response.IsSuccessStatusCode) {
                throw new DnsClientException($"DNS JSON endpoint returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }
            if (response.Content == null)
                throw new DnsClientException("Response content is missing, can't parse as JSON.");
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value == 0)
                throw new DnsClientException("Response content is empty, can't parse as JSON.");
            try {
                byte[] bytes = await DnsHttpResponseBody.ReadBoundedAsync(
                    response.Content, DnsHttpResponseBody.MaxJsonBytes, cancellationToken).ConfigureAwait(false);
                if (debug) {
                    string json = System.Text.Encoding.UTF8.GetString(bytes);
                    Settings.Logger.WriteDebug(json);
                    return JsonSerializer.Deserialize(json, typeInfo)!;
                }
                return JsonSerializer.Deserialize(bytes, typeInfo)
                    ?? throw new DnsClientException("Failed to parse JSON response.");
            } catch (DnsClientException) {
                throw;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (JsonException jsonEx) {
                throw new DnsClientException($"Failed to parse JSON due to a JsonException: {jsonEx.Message}", jsonEx);
            } catch (IOException ioEx) {
                throw new DnsClientException($"Failed to read the response stream due to an IOException: {ioEx.Message}", ioEx);
            } catch (Exception ex) {
                throw new DnsClientException($"Unexpected exception while parsing JSON: {ex.GetType().Name} => {ex.Message}", ex);
            }
        }

        internal static Task<DnsResponse> DeserializeResponse(this HttpResponseMessage response,
            bool debug = false, CancellationToken cancellationToken = default) =>
            response.Deserialize(DnsJsonContext.Default.DnsResponse, debug, cancellationToken);
    }
}
