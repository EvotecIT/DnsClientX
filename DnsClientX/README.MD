# DnsClientX - DnsClient for .NET and PowerShell

**DnsClientX** is an async C# library for DNS over UDP, TCP, HTTPS (DoH), and TLS (DoT). It also has a PowerShell module that can be used to query DNS records. It provides a simple way to query DNS records using multiple DNS providers. It supports multiple DNS record types and parallel queries. It's available for .NET 6, .NET 7, .NET 8, .NET Standard 2.0, and .NET 4.7.2.

It provides querying multiple DNS Providers.
- [Cloudflare](https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/)
- Google
- [Quad9](https://www.quad9.net/news/blog/doh-with-quad9-dns-servers/)
- OpenDNS
- etc.

If you want to learn about DNS:
- https://www.cloudflare.com/learning/dns/what-is-dns/

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