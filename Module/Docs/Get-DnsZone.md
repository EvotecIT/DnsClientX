---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Get-DnsZone
## SYNOPSIS
Performs a DNS zone transfer (AXFR) for a given zone.

Retrieves all records for the specified zone using TCP based zone transfer.

## SYNTAX
### ExplicitServer (Default)
```powershell
Get-DnsZone [-Zone] <string> [-Server] <string> [-Port <int>] [<CommonParameters>]
```

### Recursive
```powershell
Get-DnsZone [-Zone] <string> [[-Server] <string>] -Recursive [-Port <int>] [-DnsProvider <DnsEndpoint>] [-IncludeTransferSummary] [<CommonParameters>]
```

## DESCRIPTION
Performs a DNS zone transfer (AXFR) for a given zone.

Retrieves all records for the specified zone using TCP based zone transfer.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DnsZone -DnsProvider 'Value'
```


### EXAMPLE 2
```powershell
Get-DnsZone -Recursive
```


## PARAMETERS

### -DnsProvider
Resolver profile used to discover authoritative servers when Recursive is specified and no explicit discovery server is provided.

```yaml
Type: DnsEndpoint
Parameter Sets: Recursive
Aliases: None
Possible values: System, SystemTcp, Cloudflare, CloudflareSecurity, CloudflareFamily, CloudflareWireFormat, CloudflareWireFormatPost, CloudflareJsonPost, Google, GoogleWireFormat, GoogleWireFormatPost, GoogleJsonPost, Quad9, Quad9ECS, Quad9Unsecure, OpenDNS, OpenDNSFamily, CloudflareQuic, Quad9Http3, Quad9Quic, GoogleQuic, AdGuard, AdGuardFamily, AdGuardNonFiltering, NextDNS, DnsCryptCloudflare, DnsCryptQuad9, DnsCryptRelay, RootServer, CloudflareOdoh, Custom

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeTransferSummary
Emit the recursive transfer summary object before the transferred RRsets.

```yaml
Type: SwitchParameter
Parameter Sets: Recursive
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
Port number to use. Defaults to 53.

```yaml
Type: Int32
Parameter Sets: ExplicitServer, Recursive
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recursive
Discover authoritative servers first and attempt AXFR against them in order.

```yaml
Type: SwitchParameter
Parameter Sets: Recursive
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
DNS server to query.

```yaml
Type: String
Parameter Sets: ExplicitServer, Recursive
Aliases: ServerName
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Zone
The zone to transfer.

```yaml
Type: String
Parameter Sets: ExplicitServer, Recursive
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
