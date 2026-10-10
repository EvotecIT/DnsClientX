---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Resolve-Dns
## SYNOPSIS
Resolves DNS records (A, AAAA, MX, TXT, …) over UDP, TCP, DoT, DoH, QUIC, or multicast with optional multi-resolver strategies.

Supports single-provider queries, explicit servers with transport selection, multiple providers with FirstSuccess/FastestWins/SequentialFallback/RoundRobin, direct resolver endpoints, DNSSEC, EDNS/ECS, concurrency control, and TTL-based response caching.

Specify either Name or Pattern. When no resolver source is specified, the library's default resolver is used. Timeout, retry, response, typed-record, and DNSSEC options apply to every resolver source.

## SYNTAX
### Name (Default)
```powershell
Resolve-Dns [-Name] <string[]> [[-Type] <DnsRecordType[]>] [-DnsProvider <DnsEndpoint[]>] [-DnsSelectionStrategy <DnsSelectionStrategy>] [-Server <List[string]>] [-ResolverEndpoint <string[]>] [-ResolverEndpointFile <string[]>] [-ResolverEndpointUrl <string[]>] [-ResolverDnsProvider <DnsEndpoint[]>] [-ResolverSelectionPath <string>] [-ResolverStrategy <MultiResolverStrategy>] [-MaxParallelism <int>] [-RespectEndpointTimeout] [-FastestCacheMinutes <int>] [-PerEndpointMaxInFlight <int>] [-ResponseCache] [-MaxCacheTtlSeconds <int>] [-DnsSecVerifierPath <string>] [-EdnsBufferSize <int>] [-ClientSubnet <string>] [-BootstrapResolver <string>] [-RequestFormat <DnsRequestFormat>] [-Port <int>] [-UserAgent <string>] [-HttpVersion <version>] [-IgnoreCertificateErrors] [-UseTcpFallback <bool>] [-ProxyUri <uri>] [-MaxConnectionsPerServer <int>] [-MaxConcurrency <int>] [-AllServers] [-Fallback] [-RandomServer] [-FullResponse] [-TypedRecords] [-ParseTypedTxtRecords] [-TimeOut <int>] [-RetryCount <int>] [-RetryDelayMs <int>] [-RequestDnsSec] [-ValidateDnsSec] [-EnableEdns] [-CheckingDisabled] [-RequestNsid] [<CommonParameters>]
```

### Pattern
```powershell
Resolve-Dns [-Pattern] <string> [[-Type] <DnsRecordType[]>] [-DnsProvider <DnsEndpoint[]>] [-DnsSelectionStrategy <DnsSelectionStrategy>] [-Server <List[string]>] [-ResolverEndpoint <string[]>] [-ResolverEndpointFile <string[]>] [-ResolverEndpointUrl <string[]>] [-ResolverDnsProvider <DnsEndpoint[]>] [-ResolverSelectionPath <string>] [-ResolverStrategy <MultiResolverStrategy>] [-MaxParallelism <int>] [-RespectEndpointTimeout] [-FastestCacheMinutes <int>] [-PerEndpointMaxInFlight <int>] [-ResponseCache] [-MaxCacheTtlSeconds <int>] [-DnsSecVerifierPath <string>] [-EdnsBufferSize <int>] [-ClientSubnet <string>] [-BootstrapResolver <string>] [-RequestFormat <DnsRequestFormat>] [-Port <int>] [-UserAgent <string>] [-HttpVersion <version>] [-IgnoreCertificateErrors] [-UseTcpFallback <bool>] [-ProxyUri <uri>] [-MaxConnectionsPerServer <int>] [-MaxConcurrency <int>] [-AllServers] [-Fallback] [-RandomServer] [-FullResponse] [-TypedRecords] [-ParseTypedTxtRecords] [-TimeOut <int>] [-RetryCount <int>] [-RetryDelayMs <int>] [-RequestDnsSec] [-ValidateDnsSec] [-EnableEdns] [-CheckingDisabled] [-RequestNsid] [<CommonParameters>]
```

## DESCRIPTION
Resolves DNS records (A, AAAA, MX, TXT, …) over UDP, TCP, DoT, DoH, QUIC, or multicast with optional multi-resolver strategies.

