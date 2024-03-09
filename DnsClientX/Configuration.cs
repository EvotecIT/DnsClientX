using System;

namespace DnsClientX {
    /// <summary>
    /// Class representing the configuration for a DNS-over-HTTPS endpoint.
    /// </summary>
    public class Configuration {
        /// <summary>
        /// Gets the hostname of the DNS-over-HTTPS resolver.
        /// </summary>
        public string Hostname { get; private set; }

        /// <summary>
        /// Gets the base URI to send DNS requests to. 
        /// </summary>
        public Uri BaseUri { get; private set; }

        /// <summary>
        /// The preferred HTTP request version to use.
        /// </summary>
        public static readonly Version HttpVersion = new(2, 0);

        /// <summary>
        /// The User-Agent header value to send along with DNS requests.
        /// </summary>
        public string UserAgent = "DnsClientX";

        /// <summary>
        /// Gets or sets the format of the DNS requests.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; set; }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific hostname and request format.
        /// </summary>
        /// <param name="hostname">The hostname of the DNS-over-HTTPS resolver.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(string hostname, DnsRequestFormat requestFormat) {
            Hostname = hostname;
            RequestFormat = requestFormat;
            BaseUri = new Uri($"https://{Hostname}/dns-query");
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific base URI and request format.
        /// </summary>
        /// <param name="baseUri">The base URI to send DNS requests to.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(Uri baseUri, DnsRequestFormat requestFormat) {
            BaseUri = baseUri;
            RequestFormat = requestFormat;
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific DNS endpoint.
        /// </summary>
        /// <param name="endpoint">The DNS endpoint to use.</param>
        /// <exception cref="System.ArgumentException">Thrown when an invalid endpoint is provided.</exception>
        public Configuration(DnsEndpoint endpoint) {
            switch (endpoint) {
                case DnsEndpoint.Cloudflare:
                    Hostname = "1.1.1.1";
                    RequestFormat = DnsRequestFormat.JSON;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                case DnsEndpoint.CloudflareWireFormat:
                    Hostname = "cloudflare-dns.com";
                    RequestFormat = DnsRequestFormat.WireFormatGet;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                //case DnsEndpoint.CloudflareWireFormatPost:
                //    Hostname = "cloudflare-dns.com";
                //    RequestFormat = DnsRequestFormat.WireFormatPost;
                //    BaseUri = new Uri($"https://{Hostname}/dns-query");
                //    break;
                case DnsEndpoint.CloudflareSecurity:
                    Hostname = "security.cloudflare-dns.com";
                    RequestFormat = DnsRequestFormat.JSON;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                case DnsEndpoint.CloudflareFamily:
                    Hostname = "family.cloudflare-dns.com";
                    RequestFormat = DnsRequestFormat.JSON;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                case DnsEndpoint.Google:
                    Hostname = "8.8.8.8";
                    RequestFormat = DnsRequestFormat.JSON;
                    BaseUri = new Uri($"https://{Hostname}/resolve");
                    break;
                case DnsEndpoint.Quad9:
                    Hostname = "9.9.9.9:5053";
                    RequestFormat = DnsRequestFormat.JSON;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                case DnsEndpoint.Quad9ECS:
                    Hostname = "9.9.9.11:5053";
                    RequestFormat = DnsRequestFormat.JSON;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                case DnsEndpoint.Quad9Unsecure:
                    Hostname = "9.9.9.10:5053";
                    RequestFormat = DnsRequestFormat.JSON;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                case DnsEndpoint.OpenDNS:
                    Hostname = "208.67.222.222";
                    Hostname = "doh.opendns.com";
                    RequestFormat = DnsRequestFormat.WireFormatGet;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                case DnsEndpoint.OpenDNSFamily:
                    Hostname = "208.67.222.123";
                    RequestFormat = DnsRequestFormat.WireFormatGet;
                    BaseUri = new Uri($"https://{Hostname}/dns-query");
                    break;
                default:
                    throw new ArgumentException("Invalid endpoint", nameof(endpoint));
            }
        }
    }
}
