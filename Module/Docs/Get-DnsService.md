---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# Get-DnsService
## SYNOPSIS
Retrieves services advertised via DNS Service Discovery.

Streams individual DNS-SD instances under a domain, browses one service type, or resolves SRV targets in connection-attempt order. Empty discovery produces no pipeline objects; use an array expression to collect zero or more results.

## SYNTAX
### Discover (Default)
```powershell
Get-DnsService [-Domain] <string> [-DnsProvider <DnsEndpoint>] [-ResolverEndpoint <string>] [-ResolverSelectionPath <string>] [-TimeOut <int>] [-BootstrapResolver <string>] [<CommonParameters>]
```

### Browse
```powershell
Get-DnsService [-Domain] <string> -Service <string> [-Protocol <string>] [-DnsProvider <DnsEndpoint>] [-ResolverEndpoint <string>] [-ResolverSelectionPath <string>] [-TimeOut <int>] [-BootstrapResolver <string>] [<CommonParameters>]
```

### Resolve
```powershell
Get-DnsService [-Domain] <string> -Service <string> -Resolve [-Protocol <string>] [-ResolveHosts] [-DnsProvider <DnsEndpoint>] [-ResolverEndpoint <string>] [-ResolverSelectionPath <string>] [-TimeOut <int>] [-BootstrapResolver <string>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves services advertised via DNS Service Discovery.

Streams individual DNS-SD instances under a domain, browses one service type, or resolves SRV targets in connection-attempt order. Empty discovery produces no pipeline objects; use an array expression to collect zero or more results.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DnsService -Domain example.com
```

Discover services under a domain

### EXAMPLE 2
```powershell
Get-DnsService -Domain example.com | Select-Object ServiceName,Target,Port
```

Pipe results to select useful fields

### EXAMPLE 3
```powershell
Get-DnsService -Domain example.com -Service http -ResolverEndpoint 'https://dns.google/dns-query'
```

Browse HTTP service instances through an explicit resolver

### EXAMPLE 4
```powershell
Get-DnsService -Domain example.com -Service ldap -Resolve -ResolveHosts
```

Resolve LDAP targets and addresses in connection-attempt order

## PARAMETERS

### -BootstrapResolver
IP-literal UDP/TCP resolver used to bootstrap endpoint hostnames.

```yaml
Type: String
Parameter Sets: Discover, Browse, Resolve
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DnsProvider
Built-in resolver used for discovery. Defaults to the system resolver.

```yaml
Type: DnsEndpoint
Parameter Sets: Discover, Browse, Resolve
Aliases: None
Possible values: System, SystemTcp, Cloudflare, CloudflareSecurity, CloudflareFamily, CloudflareWireFormat, CloudflareWireFormatPost, CloudflareJsonPost, Google, GoogleWireFormat, GoogleWireFormatPost, GoogleJsonPost, Quad9, Quad9ECS, Quad9Unsecure, OpenDNS, OpenDNSFamily, CloudflareQuic, Quad9Http3, Quad9Quic, GoogleQuic, AdGuard, AdGuardFamily, AdGuardNonFiltering, NextDNS, DnsCryptCloudflare, DnsCryptQuad9, DnsCryptRelay, RootServer, CloudflareOdoh, Custom

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Domain
Domain name to search for advertised services.

```yaml
Type: String
Parameter Sets: Discover, Browse, Resolve
Aliases: Name, DnsName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -Protocol
Service transport label, such as tcp or udp.

```yaml
Type: String
Parameter Sets: Browse, Resolve
Aliases: None
Possible values: tcp, udp, _tcp, _udp

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Resolve
Resolve SRV targets in RFC 2782 connection-attempt order instead of browsing DNS-SD instances.

```yaml
Type: SwitchParameter
Parameter Sets: Resolve
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolveHosts
With Resolve, include A and AAAA addresses for each target host.

```yaml
Type: SwitchParameter
Parameter Sets: Resolve
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverEndpoint
One explicit resolver, such as udp@192.0.2.53:53, tcp@192.0.2.53:53, mdns@224.0.0.251:5353, or a DoH URL.

```yaml
Type: String
Parameter Sets: Discover, Browse, Resolve
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResolverSelectionPath
Saved resolver score snapshot whose recommended resolver is reused for discovery.

```yaml
Type: String
Parameter Sets: Discover, Browse, Resolve
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Service
Service label to browse or resolve, such as http or _http.

```yaml
Type: String
Parameter Sets: Browse, Resolve
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeOut
Per-query timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: Discover, Browse, Resolve
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

- `System.String`

## OUTPUTS

- `DnsClientX.DnsService`
- `DnsClientX.DnsSrvRecord`

## RELATED LINKS

- None
