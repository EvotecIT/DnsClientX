---
external help file: DnsClientX-help.xml
Module Name: DnsClientX
online version: https://github.com/EvotecIT/DnsClientX/blob/master/README.md
schema: 2.0.0
---
# New-DnsPrivacyTransport
## SYNOPSIS
Creates a caller-owned optional DNS privacy transport from a trusted local provider.

Loads the selected provider DLL and its dependencies, then creates a transport from a DNSCrypt resolver stamp or an ODoH target and relay. Loading a DLL executes trusted local code. Dispose clients before disposing the transport.

Pass the transport's CreateConfiguration() result to Resolve-Dns -Configuration. DNSCrypt accepts an optional anonymizing relay stamp; ODoH requires an explicit relay endpoint or stamp. This command does not publish or install packages.

## SYNTAX
### __AllParameterSets
```powershell
New-DnsPrivacyTransport -ProviderPath <string> -Resolver <string> [-Relay <string>] [<CommonParameters>]
```

## DESCRIPTION
Creates a caller-owned optional DNS privacy transport from a trusted local provider.

Loads the selected provider DLL and its dependencies, then creates a transport from a DNSCrypt resolver stamp or an ODoH target and relay. Loading a DLL executes trusted local code. Dispose clients before disposing the transport.

Pass the transport's CreateConfiguration() result to Resolve-Dns -Configuration. DNSCrypt accepts an optional anonymizing relay stamp; ODoH requires an explicit relay endpoint or stamp. This command does not publish or install packages.

## EXAMPLES

### EXAMPLE 1
```powershell
$transport = New-DnsPrivacyTransport -ProviderPath ./DnsClientX.Odoh.dll -Resolver https://target.example/dns-query -Relay https://relay.example/dns-query
try { Resolve-Dns example.com -Configuration $transport.CreateConfiguration() -FullResponse } finally { $transport.Dispose() }
```

Query through an explicitly selected ODoH relay and target

## PARAMETERS

### -ProviderPath
Trusted local provider DLL path, with dependencies beside it.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Relay
Optional DNSCrypt relay stamp; required ODoH relay stamp/HTTPS endpoint.

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

### -Resolver
Trusted DNSCrypt resolver stamp or ODoH target stamp/HTTPS endpoint.

```yaml
Type: String
Parameter Sets: __AllParameterSets
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

- `DnsClientX.IDnsWireTransportProvider`

## RELATED LINKS

- None
