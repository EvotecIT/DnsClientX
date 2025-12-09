using System.Text.Json.Serialization;

namespace DnsClientX {
    /// <summary>
    /// Request payload for JSON resolve (POST).
    /// </summary>
    internal sealed class ResolveRequest {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        [JsonPropertyName("do")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Do { get; set; }

        [JsonPropertyName("cd")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Cd { get; set; }
    }

    /// <summary>
    /// Request payload for JSON update (POST).
    /// </summary>
    internal sealed class UpdateRequest {
        [JsonPropertyName("zone")]
        public string Zone { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Data { get; set; }

        [JsonPropertyName("ttl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Ttl { get; set; }

        [JsonPropertyName("delete")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Delete { get; set; }
    }
}
