using System.Text.Json.Serialization;

namespace DnsClientX {
    [JsonSourceGenerationOptions(WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UseStringEnumConverter = true)]
    [JsonSerializable(typeof(DnsResponse))]
    [JsonSerializable(typeof(ResolverHealthProfile))]
    [JsonSerializable(typeof(ResolverScoreSnapshot))]
    [JsonSerializable(typeof(Rfc5011StateFile))]
    internal partial class DnsClientXJsonContext : JsonSerializerContext { }
}
