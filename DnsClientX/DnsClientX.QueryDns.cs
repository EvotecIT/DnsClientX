using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server.
        /// This method allows you to specify the DNS endpoint from a predefined list of endpoints.
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsEndpoint">The DNS endpoint to use for the query. Defaults to System.</param>
        /// <param name="dnsSelectionStrategy">The DNS selection strategy. Defaults to First</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        public static async Task<DnsResponse> QueryDns(string name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            if (dnsEndpoint == DnsEndpoint.RootServer) {
                using var client = new ClientX();
                if (cancellationToken.IsCancellationRequested) {
                    return await Task.FromCanceled<DnsResponse>(cancellationToken).ConfigureAwait(false);
                }
                return await client.ResolveFromRoot(name, recordType, cancellationToken).ConfigureAwait(false);
            } else {
                using var client = new ClientX(endpoint: dnsEndpoint, dnsSelectionStrategy);
                client.EndpointConfiguration.TimeOut = timeOutMilliseconds;
                if (cancellationToken.IsCancellationRequested) {
                    return await Task.FromCanceled<DnsResponse>(cancellationToken).ConfigureAwait(false);
                }
                var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                return data;
            }
        }

        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server. Synchronous version.
        /// This method allows you to specify the DNS endpoint from a predefined list of endpoints.
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsEndpoint">The DNS endpoint to use for the query. Defaults to System.</param>
        /// <param name="dnsSelectionStrategy">The DNS selection strategy. Defaults to First</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>The DNS response.</returns>
        public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            return QueryDns(name, recordType, dnsEndpoint, dnsSelectionStrategy, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }

        /// <summary>
        /// Sends a DNS query for multiple names for a specific record type to a DNS server.
        /// This method allows you to specify the DNS endpoint from a predefined list of endpoints.
        /// </summary>
        /// <param name="name">The domain names to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsEndpoint">The DNS endpoint to use for the query. Defaults to System.</param>
        /// <param name="dnsSelectionStrategy">The DNS selection strategy. Defaults to First</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        public static async Task<DnsResponse[]> QueryDns(string[] name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            if (dnsEndpoint == DnsEndpoint.RootServer) {
                var tasks = name.Select(n => {
                    using var client = new ClientX();
                    if (cancellationToken.IsCancellationRequested) {
                        return Task.FromCanceled<DnsResponse>(cancellationToken);
                    }
                    return client.ResolveFromRoot(n, recordType, cancellationToken);
                });
                return await Task.WhenAll(tasks).ConfigureAwait(false);
            } else {
                using var client = new ClientX(endpoint: dnsEndpoint, dnsSelectionStrategy) {
                    EndpointConfiguration = {
                        TimeOut = timeOutMilliseconds
                    }
                };
                if (cancellationToken.IsCancellationRequested) {
                    return await Task.FromCanceled<DnsResponse[]>(cancellationToken).ConfigureAwait(false);
                }
                var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                return data;
            }
        }

        /// <summary>
        /// Sends a DNS query for multiple names for a specific record type to a DNS server. Synchronous version.
        /// This method allows you to specify the DNS endpoint from a predefined list of endpoints.
        /// </summary>
        /// <param name="name">The domain names to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsEndpoint">The DNS endpoint to use for the query. Defaults to System.</param>
        /// <param name="dnsSelectionStrategy">The DNS selection strategy. Defaults to First</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>The DNS response.</returns>
        public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            return QueryDns(name, recordType, dnsEndpoint, dnsSelectionStrategy, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }

        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server.
        /// This method allows you to specify the DNS endpoint by providing a full URI and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsUri">The full URI of the DNS server to query.</param>
        /// <param name="requestFormat">The format of the DNS request.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        public static async Task<DnsResponse> QueryDns(string name, DnsRecordType recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            using var client = new ClientX(dnsUri, requestFormat) {
                EndpointConfiguration = {
                    TimeOut = timeOutMilliseconds
                }
            };
            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<DnsResponse>(cancellationToken).ConfigureAwait(false);
            }
            var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server. Synchronous version.
        /// This method allows you to specify the DNS endpoint by providing a full URI and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="dnsUri">The full URI of the DNS server to query.</param>
        /// <param name="requestFormat">The format of the DNS request.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>The DNS response.</returns>
        public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            return QueryDns(name, recordType, dnsUri, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }

        /// <summary>
        /// Sends a DNS query for multiple domain names and multiple record types to a DNS server using a full URI and request format.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="dnsUri">The DNS URI.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns></returns>
        public static async Task<DnsResponse[]> QueryDns(string[] name, DnsRecordType[] recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            using var client = new ClientX(dnsUri, requestFormat) {
                EndpointConfiguration = {
                    TimeOut = timeOutMilliseconds
                }
            };
            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<DnsResponse[]>(cancellationToken).ConfigureAwait(false);
            }
            var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Sends a DNS query for multiple domain names and multiple record types to a DNS server using a full URI and request format. Synchronous version.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="dnsUri">The DNS URI.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns></returns>
        public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            return QueryDns(name, recordType, dnsUri, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }

        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server.
        /// This method allows you to specify the DNS endpoint by providing a hostname and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="hostName">The hostname of the DNS server to query.</param>
        /// <param name="requestFormat">The format of the DNS request.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the DNS response.</returns>
        public static async Task<DnsResponse> QueryDns(string name, DnsRecordType recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            using var client = new ClientX(hostName, requestFormat) {
                EndpointConfiguration = {
                    TimeOut = timeOutMilliseconds
                }
            };
            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<DnsResponse>(cancellationToken).ConfigureAwait(false);
            }
            var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Sends a DNS query for a specific record type to a DNS server. Synchronous version.
        /// This method allows you to specify the DNS endpoint by providing a hostname and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">The domain name to query.</param>
        /// <param name="recordType">The type of DNS record to query.</param>
        /// <param name="hostName">The hostname of the DNS server to query.</param>
        /// <param name="requestFormat">The format of the DNS request.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns>The DNS response.</returns>
        public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            return QueryDns(name, recordType, hostName, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }

        /// <summary>
        /// Sends a DNS query for multiple domain names and multiple record types to a DNS server using HostName and RequestFormat.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns></returns>
        public static async Task<DnsResponse[]> QueryDns(string[] name, DnsRecordType[] recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            using var client = new ClientX(hostName, requestFormat) {
                EndpointConfiguration = {
                    TimeOut = timeOutMilliseconds
                }
            };
            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<DnsResponse[]>(cancellationToken).ConfigureAwait(false);
            }
            var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Sends a DNS query for multiple domain names and multiple record types to a DNS server using HostName and RequestFormat.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns></returns>
        public static async Task<DnsResponse[]> QueryDns(string[] name, DnsRecordType recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            using var client = new ClientX(hostName, requestFormat) {
                EndpointConfiguration = {
                    TimeOut = timeOutMilliseconds
                }
            };
            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<DnsResponse[]>(cancellationToken).ConfigureAwait(false);
            }
            var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Sends a DNS query for multiple domain names and multiple record types to a DNS server using HostName and RequestFormat. Synchronous version.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="requestFormat">The request format.</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns></returns>
        public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            return QueryDns(name, recordType, hostName, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }

        /// <summary>
        /// Sends a DNS query for multiple domain names and single record types to a DNS server using HostName and RequestFormat.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="recordType">Type of the record.</param>
        /// <param name="dnsEndpoint">The DNS endpoint. Default endpoint is System</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns></returns>
        public static async Task<DnsResponse[]> QueryDns(string[] name, DnsRecordType[] recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            using var client = new ClientX(endpoint: dnsEndpoint) {
                EndpointConfiguration = {
                    TimeOut = timeOutMilliseconds
                }
            };
            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<DnsResponse[]>(cancellationToken).ConfigureAwait(false);
            }
            var data = await client.Resolve(name, recordType, retryOnTransient: retryOnTransient, maxRetries: maxRetries, retryDelayMs: retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Sends a DNS query to multiple domains and multiple record types to a DNS server. Synchronous version.
        /// This method allows you to specify the DNS endpoint by providing a hostname and request format (JSON, WireFormatGet).
        /// </summary>
        /// <param name="name">Multiple domain names to check for given type</param>
        /// <param name="recordType">Multiple types to check for given name.</param>
        /// <param name="dnsEndpoint">The DNS endpoint. Default endpoint is System</param>
        /// <param name="timeOutMilliseconds">The timeout for DNS requests in milliseconds.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="retryDelayMs">Retry delay in milliseconds</param>
        /// <returns></returns>
        public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, int timeOutMilliseconds = Configuration.DefaultTimeout, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default) {
            return QueryDns(name, recordType, dnsEndpoint, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync();
        }
    }
}
