---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Find-DnsService
## SYNOPSIS
Discovers services advertised via DNS Service Discovery.

Wraps DiscoverServices to return services under a domain.

## SYNTAX
### __AllParameterSets
```powershell
Find-DnsService [-Domain] <string> [<CommonParameters>]
```

## DESCRIPTION
Discovers services advertised via DNS Service Discovery.

Wraps DiscoverServices to return services under a domain.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-DnsService -Domain 'Value'
```


## PARAMETERS

### -Domain
Domain name to search for advertised services.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- None
