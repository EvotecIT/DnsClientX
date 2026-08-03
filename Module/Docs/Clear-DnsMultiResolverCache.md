---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Clear-DnsMultiResolverCache
## SYNOPSIS
Clears the multi-resolver fastest-endpoint cache used by the FastestWins strategy.

Clears the in-memory cache used to remember the fastest endpoint for a given set of endpoints. Use this when you change network conditions or want to force re-probing. Does not affect TTL-based response caching, which expires automatically.

## SYNTAX
### All (Default)
```powershell
Clear-DnsMultiResolverCache [-All] [<CommonParameters>]
```

### ResolverEndpoint
```powershell
Clear-DnsMultiResolverCache -ResolverEndpoint <string[]> [<CommonParameters>]
```

### ResolverDnsProvider
```powershell
Clear-DnsMultiResolverCache -ResolverDnsProvider <DnsEndpoint[]> [<CommonParameters>]
```

### DnsProvider
```powershell
Clear-DnsMultiResolverCache -DnsProvider <DnsEndpoint[]> [<CommonParameters>]
```

## DESCRIPTION
Clears the multi-resolver fastest-endpoint cache used by the FastestWins strategy.

Clears the in-memory cache used to remember the fastest endpoint for a given set of endpoints. Use this when you change network conditions or want to force re-probing. Does not affect TTL-based response caching, which expires automatically.

## EXAMPLES

### EXAMPLE 1
```powershell
Clear-DnsMultiResolverCache -All
```


### EXAMPLE 2
```powershell
Clear-DnsMultiResolverCache -DnsProvider @('Value')
```


### EXAMPLE 3
```powershell
Clear-DnsMultiResolverCache -ResolverDnsProvider @('Value')
```


## PARAMETERS

### -All
Clear all cached fastest-endpoint choices.

```yaml
Type: SwitchParameter
Parameter Sets: All
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsProvider
Alternate parameter: providers to clear when using the classic -DnsProvider parameter style.

```yaml
Type: DnsEndpoint[]
Parameter Sets: DnsProvider
Aliases: None
Possible values: System, SystemTcp, Cloudflare, CloudflareSecurity, CloudflareFamily, CloudflareWireFormat, CloudflareWireFormatPost, CloudflareJsonPost, Google, GoogleWireFormat, GoogleWireFormatPost, GoogleJsonPost, Quad9, Quad9ECS, Quad9Unsecure, OpenDNS, OpenDNSFamily, CloudflareQuic, Quad9Http3, Quad9Quic, GoogleQuic, AdGuard, AdGuardFamily, AdGuardNonFiltering, NextDNS, DnsCryptCloudflare, DnsCryptQuad9, DnsCryptRelay, RootServer, CloudflareOdoh, Custom

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverDnsProvider
Clear cache entries for the specified predefined providers (DnsEndpoint enum).

```yaml
Type: DnsEndpoint[]
Parameter Sets: ResolverDnsProvider
Aliases: None
Possible values: System, SystemTcp, Cloudflare, CloudflareSecurity, CloudflareFamily, CloudflareWireFormat, CloudflareWireFormatPost, CloudflareJsonPost, Google, GoogleWireFormat, GoogleWireFormatPost, GoogleJsonPost, Quad9, Quad9ECS, Quad9Unsecure, OpenDNS, OpenDNSFamily, CloudflareQuic, Quad9Http3, Quad9Quic, GoogleQuic, AdGuard, AdGuardFamily, AdGuardNonFiltering, NextDNS, DnsCryptCloudflare, DnsCryptQuad9, DnsCryptRelay, RootServer, CloudflareOdoh, Custom

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpoint
Specific endpoints to clear, e.g. "1.1.1.1:53", "[2606:4700:4700::1111]:53", or DoH URLs.

```yaml
Type: String[]
Parameter Sets: ResolverEndpoint
Aliases: None
Possible values:

Required: True
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

- `None`

## RELATED LINKS

- None
