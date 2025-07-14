using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace DnsClientX {
    /// <summary>
    /// Class representing the configuration for a DNS-over-HTTPS endpoint.
    /// </summary>
    /// <remarks>
    /// Instances of this class are used by <see cref="ClientXBuilder"/> to describe the target server and connection settings.
    /// </remarks>
    public class Configuration {
        /// <summary>
        /// Random generator used for hostname selection on frameworks lacking
        /// <see cref="Random.Shared"/>.
        /// </summary>
#if NET6_0_OR_GREATER
        // Random.Shared provides a threadsafe instance starting with .NET 6
#else
        private static readonly object _randLock = new();
        private static readonly Random _rand = new();
#endif


        private readonly List<string> hostnames = new();
        private readonly Dictionary<string, DateTime> unavailable = new();
        private string baseUriFormat;
        private int hostnameIndex;

        /// <summary>
        /// Gets or sets the cooldown period for hosts marked as unavailable.
        /// </summary>
        public TimeSpan UnavailableCooldown { get; set; } = TimeSpan.FromMinutes(1);


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
        public string? Hostname { get; set; }

        /// <summary>
        /// Gets the base URI to send DNS requests to.
        /// </summary>
        public Uri? BaseUri { get; set; }

        /// <summary>
        /// The preferred HTTP request version to use by default.
        /// </summary>
        public static readonly Version DefaultHttpVersion = new(2, 0);

        /// <summary>
        /// Default timeout for DNS queries in milliseconds.
        /// </summary>
        public const int DefaultTimeout = 1000;

        /// <summary>
        /// Default connection limit per server for HTTP requests.
        /// </summary>
        public const int DefaultMaxConnectionsPerServer = 10;

        /// <summary>
        /// Gets or sets the HTTP version used when communicating with the DNS provider.
        /// </summary>
        public Version HttpVersion { get; set; } = DefaultHttpVersion;

        /// <summary>
        /// Gets or sets the User-Agent header value to send along with DNS requests.
        /// </summary>
        public string UserAgent { get; set; } = "DnsClientX";

        /// <summary>
        /// Time-out for DNS Query in milliseconds. Valid only for UDP (for now).
        /// </summary>
        public int TimeOut = DefaultTimeout;

        /// <summary>
        /// Validates DS and DNSKEY records against a builtin root key set.
        /// </summary>
        public bool ValidateRootDnsSec { get; set; }

        /// <summary>
        /// Sets the CD (Checking Disabled) flag on queries.
        /// </summary>
        public bool CheckingDisabled { get; set; }

        /// <summary>
        /// Determines whether to fall back to TCP when a UDP response is truncated.
        /// </summary>
        public bool UseTcpFallback { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of HTTP connections allowed per server.
        /// </summary>
        public int MaxConnectionsPerServer { get; set; } = DefaultMaxConnectionsPerServer;

        /// <summary>
        /// Optional key used to sign outgoing DNS messages.
        /// </summary>
        public AsymmetricAlgorithm? SigningKey { get; set; }

        /// <summary>
        /// Gets or sets the UDP buffer size used when sending EDNS queries.
        /// </summary>
        /// <value>The size of the UDP buffer.</value>
        public int UdpBufferSize { get; set; } = 4096;

        /// <summary>
        /// Gets or sets a value indicating whether EDNS should be enabled for queries.
        /// </summary>
        /// <value><c>true</c> to include the OPT record; otherwise, <c>false</c>.</value>
        public bool EnableEdns { get; set; }

        /// <summary>
        /// Gets or sets the EDNS Client Subnet (ECS) to include in DNS queries.
        /// Specify as CIDR notation, for example <c>192.0.2.0/24</c>.
        /// When configured, EDNS will be automatically enabled.
        /// </summary>
        public string? Subnet { get; set; }

        /// <summary>
        /// Gets or sets additional EDNS options. When configured, these values
        /// override <see cref="EnableEdns"/>, <see cref="UdpBufferSize"/> and
        /// <see cref="Subnet"/>.
        /// </summary>
        public EdnsOptions? EdnsOptions { get; set; }

        private DnsRequestFormat requestFormat;

        /// <summary>
        /// Gets or sets the format of the DNS requests.
        /// Updating the format adjusts <see cref="Port"/> according
        /// to the same rules used by the constructors.
        /// </summary>
        public DnsRequestFormat RequestFormat {
            get => requestFormat;
            set {
                requestFormat = value;
                if (value == DnsRequestFormat.DnsOverTLS || value == DnsRequestFormat.DnsOverQuic) {
                    Port = 853;
                } else if (value == DnsRequestFormat.DnsOverUDP || value == DnsRequestFormat.DnsOverTCP || value == DnsRequestFormat.DnsCryptRelay) {
                    Port = 53;
                } else if (value == DnsRequestFormat.Multicast) {
                    Port = 5353;
                } else {
                    Port = 443;
                }
            }
        }

        /// <summary>
        /// Gets or sets the port. The default value is 53 for DNS over UDP or DNS over TCP, and 853 for DNS over TLS.
        /// Only used when the request format is DNS over TLS, or DNS over UDP or TCP.
        /// In the case of DNS over HTTPS, the port is determined by the base URI.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public int Port { get; set; }

        private void ApplyPortToBaseUri() {
            if ((RequestFormat == DnsRequestFormat.DnsOverTLS || RequestFormat == DnsRequestFormat.DnsOverQuic) && BaseUri != null && BaseUri.Port != Port) {
                var builder = new UriBuilder(BaseUri) { Port = Port };
                BaseUri = builder.Uri;
            }
        }

        private bool IsUnavailable(string host) {
            lock (unavailable) {
                if (unavailable.TryGetValue(host, out var until)) {
                    if (until > DateTime.UtcNow) {
                        return true;
                    }

                    unavailable.Remove(host);
                }

                return false;
            }
        }

        /// <summary>
        /// Marks the specified host as unavailable for the configured cooldown period.
        /// </summary>
        public void MarkHostnameUnavailable(string? host) {
            if (string.IsNullOrEmpty(host)) return;

            lock (unavailable) {
                unavailable[host] = DateTime.UtcNow.Add(UnavailableCooldown);
            }
        }

        /// <summary>
        /// Marks the current hostname as unavailable.
        /// </summary>
        public void MarkCurrentHostnameUnavailable() => MarkHostnameUnavailable(Hostname);

        internal void AdvanceToNextHostname() {
            if (hostnames.Count <= 1) {
                return;
            }

            hostnameIndex++;
            if (hostnameIndex >= hostnames.Count) {
                hostnameIndex = 0;
            }
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific hostname and request format.
        /// </summary>
        /// <param name="hostname">The hostname of the DNS-over-HTTPS resolver.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(string hostname, DnsRequestFormat requestFormat) {
            if (string.IsNullOrWhiteSpace(hostname)) throw new ArgumentException("Hostname is null or whitespace.", nameof(hostname));
            hostnames = new List<string> { hostname };
            RequestFormat = requestFormat;
            if (requestFormat != DnsRequestFormat.DnsOverUDP && requestFormat != DnsRequestFormat.DnsOverTCP && requestFormat != DnsRequestFormat.DnsCryptRelay) {
                if (IPAddress.TryParse(hostname, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6) {
                    baseUriFormat = "https://[{0}]/dns-query";
                } else {
                    baseUriFormat = "https://{0}/dns-query";
                }
                BaseUri = new Uri(string.Format(baseUriFormat, hostname));
            } else {
                baseUriFormat = null;
                BaseUri = null;
            }
            hostnameIndex = 0;

            if (requestFormat == DnsRequestFormat.DnsOverTLS || requestFormat == DnsRequestFormat.DnsOverQuic) {
                Port = 853;
            } else if (requestFormat == DnsRequestFormat.DnsOverUDP || requestFormat == DnsRequestFormat.DnsOverTCP) {
                Port = 53;
            } else if (requestFormat == DnsRequestFormat.Multicast) {
                Port = 5353;
            } else {
                Port = 443;
            }
            ApplyPortToBaseUri();
        }

        /// <summary>
        /// Initializes a new instance of the EndpointConfiguration class with a specific base URI and request format.
        /// </summary>
        /// <param name="baseUri">The base URI to send DNS requests to.</param>
        /// <param name="requestFormat">The format of the DNS requests.</param>
        public Configuration(Uri baseUri, DnsRequestFormat requestFormat) {
            BaseUri = baseUri;
            RequestFormat = requestFormat;
            Hostname = baseUri.Host;
            hostnames = new List<string> { baseUri.Host };
            hostnameIndex = 0;

            if (requestFormat == DnsRequestFormat.DnsOverTLS || requestFormat == DnsRequestFormat.DnsOverQuic) {
                Port = 853;
            } else if (requestFormat == DnsRequestFormat.DnsOverUDP || requestFormat == DnsRequestFormat.DnsOverTCP) {
                Port = 53;
            } else {
                Port = 443;
            }
            ApplyPortToBaseUri();
        }

        /// <summary>
        /// Selects the Dns server based on the selection strategy.
        /// </summary>
        public void SelectHostNameStrategy() {
            if (hostnames.Count == 1) {
                Hostname = hostnames[0];
                if (baseUriFormat != null) {
                    BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
                    ApplyPortToBaseUri();
                }
            } else if (hostnames.Count == 0) {
                // use BaseUri as is
            } else {
                // Select a hostname based on the selection strategy
                switch (SelectionStrategy) {
                    case DnsSelectionStrategy.First:
                        hostnameIndex = 0;
                        break;
                    case DnsSelectionStrategy.Random:
#if NET6_0_OR_GREATER
                        hostnameIndex = Random.Shared.Next(hostnames.Count);
#else
                        lock (_randLock) {
                            hostnameIndex = _rand.Next(hostnames.Count);
                        }
#endif
                        break;
                    case DnsSelectionStrategy.Failover:
                        if (hostnameIndex >= hostnames.Count) {
                            hostnameIndex = 0;
                        }
                        break;
                }

                var startIndex = hostnameIndex;
                for (var i = 0; i < hostnames.Count; i++) {
                    var candidate = hostnames[hostnameIndex];
                    if (!IsUnavailable(candidate)) {
                        Hostname = candidate;
                        break;
                    }

                    hostnameIndex++;
                    if (hostnameIndex >= hostnames.Count) {
                        hostnameIndex = 0;
                    }

                    if (hostnameIndex == startIndex) {
                        Hostname = candidate;
                        break;
                    }
                }

                BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
                ApplyPortToBaseUri();
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
                    RequestFormat = DnsRequestFormat.DnsOverUDP;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.SystemTcp:
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
                    RequestFormat = DnsRequestFormat.DnsOverHttpsWirePost;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareJsonPost:
                    hostnames = new List<string> { "1.1.1.1", "1.0.0.1" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSONPOST;
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
                case DnsEndpoint.CloudflareQuic:
                    hostnames = new List<string> { "1.1.1.1", "1.0.0.1" };
                    RequestFormat = DnsRequestFormat.DnsOverQuic;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.CloudflareOdoh:
                    hostnames = ["odoh.cloudflare-dns.com"];
                    RequestFormat = DnsRequestFormat.ObliviousDnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Custom:
                    hostnames = [];
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = null;
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
                    RequestFormat = DnsRequestFormat.DnsOverHttpsWirePost;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.GoogleJsonPost:
                    hostnames = new List<string> { "8.8.8.8", "8.8.4.4" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSONPOST;
                    baseUriFormat = "https://{0}/resolve";
                    break;
                case DnsEndpoint.GoogleQuic:
                    hostnames = new List<string> { "8.8.8.8", "8.8.4.4" };
                    RequestFormat = DnsRequestFormat.DnsOverQuic;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.AdGuard:
                    hostnames = new List<string> { "dns.adguard.com" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.AdGuardFamily:
                    hostnames = new List<string> { "dns-family.adguard.com" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.AdGuardNonFiltering:
                    hostnames = new List<string> { "dns-unfiltered.adguard.com" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.NextDNS:
                    hostnames = new List<string> { "dns.nextdns.io" };
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9:
                    hostnames = new List<string> { "dns.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9ECS:
                    hostnames = new List<string> { "dns11.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.Quad9Unsecure:
                    hostnames = new List<string> { "dns10.quad9.net" };
                    RequestFormat = DnsRequestFormat.DnsOverHttps;
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
                case DnsEndpoint.DnsCryptCloudflare:
                    hostnames = new List<string> { "1.1.1.1", "1.0.0.1" };
                    RequestFormat = DnsRequestFormat.DnsCrypt;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.DnsCryptQuad9:
                    hostnames = new List<string> { "9.9.9.9", "149.112.112.9" };
                    RequestFormat = DnsRequestFormat.DnsCrypt;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                case DnsEndpoint.DnsCryptRelay:
                    hostnames = new List<string> { "94.198.41.235", "37.120.142.115" };
                    RequestFormat = DnsRequestFormat.DnsCryptRelay;
                    baseUriFormat = "https://{0}/dns-query";
                    break;
                default:
                    throw new ArgumentException("Invalid endpoint", nameof(endpoint));
            }

            if (endpoint == DnsEndpoint.Custom && hostnames.Count == 0) {
                throw new ArgumentException("At least one hostname must be specified for a custom endpoint.", nameof(endpoint));
            }
            // Select a hostname based on the selection strategy
            this.hostnames = hostnames;
            this.baseUriFormat = baseUriFormat;
            hostnameIndex = 0;

            SelectHostNameStrategy();

            if (RequestFormat == DnsRequestFormat.DnsOverTLS ||
                RequestFormat == DnsRequestFormat.DnsOverQuic) {
                Port = 853;
            } else if (RequestFormat == DnsRequestFormat.DnsOverUDP ||
                       RequestFormat == DnsRequestFormat.DnsOverTCP ||
                       RequestFormat == DnsRequestFormat.DnsCryptRelay) {
                Port = 53;
            } else if (RequestFormat == DnsRequestFormat.Multicast) {
                Port = 5353;
            } else {
                Port = 443;
            }

            if (baseUriFormat != null && !string.IsNullOrEmpty(Hostname)) {
                BaseUri = new Uri(string.Format(baseUriFormat, Hostname));
                ApplyPortToBaseUri();
            }
        }
    }
}
