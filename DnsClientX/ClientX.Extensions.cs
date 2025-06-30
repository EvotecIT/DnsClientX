namespace DnsClientX;

using System;
using System.Text.RegularExpressions;
using System.Threading;

public static class ClientXExtensions
{
    public static DnsResponse ResolveSync(this ClientX client, string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100)
        => client.Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsResponse[] ResolveSync(this ClientX client, string name, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.Resolve(name, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsResponse[] ResolveSync(this ClientX client, string[] names, DnsRecordType[] types, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.Resolve(names, types, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsResponse[] ResolveSync(this ClientX client, string[] names, DnsRecordType type, bool requestDnsSec = false, bool validateDnsSec = false, bool returnAllTypes = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.Resolve(names, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsAnswer? ResolveFirstSync(this ClientX client, string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 100)
        => client.ResolveFirst(name, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsAnswer[] ResolveAllSync(this ClientX client, string name, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.ResolveAll(name, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsAnswer[] ResolveAllSync(this ClientX client, string name, string filter, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.ResolveAll(name, filter, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsAnswer[] ResolveAllSync(this ClientX client, string name, Regex regexPattern, DnsRecordType type = DnsRecordType.A, bool requestDnsSec = false, bool validateDnsSec = false, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => client.ResolveAll(name, regexPattern, type, requestDnsSec, validateDnsSec, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsEndpoint, dnsSelectionStrategy, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, DnsSelectionStrategy dnsSelectionStrategy = DnsSelectionStrategy.First, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsEndpoint, dnsSelectionStrategy, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsUri, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, Uri dnsUri, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200)
        => ClientX.QueryDns(name, recordType, dnsUri, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs).GetAwaiter().GetResult();

    public static DnsResponse QueryDnsSync(string name, DnsRecordType recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, hostName, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, string hostName, DnsRequestFormat requestFormat, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, hostName, requestFormat, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();

    public static DnsResponse[] QueryDnsSync(string[] name, DnsRecordType[] recordType, DnsEndpoint dnsEndpoint = DnsEndpoint.System, int timeOutMilliseconds = 1000, bool retryOnTransient = true, int maxRetries = 3, int retryDelayMs = 200, CancellationToken cancellationToken = default)
        => ClientX.QueryDns(name, recordType, dnsEndpoint, timeOutMilliseconds, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).GetAwaiter().GetResult();
}
