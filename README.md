# DnsClientX - DnsClient for .NET and PowerShell

DnsClientX is available as NuGet from the Nuget Gallery and as PowerShell module from PSGallery

### 📦 NuGet Package

<p align="center">

[![nuget downloads](https://img.shields.io/nuget/dt/DnsClientX?label=nuget%20downloads)](https://www.nuget.org/packages/DnsClientX)
[![nuget version](https://img.shields.io/nuget/v/DnsClientX)](https://www.nuget.org/packages/DnsClientX)

</p>

### 💻 PowerShell Module

<p align="center">

[![powershell gallery version](https://img.shields.io/powershellgallery/v/DnsClientX.svg)](https://www.powershellgallery.com/packages/DnsClientX)
[![powershell gallery preview](https://img.shields.io/powershellgallery/vpre/DnsClientX.svg?label=powershell%20gallery%20preview&colorB=yellow)](https://www.powershellgallery.com/packages/DnsClientX)
[![powershell gallery platforms](https://img.shields.io/powershellgallery/p/DnsClientX.svg)](https://www.powershellgallery.com/packages/DnsClientX)
[![powershell gallery downloads](https://img.shields.io/powershellgallery/dt/DnsClientX.svg)](https://www.powershellgallery.com/packages/DnsClientX)

</p>

### 🛠️ Project Information

<p align="center">

[![top language](https://img.shields.io/github/languages/top/evotecit/DnsClientX.svg)](https://github.com/EvotecIT/DnsClientX)
[![license](https://img.shields.io/github/license/EvotecIT/DnsClientX.svg)](https://github.com/EvotecIT/DnsClientX)

</p>

### 👨‍💻 Author & Social

<p align="center">

[![Twitter follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Threads](https://img.shields.io/badge/Threads-@PrzemyslawKlys-000000.svg?logo=Threads&logoColor=White)](https://www.threads.net/@przemyslaw.klys)

</p>

## What it's all about

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
> We try to unify the responses as much as possible for common use cases by translating on the fly. This is because different providers do not store it always the same way. If you find discrepancies please **open an issue** or better **pull request**.

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

| Platform | Status | Test Report | Code Coverage | .NET |
| -------- | ------ | ----------- | ------------- | ---- |
| Windows  | [![Tests Windows](https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/30/master?compact_message&style=flat&label=Tests%20Windows)](https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=30) | [![Test Analytics](https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/30/master?compact_message&style=flat&label=Test%20Analytics)](https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=30&contextType=build) | [![Coverage](https://img.shields.io/azure-devops/coverage/evotecpl/DnsClientX/30)](https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=30&view=ms.vss-pipelineanalytics-web.new-build-definition-pipeline-analytics-view-cardmetrics) | .NET 4.7.2, NET 4.8, .NET 6.0, .NET 7.0, .NET 8.0, .NET Standard 2.0 |
| Linux    | [![Tests Linux](https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/31/master?compact_message&style=flat&label=Tests%20Linux)](https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=31) | [![Test Analytics](https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/31/master?compact_message&style=flat&label=Test%20Analytics)](https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=31&contextType=build) |  | .NET 6.0, .NET 7.0, .NET Standard 2.0, .NET 8.0 |
| MacOs    | [![Tests MacOs](https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/32/master?compact_message&style=flat&label=Tests%20MacOs)](https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=32) | [![Test Analytics](https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/32/master?compact_message&style=flat&label=Test%20Analytics)](https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=32&contextType=build) |  | .NET 6.0, .NET 7.0, .NET Standard 2.0, .NET 8.0 |

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
- [x] Implements IDisposable to release cached HttpClient resources
- [x] Multi-line record data normalized to use `\n` line endings

## Understanding DNS Query Behavior

### Different Results from Different Providers

When querying DNS records using DnsClientX, you may notice that different DNS providers can return different results for the same domain. This is **normal behavior** and occurs for several legitimate reasons:

#### Content Delivery Networks (CDNs)

Many popular websites use CDNs (like Cloudflare, Akamai, AWS CloudFront) to serve content from servers geographically closer to users. CDN-backed domains will return different IP addresses based on:

- **Geographic location**: Providers route queries to the nearest edge server
- **Provider routing policies**: Different DNS providers may have different relationships with CDNs
- **Load balancing**: CDNs dynamically distribute traffic across multiple servers

**Example**: A domain like `www.example.com` might return:
- From Cloudflare: `23.47.124.71`, `23.47.124.85`
- From OpenDNS: `185.225.251.105`, `185.225.251.40`

Both responses are **correct** - they're just optimized for different network paths.

#### DNS Provider Characteristics

Different DNS providers have distinct characteristics:

- **Cloudflare (1.1.1.1)**: Privacy-focused, fast, global Anycast network
- **Google (8.8.8.8)**: Extensive caching, Google's global infrastructure
- **Quad9 (9.9.9.9)**: Security-focused, blocks malicious domains
- **OpenDNS**: Content filtering options, enterprise features

These differences can result in:
- Varying response times (typically 10-500ms)
- Different cached TTL values
- Different IP addresses for CDN domains
- Slightly different DNSSEC validation results

#### When to Expect Consistent Results

You should expect **consistent results** for:
- **Non-CDN domains**: Simple domains with static IP assignments
- **Infrastructure domains**: DNS servers, mail servers, etc.
- **Record types other than A/AAAA**: TXT, MX, NS, DS records are usually consistent

#### When to Expect Different Results

You should expect **different results** for:
- **CDN-backed websites**: Major websites, cloud services, media platforms
- **Geographically distributed services**: Global services with regional presence
- **Load-balanced applications**: Services with multiple server endpoints

### Performance Considerations

#### Response Times

DNS query response times can vary significantly:
- **Local/ISP DNS**: 1-50ms (but may have outdated records)
- **Public DNS providers**: 10-200ms (usually more up-to-date)
- **International queries**: 100-500ms (depending on geographic distance)

#### Timeout and Retry Behavior

DnsClientX implements intelligent timeout and retry logic:
- **Default timeout**: 1000ms (1 second) - optimized for fast responses
- **Automatic retry**: Failed queries are retried with exponential backoff
- **Provider fallback**: Can automatically switch between providers
- **Protocol fallback**: UDP → TCP → HTTPS/TLS as needed

### Best Practices for Testing

When testing DNS resolution:

1. **Use stable domains** for consistency tests (e.g., `google.com`, `github.com`)
2. **Use CDN domains** to test geographic/provider differences
3. **Test multiple record types** (A, AAAA, TXT, MX, NS, DS)
4. **Allow for reasonable response time variation** (50-500ms)
5. **Validate structure, not exact content** for CDN domains

### Troubleshooting Common Issues

#### "Different IP addresses returned"
- ✅ **Normal for CDN domains** - indicates proper geographic optimization
- ⚠️ **Investigate for non-CDN domains** - may indicate DNS propagation issues

#### "Slow response times"
- Check network connectivity to the DNS provider
- Consider using geographically closer DNS servers
- Verify firewall/proxy settings aren't interfering

#### "Intermittent failures"
- Enable retry logic and exponential backoff
- Test with multiple DNS providers
- Check for rate limiting or blocking

This behavior is by design and reflects the modern, distributed nature of internet infrastructure. DnsClientX provides tools to work effectively with this reality while maintaining reliable DNS resolution.

## System DNS Fallback Mechanism

DnsClientX provides robust system DNS resolution through `DnsEndpoint.System` (UDP) and `DnsEndpoint.SystemTcp` (TCP) endpoints. These endpoints automatically discover and use your system's configured DNS servers with intelligent cross-platform fallback behavior.

### How System DNS Discovery Works

#### 1. Windows and Cross-Platform (.NET)
**Primary Method**: Network Interface Detection
- Enumerates all active network interfaces using `NetworkInterface.GetAllNetworkInterfaces()`
- **Prioritizes interfaces with default gateways** (internet-connected interfaces)
- Extracts DNS server addresses from interface properties
- Filters out invalid addresses (link-local, multicast, etc.)
- **Fallback**: If no DNS servers found from gateway interfaces, checks all active interfaces

#### 2. Unix/Linux Systems
**Fallback Method**: `/etc/resolv.conf` Parsing
- If network interface enumeration fails or returns no results
- Reads and parses `/etc/resolv.conf` file
- Extracts `nameserver` entries
- Validates IP addresses and formats them properly
- Handles both IPv4 and IPv6 addresses

#### 3. Final Safety Net
**Public DNS Fallback**: If no system DNS servers are discovered
- **Cloudflare Primary**: `1.1.1.1`
- **Google Primary**: `8.8.8.8`
- Ensures DNS resolution always works, even in misconfigured environments

### Address Validation and Formatting

The system applies intelligent filtering to ensure reliable DNS servers:

#### IPv4 Filtering
- ✅ **Valid**: Public and private IP ranges
- ❌ **Filtered**: Link-local addresses (`169.254.x.x`)
- ❌ **Filtered**: Loopback addresses

#### IPv6 Filtering
- ✅ **Valid**: Global and unique local addresses
- ❌ **Filtered**: Link-local addresses (`fe80::`)
- ❌ **Filtered**: Multicast addresses
- ❌ **Filtered**: Site-local addresses (`fec0::` - deprecated)
- **Auto-formatting**: Removes zone identifiers (`%15`) and adds brackets (`[::1]`)

### Protocol Support

#### System UDP (`DnsEndpoint.System`)
- **Primary protocol**: DNS over UDP (port 53)
- **Automatic fallback**: Switches to TCP when UDP packet size limit exceeded
- **Timeout**: 1000ms default (configurable)
- **Best for**: General DNS queries, fastest response times

#### System TCP (`DnsEndpoint.SystemTcp`)
- **Primary protocol**: DNS over TCP (port 53)
- **Connection management**: Efficient connection pooling
- **Timeout**: 1000ms default (configurable)
- **Best for**: Large responses, firewall-restricted environments

### Platform-Specific Behavior

| Platform | Primary Method | Fallback Method | Final Fallback |
|----------|---------------|-----------------|----------------|
| **Windows** | Network Interface APIs | *(Not applicable)* | Public DNS |
| **Linux** | Network Interface APIs | `/etc/resolv.conf` | Public DNS |
| **macOS** | Network Interface APIs | `/etc/resolv.conf` | Public DNS |
| **Docker/Container** | Network Interface APIs | `/etc/resolv.conf` | Public DNS |

### Example Usage

```csharp
// Use system DNS with UDP (auto-fallback to TCP)
var response = await ClientX.QueryDns("google.com", DnsRecordType.A, DnsEndpoint.System);

// Use system DNS with TCP only
var response = await ClientX.QueryDns("google.com", DnsRecordType.A, DnsEndpoint.SystemTcp);

// Get system DNS servers programmatically (cached)
var systemDnsServers = SystemInformation.GetDnsFromActiveNetworkCard();
// Refresh the cache when network configuration changes
var refreshedDnsServers = SystemInformation.GetDnsFromActiveNetworkCard(refresh: true);
```

### Advantages of System DNS

1. **Respects local configuration**: Uses DNS servers configured by network admin/DHCP
2. **Corporate environment friendly**: Works with internal DNS servers and split-horizon DNS
3. **VPN compatibility**: Automatically uses VPN-provided DNS servers
4. **No external dependencies**: Works even when public DNS is blocked
5. **Platform native**: Leverages OS-specific network configuration

### Troubleshooting System DNS

#### Common Issues and Solutions

**"No DNS servers found"**
- Check network connectivity (`ipconfig /all` on Windows, `cat /etc/resolv.conf` on Linux)
- Verify network interfaces are up and have gateways
- Falls back to public DNS (1.1.1.1, 8.8.8.8) automatically

**"Slow response times"**
- System DNS may be slower than public DNS providers
- Consider using specific endpoints like `DnsEndpoint.Cloudflare` for better performance
- Check if system DNS servers are geographically distant

**"Resolution failures in containers"**
- Ensure container has proper network configuration
- Docker containers should inherit host DNS or have DNS configured
- `/etc/resolv.conf` should be readable in Linux containers

The system DNS endpoints provide the most compatible and network-environment-aware DNS resolution, making them excellent default choices for applications that need to work across diverse network configurations.

## TO DO

> [!IMPORTANT]
> This library is still in development and there are things that need to be done, tested and fixed.
> If you would like to help, please do so by opening an issue or a pull request.
> Things may and will change, as I'm not quite sure what I am doing :-)

- [ ] [Add more providers](https://dnscrypt.info/public-servers/)
- [ ] Add more tests
- [ ] Go thru all additional parameters and make sure they have proper responses

## Usage in .NET

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

### Customizing HTTP settings

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare, userAgent: "MyApp/1.0", httpVersion: new Version(1, 1));
```
You can also modify `client.EndpointConfiguration.UserAgent` and `client.EndpointConfiguration.HttpVersion` after construction.

### Querying DS record with DNSSEC

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
var ds = await client.Resolve("evotec.pl", DnsRecordType.DS, requestDnsSec: true);
ds.DisplayToConsole();
```

### Querying DNS over HTTPS via defined endpoint using ResolveAll

```csharp
using var client = new ClientX(DnsEndpoint.OpenDNS);
var data = await client.ResolveAll(domainName, type);
```
Because `ClientX` implements `IDisposable`, wrapping it in a `using` statement ensures internal `HttpClient` instances are released.

### Querying DNS over HTTPS with single endpoint using ResolveAll

```csharp
using var client = new ClientX(DnsEndpoint.OpenDNS);
var data = await client.ResolveAll(domainName, type);
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
    DnsRecordType.DS,
    DnsRecordType.DNSKEY,
    DnsRecordType.NSEC
};

foreach (var endpoint in dnsEndpoints) {
    if (excludeEndpoints.Contains(endpoint)) {
        continue; // Skip this iteration if the endpoint is in the exclude list
    }

    // Create a new client for each endpoint
    using var client = new ClientX(endpoint) {
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

## Usage in PowerShell

DnsClientX is also available as a PowerShell module. Below are some examples.

```powershell
Resolve-DnsQuery -Name 'evotec.pl' -Type A | Format-Table
Resolve-DnsQuery -Name 'evotec.pl' -Type A -DnsProvider Cloudflare -Verbose | Format-Table
Resolve-DnsQuery -Name 'evotec.pl' -Type TXT -DnsProvider System -Verbose | Format-Table
Resolve-DnsQuery -Name 'evotec.pl' -Type DS -DnsProvider Cloudflare -Verbose | Format-Table
Resolve-DnsQuery -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT -DnsProvider System -Verbose | Format-Table
```

It can also deliver more detailed information.

```powershell
$Output = Resolve-DnsQuery -Name '_25._tcp.mail.ietf.org' -Type TLSA -DnsProvider Cloudflare -Verbose -FullResponse
$Output.Questions | Format-Table
$Output.AnswersMinimal | Format-Table

$Output = Resolve-DnsQuery -Name 'evotec.pl' -Type DS -DnsProvider Cloudflare -Verbose -FullResponse
$Output.Questions | Format-Table
$Output.AnswersMinimal | Format-Table

$Output = Resolve-DnsQuery -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT -DnsProvider Google -Verbose -FullResponse
$Output.Questions | Format-Table
$Output.AnswersMinimal | Format-Table

$Output = Resolve-DnsQuery -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT -DnsProvider Cloudflare -Verbose -FullResponse
$Output.Questions | Format-Table
$Output.AnswersMinimal | Format-Table

$Output = Resolve-DnsQuery -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT, A -Verbose -Server "192.168.241.5" -FullResponse
$Output.Questions | Format-Table
$Output.AnswersMinimal | Format-Table

$Output = Resolve-DnsQuery -Name 'evotec.pl' -Type A -Server '1.1.1.1','8.8.8.8' -Fallback
$Output.AnswersMinimal | Format-Table

$Output = Resolve-DnsQuery -Name 'evotec.pl' -Type A -Server '1.1.1.1','8.8.8.8' -Fallback -RandomServer
$Output.AnswersMinimal | Format-Table
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