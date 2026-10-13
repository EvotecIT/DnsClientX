---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Compare-DnsResolverSnapshot
## SYNOPSIS
Compares resolver health and answers between saved DNS snapshots.

Reports candidate and answer changes, health regressions, and incompatible query scopes. Optional authoritative responses provide comparison context; a majority answer is not proof of authoritative correctness.

## SYNTAX
### Path (Default)
```powershell
Compare-DnsResolverSnapshot [-BaselinePath] <string> [-CurrentPath] <string> [-AuthoritativeReference <ResolverAnswerReference[]>] [<CommonParameters>]
```

### Snapshot
```powershell
Compare-DnsResolverSnapshot [-Baseline] <ResolverScoreSnapshot> [-Current] <ResolverScoreSnapshot> [-AuthoritativeReference <ResolverAnswerReference[]>] [<CommonParameters>]
```

## DESCRIPTION
Compares resolver health and answers between saved DNS snapshots.

Reports candidate and answer changes, health regressions, and incompatible query scopes. Optional authoritative responses provide comparison context; a majority answer is not proof of authoritative correctness.

## EXAMPLES

### EXAMPLE 1
```powershell
Compare-DnsResolverSnapshot -BaselinePath './before.json' -CurrentPath './after.json'
```

Compare two saved probe or benchmark snapshots

## PARAMETERS

### -AuthoritativeReference
Caller-supplied authoritative responses for name/type comparison context. These must assert the AA flag; response origin remains the caller's responsibility.

```yaml
Type: ResolverAnswerReference[]
Parameter Sets: Path, Snapshot
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Baseline
Baseline typed resolver score snapshot.

```yaml
Type: ResolverScoreSnapshot
Parameter Sets: Snapshot
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BaselinePath
Baseline resolver score snapshot JSON path.

```yaml
Type: String
Parameter Sets: Path
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Current
Current typed resolver score snapshot, supplied directly or through the pipeline.

```yaml
Type: ResolverScoreSnapshot
Parameter Sets: Snapshot
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -CurrentPath
Current resolver score snapshot JSON path.

```yaml
Type: String
Parameter Sets: Path
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `DnsClientX.ResolverScoreSnapshot`

## OUTPUTS

- `DnsClientX.ResolverScoreComparison`

## RELATED LINKS

- None
