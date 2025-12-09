using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnsClientX {
    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(DnsResponse))]
    [JsonSerializable(typeof(ResolveRequest))]
    [JsonSerializable(typeof(UpdateRequest))]
    [JsonSerializable(typeof(DnsAnswerMinimal))]
    internal partial class DnsJsonContext : JsonSerializerContext {
    }
}
