---
title: "DnsClientX for PowerShell"
description: "A PowerShell module for DNS lookups, provider comparison, DNSSEC-aware queries, full response inspection, and automation-friendly DNS workflows."
layout: product
slug: powershell
install: "Install-Module DnsClientX"
package_label: "PowerShell Gallery"
package_url: "https://www.powershellgallery.com/packages/DnsClientX"
docs_url: "/docs/powershell/"
api_url: "/api/powershell/"
api_label: "Cmdlet Reference"
product_color: "#f59e0b"
---

## What It Gives You

- `Resolve-Dns` for everyday DNS lookups and multi-provider checks.
- Full response inspection when you need questions, answers, authority, and additional records.
- Resolver strategies for comparing providers or choosing the fastest winner.
- A script-first entry point for CI, diagnostics, and ad-hoc troubleshooting.

## Typical Usage

```powershell
Resolve-Dns -Name 'evotec.pl' -Type A -DnsProvider Cloudflare | Format-Table
```

```powershell
Resolve-Dns -Name 'evotec.pl' -Type MX -DnsProvider Cloudflare,Google -ResolverStrategy FirstSuccess | Format-Table
```

## Recommended Next Steps

- Start with the [PowerShell guide](/docs/powershell/).
- Browse the generated [cmdlet reference](/api/powershell/).
- Open the [playground](/playground/) when you want to test a domain before scripting it.
