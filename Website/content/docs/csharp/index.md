---
title: "C# Usage"
description: "Use DnsClientX from application code with direct queries, typed records, and builder-based configuration."
layout: docs
slug: csharp
---

## Direct Query

```csharp
using DnsClientX;

using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("evotec.pl", DnsRecordType.A);

foreach (var answer in response.Answers) {
    Console.WriteLine($"{answer.Type}: {answer.Data}");
}
```

## Static Helper

```csharp
using DnsClientX;

var responses = await ClientX.QueryDns("evotec.pl", DnsRecordType.MX, DnsEndpoint.Google);

foreach (var response in responses) {
    foreach (var answer in response.Answers) {
        Console.WriteLine($"{answer.Type}: {answer.Data}");
    }
}
```

## Typed Records

```csharp
using DnsClientX;

using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("google.com", DnsRecordType.MX, typedRecords: true);

foreach (var answer in response.TypedAnswers ?? []) {
    Console.WriteLine(answer.GetType().Name);
}
```

## Builder With EDNS Client Subnet

```csharp
using DnsClientX;

using var client = new ClientXBuilder()
    .WithEndpoint(DnsEndpoint.Google)
    .WithEdnsOptions(new EdnsOptions {
        Subnet = new EdnsClientSubnetOption("203.0.113.0/24")
    })
    .Build();

var response = await client.Resolve(
    "evotec.pl",
    DnsRecordType.A,
    requestDnsSec: true,
    validateDnsSec: false);
```

## When To Open The API Docs

Use the generated [.NET API reference](/api/dnsclientx/) when you want signatures, namespaces, XML summaries, and source links for the exact types involved in your workflow.
