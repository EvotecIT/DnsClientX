---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Get-DnsTransportCapability
## SYNOPSIS
Returns runtime transport support information for the DnsClientX core transport surface.

Reports which core transports are available on the current runtime, including modern DoH3 and DoQ support on .NET 8+.

## SYNTAX
### Core (Default)
```powershell
Get-DnsTransportCapability [-ModernOnly] [<CommonParameters>]
```

### Configured
```powershell
Get-DnsTransportCapability -Configuration <Configuration> [<CommonParameters>]
```

## DESCRIPTION
Returns runtime transport support information for the DnsClientX core transport surface.

Reports which core transports are available on the current runtime, including modern DoH3 and DoQ support on .NET 8+.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DnsTransportCapability
```

List the full core transport capability report

### EXAMPLE 2
```powershell
Get-DnsTransportCapability -ModernOnly
```

Show only the runtime-gated modern transport entries

## PARAMETERS

### -Configuration
Reports the transport and optional provider supplied by this configuration.

```yaml
Type: Configuration
Parameter Sets: Configured
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ModernOnly
Emits only modern runtime-gated transports such as DoH3 and DoQ.

```yaml
Type: SwitchParameter
Parameter Sets: Core
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

- `DnsClientX.Configuration`

## OUTPUTS

- `DnsClientX.DnsTransportCapabilityInfo`

## RELATED LINKS

- None
