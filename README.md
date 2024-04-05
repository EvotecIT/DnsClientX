# DnsClientX - DnsClient for .NET and PowerShell

DnsClientX is available as NuGet from the Nuget Gallery and as PowerShell module from PSGallery

<p align="center">
  <a href="https://www.nuget.org/packages/DnsClientX"><img src="https://img.shields.io/nuget/dt/DnsClientX?label=nuget%20downloads" alt="nuget downloads"></a>
  <a href="https://www.nuget.org/packages/DnsClientX"><img src="https://img.shields.io/nuget/v/DnsClientX" alt="nuget version"></a>
</p>

<p align="center">
  <!-- <a href="https://dev.azure.com/evotecpl/DnsClientX/_build/latest?definitionId=3"><img src="https://dev.azure.com/evotecpl/DnsClientX/_apis/build/status/EvotecIT.DnsClientX"></a> -->
  <a href="https://www.powershellgallery.com/packages/DnsClientX"><img src="https://img.shields.io/powershellgallery/v/DnsClientX.svg"></a>
  <a href="https://www.powershellgallery.com/packages/DnsClientX"><img src="https://img.shields.io/powershellgallery/vpre/DnsClientX.svg?label=powershell%20gallery%20preview&colorB=yellow"></a>
  <a href="https://www.powershellgallery.com/packages/DnsClientX"><img src="https://img.shields.io/powershellgallery/p/DnsClientX.svg"></a>
  <a href="https://www.powershellgallery.com/packages/DnsClientX"><img src="https://img.shields.io/powershellgallery/dt/DnsClientX.svg"></a>
</p>

<p align="center">
  <a href="https://github.com/EvotecIT/DnsClientX"><img src="https://img.shields.io/github/languages/top/evotecit/DnsClientX.svg"></a>
  <a href="https://github.com/EvotecIT/DnsClientX"><img src="https://img.shields.io/github/license/EvotecIT/DnsClientX.svg"></a>
</p>

<p align="center">
  <a href="https://twitter.com/PrzemyslawKlys"><img src="https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social"></a>
  <a href="https://evotec.xyz/hub"><img src="https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg"></a>
  <a href="https://www.linkedin.com/in/pklys"><img src="https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn"></a>
  <a href="https://www.threads.net/@przemyslaw.klys"><img src="https://img.shields.io/badge/Threads-@PrzemyslawKlys-000000.svg?logo=Threads&logoColor=White"></a>
</p>

## What it's all about

<img width="256" height="256" align=right src="https://raw.githubusercontent.com/EvotecIT/DnsClientX/master/Assets/Icons/DnsClientX3.png">

**DnsClientX** is an async C# library for DNS over UDP, TCP, HTTPS (DoH), and TLS (DoT). It also has a PowerShell module that can be used to query DNS records. It provides a simple way to query DNS records using multiple DNS providers. It supports multiple DNS record types and parallel queries. It's available for .NET 6, .NET 7, .NET 8, .NET Standard 2.0, and .NET 4.7.2.

