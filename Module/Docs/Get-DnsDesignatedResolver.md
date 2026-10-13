---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Get-DnsDesignatedResolver
## SYNOPSIS
Discovers encrypted resolvers designated by an explicit IP DNS resolver.

Returns bounded RFC 9462 candidates with explicit certificate verification outcomes.
Discovery does not switch resolvers. Only a verified, unexpired candidate can create an encrypted configuration.

## SYNTAX
### Server (Default)
```powershell
Get-DnsDesignatedResolver [-Server] <ipaddress> [-Port <int>] [-Tcp] [-TimeOut <int>] [-Options <DnsDesignatedResolverDiscoveryOptions>] [<CommonParameters>]
```

### Configuration
```powershell
Get-DnsDesignatedResolver -Configuration <Configuration> [-Options <DnsDesignatedResolverDiscoveryOptions>] [<CommonParameters>]
```

## DESCRIPTION
Discovers encrypted resolvers designated by an explicit IP DNS resolver.

Returns bounded RFC 9462 candidates with explicit certificate verification outcomes.
Discovery does not switch resolvers. Only a verified, unexpired candidate can create an encrypted configuration.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DnsDesignatedResolver -Server 192.0.2.53 | Select-Object Binding,Verification,VerificationError,ExpiresAt
```

Discover and inspect designated resolvers.

### EXAMPLE 2
```powershell
$candidate = Get-DnsDesignatedResolver -Server 192.0.2.53 | Where-Object Verification -eq Verified | Select-Object -First 1
if ($null -eq $candidate) { throw 'No verified designated resolver was found.' }
Resolve-Dns -Name example.com -Configuration ($candidate.CreateConfiguration())
```

Use a verified designation for a query.

## PARAMETERS

### -Configuration
Explicit IP UDP/TCP resolver configuration to use for discovery.

```yaml
Type: Configuration
Parameter Sets: Configuration
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Options
Core discovery bounds, ALPN preferences, and certificate trust policy.

```yaml
Type: DnsDesignatedResolverDiscoveryOptions
Parameter Sets: Server, Configuration
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
Original resolver's DNS port.

```yaml
Type: Int32
Parameter Sets: Server
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
Original UDP or TCP resolver's IP address.

```yaml
Type: IPAddress
Parameter Sets: Server
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tcp
Use TCP for discovery DNS queries instead of UDP.

```yaml
Type: SwitchParameter
Parameter Sets: Server
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeOut
Timeout for discovery DNS queries, in milliseconds.

```yaml
Type: Int32
Parameter Sets: Server
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

- `DnsClientX.DnsDesignatedResolver`

## RELATED LINKS

- None
