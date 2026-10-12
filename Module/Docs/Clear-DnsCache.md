---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Clear-DnsCache
## SYNOPSIS
Clears process-wide cached DNS responses.

Clears all response entries, or entries for one queried name and optional type across all resolver policies. Returns the number removed. This affects every cache-enabled DnsClientX client in this process; active queries may repopulate the cache. FastestWins endpoint choices are managed by Clear-DnsMultiResolverCache.

## SYNTAX
### All (Default)
```powershell
Clear-DnsCache [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Name
```powershell
Clear-DnsCache [-Name] <string> [[-Type] <DnsRecordType>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Clears process-wide cached DNS responses.

Clears all response entries, or entries for one queried name and optional type across all resolver policies. Returns the number removed. This affects every cache-enabled DnsClientX client in this process; active queries may repopulate the cache. FastestWins endpoint choices are managed by Clear-DnsMultiResolverCache.

## EXAMPLES

### EXAMPLE 1
```powershell
Clear-DnsCache
```

Clear all cached responses

### EXAMPLE 2
```powershell
Clear-DnsCache -Name example.com -Type A
```

Remove cached A responses for one name

### EXAMPLE 3
```powershell
Clear-DnsCache -Name example.com -WhatIf
```

Preview selective invalidation

## PARAMETERS

### -Name
Original queried name to invalidate, including the expanded system-search name when applicable. Names normalize case, Unicode and the trailing root dot. Alias targets do not invalidate a different original query.

```yaml
Type: String
Parameter Sets: Name
Aliases: Domain, DnsName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -Type
Only remove this query type. Omit to remove every cached type for Name.

```yaml
Type: DnsRecordType
Parameter Sets: Name
Aliases: None
Possible values: Reserved, A, NS, MD, MF, CNAME, SOA, MB, MG, MR, NULL, WKS, PTR, HINFO, MINFO, MX, TXT, RP, AFSDB, X25, ISDN, RT, NSAP, NSAP_PTR, SIG, PX, AAAA, LOC, NXT, SRV, ATMA, NAPTR, KX, CERT, A6, DNAME, SINK, OPT, APL, DS, SSHFP, IPSECKEY, RRSIG, NSEC, DNSKEY, DHCID, NSEC3, NSEC3PARAM, TLSA, SMIMEA, HIP, NINFO, RKEY, TALINK, CDS, CDNSKEY, OPENPGPKEY, CSYNC, ZONEMD, SVCB, HTTPS, SPF, LP, NXNAME, TKEY, TSIG, IXFR, AXFR, MAILB, MAILA, ANY, URI, CAA, AVC, DOA, AMTRELAY, RESINFO, TA, DLV

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `System.Int32`

## RELATED LINKS

- None
