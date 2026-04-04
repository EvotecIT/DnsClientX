---
title: "DnsClientX for .NET"
description: "A modern DNS client library for .NET with typed records, multiple transports, DNSSEC-aware workflows, EDNS support, and multi-resolver strategies."
layout: product
slug: dnsclientx
install: "dotnet add package DnsClientX"
package_label: "NuGet Package"
package_url: "https://www.nuget.org/packages/DnsClientX"
docs_url: "/docs/csharp/"
api_url: "/api/dnsclientx/"
api_label: ".NET API Reference"
product_color: "#38bdf8"
---

## What It Gives You

- Async queries for common and advanced DNS record types.
- Resolver selection across system DNS, DoH, DoT, DoQ, and more.
- Typed records when you want structured objects instead of raw strings.
- EDNS support including client subnet scenarios.
- Multi-resolver strategies when you want to compare or race providers.

## Typical Usage

```csharp
using DnsClientX;

using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("evotec.pl", DnsRecordType.MX, typedRecords: true);

foreach (var answer in response.Answers) {
    Console.WriteLine($"{answer.Type}: {answer.Data}");
}
```

## Recommended Next Steps

- Start with the [C# guide](/docs/csharp/).
- Browse the generated [.NET API reference](/api/dnsclientx/).
- Use the [playground](/playground/) when you want to inspect a live answer set first.
