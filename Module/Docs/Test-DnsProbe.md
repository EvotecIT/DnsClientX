---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Test-DnsProbe
## SYNOPSIS
Probes one built-in resolver profile or a custom resolver set and reports health, consensus, and recommendation data.

Runs a single DNS query against each candidate, highlights answer mismatches, applies optional success and consensus policy gates, and can persist the scored result set for later resolver selection and reuse.

## SYNTAX
### DnsProvider (Default)
```powershell
Test-DnsProbe [-Name] <string> [[-Type] <DnsRecordType>] [-DnsProvider <DnsEndpoint>] [-TimeOut <int>] [-RequestDnsSec] [-ValidateDnsSec] [-RequireConsensus] [-MinConsensusPercent <int>] [-MinSuccessCount <int>] [-MinSuccessPercent <int>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

### ResolverEndpoint
```powershell
Test-DnsProbe [-Name] <string> [[-Type] <DnsRecordType>] [-ResolverEndpoint <string[]>] [-ResolverEndpointFile <string[]>] [-ResolverEndpointUrl <string[]>] [-TimeOut <int>] [-RequestDnsSec] [-ValidateDnsSec] [-RequireConsensus] [-MinConsensusPercent <int>] [-MinSuccessCount <int>] [-MinSuccessPercent <int>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

### ResolverSelection
```powershell
Test-DnsProbe [-Name] <string> [[-Type] <DnsRecordType>] -ResolverSelectionPath <string> [-TimeOut <int>] [-RequestDnsSec] [-ValidateDnsSec] [-RequireConsensus] [-MinConsensusPercent <int>] [-MinSuccessCount <int>] [-MinSuccessPercent <int>] [-IncludeSummary] [-SummaryOnly] [-SavePath <string>] [<CommonParameters>]
```

## DESCRIPTION
Probes one built-in resolver profile or a custom resolver set and reports health, consensus, and recommendation data.

Runs a single DNS query against each candidate, highlights answer mismatches, applies optional success and consensus policy gates, and can persist the scored result set for later resolver selection and reuse.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-DnsProbe -ResolverSelectionPath 'C:\Path'
```


## PARAMETERS

### -DnsProvider
Built-in resolver profile to probe.

```yaml
Type: DnsEndpoint
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
Emits a run-level summary object after the per-candidate probe rows.

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

### -MinConsensusPercent
Require the leading answer group to reach at least this consensus percentage.

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

### -MinSuccessCount
Require at least this many successful probe candidates.

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
Require at least this successful probe percentage across all candidates.

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
Domain name to probe.

```yaml
Type: String
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

### -RequireConsensus
Require unanimous answer consensus among successful candidates.

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
Explicit resolver endpoints to probe.

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
Files containing resolver endpoints to probe.

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
HTTP or HTTPS URLs exposing resolver endpoints to probe.

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
Path to a saved resolver score snapshot whose recommended resolver should be reused as the single probe candidate.

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
Optional path where the probe score snapshot should be saved for later selection and reuse.

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
Record type to probe.

```yaml
Type: DnsRecordType
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

- `DnsClientX.PowerShell.DnsProbeResult`: Per-candidate DNS probe output for PowerShell consumers.
- `DnsClientX.PowerShell.DnsProbeSummary`: Run-level DNS probe summary for PowerShell consumers.

## RELATED LINKS

- None
