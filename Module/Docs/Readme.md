---
Module Name: DnsClientX
Module Guid: 77fa806c-70b7-48d9-8b88-942ed73f24ed
Download Help Link: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
Help Version: 2.1.2
Locale: en-US
---
# DnsClientX Module
## Description
DnsClientX is PowerShell module that allows you to query DNS servers for information. It supports DNS over UDP, TCP and DNS over HTTPS (DoH) and DNS over TLS (DoT). It supports multiple types of DNS queries and can be used to query public DNS servers, private DNS servers and has built-in DNS Providers.

## DnsClientX Cmdlets
### [Clear-DnsCache](Clear-DnsCache.md)
Clears process-wide cached DNS responses.

Clears all response entries, or entries for one queried name and optional type across all resolver policies. Returns the number removed. This affects every cache-enabled DnsClientX client in this process; active queries may repopulate the cache. FastestWins endpoint choices are managed by Clear-DnsMultiResolverCache.

### [Clear-DnsMultiResolverCache](Clear-DnsMultiResolverCache.md)
Clears the multi-resolver fastest-endpoint cache used by the FastestWins strategy.

Clears the in-memory cache used to remember the fastest endpoint for a given set of endpoints. Use this when you change network conditions or want to force re-probing. Does not affect TTL-based response caching, which expires automatically.

### [Compare-DnsResolverSnapshot](Compare-DnsResolverSnapshot.md)
Compares resolver health and answers between saved DNS snapshots.

Reports candidate and answer changes, health regressions, and incompatible query scopes. Optional authoritative responses provide comparison context; a majority answer is not proof of authoritative correctness.

### [ConvertFrom-DnsStamp](ConvertFrom-DnsStamp.md)
Parses a DNS stamp into a resolver endpoint description.

Converts a supported sdns:// DNS stamp into the same endpoint model used by DnsClientX resolver workflows. This command does not perform a DNS query.

### [Find-DnsService](Find-DnsService.md)
Discovers services advertised via DNS Service Discovery.

Streams individual service instances under a domain, browses one service type, or lists advertised service types. An empty discovery produces no pipeline objects; use an array expression to collect zero or more results.

### [Get-DnsCache](Get-DnsCache.md)
Gets process-wide DNS response cache statistics.

Returns capacity, current entries and lifetime lookup counters shared by all cache-enabled DnsClientX clients in this PowerShell process. Clearing entries does not reset these counters.

### [Get-DnsQueryExplanation](Get-DnsQueryExplanation.md)
Explains captured evidence from a completed DNS response.

Projects resolver, transport, cache, alias, authority and local DNSSEC evidence without additional DNS queries. UnavailableEvidence lists facts that the response cannot establish, including a complete delegation or DNSSEC trust-chain traversal.

### [Get-DnsResolverSelection](Get-DnsResolverSelection.md)
Loads a saved resolver score snapshot and returns the recommended resolver selection.

Reads a persisted resolver score snapshot, applies the shared recommendation logic, and returns either the structured selection object or the raw target string for automation use.

### [Get-DnsService](Get-DnsService.md)
Retrieves services advertised via DNS Service Discovery.

Streams individual DNS-SD instances under a domain, browses one service type, or resolves SRV targets in connection-attempt order. Empty discovery produces no pipeline objects; use an array expression to collect zero or more results.

### [Get-DnsTransportCapability](Get-DnsTransportCapability.md)
Returns runtime transport support information for the DnsClientX core transport surface.

Reports which core transports are available on the current runtime, including modern DoH3 and DoQ support on .NET 8+.

### [Get-DnsZone](Get-DnsZone.md)
Performs a DNS zone transfer (AXFR) for a given zone.

Retrieves all records for the specified zone using TCP based zone transfer.

### [Invoke-DnsUpdate](Invoke-DnsUpdate.md)
Sends DNS UPDATE messages to a server.

Adds or removes records in a zone using RFC 2136 over TCP.

Use WhatIf to preview an update without contacting the server, or Confirm to approve each change.

### [Resolve-Dns](Resolve-Dns.md)
Resolves DNS records (A, AAAA, MX, TXT, …) over UDP, TCP, DoT, DoH, QUIC, or multicast with optional multi-resolver strategies.

Supports single-provider queries, explicit servers with transport selection, multiple providers with FirstSuccess/FastestWins/SequentialFallback/RoundRobin, direct resolver endpoints, DNSSEC, EDNS/ECS, concurrency control, and TTL-based response caching.

Specify either Name or Pattern. When no resolver source is specified, the library's default resolver is used. Timeout, retry, response, typed-record, and DNSSEC options apply to every resolver source.

Name accepts pipeline strings or objects with Name, Domain, or DnsName properties. By default each answer is an individual pipeline row. Use FullResponse for complete response objects, TypedRecords for typed values, or TypedRecordMetadata to keep typed values and record metadata together.

### [Test-DnsBenchmark](Test-DnsBenchmark.md)
Benchmarks one or more DNS providers or explicit resolver endpoints across repeated queries.

Returns one object per candidate with latency, success rate, answer consistency, rank, and recommendation metadata. PowerShell consumers can sort, filter, format, or export the results themselves.

### [Test-DnsHealth](Test-DnsHealth.md)
Runs a repeatable DNS resolver health profile.

Runs the profile's DNS query catalog and policy against its configured resolvers. IncludeBenchmark measures the same catalog. Returns the complete report; resolver agreement alone does not establish authoritative correctness.

### [Test-DnsProbe](Test-DnsProbe.md)
Probes one built-in resolver profile or a custom resolver set and reports health, consensus, and recommendation data.

Runs a single DNS query against each candidate, highlights answer mismatches, applies optional success and consensus policy gates, and can persist the scored result set for later resolver selection and reuse.

### [Test-DnsResolverCatalog](Test-DnsResolverCatalog.md)
Validates resolver endpoint catalog inputs without querying DNS.

Checks inline resolver endpoints, resolver endpoint files, and resolver endpoint URLs using the same parser used by probe and benchmark workflows.
