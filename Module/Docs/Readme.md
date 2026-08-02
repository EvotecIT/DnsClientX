---
Module Name: DnsClientX
Module Guid: 77fa806c-70b7-48d9-8b88-942ed73f24ed
Download Help Link: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
Help Version: 2.1.0
Locale: en-US
---
# DnsClientX Module
## Description
DnsClientX is PowerShell module that allows you to query DNS servers for information. It supports DNS over UDP, TCP and DNS over HTTPS (DoH) and DNS over TLS (DoT). It supports multiple types of DNS queries and can be used to query public DNS servers, private DNS servers and has built-in DNS Providers.

## DnsClientX Cmdlets
### [Clear-DnsMultiResolverCache](Clear-DnsMultiResolverCache.md)
Clears the multi-resolver fastest-endpoint cache used by the FastestWins strategy.

Clears the in-memory cache used to remember the fastest endpoint for a given set of endpoints. Use this when you change network conditions or want to force re-probing. Does not affect TTL-based response caching, which expires automatically.

### [ConvertFrom-DnsStamp](ConvertFrom-DnsStamp.md)
Parses a DNS stamp into a resolver endpoint description.

Converts a supported sdns:// DNS stamp into the same endpoint model used by DnsClientX resolver workflows. This command does not perform a DNS query.

### [Find-DnsService](Find-DnsService.md)
Discovers services advertised via DNS Service Discovery.

Wraps DiscoverServices to return services under a domain.

### [Get-DnsResolverSelection](Get-DnsResolverSelection.md)
Loads a saved resolver score snapshot and returns the recommended resolver selection.

Reads a persisted resolver score snapshot, applies the shared recommendation logic, and returns either the structured selection object or the raw target string for automation use.

### [Get-DnsService](Get-DnsService.md)
Retrieves services advertised via DNS Service Discovery.

Queries the _services._dns-sd._udp tree for the specified domain and returns SRV/TXT data describing each advertised service.

### [Get-DnsTransportCapability](Get-DnsTransportCapability.md)
Returns runtime transport support information for the DnsClientX core transport surface.

Reports which core transports are available on the current runtime, including modern DoH3 and DoQ support on .NET 8+.

### [Get-DnsZone](Get-DnsZone.md)
Performs a DNS zone transfer (AXFR) for a given zone.

Retrieves all records for the specified zone using TCP based zone transfer.

### [Invoke-DnsUpdate](Invoke-DnsUpdate.md)
Sends DNS UPDATE messages to a server.

Adds or removes records in a zone using RFC 2136 over TCP.

### [Resolve-Dns](Resolve-Dns.md)
Resolves DNS records (A, AAAA, MX, TXT, …) over UDP, TCP, DoT, DoH, QUIC, or multicast with optional multi-resolver strategies.

Supports single-provider queries, explicit servers with transport selection, multiple providers with FirstSuccess/FastestWins/SequentialFallback/RoundRobin, direct resolver endpoints, DNSSEC, EDNS/ECS, concurrency control, and TTL-based response caching.

### [Test-DnsBenchmark](Test-DnsBenchmark.md)
Benchmarks one or more DNS providers or explicit resolver endpoints across repeated queries.

Returns one object per candidate with latency, success rate, answer consistency, rank, and recommendation metadata. PowerShell consumers can sort, filter, format, or export the results themselves.

### [Test-DnsProbe](Test-DnsProbe.md)
Probes one built-in resolver profile or a custom resolver set and reports health, consensus, and recommendation data.

Runs a single DNS query against each candidate, highlights answer mismatches, applies optional success and consensus policy gates, and can persist the scored result set for later resolver selection and reuse.

### [Test-DnsResolverCatalog](Test-DnsResolverCatalog.md)
Validates resolver endpoint catalog inputs without querying DNS.

Checks inline resolver endpoints, resolver endpoint files, and resolver endpoint URLs using the same parser used by probe and benchmark workflows.
