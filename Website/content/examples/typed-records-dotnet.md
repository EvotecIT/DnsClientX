---
title: "Use typed records in .NET"
description: "Read DNS query results as typed .NET record objects."
layout: docs
---

This pattern is useful when application code needs structured DNS data instead of raw strings.

It is adapted from `DnsClientX.Examples/DemoTypedRecords.cs`.

## Example

```csharp
using DnsClientX;

using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("example.com", DnsRecordType.A, typedRecords: true);

foreach (var record in response.TypedAnswers ?? [])
{
    Console.WriteLine(record);
}
```

## What this demonstrates

- using the .NET client directly
- enabling typed records for safer downstream processing
- keeping the example independent of private zones

## Source

- [DemoTypedRecords.cs](https://github.com/EvotecIT/DnsClientX/blob/master/DnsClientX.Examples/DemoTypedRecords.cs)

