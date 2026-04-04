---
title: "Installation"
description: "Install DnsClientX from NuGet or the PowerShell Gallery."
layout: docs
slug: installation
---

## .NET

```bash
dotnet add package DnsClientX
```

- NuGet package: [DnsClientX on NuGet](https://www.nuget.org/packages/DnsClientX)
- Release history: [GitHub Releases](https://github.com/EvotecIT/DnsClientX/releases)

## PowerShell

```powershell
Install-Module DnsClientX
```

- PowerShell Gallery: [DnsClientX on PowerShell Gallery](https://www.powershellgallery.com/packages/DnsClientX)
- Example scripts: [Module/Examples](https://github.com/EvotecIT/DnsClientX/tree/main/Module/Examples)

## Repository Structure

- `DnsClientX/` contains the core library.
- `DnsClientX.PowerShell/` contains the PowerShell cmdlets and generated help.
- `Module/Examples/` contains script examples used by the website API-doc sync flow.
- `Website/` contains the PowerForge website, generated API landing pages, and the playground.
