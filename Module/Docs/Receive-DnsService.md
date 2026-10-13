---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Receive-DnsService
## SYNOPSIS
Continuously reports additions, changes, goodbyes and expirations of multicast DNS services.

Uses a bounded persistent RFC 6762 multicast browse session. Stopping the pipeline
cancels and drains its receiver. Each output is an independent service snapshot with its observed change.

## SYNTAX
### __AllParameterSets
```powershell
Receive-DnsService [-Service] <string> [-Protocol] <string> [-Domain <string>] [-Configuration <Configuration>] [-Options <DnsServiceBrowseOptions>] [<CommonParameters>]
```

## DESCRIPTION
Continuously reports additions, changes, goodbyes and expirations of multicast DNS services.

Uses a bounded persistent RFC 6762 multicast browse session. Stopping the pipeline
cancels and drains its receiver. Each output is an independent service snapshot with its observed change.

## EXAMPLES

### EXAMPLE 1
```powershell
Receive-DnsService -Service http -Protocol tcp | Select-Object Change,Service,Addresses,ObservedAt
```

Watch HTTP services on the local multicast network.

## PARAMETERS

### -Configuration
Optional multicast configuration with the desired interface and port.

```yaml
Type: Configuration
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Domain
Domain to browse. The default is local.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Options
Core cache, instance and output-buffer resource bounds.

```yaml
Type: DnsServiceBrowseOptions
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Protocol
Transport label, with or without its leading underscore.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Service
Service label, with or without its leading underscore.

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

- `DnsClientX.DnsServiceBrowseEvent`

## RELATED LINKS

- None