Supports single-provider queries, explicit servers with transport selection, multiple providers with FirstSuccess/FastestWins/SequentialFallback/RoundRobin, direct resolver endpoints, DNSSEC, EDNS/ECS, concurrency control, and TTL-based response caching.

Specify either Name or Pattern. When no resolver source is specified, the library's default resolver is used. Timeout, retry, response, typed-record, and DNSSEC options apply to every resolver source.

## EXAMPLES

### EXAMPLE 1
```powershell
Resolve-Dns -Name "example.com" -Type A
```

Simple (system default)

### EXAMPLE 2
```powershell
Resolve-Dns -Name "example.com" -Type A -DnsProvider Cloudflare
```

Single provider (classic)

### EXAMPLE 3
```powershell
Resolve-Dns -Name "example.com" -Type A -DnsProvider Cloudflare,Google -ResolverStrategy FirstSuccess
```

FirstSuccess across providers

### EXAMPLE 4
```powershell
Resolve-Dns -Name "example.com" -Type A -DnsProvider Cloudflare,Google -ResolverStrategy FastestWins -FastestCacheMinutes 10 -ResponseCache
```

FastestWins with cache

### EXAMPLE 5
```powershell
Resolve-Dns -Name @('a.com','b.com') -Type A -DnsProvider System,Cloudflare,Quad9 -ResolverStrategy RoundRobin -MaxParallelism 16 -PerEndpointMaxInFlight 4
```

RoundRobin with per-endpoint cap

### EXAMPLE 6
```powershell
Resolve-Dns -Name 'example.com' -Type TXT -ResolverEndpoint '1.1.1.1:53','https://dns.google/dns-query' -ResolverStrategy FirstSuccess
```

Mixed endpoints (UDP + DoH)

### EXAMPLE 7
```powershell
Resolve-Dns -Name 'example.com' -Type A -ResolverEndpoint 'doq@dns.quad9.net:853','doh3@https://dns.quad9.net/dns-query' -ResolverStrategy FirstSuccess
```

Modern endpoints (DoQ + DoH3) through the shared endpoint parser

### EXAMPLE 8
```powershell
Resolve-Dns -Name 'example.com' -Type MX -DnsProvider Cloudflare,Google -ResponseCache -MaxCacheTtlSeconds 3600
```

Enable TTL-based response cache with bounds

### EXAMPLE 9
```powershell
Resolve-Dns -Name 'example.com' -Type A -Server 'dns.google' -RequestFormat DnsOverHttps -Port 443 -UserAgent 'DnsClientX/PowerShell' -HttpVersion 2.0
```

Query a specific server over DoH with explicit transport settings

### EXAMPLE 10
```powershell
Resolve-Dns -Name 'example.com' -Type A -DnsProvider Quad9ECS -EnableEdns -ClientSubnet '192.0.2.0/24' -RequestNsid -FullResponse
```

Send EDNS client subnet and request NSID metadata

### EXAMPLE 11
```powershell
Resolve-Dns -Name 'example.com' -Type A -ResolverSelectionPath '.\resolver-score.json'
```

Reuse the recommended resolver from a saved selection snapshot

## PARAMETERS

### -AllServers
If specified, all servers listed in Server are queried sequentially and the responses are aggregated in server order.

When not specified, only the first server is queried for faster results.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BootstrapResolver
Resolves endpoint hostnames through an IP-literal UDP/TCP endpoint, such as udp@1.1.1.1:53. Failures do not fall back to system DNS.

```yaml
Type: String
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CheckingDisabled
Sets the CD (checking disabled) bit on outgoing queries.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSubnet
Sets the EDNS Client Subnet (ECS) in CIDR notation, for example 192.0.2.0/24.

```yaml
Type: String
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsProvider
Predefined provider(s) (DnsEndpoint) for the query.

When a single provider is specified, the classic single-resolver path is used. When multiple providers are specified, the multi-resolver path is used.

If not specified, the default provider System (UDP) is used.

