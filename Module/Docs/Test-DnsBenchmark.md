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
Test-DnsBenchmark [-Name] <string[]> [[-Type] <DnsRecordType[]>] [-DnsProvider <DnsEndpoint[]>] [-Attempts <int>] [-MaxConcurrency <int>] [-TimeOut <int>] [-RequestDnsSec] [-ValidateDnsSec] [-MinSuccessPercent <int>] [-MinSuccessfulCandidates <int>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

### ResolverEndpoint
```powershell
Test-DnsBenchmark [-Name] <string[]> [[-Type] <DnsRecordType[]>] [-ResolverEndpoint <string[]>] [-ResolverEndpointFile <string[]>] [-ResolverEndpointUrl <string[]>] [-Attempts <int>] [-MaxConcurrency <int>] [-TimeOut <int>] [-RequestDnsSec] [-ValidateDnsSec] [-MinSuccessPercent <int>] [-MinSuccessfulCandidates <int>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

### ResolverSelection
```powershell
Test-DnsBenchmark [-Name] <string[]> [[-Type] <DnsRecordType[]>] -ResolverSelectionPath <string> [-Attempts <int>] [-MaxConcurrency <int>] [-TimeOut <int>] [-RequestDnsSec] [-ValidateDnsSec] [-MinSuccessPercent <int>] [-MinSuccessfulCandidates <int>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

## DESCRIPTION
Benchmarks one or more DNS providers or explicit resolver endpoints across repeated queries.

Returns one object per candidate with latency, success rate, answer consistency, rank, and recommendation metadata. PowerShell consumers can sort, filter, format, or export the results themselves.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-DnsBenchmark -ResolverSelectionPath 'C:\Path'
```


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
Type: Nullable`1
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
Type: Nullable`1
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
Record types to benchmark.

```yaml
Type: DnsRecordType[]
Parameter Sets: DnsProvider, ResolverEndpoint, ResolverSelection
Aliases: None
Possible values: Reserved, A, NS, MD, MF, CNAME, SOA, MB, MG, MR, NULL, WKS, PTR, HINFO, MINFO, MX, TXT, RP, AFSDB, X25, ISDN, RT, NSAP, NSAP_PTR, SIG, PX, AAAA, LOC, NXT, SRV, ATMA, NAPTR, KX, CERT, A6, DNAME, SINK, OPT, APL, DS, SSHFP, IPSECKEY, RRSIG, NSEC, DNSKEY, DHCID, NSEC3, NSEC3PARAM, TLSA, SMIMEA, HIP, NINFO, RKEY, TALINK, CDS, CDNSKEY, OPENPGPKEY, CSYNC, ZONEMD, SVCB, HTTPS, SPF, LP, TKEY, TSIG, IXFR, AXFR, MAILB, MAILA, ANY, URI, CAA, AVC, DOA, AMTRELAY, RESINFO, TA, DLV

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
