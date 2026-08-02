---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Get-DnsResolverSelection
## SYNOPSIS
Loads a saved resolver score snapshot and returns the recommended resolver selection.

Reads a persisted resolver score snapshot, applies the shared recommendation logic, and returns either the structured selection object or the raw target string for automation use.

## SYNTAX
### __AllParameterSets
```powershell
Get-DnsResolverSelection [-Path] <string> [-AsString] [<CommonParameters>]
```

## DESCRIPTION
Loads a saved resolver score snapshot and returns the recommended resolver selection.

Reads a persisted resolver score snapshot, applies the shared recommendation logic, and returns either the structured selection object or the raw target string for automation use.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DnsResolverSelection -Path 'C:\Path'
```


## PARAMETERS

### -AsString
Emits only the raw selected target string instead of the structured selection object.

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

### -Path
Path to the saved resolver score snapshot.

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

- `DnsClientX.ResolverSelectionResult`
- `System.String`

## RELATED LINKS

- None
