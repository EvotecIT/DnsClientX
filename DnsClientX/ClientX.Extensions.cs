namespace DnsClientX;

using System;
using System.Text.RegularExpressions;
using System.Threading;

public static class ClientXExtensions
{
    /// <summary>
    /// Resolves a domain name using DNS over HTTPS. This method provides full control over the output.
    /// </summary>
    /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
    /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
    /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
    /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>The DNS response.</returns>
    /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
    public static DnsResponse ResolveSync(this ClientX client, string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100)
        => client.Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves multiple DNS resource types for a domain name in parallel using DNS over HTTPS.
    /// </summary>
    /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
    /// <param name="types">The array of DNS resource record types to resolve. By default, this is the <c>A</c> record.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
    /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
    /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>An array of DNS responses.</returns>
    /// <exception cref="DnsClientException">Thrown when an invalid RequestFormat is provided.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the provided name is null or empty.</exception>
    public static DnsResponse[] ResolveSync(this ClientX client, string name, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.Resolve(name, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves multiple domain names for multiple DNS record types in parallel using DNS over HTTPS. Synchronous version.
    /// </summary>
    /// <param name="names">The array of domain names to resolve.</param>
    /// <param name="types">The array of DNS resource record types to resolve.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
    /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
    /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>An array of DNS responses.</returns>
    public static DnsResponse[] ResolveSync(this ClientX client, string[] names, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.Resolve(names, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves multiple domain names for single DNS record type in parallel using DNS over HTTPS. Synchronous version.
    /// </summary>
    /// <param name="names">The names.</param>
    /// <param name="type">The type.</param>
    /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
    /// <param name="validateDnsSec">if set to <c>true</c> [validate DNS sec].</param>
    /// <param name="returnAllTypes">Whether to return all DNS record types in the response as returned by provider. When set to <c>true</c>, the <see cref="DnsResponse.Answers"/> array will contain all types.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>An array of DNS responses.</returns>
    public static DnsResponse[] ResolveSync(this ClientX client, string[] names, DnsRecordType type, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.Resolve(names, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves a domain name using DNS over HTTPS and returns the first answer of the provided type.
    /// </summary>
    /// <param name="name">The domain name to resolve.</param>
    /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
    /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>The first DNS answer of the provided type, or null if no such answer exists.</returns>
    public static DnsAnswer? ResolveFirstSync(this ClientX client, string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100)
        => client.ResolveFirst(name, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves a domain name using DNS over HTTPS and returns all answers of the provided type.
    /// </summary>
    /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
    /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
    /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of all DNS answers of the provided type.</returns>
    public static DnsAnswer[] ResolveAllSync(this ClientX client, string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.ResolveAll(name, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves a domain name using DNS over HTTPS and returns all answers of the provided type.
    /// This helper method is useful when you need all answers of a specific type.
    /// </summary>
    /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
    /// <param name="filter">Filter out results based on string. It can be helpful to filter out records such as SPF1 in TXT</param>
    /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
    /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of all DNS answers of the provided type.</returns>
    public static DnsAnswer[] ResolveAllSync(this ClientX client, string name, string filter, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.ResolveAll(name, filter, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Resolves a domain name using DNS over HTTPS and returns all answers of the provided type.
    /// This helper method is useful when you need all answers of a specific type.
    /// </summary>
    /// <param name="name">The fully qualified domain name (FQDN) to resolve. Example: <c>foo.bar.example.com</c></param>
    /// <param name="regexPattern">Filter out results based on Regex Pattern. It can be helpful to filter out records such as SPF1 in TXT</param>
    /// <param name="type">The DNS resource type to resolve. By default, this is the <c>A</c> record.</param>
    /// <param name="requestDnsSec">Whether to request DNSSEC data in the response. When requested, it will be accessible under the <see cref="DnsAnswer"/> array.</param>
    /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
    /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelayMs">The delay between retries in milliseconds.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of all DNS answers of the provided type.</returns>
    public static DnsAnswer[] ResolveAllSync(this ClientX client, string name, Regex regexPattern, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.ResolveAll(name, regexPattern, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Sends a DNS query for a specific record type to a DNS server. Synchronous version.
    /// This method allows you to specify the DNS endpoint from a predefined list of endpoints.
    /// </summary>
    /// <param name="name">The domain name to query.</param>
    /// <param name="recordType">The type of DNS record to query.</param>
    /// <param name="dnsEndpoint">The DNS endpoint to use for the query. Defaults to Cloudflare.</param>
    /// <param name="dnsSelectionStrategy">The DNS selection strategy. Defaults to First</param>
    /// <param name="timeOutMilliseconds"></param>
    /// <param name="retryOnTransient">Whether to retry on transient errors</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelayMs">Retry delay in milliseconds</param>
    /// <returns>The DNS response.</returns>
    public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsEndpoint, dnsSelectionStrategy, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Sends a DNS query for multiple names for a specific record type to a DNS server. Synchronous version.
    /// This method allows you to specify the DNS endpoint from a predefined list of endpoints.
    /// </summary>
    /// <param name="name">The domain names to query.</param>
    /// <param name="recordType">The type of DNS record to query.</param>
    /// <param name="dnsEndpoint">The DNS endpoint to use for the query. Defaults to Cloudflare.</param>
    /// <param name="dnsSelectionStrategy">The DNS selection strategy. Defaults to First</param>
    /// <param name="timeOutMilliseconds"></param>
    /// <param name="retryOnTransient">Whether to retry on transient errors</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelayMs">Retry delay in milliseconds</param>
    /// <returns>The DNS response.</returns>
    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsEndpoint, dnsSelectionStrategy, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Sends a DNS query for a specific record type to a DNS server. Synchronous version.
    /// This method allows you to specify the DNS endpoint by providing a full URI and request format (JSON, WireFormatGet).
    /// </summary>
    /// <param name="name">The domain name to query.</param>
    /// <param name="recordType">The type of DNS record to query.</param>
    /// <param name="dnsUri">The full URI of the DNS server to query.</param>
    /// <param name="requestFormat">The format of the DNS request.</param>
    /// <param name="timeOutMilliseconds"></param>
    /// <param name="retryOnTransient">Whether to retry on transient errors</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelayMs">Retry delay in milliseconds</param>
    /// <returns>The DNS response.</returns>
    public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsUri, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Sends a DNS query for multiple domain names and multiple record types to a DNS server using a full URI and request format. Synchronous version.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="recordType">Type of the record.</param>
    /// <param name="dnsUri">The DNS URI.</param>
    /// <param name="requestFormat">The request format.</param>
    /// <param name="timeOutMilliseconds"></param>
    /// <param name="retryOnTransient">Whether to retry on transient errors</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelayMs">Retry delay in milliseconds</param>
    /// <returns></returns>
    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => ClientX.QueryDns(name, recordType, dnsUri, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    /// <summary>
    /// Sends a DNS query for a specific record type to a DNS server. Synchronous version.
    /// This method allows you to specify the DNS endpoint by providing a hostname and request format (JSON, WireFormatGet).
    /// </summary>
    /// <param name="name">The domain name to query.</param>
    /// <param name="recordType">The type of DNS record to query.</param>
    /// <param name="hostName">The hostname of the DNS server to query.</param>
    /// <param name="requestFormat">The format of the DNS request.</param>
    /// <param name="timeOutMilliseconds"></param>
    /// <param name="retryOnTransient">Whether to retry on transient errors</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelayMs">Retry delay in milliseconds</param>
    /// <returns>The DNS response.</returns>
    public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, hostName, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Sends a DNS query for multiple domain names and multiple record types to a DNS server using HostName and RequestFormat. Synchronous version.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="recordType">Type of the record.</param>
    /// <param name="hostName">Name of the host.</param>
    /// <param name="requestFormat">The request format.</param>
    /// <param name="timeOutMilliseconds"></param>
    /// <param name="retryOnTransient">Whether to retry on transient errors</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelayMs">Retry delay in milliseconds</param>
    /// <returns></returns>
    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, hostName, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Sends a DNS query to multiple domains and multiple record types to a DNS server. Synchronous version.
    /// This method allows you to specify the DNS endpoint by providing a hostname and request format (JSON, WireFormatGet).
    /// </summary>
    /// <param name="name">Multiple domain names to check for given type</param>
    /// <param name="recordType">Multiple types to check for given name.</param>
    /// <param name="dnsEndpoint">The DNS endpoint. Default endpoint is Cloudflare</param>
    /// <param name="timeOutMilliseconds"></param>
    /// <param name="retryOnTransient">Whether to retry on transient errors</param>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelayMs">Retry delay in milliseconds</param>
    /// <returns></returns>
    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsEndpoint, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();
}
