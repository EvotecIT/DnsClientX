---
title: "Resolve DNS from PowerShell"
description: "Resolve DNS records through selected providers and transports."
layout: docs
---

This pattern is useful when you need a quick, repeatable resolver check.

It is adapted from `DnsClientX.PowerShell/CmdletResolveDnsQuery.cs`.

## Example

```powershell
Import-Module DnsClientX

Resolve-Dns -Name 'example.com' -Type A -DnsProvider Cloudflare

Resolve-Dns -Name 'example.com' -Type MX -DnsProvider Cloudflare, Google -ResolverStrategy FirstSuccess

Resolve-Dns -Name 'example.com' -Type TXT -ResolverEndpoint '1.1.1.1:53', 'https://dns.google/dns-query' -ResolverStrategy FirstSuccess
```

## What this demonstrates

- selecting a DNS provider explicitly
- querying multiple providers with a resolver strategy
- mixing endpoint styles without hardcoding internal resolver addresses

## Source

- [CmdletResolveDnsQuery.cs](https://github.com/EvotecIT/DnsClientX/blob/master/DnsClientX.PowerShell/CmdletResolveDnsQuery.cs)

