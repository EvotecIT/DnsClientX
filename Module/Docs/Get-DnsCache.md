---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Get-DnsCache
## SYNOPSIS
Gets process-wide DNS response cache statistics.

Returns capacity, current entries and lifetime lookup counters shared by all cache-enabled DnsClientX clients in this PowerShell process. Clearing entries does not reset these counters.

## SYNTAX
### __AllParameterSets
```powershell
Get-DnsCache [<CommonParameters>]
```

## DESCRIPTION
Gets process-wide DNS response cache statistics.

Returns capacity, current entries and lifetime lookup counters shared by all cache-enabled DnsClientX clients in this PowerShell process. Clearing entries does not reset these counters.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DnsCache
```

Inspect the response cache used by Resolve-Dns

## PARAMETERS

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `DnsClientX.DnsResponseCacheStatistics`

## RELATED LINKS

- None
