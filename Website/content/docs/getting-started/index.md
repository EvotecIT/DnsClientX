---
title: "Getting Started"
description: "Install DnsClientX, run the first query, and pick the right API surface for your workflow."
layout: docs
slug: getting-started
---

## Choose Your Entry Point

- Use `.NET` when DnsClientX is part of an app, service, library, CLI, or a bigger product like DomainDetective.
- Use PowerShell when the work belongs in scripts, automation, validation, or operational diagnostics.
- Use the [playground](/playground/) when you want to explore a domain and its record types in the browser first.

## First Query In C#

```csharp
using DnsClientX;

using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("evotec.pl", DnsRecordType.A);

foreach (var answer in response.Answers) {
    Console.WriteLine($"{answer.Type}: {answer.Data}");
}
```

## First Query In PowerShell

```powershell
Resolve-Dns -Name 'evotec.pl' -Type A -DnsProvider Cloudflare | Format-Table
```

## Where To Go Next

- [Installation](/docs/getting-started/installation/) for package-manager commands.
- [C# Usage](/docs/csharp/) for library patterns and examples.
- [PowerShell Usage](/docs/powershell/) for cmdlet-driven workflows.
