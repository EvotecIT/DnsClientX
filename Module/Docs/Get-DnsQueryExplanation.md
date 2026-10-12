---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Get-DnsQueryExplanation
## SYNOPSIS
Explains captured evidence from a completed DNS response.

Projects resolver, transport, cache, alias, authority and local DNSSEC evidence without additional DNS queries. UnavailableEvidence lists facts that the response cannot establish, including a complete delegation or DNSSEC trust-chain traversal.

## SYNTAX
### __AllParameterSets
```powershell
Get-DnsQueryExplanation [-InputObject] <DnsResponse> [-AuditTrail <AuditEntry[]>] [<CommonParameters>]
```

## DESCRIPTION
Explains captured evidence from a completed DNS response.

Projects resolver, transport, cache, alias, authority and local DNSSEC evidence without additional DNS queries. UnavailableEvidence lists facts that the response cannot establish, including a complete delegation or DNSSEC trust-chain traversal.

## EXAMPLES

### EXAMPLE 1
```powershell
Resolve-Dns example.com -FullResponse | Get-DnsQueryExplanation
```

Explain a response returned by Resolve-Dns

## PARAMETERS

### -AuditTrail
Optional captured audit entries for the supplied response. Omit this parameter when individual attempt evidence was not captured.

```yaml
Type: AuditEntry[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Completed DNS response, supplied directly or through the pipeline.

```yaml
Type: DnsResponse
Parameter Sets: __AllParameterSets
Aliases: Response
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `DnsClientX.DnsResponse`

## OUTPUTS

- `DnsClientX.DnsQueryExplanation`

## RELATED LINKS

- None
