---
title: "Install DnsClientX"
description: "Install DnsClientX for PowerShell or .NET."
layout: docs
---

Install the PowerShell module:

```powershell
Install-Module -Name DnsClientX -Scope CurrentUser
Import-Module DnsClientX
```

Use the .NET library from NuGet:

```powershell
dotnet add package DnsClientX
```

The PowerShell module is the quickest way to test resolver behavior interactively. The .NET package is the better fit when DNS queries are part of an application or service.