```yaml
Type: DnsEndpoint[]
Parameter Sets: Name, Pattern
Aliases: None
Possible values: System, SystemTcp, Cloudflare, CloudflareSecurity, CloudflareFamily, CloudflareWireFormat, CloudflareWireFormatPost, CloudflareJsonPost, Google, GoogleWireFormat, GoogleWireFormatPost, GoogleJsonPost, Quad9, Quad9ECS, Quad9Unsecure, OpenDNS, OpenDNSFamily, CloudflareQuic, Quad9Http3, Quad9Quic, GoogleQuic, AdGuard, AdGuardFamily, AdGuardNonFiltering, NextDNS, DnsCryptCloudflare, DnsCryptQuad9, DnsCryptRelay, RootServer, CloudflareOdoh, Custom

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsSecVerifierPath
Local optional DNSSEC provider DLL path, with its dependencies beside it. Enables additional signature algorithms when ValidateDnsSec is selected.

```yaml
Type: String
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsSelectionStrategy
How to choose among built-in provider hostnames when a single provider exposes multiple backends.

```yaml
Type: DnsSelectionStrategy
Parameter Sets: Name, Pattern
Aliases: None
Possible values: First, Random, Failover

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EdnsBufferSize
Sets the EDNS UDP buffer size. When specified, EDNS is enabled automatically.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EnableEdns
Enables EDNS on outgoing queries.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Fallback
If specified, the cmdlet sequentially queries each server until a successful response is received.

This option stops on the first server that returns DnsResponseCode.NoError.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FastestCacheMinutes
Cache duration in minutes for FastestWins strategy.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FullResponse
Provides the full response of the query. If not specified, only the minimal response is provided (just the answer).

If specified, the full response is provided (answer, authority, and additional sections).

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HttpVersion
Optional preferred HTTP protocol version, for example 2.0 or 3.0.

```yaml
Type: Version
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IgnoreCertificateErrors
Ignore TLS certificate validation errors.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxCacheTtlSeconds
Maximal TTL allowed for cached entries (seconds). 0 leaves library default.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConcurrency
Optional cap on client-side query concurrency for single-resolver operations. When 0, the library default is used.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConnectionsPerServer
Maximum HTTP connections allowed per server. When 0, the library default is used.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxParallelism
Limits concurrent queries across endpoints. Defaults to 4.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
The name of the DNS record to query for

```yaml
Type: String[]
Parameter Sets: Name
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ParseTypedTxtRecords
Gets or sets a value indicating whether TXT records should be
parsed into specialized types (DMARC, SPF, etc.) when TypedRecords is
specified. When false, returns simple TXT records.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Pattern
Pattern to expand into multiple DNS queries.

```yaml
Type: String
Parameter Sets: Pattern
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PerEndpointMaxInFlight
Limits concurrent queries per endpoint when using the multi-resolver. Set to a positive value to cap in-flight queries per endpoint; 0 disables the cap.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
Optional port override for the -Server path. If omitted, the selected request format decides the default port.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyUri
Optional web proxy URI for HTTP-based transports.

```yaml
Type: Uri
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RandomServer
If specified, the order of servers defined in Server is randomized before querying.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestDnsSec
Request DNSSEC data (sets the DO bit).

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestFormat
Explicit request format for the -Server path, such as DnsOverUDP, DnsOverTCP, DnsOverTLS, or DnsOverHttps.

```yaml
Type: DnsRequestFormat
Parameter Sets: Name, Pattern
Aliases: None
Possible values: DnsOverHttps, DnsOverHttpsJSON, DnsOverHttpsPOST, DnsOverHttpsWirePost, DnsOverHttpsJSONPOST, DnsOverUDP, DnsOverTCP, DnsOverTLS, DnsOverQuic, DnsOverHttp2, DnsOverHttp3, DnsCrypt, DnsCryptRelay, ObliviousDnsOverHttps, DnsOverGrpc, Multicast

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestNsid
Requests the NSID EDNS option so compatible servers may include resolver identity metadata in the response.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverDnsProvider
One or more predefined providers (DnsEndpoint enum) to expand into endpoints for the multi-resolver.

This enables strategy control (FirstSuccess/FastestWins/SequentialFallback) and other multi-resolver options.