It provides querying multiple DNS Providers.
- [Cloudflare](https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/)
- Google
- [Quad9](https://www.quad9.net/news/blog/doh-with-quad9-dns-servers/)
- OpenDNS
- etc.

If you want to learn about DNS:
- https://www.cloudflare.com/learning/dns/what-is-dns/

> [!WARNING]
> We try to unify the responses as much as possible for common use cases by translating on the fly. This is because different providers do not store it always the same way. If you find disprepencies please **open an issue** or better **pull request**.

## Supported .NET Versions

This library supports multiple NET versions:
- .NET 6
  - No dependencies
- .NET 7
  - No dependencies
- .NET 8
  - No dependencies
- .NET Standard 2.0
  - System.Text.Json
- .NET 4.7.2
  - System.Text.Json

## Build Status

| Platform | Status                                                                                                                                                                                                              | Test Report                                                                                                                                                                                                                                   | Code Coverage                                                                                                                                                                                                                                             | .NET                                                                 |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| Windows  | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=30"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/30/master?compact_message&style=flat&label=Tests%20Windows"></a> | <a href="https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=30&contextType=build"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/30/master?compact_message&style=flat&label=Test Analytics"></a> | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=30&view=ms.vss-pipelineanalytics-web.new-build-definition-pipeline-analytics-view-cardmetrics"><img src="https://img.shields.io/azure-devops/coverage/evotecpl/DnsClientX/30"></a> | .NET 4.7.2, NET 4.8, .NET 6.0, .NET 7.0, .NET 8.0, .NET Standard 2.0 |
| Linux    | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=31"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/31/master?compact_message&style=flat&label=Tests%20Linux"></a>   | <a href="https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=31&contextType=build"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/31/master?compact_message&style=flat&label=Test Analytics"></a> |                                                                                                                                                                                                                                                           | .NET 6.0, .NET 7.0, .NET Standard 2.0, .NET 8.0                      |
| MacOs    | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=32"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/32/master?compact_message&style=flat&label=Tests%20MacOs"></a>   | <a href="https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=32&contextType=build"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/32/master?compact_message&style=flat&label=Test Analytics"></a> |                                                                                                                                                                                                                                                           | .NET 6.0, .NET 7.0, .NET Standard 2.0, .NET 8.0                      |
## Features

- [x] Supports multiple built-in DNS Providers (System, Cloudflare, Google, Quad9, OpenDNS, etc.)
- [x] Supports both JSON and Wireformat
- [x] Supports DNS over HTTPS (DoH) using GET and POST methods
- [x] Supports DNS over TLS (DoT)
- [x] Supports DNS over UDP, and switches to TCP if needed
- [x] Supports DNS over TCP
- [x] Supports DNSSEC
- [x] Supports multiple DNS record types
- [x] Supports parallel queries
- [x] No external dependencies on .NET 6, .NET 7 and .NET 8
- [x] Minimal dependencies on .NET Standard 2.0 and .NET 4.7.2

## TO DO

> [!IMPORTANT]
> This library is still in development and there are things that need to be done, tested and fixed.
> If you would like to help, please do so by opening an issue or a pull request.
> Things may and will change, as I'm not quite sure what I am doing :-)

- [ ] [Add more providers](https://dnscrypt.info/public-servers/)
- [ ] Add more tests
- [ ] Go thru all additional parameters and make sure they have proper responses


## Usage

There are multiple ways to use DnsClientX.

```csharp
using DnsClientX;
```

Below are some examples.

### Querying DNS over HTTPS via provided hostname that uses /dns-query endpoint and JSON format

```csharp
var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, "1.1.1.1", DnsRequestFormat.JSON);
data.Answers
```

### Querying DNS over HTTPS via defined endpoint using QueryDns

```csharp
var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareWireFormat);
data.Answers
```

### Querying DNS over HTTPS via full Uri using QueryDNS and JSON format

```csharp
var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
data.Answers
```

### Querying DNS over HTTPS via defined endpoint using ResolveAll

```csharp
var Client = new ClientX(DnsEndpoint.OpenDNS);
var data = await Client.ResolveAll(domainName, type);
data
```

### Querying DNS over HTTPS with single endpoint using ResolveAll

```csharp
var Client = new ClientX(DnsEndpoint.OpenDNS);
var data = await Client.ResolveAll(domainName, type);
data
```

### Querying DNS over HTTPS with multiple endpoints using Resolve

```csharp
var dnsEndpoints = new List<DnsEndpoint> {
    DnsEndpoint.Cloudflare,
    DnsEndpoint.CloudflareSecurity,
    DnsEndpoint.CloudflareFamily,
    DnsEndpoint.CloudflareWireFormat,
    DnsEndpoint.Google,
    DnsEndpoint.Quad9,
    DnsEndpoint.Quad9ECS,
    DnsEndpoint.Quad9Unsecure,
    DnsEndpoint.OpenDNS,
    DnsEndpoint.OpenDNSFamily
};

// List of endpoints to exclude
var excludeEndpoints = new List<DnsEndpoint> {

};

var domains = new List<string> {
    "github.com",
    "microsoft.com",
    "evotec.xyz"
};

// List of record types to query
var recordTypes = new List<DnsRecordType> {
    DnsRecordType.A,
    DnsRecordType.TXT,
    DnsRecordType.AAAA,
    DnsRecordType.MX,
    DnsRecordType.NS,
    DnsRecordType.SOA,
    DnsRecordType.DNSKEY,
    DnsRecordType.NSEC
};

foreach (var endpoint in dnsEndpoints) {
    if (excludeEndpoints.Contains(endpoint)) {
        continue; // Skip this iteration if the endpoint is in the exclude list
    }

    // Create a new client for each endpoint
    var client = new ClientX(endpoint) {
        Debug = false
    };

    foreach (var domain in domains) {
        foreach (var recordType in recordTypes) {
            DnsResponse? response = await client.Resolve(domain, recordType);
            response.DisplayToConsole();
        }
    }
}
```

## Please share with the community

Please consider sharing a post about DnsClientX and the value it provides. It really does help!

[![Share on reddit](https://img.shields.io/badge/share%20on-reddit-red?logo=reddit)](https://reddit.com/submit?url=https://github.com/EvotecIT/DnsClientX&title=DnsClientX)
[![Share on hacker news](https://img.shields.io/badge/share%20on-hacker%20news-orange?logo=ycombinator)](https://news.ycombinator.com/submitlink?u=https://github.com/EvotecIT/DnsClientX)
[![Share on twitter](https://img.shields.io/badge/share%20on-twitter-03A9F4?logo=twitter)](https://twitter.com/share?url=https://github.com/EvotecIT/DnsClientX&t=DnsClientX)
[![Share on facebook](https://img.shields.io/badge/share%20on-facebook-1976D2?logo=facebook)](https://www.facebook.com/sharer/sharer.php?u=https://github.com/EvotecIT/DnsClientX)
[![Share on linkedin](https://img.shields.io/badge/share%20on-linkedin-3949AB?logo=linkedin)](https://www.linkedin.com/shareArticle?url=https://github.com/EvotecIT/DnsClientX&title=DnsClientX)

## Credits

This project general idea is based on [DnsOverHttps](https://github.com/actually-akac/DnsOverHttps) by [@akac](https://github.com/actually-akac) which was an inspiration for **DnsClientX**.

## Other libraries

- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) - DnsClient is a simple yet very powerful and high performant open source library for the .NET Framework to do DNS lookups. If you need standard DNS support - this one is for you.
- [DnsOverHttps](https://github.com/actually-akac/DnsOverHttps) - DnsOverHttps is a simple yet very powerful and high performant open source library for the .NET Framework to do DNS lookups over HTTPS using Cloudflare. If you only need Cloudflare support and target newer .NET versions - this one is for you.
- [DinoDNS](https://github.com/TurnerSoftware/DinoDNS) - another DNS library with a lot of features.
