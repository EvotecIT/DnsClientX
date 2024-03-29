using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DnsClientX {
    /// <summary>
    /// Class representing the configuration for a DNS-over-HTTPS endpoint.
    /// </summary>
    public class Configuration {
        /// <summary>
        /// The random number generator.
        /// </summary>
        private static readonly Random random = new Random();


        private List<string> hostnames = new List<string>();
        private string baseUriFormat;


        /// <summary>
        /// Gets or sets how to select the DNS server to use when multiple are available.
        /// </summary>
        /// <value>
        /// The selection strategy.
        /// </value>
        public DnsSelectionStrategy SelectionStrategy { get; set; }

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
        /// Gets or sets the port. The default value is 53 for DNS over UDP or DNS over TCP, and 853 for DNS over TLS.
        /// Only used when the request format is DNS over TLS, or DNS over UDP or TCP.
        /// In the case of DNS over HTTPS, the port is determined by the base URI.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public int Port { get; set; }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific hostname and request format.
        /// </summary>
        /// <param name="hostname">The hostname of the DNS-over-HTTPS resolver.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(string hostname, DnsRequestFormat requestFormat) {
            hostnames = new List<string> { hostname };
            RequestFormat = requestFormat;
            baseUriFormat = "https://{0}/dns-query"; ;
            BaseUri = new Uri(string.Format(baseUriFormat, hostname));

            if (requestFormat == DnsRequestFormat.DnsOverTLS) {
                Port = 853;
            } else if (requestFormat == DnsRequestFormat.DnsOverUDP || requestFormat == DnsRequestFormat.DnsOverTCP) {
                Port = 53;
            } else {
                Port = 443;
            }
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific base URI and request format.
        /// </summary>
        /// <param name="baseUri">The base URI to send DNS requests to.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(Uri baseUri, DnsRequestFormat requestFormat) {
            BaseUri = baseUri;
            RequestFormat = requestFormat;
            hostnames = new List<string> { Hostname };

            if (requestFormat == DnsRequestFormat.DnsOverTLS) {
                Port = 853;
            } else if (requestFormat == DnsRequestFormat.DnsOverUDP || requestFormat == DnsRequestFormat.DnsOverTCP) {
                Port = 53;
            } else {
                Port = 443;
            }
        }

        /// <summary>
        /// Selects the Dns server based on the selection strategy.
        /// </summary>
        public void SelectHostNameStrategy() {
            if (hostnames.Count == 1) {
                Hostname = hostnames[0];
                if (baseUriFormat != null) {
                    BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
                }
            } else if (hostnames.Count == 0) {
                // use BaseUri as is
            } else {
                // Select a hostname based on the selection strategy
                switch (SelectionStrategy) {
                    case DnsSelectionStrategy.First:
                        Hostname = hostnames[0];
                        break;
                    case DnsSelectionStrategy.Random:
                        Hostname = hostnames[random.Next(hostnames.Count)];
                        break;
                    case DnsSelectionStrategy.Failover:
                        // TODO: Implement failover strategy
                        // Try each hostname in order until one succeeds
                        foreach (var hostname in hostnames) {
                            try {
                                // Try to make a DNS request...
                                Hostname = hostname;
                                break;
                            } catch {
                                // If the request fails, try the next hostname
                            }
                        }

                        break;
                }

                BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
            }
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific DNS endpoint.
        /// </summary>
        /// <param name="endpoint">The DNS endpoint to use.</param>
        /// <param name="selectionStrategy">DNS Selection Strategy</param>
        /// <exception cref="System.ArgumentException">Thrown when an invalid endpoint is provided.</exception>
        public Configuration(DnsEndpoint endpoint, DnsSelectionStrategy selectionStrategy = DnsSelectionStrategy.First) {
            List<string> hostnames;
            SelectionStrategy = selectionStrategy;
            string baseUriFormat;
            switch (endpoint) {
                case DnsEndpoint.System:
                    // Use the system's default DNS resolver
                    hostnames = SystemInformation.GetDnsFromActiveNetworkCard();
                    RequestFormat = DnsRequestFormat.DnsOverTCP;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Cloudflare:
                    hostnames = ["1.1.1.1", "1.0.0.1"];
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareWireFormat:
                    hostnames = new List<string> { "1.1.1.1", "1.0.0.1" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareWireFormatPost:
                    hostnames = new List<string> { "1.1.1.1", "1.0.0.1" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsPOST;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareSecurity:
                    hostnames = new List<string> { "1.1.1.2", "1.0.0.2" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareFamily:
                    hostnames = new List<string> { "1.1.1.3", "1.0.0.3" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Google:
                    hostnames = new List<string> { "8.8.8.8", "8.8.4.4" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/resolve";
                    break;
                case DnsEndpoint.GoogleWireFormat:
                    hostnames = new List<string> { "8.8.8.8", "8.8.4.4" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.GoogleWireFormatPost:
                    hostnames = new List<string> { "8.8.8.8", "8.8.4.4" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsPOST;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9:
                    hostnames = new List<string> { "9.9.9.9:5053", "149.112.112.112:5053" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9ECS:
                    hostnames = new List<string> { "9.9.9.11:5053", "149.112.112.11:5053" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9Unsecure:
                    hostnames = new List<string> { "9.9.9.10:5053", "149.112.112.10:5053" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.OpenDNS:
                    hostnames = new List<string> { "208.67.222.222", "208.67.220.220" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.OpenDNSFamily:
                    hostnames = ["208.67.222.123", "208.67.220.123"];
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                default:
                    throw new ArgumentException("Invalid endpoint", nameof(endpoint));
            }
            // Select a hostname based on the selection strategy
            this.hostnames = hostnames;
            this.baseUriFormat = baseUriFormat;

            SelectHostNameStrategy();

            if (RequestFormat == DnsRequestFormat.DnsOverTLS) {
                Port = 853;
            } else if (RequestFormat == DnsRequestFormat.DnsOverUDP || RequestFormat == DnsRequestFormat.DnsOverTCP) {
                Port = 53;
            } else {
                Port = 443;
            }

            BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
        }
    }
}
