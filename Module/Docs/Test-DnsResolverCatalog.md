---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Test-DnsResolverCatalog
## SYNOPSIS
Validates resolver endpoint catalog inputs without querying DNS.

Checks inline resolver endpoints, resolver endpoint files, and resolver endpoint URLs using the same parser used by probe and benchmark workflows.

## SYNTAX
### __AllParameterSets
```powershell
Test-DnsResolverCatalog [-ResolverEndpoint <string[]>] [-ResolverEndpointFile <string[]>] [-ResolverEndpointUrl <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Validates resolver endpoint catalog inputs without querying DNS.

Checks inline resolver endpoints, resolver endpoint files, and resolver endpoint URLs using the same parser used by probe and benchmark workflows.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-DnsResolverCatalog -ResolverEndpoint @('Value')
```


## PARAMETERS

### -ResolverEndpoint
Inline resolver endpoint entries.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ResolverEndpointFile
Files containing resolver endpoint entries.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpointUrl
HTTP or HTTPS URLs containing resolver endpoint entries.

```yaml
Type: String[]
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

- `System.String[]`

## OUTPUTS

- `DnsClientX.ResolverEndpointValidationResult`

## RELATED LINKS

- None
