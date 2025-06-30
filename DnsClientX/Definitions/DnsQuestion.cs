using System;
using System.Text.Json.Serialization;

namespace DnsClientX {
    /// <summary>   
    /// DNS question sent by the client.
    /// </summary>
    public struct DnsQuestion {
        private string _name;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsQuestion"/> struct.
        /// </summary>
        public DnsQuestion() {
            _name = string.Empty;
            OriginalName = string.Empty;
            Type = DnsRecordType.A;
            HostName = string.Empty;
            BaseUri = null!;
            RequestFormat = DnsRequestFormat.DnsOverHttps;
            Port = 0;
        }

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
                if (string.IsNullOrEmpty(value)) {
                    _name = value;
                } else {
                    _name = value.EndsWith(".") ? value.TrimEnd('.') : value;
                }
            }
        }

        /// <summary>
        /// The type of DNS record requested.
        /// </summary>
        [JsonPropertyName("type")]
        public DnsRecordType Type { get; set; }

        /// <summary>
        /// HostName or IP address of the DNS server which received the query.
        /// </summary>
        [JsonIgnore]
        public string HostName { get; set; }

        /// <summary>
        /// Base URI of the DNS server which received the query.
        /// </summary>
        [JsonIgnore]
        public Uri BaseUri { get; set; }

        /// <summary>
        /// Request format of the DNS server which received the query.
        /// </summary>
        [JsonIgnore]
        public DnsRequestFormat RequestFormat { get; set; }

        /// <summary>
        /// Port of the DNS server which received the query.
        /// </summary>
        [JsonIgnore]
        public int Port { get; set; }
    }
}
