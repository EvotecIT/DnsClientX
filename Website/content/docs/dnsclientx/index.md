---
title: "DnsClientX .NET Guide"
description: "Start here when you want the conceptual guide that matches the generated DnsClientX .NET API reference."
layout: docs
slug: dnsclientx
---

This page is the conceptual landing point for the generated [DnsClientX .NET API reference](/api/dnsclientx/).

Use it when you want the higher-level workflow before you dive into individual types:

- Read the main [C# usage guide](/docs/csharp/) for practical examples.
- Open the [.NET API reference](/api/dnsclientx/) for signatures, summaries, and source links.
- Try the [playground](/playground/) if you want to inspect a live answer set before writing code.

## Recommended Starting Example

```csharp
using DnsClientX;

using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("evotec.pl", DnsRecordType.A);

foreach (var answer in response.Answers) {
    Console.WriteLine($"{answer.Type}: {answer.Data}");
}
```
