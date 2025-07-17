# DnsClientX - Modern DNS Client for .NET and PowerShell

DnsClientX is available as NuGet from the Nuget Gallery and as PowerShell module from PSGallery

📦 NuGet Package

[![nuget downloads](https://img.shields.io/nuget/dt/DnsClientX?label=nuget%20downloads)](https://www.nuget.org/packages/DnsClientX)
[![nuget version](https://img.shields.io/nuget/v/DnsClientX)](https://www.nuget.org/packages/DnsClientX)

💻 PowerShell Module

[![powershell gallery version](https://img.shields.io/powershellgallery/v/DnsClientX.svg)](https://www.powershellgallery.com/packages/DnsClientX)
[![powershell gallery preview](https://img.shields.io/powershellgallery/vpre/DnsClientX.svg?label=powershell%20gallery%20preview&colorB=yellow)](https://www.powershellgallery.com/packages/DnsClientX)
[![powershell gallery platforms](https://img.shields.io/powershellgallery/p/DnsClientX.svg)](https://www.powershellgallery.com/packages/DnsClientX)
[![powershell gallery downloads](https://img.shields.io/powershellgallery/dt/DnsClientX.svg)](https://www.powershellgallery.com/packages/DnsClientX)

🛠️ Project Information

[![top language](https://img.shields.io/github/languages/top/evotecit/DnsClientX.svg)](https://github.com/EvotecIT/DnsClientX)
[![license](https://img.shields.io/github/license/EvotecIT/DnsClientX.svg)](https://github.com/EvotecIT/DnsClientX)
[![codecov](https://codecov.io/gh/EvotecIT/DnsClientX/branch/main/graph/badge.svg)](https://codecov.io/gh/EvotecIT/DnsClientX)

👨‍💻 Author & Social

[![Twitter follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Threads](https://img.shields.io/badge/Threads-@PrzemyslawKlys-000000.svg?logo=Threads&logoColor=White)](https://www.threads.net/@przemyslaw.klys)
[![Discord](https://img.shields.io/discord/508328927853281280?style=flat-square&label=discord%20chat)](https://evo.yt/discord)

## What it's all about

**DnsClientX** is an async C# library for DNS over UDP, TCP, HTTPS (DoH), and TLS (DoT). It also has a PowerShell module that can be used to query DNS records. It provides a simple way to query DNS records using multiple DNS providers. It supports multiple DNS record types and parallel queries. It's available for .NET 8, .NET Standard 2.0, and .NET 4.7.2.

## Response Format Options: Typed vs Non-Typed Records

DnsClientX gives you **two ways** to work with DNS query results, depending on your needs:

### 🔤 **Non-Typed Records (Default)**
Returns DNS data as **raw strings** - simple and straightforward:
```csharp
var response = await ClientX.QueryDns("google.com", DnsRecordType.MX, DnsEndpoint.Cloudflare);
foreach (var answer in response.Answers) {
    Console.WriteLine($"Raw data: {answer.Data}"); // "10 smtp.google.com"
}
```

### 🎯 **Typed Records (Strongly-Typed)**
Automatically parses DNS data into **strongly-typed .NET objects** with properties:
```csharp
var response = await client.Resolve("google.com", DnsRecordType.MX, typedRecords: true);
foreach (var answer in response.TypedAnswers!) {
    if (answer is MxRecord mx) {
        Console.WriteLine($"Mail server: {mx.Exchange}, Priority: {mx.Preference}");
    }
}
```

**When to use each approach:**
- **Non-typed**: Quick queries, simple data extraction, minimal processing
- **Typed**: Complex applications, structured data processing, type safety, IntelliSense support

Both approaches work with **all DNS record types** and **all query methods** - simply add `typedRecords: true` to any query to get strongly-typed results.

## Supported DNS Providers

It provides querying multiple DNS Providers.

| Endpoint                 | DoH | DoQ | DoT | UDP | TCP | DnsCrypt | ODoH |
| ------------------------ | --- | --- | --- | --- | --- | -------- | ---- |
| System                   |     |     |     | ✓   |     |          |      |
| SystemTcp                |     |     |     |     | ✓   |          |      |
| Cloudflare               | ✓   |     |     |     |     |          |      |
| CloudflareWireFormat     | ✓   |     |     |     |     |          |      |
| CloudflareWireFormatPost | ✓   |     |     |     |     |          |      |
| CloudflareSecurity       | ✓   |     |     |     |     |          |      |
| CloudflareFamily         | ✓   |     |     |     |     |          |      |
| CloudflareQuic           |     | ✓   |     |     |     |          |      |
| CloudflareOdoh           |     |     |     |     |     |          | ❌    |
| Google                   | ✓   |     |     |     |     |          |      |
| GoogleWireFormat         | ✓   |     |     |     |     |          |      |
| GoogleWireFormatPost     | ✓   |     |     |     |     |          |      |
| GoogleQuic               |     | ✓   |     |     |     |          |      |
| AdGuard                  | ✓   |     |     |     |     |          |      |
| AdGuardFamily            | ✓   |     |     |     |     |          |      |
| AdGuardNonFiltering      | ✓   |     |     |     |     |          |      |
| Quad9                    | ✓   |     |     |     |     |          |      |
| Quad9ECS                 | ✓   |     |     |     |     |          |      |
| Quad9Unsecure            | ✓   |     |     |     |     |          |      |
| OpenDNS                  | ✓   |     |     |     |     |          |      |
| OpenDNSFamily            | ✓   |     |     |     |     |          |      |
| DnsCryptCloudflare       |     |     |     |     |     | ❌        |      |
| DnsCryptQuad9            |     |     |     |     |     | ❌        |      |
| DnsCryptRelay            |     |     |     |     |     | ❌        |      |
| RootServer               |     |     |     | ✓   | ✓   |          |      |

If you want to learn about DNS:
- https://www.cloudflare.com/learning/dns/what-is-dns/

> [!WARNING]
> We try to unify the responses as much as possible for common use cases by translating on the fly. This is because different providers do not store it always the same way. If you find discrepancies please **open an issue** or better **pull request**.

## Supported .NET Versions and Dependencies

### Core Library (DnsClientX)
- **.NET 8.0 / .NET 9.0** (Windows, Linux, macOS)
  - No external dependencies
- **.NET Standard 2.0** (Cross-platform compatibility)
  - System.Net.Http (4.3.4)
  - System.Text.Json (8.0.5)
  - Microsoft.Bcl.AsyncInterfaces (8.0.0)
- **.NET Framework 4.7.2** (Windows only)
  - System.Net.Http (built-in)
  - System.Text.Json (8.0.5)
  - Microsoft.Bcl.AsyncInterfaces (8.0.0)

### Command Line Interface (DnsClientX.exe)
- **.NET 8.0 / .NET 9.0** (Windows, Linux, macOS)
- **.NET Framework 4.7.2** (Windows only)
- Single-file deployment supported

### PowerShell Module
- **.NET 8.0** (Cross-platform)
- **.NET Standard 2.0** (Windows PowerShell 5.1+ compatibility)
- **.NET Framework 4.7.2** (Windows PowerShell 5.1)
- PowerShellStandard.Library (5.1.1)

### Examples Project
- **.NET 8.0** only
- Spectre.Console (0.50.0) for enhanced console output

## Build Status

[![Test .NET](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-dotnet.yml/badge.svg)](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-dotnet.yml)
[![Test PowerShell](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-powershell.yml/badge.svg)](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-powershell.yml)
[![codecov](https://codecov.io/gh/EvotecIT/DnsClientX/branch/main/graph/badge.svg)](https://codecov.io/gh/EvotecIT/DnsClientX)

**Cross-Platform Testing:** All tests run simultaneously across Windows, Linux, and macOS to ensure compatibility.

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
- [x] No external dependencies on .NET 8
- [x] Minimal dependencies on .NET Standard 2.0 and .NET 4.7.2
- [x] Implements IDisposable to release cached HttpClient resources
- [x] Multi-line record data normalized to use `\n` line endings
- [x] Supports DNS Service Discovery (DNS-SD)

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

| Provider            | Hostname                            | Request Format |
| ------------------- | ----------------------------------- | -------------- |
| Cloudflare          | `1.1.1.1` / `1.0.0.1`               | JSON           |
| Google              | `8.8.8.8` / `8.8.4.4`               | JSON           |
| Quad9               | `dns.quad9.net`                     | Wire           |
| OpenDNS             | `208.67.222.222` / `208.67.220.220` | Wire           |
| AdGuard             | `dns.adguard.com`                   | Wire           |
| AdGuardFamily       | `dns-family.adguard.com`            | Wire           |
| AdGuardNonFiltering | `dns-unfiltered.adguard.com`        | Wire           |

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
- **Default timeout**: 2000ms (2 seconds) - optimized for fast responses
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
- **Timeout**: 2000ms default (configurable)
- **Best for**: General DNS queries, fastest response times

#### System TCP (`DnsEndpoint.SystemTcp`)
- **Primary protocol**: DNS over TCP (port 53)
- **Connection management**: Efficient connection pooling
- **Timeout**: 2000ms default (configurable)
- **Best for**: Large responses, firewall-restricted environments

### Platform-Specific Behavior

| Platform             | Primary Method         | Fallback Method    | Final Fallback |
| -------------------- | ---------------------- | ------------------ | -------------- |
| **Windows**          | Network Interface APIs | *(Not applicable)* | Public DNS     |
| **Linux**            | Network Interface APIs | `/etc/resolv.conf` | Public DNS     |
| **macOS**            | Network Interface APIs | `/etc/resolv.conf` | Public DNS     |
| **Docker/Container** | Network Interface APIs | `/etc/resolv.conf` | Public DNS     |

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

DnsClientX provides multiple ways to query DNS records with extensive customization options.

```csharp
using DnsClientX;
```

### Quick Start Examples

#### Basic DNS Query
```csharp
// Simple A record lookup using Cloudflare
var response = await ClientX.QueryDns("google.com", DnsRecordType.A, DnsEndpoint.Cloudflare);
foreach (var answer in response.Answers) {
    Console.WriteLine($"{answer.Name} -> {answer.Data}");
}
```

#### System DNS Query
```csharp
// Use system-configured DNS servers
var response = await ClientX.QueryDns("google.com", DnsRecordType.A, DnsEndpoint.System);
response.Answers.DisplayToConsole();
```

#### Multiple Record Types
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
var recordTypes = new[] { DnsRecordType.A, DnsRecordType.AAAA, DnsRecordType.MX };
var responses = await client.Resolve("google.com", recordTypes);
responses.DisplayTable();
```

### Client Configuration

#### Using ClientXBuilder
```csharp
using var client = new ClientXBuilder()
    .WithEndpoint(DnsEndpoint.Cloudflare)
    .WithTimeout(5000)
    .WithRetryCount(3)
    .WithUserAgent("MyApp/1.0")
    .Build();
```

#### Custom HTTP Settings
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare,
    userAgent: "MyApp/1.0",
    httpVersion: new Version(2, 0),
    maxConnectionsPerServer: 10);
```

#### Custom Endpoint Configuration
```csharp
using var client = new ClientX(DnsEndpoint.Custom);
client.EndpointConfiguration.Hostname = "1.1.1.1";
client.EndpointConfiguration.RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
client.EndpointConfiguration.BaseUri = new Uri("https://1.1.1.1/dns-query");
client.EndpointConfiguration.Port = 443;
```

### Advanced Query Methods

#### Typed Records
DnsClientX can automatically parse DNS records into strongly-typed objects, making it easier to work with structured data like SPF, DMARC, DKIM, and other complex record types.

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

// Get typed records instead of raw strings
var response = await client.Resolve("google.com", DnsRecordType.MX, typedRecords: true);

// Access strongly-typed properties
foreach (var typedAnswer in response.TypedAnswers!) {
    if (typedAnswer is MxRecord mx) {
        Console.WriteLine($"Mail Server: {mx.Exchange}, Priority: {mx.Preference}");
    }
}
```

**Available Typed Record Classes:**

| Record Type | Typed Class                                           | Key Properties                                                      |
| ----------- | ----------------------------------------------------- | ------------------------------------------------------------------- |
| A           | `ARecord`                                             | `Address` (IPAddress)                                               |
| AAAA        | `AAAARecord`                                          | `Address` (IPAddress)                                               |
| MX          | `MxRecord`                                            | `Exchange`, `Preference`                                            |
| SRV         | `SrvRecord`                                           | `Target`, `Port`, `Priority`, `Weight`                              |
| TXT         | `TxtRecord`, `SpfRecord`, `DmarcRecord`, `DkimRecord` | Parsed content                                                      |
| CAA         | `CaaRecord`                                           | `Flags`, `Tag`, `Value`                                             |
| TLSA        | `TlsaRecord`                                          | `CertificateUsage`, `Selector`, `MatchingType`, `AssociationData`   |
| SOA         | `SoaRecord`                                           | `PrimaryNameServer`, `ResponsibleEmail`, `Serial`, etc.             |
| CNAME       | `CNameRecord`                                         | `Target`                                                            |
| NS          | `NsRecord`                                            | `NameServer`                                                        |
| PTR         | `PtrRecord`                                           | `DomainName`                                                        |
| DNSKEY      | `DnsKeyRecord`                                        | `Flags`, `Protocol`, `Algorithm`, `PublicKey`                       |
| DS          | `DsRecord`                                            | `KeyTag`, `Algorithm`, `DigestType`, `Digest`                       |
| NAPTR       | `NaptrRecord`                                         | `Order`, `Preference`, `Flags`, `Services`, `Regexp`, `Replacement` |
| LOC         | `LocRecord`                                           | `Latitude`, `Longitude`, `Altitude`, `Size`, etc.                   |

**Specialized TXT Record Parsing:**

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

// SPF Records
var spfResponse = await client.Resolve("google.com", DnsRecordType.TXT, typedRecords: true);
foreach (var answer in spfResponse.TypedAnswers!) {
    if (answer is SpfRecord spf) {
        Console.WriteLine($"SPF Mechanisms: {string.Join(", ", spf.Mechanisms)}");
    }
}

// DMARC Records
var dmarcResponse = await client.Resolve("_dmarc.google.com", DnsRecordType.TXT, typedRecords: true);
foreach (var answer in dmarcResponse.TypedAnswers!) {
    if (answer is DmarcRecord dmarc) {
        Console.WriteLine($"DMARC Policy: {dmarc.Tags["p"]}");
        Console.WriteLine($"DMARC Percentage: {dmarc.Tags.GetValueOrDefault("pct", "100")}");
    }
}

// DKIM Records
var dkimResponse = await client.Resolve("default._domainkey.google.com", DnsRecordType.TXT, typedRecords: true);
foreach (var answer in dmarcResponse.TypedAnswers!) {
    if (answer is DkimRecord dkim) {
        Console.WriteLine($"DKIM Key Type: {dkim.Tags.GetValueOrDefault("k", "rsa")}");
        Console.WriteLine($"DKIM Public Key: {dkim.Tags["p"]}");
    }
}
```

**Working with Service Records:**

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

// SRV Records for service discovery
var srvResponse = await client.Resolve("_sip._tcp.example.com", DnsRecordType.SRV, typedRecords: true);
foreach (var answer in srvResponse.TypedAnswers!) {
    if (answer is SrvRecord srv) {
        Console.WriteLine($"Service: {srv.Target}:{srv.Port}");
        Console.WriteLine($"Priority: {srv.Priority}, Weight: {srv.Weight}");
    }
}

// TLSA Records for certificate validation
var tlsaResponse = await client.Resolve("_443._tcp.example.com", DnsRecordType.TLSA, typedRecords: true);
foreach (var answer in tlsaResponse.TypedAnswers!) {
    if (answer is TlsaRecord tlsa) {
        Console.WriteLine($"Certificate Usage: {tlsa.CertificateUsage}");
        Console.WriteLine($"Selector: {tlsa.Selector}");
        Console.WriteLine($"Matching Type: {tlsa.MatchingType}");
        Console.WriteLine($"Association Data: {tlsa.AssociationData}");
    }
}
```

**Certificate Authority Authorization (CAA):**

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

var caaResponse = await client.Resolve("google.com", DnsRecordType.CAA, typedRecords: true);
foreach (var answer in caaResponse.TypedAnswers!) {
    if (answer is CaaRecord caa) {
        Console.WriteLine($"CAA Tag: {caa.Tag}");
        Console.WriteLine($"CAA Value: {caa.Value}");
        Console.WriteLine($"Critical: {(caa.Flags & 128) != 0}");
    }
}
```

**Location Records:**

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

var locResponse = await client.Resolve("example.com", DnsRecordType.LOC, typedRecords: true);
foreach (var answer in locResponse.TypedAnswers!) {
    if (answer is LocRecord loc) {
        Console.WriteLine($"Location: {loc.Latitude:F6}, {loc.Longitude:F6}");
        Console.WriteLine($"Altitude: {loc.Altitude}m");
        Console.WriteLine($"Size: {loc.Size}m");
    }
}
```

**Controlling TXT Record Parsing:**

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

// Force all TXT records to be returned as simple TxtRecord objects
// instead of parsing into specialized types like SpfRecord, DmarcRecord, etc.
var response = await client.Resolve("google.com", DnsRecordType.TXT,
    typedRecords: true,
    typedTxtAsTxt: true);

foreach (var answer in response.TypedAnswers!) {
    if (answer is TxtRecord txt) {
        Console.WriteLine($"TXT Data: {string.Join(" ", txt.Strings)}");
    }
}
```

**Mixed Record Type Handling:**

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

var response = await client.Resolve("google.com",
    new[] { DnsRecordType.A, DnsRecordType.MX, DnsRecordType.TXT },
    typedRecords: true);

foreach (var answer in response.TypedAnswers!) {
    switch (answer) {
        case ARecord a:
            Console.WriteLine($"A Record: {a.Address}");
            break;
        case MxRecord mx:
            Console.WriteLine($"MX Record: {mx.Exchange} (Priority: {mx.Preference})");
            break;
        case SpfRecord spf:
            Console.WriteLine($"SPF Record: {string.Join(" ", spf.Mechanisms)}");
            break;
        case TxtRecord txt:
            Console.WriteLine($"TXT Record: {string.Join(" ", txt.Strings)}");
            break;
        case UnknownRecord unknown:
            Console.WriteLine($"Unknown Record: {unknown.Data}");
            break;
    }
}
```

#### DNSSEC Validation
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
var response = await client.Resolve("google.com", DnsRecordType.A,
    requestDnsSec: true,
    validateDnsSec: true);
Console.WriteLine($"DNSSEC Valid: {response.IsSecure}");
```

#### Pattern-Based Queries
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
// Query server1.example.com, server2.example.com, server3.example.com
var responses = await client.ResolvePattern("server[1-3].example.com", DnsRecordType.A);
responses.DisplayTable();
```

#### Filtered Queries
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
// Find TXT records containing "v=spf1"
var response = await client.ResolveFilter("google.com", DnsRecordType.TXT, "v=spf1");
response?.DisplayTable();
```

#### Parallel Queries Across Multiple Providers
```csharp
var endpoints = new[] {
    DnsEndpoint.Cloudflare,
    DnsEndpoint.Google,
    DnsEndpoint.Quad9
};

var tasks = endpoints.Select(async endpoint => {
    using var client = new ClientX(endpoint);
    return await client.Resolve("google.com", DnsRecordType.A);
}).ToArray();

var responses = await Task.WhenAll(tasks);
foreach (var response in responses) {
    response?.DisplayTable();
}
```

#### Streaming Queries
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
var domains = new[] { "google.com", "github.com", "microsoft.com" };
var recordTypes = new[] { DnsRecordType.A, DnsRecordType.AAAA };

await foreach (var response in client.ResolveStream(domains, recordTypes)) {
    Console.WriteLine($"Resolved: {response.Question.Name} ({response.Question.Type})");
    response.DisplayTable();
}
```

### Protocol-Specific Examples

#### DNS over HTTPS (DoH)
```csharp
// JSON format
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.Cloudflare);

// Wire format
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.CloudflareWireFormat);
```

#### DNS over QUIC (DoQ)
```csharp
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.CloudflareQuic);
```

#### DNS over TCP/UDP
```csharp
// System DNS with UDP (fallback to TCP)
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.System);

// Force TCP
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.SystemTcp);
```

#### Root Server Queries
```csharp
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.RootServer);
```

### Performance and Reliability Features

#### Latency Measurement
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
var latency = await client.MeasureLatencyAsync();
Console.WriteLine($"Latency: {latency.TotalMilliseconds}ms");
```

#### Retry Configuration
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare) {
    RetryCount = 3,
    Timeout = TimeSpan.FromSeconds(5)
};
```

#### Fallback Strategies
```csharp
var endpoints = new[] {
    DnsEndpoint.Cloudflare,
    DnsEndpoint.Google,
    DnsEndpoint.System
};

DnsResponse? response = null;
foreach (var endpoint in endpoints) {
    try {
        using var client = new ClientX(endpoint);
        response = await client.Resolve("google.com", DnsRecordType.A);
        break; // Success, exit loop
    } catch (Exception ex) {
        Console.WriteLine($"Failed with {endpoint}: {ex.Message}");
        // Continue to next endpoint
    }
}
```

### Service Discovery and Zone Operations

#### DNS Service Discovery (DNS-SD)
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);

// Discover all services in a domain
var services = await client.DiscoverServices("example.com");
foreach (var service in services) {
    Console.WriteLine($"Service: {service.ServiceName} -> {service.Target}:{service.Port}");
}

// Query specific service
var srvRecords = await client.ResolveServiceAsync("ldap", "tcp", "example.com", resolveHosts: true);
foreach (var srv in srvRecords) {
    Console.WriteLine($"Server: {srv.Target}:{srv.Port} (Priority: {srv.Priority})");
    if (srv.Addresses != null) {
        Console.WriteLine($"IPs: {string.Join(", ", srv.Addresses)}");
    }
}
```

#### Zone Transfer (AXFR)
```csharp
// Full zone transfer
using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
    EndpointConfiguration = { Port = 5353 }
};
var zoneRecords = await client.ZoneTransferAsync("example.com");
foreach (var rrset in zoneRecords) {
    Console.WriteLine($"Zone chunk {rrset.Index}: {string.Join(", ", rrset.Records)}");
}

// Streaming zone transfer
await foreach (var rrset in client.ZoneTransferStreamAsync("example.com")) {
    Console.WriteLine($"Received: {string.Join(", ", rrset)}");
}
```

#### DNS Updates
```csharp
using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
    EndpointConfiguration = { Port = 5353 }
};

// Add a record
await client.UpdateAsync("example.com", "www", DnsRecordType.A, "192.0.2.1", ttl: 300);

// Delete a record
await client.UpdateAsync("example.com", "www", DnsRecordType.A, delete: true);
```

### Multicast DNS (mDNS)
```csharp
using var client = new ClientX("224.0.0.251", DnsRequestFormat.Multicast) {
    EndpointConfiguration = { Port = 5353 }
};
var response = await client.Resolve("printer.local", DnsRecordType.A);
```

### Error Handling and Debugging

#### Debug Mode
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare) {
    Debug = true  // Enables detailed logging
};
```

#### Comprehensive Error Handling
```csharp
try {
    using var client = new ClientX(DnsEndpoint.Cloudflare);
    var response = await client.Resolve("nonexistent.domain", DnsRecordType.A);

    if (response.HasError) {
        Console.WriteLine($"DNS Error: {response.ErrorMessage}");
    } else if (!response.Answers.Any()) {
        Console.WriteLine("No records found");
    } else {
        response.DisplayTable();
    }
} catch (TimeoutException) {
    Console.WriteLine("Query timed out");
} catch (Exception ex) {
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## Usage in PowerShell

DnsClientX provides a comprehensive PowerShell module with multiple cmdlets for DNS operations.

### Installation

```powershell
# Install from PowerShell Gallery
Install-Module -Name DnsClientX -Scope CurrentUser

# Import the module
Import-Module DnsClientX
```

### Available Cmdlets

| Cmdlet             | Alias                 | Description                              |
| ------------------ | --------------------- | ---------------------------------------- |
| `Resolve-Dns`      | `Resolve-DnsQuery`    | Query DNS records with various providers |
| `Get-DnsService`   |                       | Discover DNS services (DNS-SD)           |
| `Find-DnsService`  |                       | Find specific DNS services               |
| `Get-DnsZone`      | `Get-DnsZoneTransfer` | Perform DNS zone transfers               |
| `Invoke-DnsUpdate` |                       | Update DNS records (Dynamic DNS)         |

### Quick Start Examples

#### Basic DNS Queries
```powershell
# Simple A record lookup
Resolve-Dns -Name 'google.com' -Type A | Format-Table

# Query with specific DNS provider
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Cloudflare | Format-Table

# Multiple domains at once
Resolve-Dns -Name 'google.com', 'github.com', 'microsoft.com' -Type A | Format-Table

# Multiple record types
Resolve-Dns -Name 'google.com' -Type A, AAAA, MX | Format-Table
```

#### System DNS Queries
```powershell
# Use system-configured DNS servers
Resolve-Dns -Name 'google.com' -Type A -DnsProvider System -Verbose | Format-Table

# Query with custom DNS server
Resolve-Dns -Name 'google.com' -Type A -Server '8.8.8.8' | Format-Table

# Multiple servers with fallback
Resolve-Dns -Name 'google.com' -Type A -Server '1.1.1.1', '8.8.8.8' -Fallback | Format-Table
```

### Advanced Query Options

#### DNSSEC Validation
```powershell
# Request and validate DNSSEC
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Cloudflare -RequestDnsSec -ValidateDnsSec | Format-Table

# Query DNSSEC-specific records
Resolve-Dns -Name 'google.com' -Type DS, DNSKEY -DnsProvider Cloudflare | Format-Table
```

#### Pattern-Based Queries
```powershell
# Query multiple servers using pattern
Resolve-Dns -Pattern 'server[1-3].example.com' -Type A -DnsProvider Cloudflare | Format-Table

# Range patterns
Resolve-Dns -Pattern 'host[10-15].example.com' -Type A | Format-Table
```

#### Full Response Details
```powershell
# Get complete DNS response information
$Response = Resolve-Dns -Name 'google.com' -Type A -DnsProvider Cloudflare -FullResponse

# Display different parts of the response
$Response.Questions | Format-Table
$Response.Answers | Format-Table
$Response.AnswersMinimal | Format-Table
$Response.Authority | Format-Table
$Response.Additional | Format-Table

# Check response metadata
Write-Host "Response Time: $($Response.ResponseTime)ms"
Write-Host "Server: $($Response.Server)"
Write-Host "DNSSEC: $($Response.IsSecure)"
```

#### Timeout and Retry Configuration
```powershell
# Custom timeout (in milliseconds)
Resolve-Dns -Name 'google.com' -Type A -TimeOut 5000 | Format-Table

# With verbose output for troubleshooting
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Cloudflare -Verbose | Format-Table
```

### DNS Provider Examples

#### Cloudflare Variants
```powershell
# Standard Cloudflare (JSON)
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Cloudflare | Format-Table

# Cloudflare Wire Format
Resolve-Dns -Name 'google.com' -Type A -DnsProvider CloudflareWireFormat | Format-Table

# Cloudflare Security (blocks malware)
Resolve-Dns -Name 'google.com' -Type A -DnsProvider CloudflareSecurity | Format-Table

# Cloudflare Family (blocks malware + adult content)
Resolve-Dns -Name 'google.com' -Type A -DnsProvider CloudflareFamily | Format-Table
```

#### Other Major Providers
```powershell
# Google DNS
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Google | Format-Table

# Quad9 (security-focused)
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Quad9 | Format-Table

# OpenDNS
Resolve-Dns -Name 'google.com' -Type A -DnsProvider OpenDNS | Format-Table

# AdGuard (ad-blocking)
Resolve-Dns -Name 'google.com' -Type A -DnsProvider AdGuard | Format-Table
```

### Specialized Record Types

#### Mail Records
```powershell
# MX records for email
Resolve-Dns -Name 'google.com' -Type MX | Format-Table

# SPF records (TXT records containing v=spf1)
Resolve-Dns -Name 'google.com' -Type TXT | Where-Object { $_.Data -like "*v=spf1*" } | Format-Table
```

#### Security Records
```powershell
# TLSA records for certificate validation
Resolve-Dns -Name '_443._tcp.google.com' -Type TLSA | Format-Table

# CAA records for certificate authority authorization
Resolve-Dns -Name 'google.com' -Type CAA | Format-Table
```

#### Service Records
```powershell
# SRV records for services
Resolve-Dns -Name '_sip._tcp.example.com' -Type SRV | Format-Table

# NAPTR records for service discovery
Resolve-Dns -Name 'example.com' -Type NAPTR | Format-Table
```

### DNS Service Discovery

#### Discover Services
```powershell
# Discover all services in a domain
Get-DnsService -Domain 'example.com' | Format-Table

# Find specific service types
Find-DnsService -Domain 'example.com' -Service 'http' -Protocol 'tcp' | Format-Table
```

### Zone Operations

#### Zone Transfer
```powershell
# Full zone transfer (AXFR)
Get-DnsZone -Zone 'example.com' -Server '127.0.0.1' -Port 5353 | Format-Table

# Zone transfer with specific server
Get-DnsZoneTransfer -Zone 'example.com' -Server 'ns1.example.com' | Format-Table
```

#### DNS Updates
```powershell
# Add a DNS record
Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '192.0.2.1' -Ttl 300

# Delete a DNS record
Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Delete

# Update an existing record
Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '192.0.2.2' -Ttl 600
```

### Error Handling and Troubleshooting

#### Handling Failed Queries
```powershell
# Test connectivity to DNS servers
$Servers = @('1.1.1.1', '8.8.8.8', '9.9.9.9')
foreach ($Server in $Servers) {
    try {
        $Result = Resolve-Dns -Name 'google.com' -Type A -Server $Server -TimeOut 2000
        Write-Host "✓ $Server responded in $($Result.ResponseTime)ms" -ForegroundColor Green
    } catch {
        Write-Host "✗ $Server failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}
```

#### Verbose Troubleshooting
```powershell
# Enable verbose output for debugging
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Cloudflare -Verbose

# Test with fallback servers
Resolve-Dns -Name 'google.com' -Type A -Server '1.1.1.1', '8.8.8.8' -Fallback -RandomServer -Verbose
```

### Practical Examples

#### Website Monitoring
```powershell
# Monitor website DNS resolution across providers
$Providers = @('Cloudflare', 'Google', 'Quad9', 'OpenDNS')
$Domain = 'example.com'

foreach ($Provider in $Providers) {
    $Result = Resolve-Dns -Name $Domain -Type A -DnsProvider $Provider
    Write-Host "$Provider -> $($Result.Data -join ', ')"
}
```

#### Email Server Validation
```powershell
# Complete email server DNS check
$Domain = 'example.com'

Write-Host "=== Email DNS Records for $Domain ===" -ForegroundColor Cyan

# MX Records
Write-Host "`nMX Records:" -ForegroundColor Yellow
Resolve-Dns -Name $Domain -Type MX | Format-Table Name, Priority, Data

# SPF Records
Write-Host "SPF Records:" -ForegroundColor Yellow
Resolve-Dns -Name $Domain -Type TXT | Where-Object { $_.Data -like "*v=spf1*" } | Format-Table Name, Data

# DMARC Records
Write-Host "DMARC Records:" -ForegroundColor Yellow
Resolve-Dns -Name "_dmarc.$Domain" -Type TXT | Format-Table Name, Data

# DKIM Records (example)
Write-Host "DKIM Records:" -ForegroundColor Yellow
Resolve-Dns -Name "default._domainkey.$Domain" -Type TXT | Format-Table Name, Data
```

#### Security Assessment
```powershell
# DNS security assessment
$Domain = 'example.com'

Write-Host "=== DNS Security Assessment for $Domain ===" -ForegroundColor Cyan

# DNSSEC validation
$DnssecResult = Resolve-Dns -Name $Domain -Type A -DnsProvider Cloudflare -RequestDnsSec -ValidateDnsSec
Write-Host "DNSSEC Status: $(if($DnssecResult.IsSecure) { 'SECURE' } else { 'NOT SECURE' })" -ForegroundColor $(if($DnssecResult.IsSecure) { 'Green' } else { 'Red' })

# CAA Records
Write-Host "`nCAA Records:" -ForegroundColor Yellow
Resolve-Dns -Name $Domain -Type CAA | Format-Table Name, Data

# Check for common security TXT records
Write-Host "Security Policy Records:" -ForegroundColor Yellow
Resolve-Dns -Name $Domain -Type TXT | Where-Object { $_.Data -match "(v=spf1|v=DMARC1|google-site-verification)" } | Format-Table Name, Data
```

## Usage with Command Line Interface (CLI)

DnsClientX includes a command-line interface for quick DNS queries and scripting.

### Installation

The CLI is distributed as `DnsClientX.exe` and can be built from source or downloaded from releases.

```bash
# Build from source
dotnet build DnsClientX.Cli/DnsClientX.Cli.csproj -c Release

# The executable will be available at:
# DnsClientX.Cli/bin/Release/net8.0/DnsClientX.exe (Windows)
# DnsClientX.Cli/bin/Release/net8.0/DnsClientX (Linux/macOS)

# Or build the entire solution
dotnet build DnsClientX.sln -c Release
```

### Basic Usage

```bash
# Simple A record query
DnsClientX.exe google.com A

# Query with specific provider
DnsClientX.exe google.com A --provider Cloudflare

# Multiple record types
DnsClientX.exe google.com A,AAAA,MX

# Custom DNS server
DnsClientX.exe google.com A --server 8.8.8.8

# JSON output format
DnsClientX.exe google.com A --format json

# Verbose output
DnsClientX.exe google.com A --verbose
```

### Advanced CLI Options

```bash
# DNSSEC validation
DnsClientX.exe google.com A --dnssec --validate

# Custom timeout (milliseconds)
DnsClientX.exe google.com A --timeout 5000

# Multiple domains
DnsClientX.exe google.com,github.com A

# Pattern-based queries
DnsClientX.exe "server[1-3].example.com" A

# Zone transfer
DnsClientX.exe example.com AXFR --server 127.0.0.1 --port 5353

# Service discovery
DnsClientX.exe example.com SRV --service http --protocol tcp
```

### Output Formats

```bash
# Table format (default)
DnsClientX.exe google.com A

# JSON format
DnsClientX.exe google.com A --format json

# CSV format
DnsClientX.exe google.com A --format csv

# Raw format (minimal)
DnsClientX.exe google.com A --format raw
```

### Scripting Examples

#### Bash/PowerShell Scripts
```bash
#!/bin/bash
# Check multiple domains for A records
domains=("google.com" "github.com" "microsoft.com")
for domain in "${domains[@]}"; do
    echo "Checking $domain..."
    DnsClientX.exe "$domain" A --provider Cloudflare
done
```

```powershell
# PowerShell script for DNS monitoring
$Domains = @('google.com', 'github.com', 'microsoft.com')
$Providers = @('Cloudflare', 'Google', 'Quad9')

foreach ($Domain in $Domains) {
    foreach ($Provider in $Providers) {
        Write-Host "Testing $Domain with $Provider" -ForegroundColor Cyan
        & DnsClientX.exe $Domain A --provider $Provider --format json | ConvertFrom-Json
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
