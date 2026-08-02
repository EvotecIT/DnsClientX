---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# ConvertFrom-DnsStamp
## SYNOPSIS
Parses a DNS stamp into a resolver endpoint description.

Converts a supported sdns:// DNS stamp into the same endpoint model used by DnsClientX resolver workflows. This command does not perform a DNS query.

## SYNTAX
### __AllParameterSets
```powershell
ConvertFrom-DnsStamp [-Stamp] <string> [<CommonParameters>]
```

## DESCRIPTION
Parses a DNS stamp into a resolver endpoint description.

Converts a supported sdns:// DNS stamp into the same endpoint model used by DnsClientX resolver workflows. This command does not perform a DNS query.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-DnsStamp -Stamp 'Value'
```


## PARAMETERS

### -Stamp
DNS stamp to parse.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
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

- `System.String`

## OUTPUTS

- `DnsClientX.DnsStampInfo`

## RELATED LINKS

- None
