---
title: "DnsClientX - Modern DNS Client for .NET and PowerShell"
description: "Use DnsClientX from C#, PowerShell, or a browser playground to inspect DNS answers, DNSSEC behavior, provider differences, and resolver transports."
layout: home
slug: index
---

## Start In The Right Place

<div class="imo-api-card-grid">
  <article class="imo-api-card" style="--api-accent: #38bdf8;">
    <div class="imo-api-card__header">
      <h3>DnsClientX for .NET</h3>
      <span class="imo-badge">Library</span>
    </div>
    <p class="imo-api-card__desc">Use the native library when you want typed DNS records, resolver selection, EDNS options, DNSSEC, and automation-friendly response objects inside your application.</p>
    <p class="imo-api-card__best">Best for: services, CLI tooling, diagnostics, background jobs, and higher-level products like DomainDetective.</p>
    <div class="imo-api-card__links">
      <a href="/docs/csharp/" class="imo-api-card__primary">C# Guide</a>
      <a href="/api/dnsclientx/" class="imo-api-card__secondary">.NET API Reference</a>
    </div>
  </article>
  <article class="imo-api-card" style="--api-accent: #f59e0b;">
    <div class="imo-api-card__header">
      <h3>DnsClientX for PowerShell</h3>
      <span class="imo-badge">Module</span>
    </div>
    <p class="imo-api-card__desc">Use the PowerShell module when the DNS workflow belongs in scripts, GitHub Actions, validation jobs, or ad-hoc troubleshooting.</p>
    <p class="imo-api-card__best">Best for: automation, CI checks, incident response, and resolver comparisons without writing a compiled app.</p>
    <div class="imo-api-card__links">
      <a href="/docs/powershell/" class="imo-api-card__primary">PowerShell Guide</a>
      <a href="/api/powershell/" class="imo-api-card__secondary">Cmdlet Reference</a>
    </div>
  </article>
</div>

## Why DnsClientX

- It gives you one product surface across UDP, TCP, DNS over HTTPS, DNS over TLS, DNS over QUIC, DNSSEC-aware workflows, and multi-resolver strategies.
- It already powers the kind of DNS exploration experience people expect from sites like Google Public DNS, but with first-class .NET and PowerShell entry points.
- It fits naturally into the DomainDetective story: browser-native exploration for humans, strong APIs for code, and automation-ready cmdlets for operations.

## What You Can Explore Here

- Use the [playground](/playground/) to pick a record type, switch public DNS JSON providers, inspect answers, and see the equivalent DnsClientX code.
- Read the [getting started guide](/docs/getting-started/) when you want the shortest path from install to first lookup.
- Jump into the generated [API docs](/api/) when you already know which library or cmdlet you want.
- Open [downloads](/downloads/) when you just need package-manager commands and release links.

## Quick Start

```bash
dotnet add package DnsClientX
```

```powershell
Install-Module DnsClientX
```

```csharp
using DnsClientX;

using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("evotec.pl", DnsRecordType.A);

foreach (var answer in response.Answers) {
    Console.WriteLine($"{answer.Type}: {answer.Data}");
}
```

```powershell
Resolve-Dns -Name 'evotec.pl' -Type A -DnsProvider Cloudflare | Format-Table
```
