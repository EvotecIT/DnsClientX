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
### __AllParameterSets
```powershell
Get-DnsTransportCapability [-ModernOnly] [<CommonParameters>]
```

## DESCRIPTION
Returns runtime transport support information for the DnsClientX core transport surface.

Reports which core transports are available on the current runtime, including modern DoH3 and DoQ support on .NET 8+.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DnsTransportCapability -ModernOnly
```


## PARAMETERS

### -ModernOnly
Emits only modern runtime-gated transports such as DoH3 and DoQ.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
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

- `DnsClientX.DnsTransportCapabilityInfo`

## RELATED LINKS

- None
