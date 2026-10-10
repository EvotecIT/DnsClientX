---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Test-DnsBenchmark
## SYNOPSIS
Benchmarks one or more DNS providers or explicit resolver endpoints across repeated queries.

Returns one object per candidate with latency, success rate, answer consistency, rank, and recommendation metadata. PowerShell consumers can sort, filter, format, or export the results themselves.

## SYNTAX
### DnsProvider (Default)
```powershell
Test-DnsBenchmark [-Name] <string[]> [[-Type] <DnsRecordType[]>] [-DnsProvider <DnsEndpoint[]>] [-Attempts <int>] [-MaxConcurrency <int>] [-ConnectionMode <ResolverQueryConnectionMode>] [-TimeOut <int>] [-RequestDnsSec] [-RequestNsid] [-BootstrapResolver <string>] [-ValidateDnsSec] [-DnsSecVerifierPath <string>] [-MinSuccessPercent <Int32>] [-MinSuccessfulCandidates <Int32>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

### ResolverEndpoint
```powershell
Test-DnsBenchmark [-Name] <string[]> [[-Type] <DnsRecordType[]>] [-ResolverEndpoint <string[]>] [-ResolverEndpointFile <string[]>] [-ResolverEndpointUrl <string[]>] [-Attempts <int>] [-MaxConcurrency <int>] [-ConnectionMode <ResolverQueryConnectionMode>] [-TimeOut <int>] [-RequestDnsSec] [-RequestNsid] [-BootstrapResolver <string>] [-ValidateDnsSec] [-DnsSecVerifierPath <string>] [-MinSuccessPercent <Int32>] [-MinSuccessfulCandidates <Int32>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

### ResolverSelection
```powershell
Test-DnsBenchmark [-Name] <string[]> [[-Type] <DnsRecordType[]>] -ResolverSelectionPath <string> [-Attempts <int>] [-MaxConcurrency <int>] [-ConnectionMode <ResolverQueryConnectionMode>] [-TimeOut <int>] [-RequestDnsSec] [-RequestNsid] [-BootstrapResolver <string>] [-ValidateDnsSec] [-DnsSecVerifierPath <string>] [-MinSuccessPercent <Int32>] [-MinSuccessfulCandidates <Int32>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

## DESCRIPTION
Benchmarks one or more DNS providers or explicit resolver endpoints across repeated queries.

Returns one object per candidate with latency, success rate, answer consistency, rank, and recommendation metadata. PowerShell consumers can sort, filter, format, or export the results themselves.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Quad9,Google -Attempts 5
```

Benchmark three built-in providers with repeated A lookups

### EXAMPLE 2
```powershell
Test-DnsBenchmark -Name example.com,microsoft.com -Type A,AAAA -ResolverEndpoint 'udp@1.1.1.1:53','tcp@9.9.9.9:53' -Attempts 3 -MaxConcurrency 8
```

Benchmark a custom resolver matrix across domains and record types

### EXAMPLE 3
```powershell
Test-DnsBenchmark -Name example.com -ResolverEndpoint 'doq@dns.quad9.net:853','doh3@https://dns.quad9.net/dns-query' -Attempts 2 -SummaryOnly
```

Benchmark modern transports without changing the core package graph

### EXAMPLE 4
```powershell
Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Quad9 -MinSuccessPercent 90 -MinSuccessfulCandidates 2
```

Require strong benchmark health before recommending a winner

### EXAMPLE 5
```powershell
Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Google -Attempts 3 -IncludeSummary
```

Include per-candidate rows plus one run-level summary object

### EXAMPLE 6
```powershell
Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Google -Attempts 3 -SummaryOnly
```

Return only the run-level summary object for automation

### EXAMPLE 7
```powershell
Test-DnsBenchmark -Name example.com,microsoft.com -Type A,AAAA -ResolverEndpoint 'udp@1.1.1.1:53','tcp@9.9.9.9:53' -Attempts 2 -SummaryOnly
```

Benchmark explicit endpoints and keep only the recommended summary for automation

### EXAMPLE 8
```powershell
Test-DnsBenchmark -Name example.com -ResolverSelectionPath '.\resolver-score.json' -Attempts 3 -SummaryOnly
```

Reuse the recommended resolver from a saved score snapshot as the single benchmark candidate

### EXAMPLE 9
```powershell
Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Google -Attempts 3 -SavePath '.\resolver-score.json' -IncludeSummary
```

Benchmark resolvers and persist the scored recommendation snapshot for later reuse

## PARAMETERS

### -Attempts
Number of attempts per domain/type combination.

```yaml
Type: Int32
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BootstrapResolver
Resolve endpoint hostnames through an IP-literal UDP/TCP resolver, such as udp@1.1.1.1:53.

```yaml
Type: String
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ConnectionMode
Use a fresh client per attempt, or retain one per target without caching DNS answers.

```yaml
Type: ResolverQueryConnectionMode
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values: Cold, Warm

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsProvider
Built-in provider candidates to benchmark.

```yaml
Type: DnsEndpoint[]
Parameter Sets: DnsProvider
Aliases: None
Possible values: System, SystemTcp, Cloudflare, CloudflareSecurity, CloudflareFamily, CloudflareWireFormat, CloudflareWireFormatPost, CloudflareJsonPost, Google, GoogleWireFormat, GoogleWireFormatPost, GoogleJsonPost, Quad9, Quad9ECS, Quad9Unsecure, OpenDNS, OpenDNSFamily, CloudflareQuic, Quad9Http3, Quad9Quic, GoogleQuic, AdGuard, AdGuardFamily, AdGuardNonFiltering, NextDNS, DnsCryptCloudflare, DnsCryptQuad9, DnsCryptRelay, RootServer, CloudflareOdoh, Custom

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsSecVerifierPath
Local optional DNSSEC provider DLL path, with its dependencies beside it.

```yaml
Type: String
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeSummary
Emits a run-level summary object after the per-candidate benchmark results.

```yaml
Type: SwitchParameter
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConcurrency
Maximum concurrent in-flight benchmark queries across the whole run.

```yaml
Type: Int32
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinSuccessfulCandidates
Require a minimum number of healthy candidates with at least one successful query.

```yaml
Type: Int32
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinSuccessPercent
Require a minimum overall successful query percentage for the run to pass policy.

```yaml
Type: Int32
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Domain names to benchmark.

```yaml
Type: String[]
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestDnsSec
Request DNSSEC records by setting the DO bit.

```yaml
Type: SwitchParameter
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestNsid
Request resolver identity through EDNS on wire transports.

```yaml
Type: SwitchParameter
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpoint
Explicit resolver endpoint candidates to benchmark.

```yaml
Type: String[]
Parameter Sets: ResolverEndpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpointFile
Files containing resolver endpoint candidates to benchmark.

```yaml
Type: String[]
Parameter Sets: ResolverEndpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpointUrl
HTTP or HTTPS URLs exposing resolver endpoint candidates to benchmark.

```yaml
Type: String[]
Parameter Sets: ResolverEndpoint
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverSelectionPath
Path to a saved resolver score snapshot whose recommended resolver should be reused as the single benchmark candidate.

```yaml
Type: String
Parameter Sets: ResolverSelection
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SavePath
Optional path where the benchmark score snapshot should be saved for later selection and reuse.

```yaml
Type: String
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SummaryOnly
Emits only the run-level summary object and suppresses per-candidate rows.

```yaml
Type: SwitchParameter
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeOut
Per-query timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Record types to benchmark. NXNAME is a denial bitmap signal and cannot be queried directly.

```yaml
Type: DnsRecordType[]
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values: Reserved, A, NS, MD, MF, CNAME, SOA, MB, MG, MR, NULL, WKS, PTR, HINFO, MINFO, MX, TXT, RP, AFSDB, X25, ISDN, RT, NSAP, NSAP_PTR, SIG, PX, AAAA, LOC, NXT, SRV, ATMA, NAPTR, KX, CERT, A6, DNAME, SINK, OPT, APL, DS, SSHFP, IPSECKEY, RRSIG, NSEC, DNSKEY, DHCID, NSEC3, NSEC3PARAM, TLSA, SMIMEA, HIP, NINFO, RKEY, TALINK, CDS, CDNSKEY, OPENPGPKEY, CSYNC, ZONEMD, SVCB, HTTPS, SPF, LP, NXNAME, TKEY, TSIG, IXFR, AXFR, MAILB, MAILA, ANY, URI, CAA, AVC, DOA, AMTRELAY, RESINFO, TA, DLV

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ValidateDnsSec
Validate DNSSEC signatures. Implies RequestDnsSec.

```yaml
Type: SwitchParameter
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
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

- `DnsClientX.PowerShell.DnsBenchmarkResult`: Per-candidate DNS benchmark output for PowerShell consumers.
- `DnsClientX.PowerShell.DnsBenchmarkSummary`: Run-level DNS benchmark summary for PowerShell consumers.

## RELATED LINKS

- None
