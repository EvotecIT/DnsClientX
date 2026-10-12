---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Test-DnsHealth
## SYNOPSIS
Runs a repeatable DNS resolver health profile.

Runs the profile's DNS query catalog and policy against its configured resolvers. IncludeBenchmark measures the same catalog. Returns the complete report; resolver agreement alone does not establish authoritative correctness.

## SYNTAX
### Path (Default)
```powershell
Test-DnsHealth [-Path] <string> [-IncludeBenchmark] [-FailOnPolicyViolation] [-DnsSecVerifierPath <string>] [<CommonParameters>]
```

### Profile
```powershell
Test-DnsHealth [-Profile] <ResolverHealthProfile> [-IncludeBenchmark] [-FailOnPolicyViolation] [-DnsSecVerifierPath <string>] [<CommonParameters>]
```

## DESCRIPTION
Runs a repeatable DNS resolver health profile.

Runs the profile's DNS query catalog and policy against its configured resolvers. IncludeBenchmark measures the same catalog. Returns the complete report; resolver agreement alone does not establish authoritative correctness.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-DnsHealth -Path './dns-health.json' -FailOnPolicyViolation -ErrorAction Stop
```

Run a saved profile and fail an automation run when its policy fails

### EXAMPLE 2
```powershell
Test-DnsHealth -Path './dns-health.json' -IncludeBenchmark
```

Include performance measurements for the same profile

## PARAMETERS

### -DnsSecVerifierPath
Local optional DNSSEC verifier DLL with dependencies beside it.

```yaml
Type: String
Parameter Sets: Path, Profile
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FailOnPolicyViolation
Write a PolicyViolation error on failed health gates. Use ErrorAction Stop for enforcement; the default only warns.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Profile
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeBenchmark
Benchmark the same DNS names, types and resolver targets after the health queries.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Profile
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Local JSON health profile path.

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

### -Profile
Typed health profile supplied directly or through the pipeline.

```yaml
Type: ResolverHealthProfile
Parameter Sets: Profile
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

- `DnsClientX.ResolverHealthProfile`

## OUTPUTS

- `DnsClientX.ResolverHealthReport`

## RELATED LINKS

- None
