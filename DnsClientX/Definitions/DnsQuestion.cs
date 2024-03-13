using System.Text.Json.Serialization;

namespace DnsClientX {
    /// <summary>   
    /// DNS question sent by the client.
    /// </summary>
    public struct DnsQuestion {
        private string _name;

        /// <summary>
        /// The FQDN record name requested.
        /// Retains original name as set by the client.
        /// </summary>
        [JsonIgnore]
        public string OriginalName;

        /// <summary>
        /// The FQDN record name requested.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name {
            get => _name;
            set {
                OriginalName = value;
                _name = value.EndsWith(".") ? value.TrimEnd('.') : value;
            }
        }

        /// <summary>
        /// The type of DNS record requested.
        /// </summary>
        [JsonPropertyName("type")]
        public DnsRecordType Type { get; set; }
    }
}
