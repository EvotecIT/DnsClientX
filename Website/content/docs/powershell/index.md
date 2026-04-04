---
title: "PowerShell Usage"
description: "Use DnsClientX from PowerShell for direct lookups, provider comparisons, and full response inspection."
layout: docs
slug: powershell
---

## Basic Query

```powershell
Resolve-Dns -Name 'evotec.pl' -Type A -DnsProvider Cloudflare | Format-Table
```

## Compare Providers

```powershell
Resolve-Dns -Name 'evotec.pl' -Type A -DnsProvider Cloudflare,Google -ResolverStrategy FirstSuccess | Format-Table
```

## Full Response

```powershell
$response = Resolve-Dns -Name 'evotec.pl' -Type MX -DnsProvider Cloudflare -FullResponse

$response.Questions | Format-Table
$response.Answers | Format-Table
$response.Authority | Format-Table
$response.Additional | Format-Table
```

## DNSSEC-Aware Query

```powershell
Resolve-Dns -Name 'evotec.pl' -Type DS -DnsProvider Cloudflare -RequestDnsSec -ValidateDnsSec | Format-Table
```

## Direct Resolver Endpoints

```powershell
Resolve-Dns -Name 'evotec.pl' -Type TXT -ResolverEndpoint '1.1.1.1:53','https://dns.google/dns-query' -ResolverStrategy FirstSuccess | Format-Table
```

## When To Open The API Docs

Use the generated [PowerShell cmdlet reference](/api/powershell/) when you want parameter-level details, examples, and the current help generated from the module source.