```yaml
Type: DnsEndpoint[]
Parameter Sets: Name, Pattern
Aliases: DnsProviders
Possible values: System, SystemTcp, Cloudflare, CloudflareSecurity, CloudflareFamily, CloudflareWireFormat, CloudflareWireFormatPost, CloudflareJsonPost, Google, GoogleWireFormat, GoogleWireFormatPost, GoogleJsonPost, Quad9, Quad9ECS, Quad9Unsecure, OpenDNS, OpenDNSFamily, CloudflareQuic, Quad9Http3, Quad9Quic, GoogleQuic, AdGuard, AdGuardFamily, AdGuardNonFiltering, NextDNS, DnsCryptCloudflare, DnsCryptQuad9, DnsCryptRelay, RootServer, CloudflareOdoh, Custom

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpoint
One or more resolver endpoints in string format. Accepted: "1.1.1.1:53", "[2606:4700:4700::1111]:53", "dns.google:53", DoH URLs like "https://dns.google/dns-query", and transport-prefixed values such as "doq@dns.quad9.net:853" or "doh3@https://dns.quad9.net/dns-query".

```yaml
Type: String[]
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpointFile
One or more files containing resolver endpoints for the multi-resolver. Blank lines and full-line comments are ignored.

```yaml
Type: String[]
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpointUrl
One or more HTTP or HTTPS URLs exposing resolver endpoints for the multi-resolver.

```yaml
Type: String[]
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverSelectionPath
Path to a saved resolver score snapshot whose recommended resolver should be reused for the query.

```yaml
Type: String
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverStrategy
Multi-resolver strategy to use when multiple endpoints are provided.

```yaml
Type: MultiResolverStrategy
Parameter Sets: Name, Pattern
Aliases: None
Possible values: FirstSuccess, FastestWins, SequentialFallback, RoundRobin, Random

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RespectEndpointTimeout
Respect endpoint-level timeouts if present. When not set, the cmdlet's -TimeOut value is used.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResponseCache
Enables response caching based on TTLs for repeated queries of the same (name,type). Disabled by default.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryCount
Number of retry attempts on transient errors.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryDelayMs
Delay between retry attempts in milliseconds.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
Server to use for the query. If not specified, the default provider System (UDP) is used.

Once a server is specified, the query will be sent to that server.

```yaml
Type: List`1
Parameter Sets: Name, Pattern
Aliases: ServerName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeOut
Specifies the timeout for the DNS query, in milliseconds. If the DNS server does not respond within this time, the query will fail. Default is 2000 ms (2 seconds) as defined by DefaultTimeout. Increase this value for slow networks or unreliable servers.

```yaml
Type: Int32
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
The type of the record to query for. If not specified, A record is queried.

```yaml
Type: DnsRecordType[]
Parameter Sets: Name, Pattern
Aliases: None
Possible values: Reserved, A, NS, MD, MF, CNAME, SOA, MB, MG, MR, NULL, WKS, PTR, HINFO, MINFO, MX, TXT, RP, AFSDB, X25, ISDN, RT, NSAP, NSAP_PTR, SIG, PX, AAAA, LOC, NXT, SRV, ATMA, NAPTR, KX, CERT, A6, DNAME, SINK, OPT, APL, DS, SSHFP, IPSECKEY, RRSIG, NSEC, DNSKEY, DHCID, NSEC3, NSEC3PARAM, TLSA, SMIMEA, HIP, NINFO, RKEY, TALINK, CDS, CDNSKEY, OPENPGPKEY, CSYNC, ZONEMD, SVCB, HTTPS, SPF, LP, NXNAME, TKEY, TSIG, IXFR, AXFR, MAILB, MAILA, ANY, URI, CAA, AVC, DOA, AMTRELAY, RESINFO, TA, DLV

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TypedRecords
When set, attempts to parse answers into typed record objects.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserAgent
Optional User-Agent header for HTTP-based transports.

```yaml
Type: String
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseTcpFallback
Controls whether UDP queries may fall back to TCP when truncated.

```yaml
Type: Boolean
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ValidateDnsSec
Validate DNSSEC signatures. Implies requesting DNSSEC data.

```yaml
Type: SwitchParameter
Parameter Sets: Name, Pattern
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- AsyncPSCmdlet
