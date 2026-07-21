# DnsClientX - Modern DNS Client for .NET and PowerShell

DnsClientX is available as NuGet from the Nuget Gallery and as PowerShell module from PSGallery

📦 NuGet Package

[![nuget downloads](https://img.shields.io/nuget/dt/DnsClientX?label=nuget%20downloads)](https://www.nuget.org/packages/DnsClientX)
[![nuget version](https://img.shields.io/nuget/v/DnsClientX)](https://www.nuget.org/packages/DnsClientX)

💻 PowerShell Module

[![powershell gallery version](https://img.shields.io/powershellgallery/v/DnsClientX.svg)](https://www.powershellgallery.com/packages/DnsClientX)
[![powershell gallery preview](https://img.shields.io/powershellgallery/v/DnsClientX.svg?label=powershell%20gallery%20preview&colorB=yellow&include_prereleases)](https://www.powershellgallery.com/packages/DnsClientX)
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

**DnsClientX** is an async C# library for DNS over UDP, TCP, HTTPS (DoH), TLS (DoT), HTTP/3 (DoH3), and QUIC (DoQ). It also has a PowerShell module that can be used to query DNS records. It provides a simple way to query DNS records using multiple DNS providers. It supports multiple DNS record types and parallel queries. It's available for .NET 8, .NET 10, .NET Standard 2.0, and .NET 4.7.2.

## DomainDetective.dev

DnsClientX is also documented as a first-class product inside DomainDetective.dev. Use those pages when you want the guided web experience instead of browsing the repository directly:

- Product overview: [https://domaindetective.dev/products/dnsclientx/](https://domaindetective.dev/products/dnsclientx/)
- C# guide: [https://domaindetective.dev/docs/dnsclientx/csharp/](https://domaindetective.dev/docs/dnsclientx/csharp/)
- PowerShell guide: [https://domaindetective.dev/docs/dnsclientx/powershell/](https://domaindetective.dev/docs/dnsclientx/powershell/)
- DNS playground: [https://domaindetective.dev/tools/raw-dns-query/](https://domaindetective.dev/tools/raw-dns-query/)
- .NET API reference: [https://domaindetective.dev/api/dnsclientx/](https://domaindetective.dev/api/dnsclientx/)
- PowerShell API reference: [https://domaindetective.dev/api/dnsclientx-powershell/](https://domaindetective.dev/api/dnsclientx-powershell/)

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

The main `Resolve` APIs accept `typedRecords: true`. Supported records are returned as dedicated types; other records remain available as `UnknownRecord`, so opting into typed parsing never requires pretending an unsupported parser is complete.

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
| CloudflareQuic           |     | ❌  |     |     |     |          |      |
| CloudflareOdoh           |     |     |     |     |     |          | ❌    |
| Google                   | ✓   |     |     |     |     |          |      |
| GoogleWireFormat         | ✓   |     |     |     |     |          |      |
| GoogleWireFormatPost     | ✓   |     |     |     |     |          |      |
| GoogleQuic               |     | ❌  |     |     |     |          |      |
| AdGuard                  | ✓   |     |     |     |     |          |      |
| AdGuardFamily            | ✓   |     |     |     |     |          |      |
| AdGuardNonFiltering      | ✓   |     |     |     |     |          |      |
| Quad9                    | ✓   |     |     |     |     |          |      |
| Quad9Http3               | ✓   |     |     |     |     |          |      |
| Quad9Quic                |     | ✓   |     |     |     |          |      |
| Quad9ECS                 | ✓   |     |     |     |     |          |      |
| Quad9Unsecure            | ✓   |     |     |     |     |          |      |
| OpenDNS                  | ✓   |     |     |     |     |          |      |
| OpenDNSFamily            | ✓   |     |     |     |     |          |      |
| DnsCryptCloudflare       |     |     |     |     |     | ❌        |      |
| DnsCryptQuad9            |     |     |     |     |     | ❌        |      |
| DnsCryptRelay            |     |     |     |     |     | ❌        |      |
| RootServer               |     |     |     | ✓   | ✓   |          |      |

`CloudflareQuic`, `GoogleQuic`, `CloudflareJsonPost`, `GoogleJsonPost`, `CloudflareOdoh`, and the DNSCrypt endpoint names are retained for source compatibility but fail explicitly: the providers do not publish the claimed service, or the protocol is not implemented. The core implements DoQ for published endpoints such as `Quad9Quic` without extra transport packages on modern .NET. ODoH and DNSCrypt v2 require dedicated protocol and cryptographic implementations and are intentionally outside the core package.

If you want to learn about DNS:
- https://www.cloudflare.com/learning/dns/what-is-dns/

> [!NOTE]
> DnsClientX normalizes presentation details such as trailing dots and TXT character-string concatenation, but preserves DNS resource-record boundaries. Resolver answers can legitimately differ because of cache state, anycast location, ECS, filtering policy, or propagation; comparison code should not assume every resolver returns an identical RRset at the same instant.

## Supported .NET Versions and Dependencies

### Core Library (DnsClientX)
- **.NET 8.0 / .NET 10.0** (Windows, Linux, macOS)
  - No external dependencies
  - DoH3 and DoQ stay in the core package with no added NuGet transport dependencies
- **.NET Standard 2.0** (Cross-platform compatibility)
  - System.Text.Json (10.0.10)
  - Microsoft.Bcl.AsyncInterfaces (10.0.10)
- **.NET Framework 4.7.2** (Windows only)
  - System.Net.Http (built-in)
  - System.Text.Json (10.0.10)
  - Microsoft.Bcl.AsyncInterfaces (10.0.10)
  - Modern transports such as DoH3 and DoQ are exposed by the shared API surface but return unsupported at runtime

### Command Line Interface (DnsClientX.exe)
- **.NET 8.0 / .NET 10.0** (Windows, Linux, macOS)
- **.NET Framework 4.7.2** (Windows only)
- Single-file deployment supported

### PowerShell Module
- **.NET 8.0** (Cross-platform)
- **.NET Standard 2.0** (Windows PowerShell 5.1+ compatibility)
- **.NET Framework 4.7.2** (Windows PowerShell 5.1)
- PowerShellStandard.Library (5.1.1)

### Examples Project
- **.NET 8.0** only
- Spectre.Console (0.57.2) for enhanced console output

## Build Status

[![Test .NET](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-dotnet.yml/badge.svg)](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-dotnet.yml)
[![Test PowerShell](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-powershell.yml/badge.svg)](https://github.com/EvotecIT/DnsClientX/actions/workflows/test-powershell.yml)
[![codecov](https://codecov.io/gh/EvotecIT/DnsClientX/branch/main/graph/badge.svg)](https://codecov.io/gh/EvotecIT/DnsClientX)

**Cross-Platform Testing:** All tests run simultaneously across Windows, Linux, and macOS to ensure compatibility.

## Features

- [x] Supports multiple built-in DNS Providers (System, Cloudflare, Google, Quad9, OpenDNS, etc.)
- [x] Supports both JSON and Wireformat
- [x] Supports DNS over HTTPS (DoH) using GET and POST methods
- [x] Supports DNS over HTTP/3 (DoH3) on .NET 8 and .NET 10
- [x] Supports DNS over TLS (DoT)
- [x] Supports DNS over QUIC (DoQ) on .NET 8 and .NET 10
- [x] Supports DNS over UDP, and switches to TCP if needed
- [x] Supports DNS over TCP
- [x] Supports DNSSEC
- [x] Validates DNSSEC locally through root trust anchors, signed RRsets, and authenticated NSEC/NSEC3 denial proofs
- [x] Authenticates RFC 2136 DNS UPDATE requests and responses with TSIG (HMAC-SHA-256/384/512 and SHA-1 compatibility)
- [x] Supports multiple DNS record types
- [x] Supports parallel queries
- [x] No external dependencies on .NET 8 and .NET 10
- [x] Minimal dependencies on .NET Standard 2.0 and .NET 4.7.2
- [x] Implements IDisposable/IAsyncDisposable to release cached HTTP, UDP, and QUIC transport resources
- [x] Multi-line record data normalized to use `\n` line endings
- [x] Supports DNS Service Discovery (DNS-SD)

## Modern Transport Support

DnsClientX keeps modern transports in the core package instead of moving them behind extra transport packages:

- **DoH3 (`DnsOverHttp3`)** is available in the core package on .NET 8 and .NET 10 with no extra NuGet dependencies.
- **DoQ (`DnsOverQuic`)** is available in the core package on .NET 8 and .NET 10 when the runtime exposes QUIC support, also with no extra NuGet dependencies.
- **.NET Standard 2.0 / .NET Framework 4.7.2** keep the same API surface, but modern transports are reported as unsupported at runtime instead of pulling in separate QUIC stacks.

Use `DnsTransportCapabilities` when you want to check support before executing a query.

PowerShell:

```powershell
Get-DnsTransportCapability
Get-DnsTransportCapability -ModernOnly
```

CLI:

```powershell
DnsClientX.Cli --capabilities
DnsClientX.Cli --capabilities --format json
```

Saved probe and benchmark snapshots also persist runtime capability hints, so recommendation files can distinguish unsupported modern transports from ordinary resolver failures.
Snapshots include a schema version, and resolver selection rejects snapshots from newer unsupported schemas before attempting reuse.

## Explicit Endpoint Syntax

Custom resolver inputs share one endpoint parser across the library, CLI, and PowerShell.

- UDP: `udp@1.1.1.1:53`
- TCP: `tcp@9.9.9.9:53`
- DoT: `dot@dns.quad9.net:853`
- DoH: `doh@https://dns.google/dns-query`
- DoH3: `doh3@https://dns.quad9.net/dns-query`
- DoQ: `doq@dns.quad9.net:853`
- DNS stamp: `sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk`

The same syntax works in:

- `Resolve-Dns -ResolverEndpoint ...`
- `Test-DnsProbe -ResolverEndpoint ...`
- `Test-DnsBenchmark -ResolverEndpoint ...`
- CLI probe and benchmark endpoint inputs

DNS stamps currently map into the core endpoint model for plain DNS, DoH, DoT, and DoQ. DNSCrypt and ODoH stamp protocols are detected and rejected with clear unsupported-protocol errors.

## CLI Diagnostics and Resolver Workflows

The CLI is intended for quick diagnostics and automation-friendly resolver checks.

Standard query output:

```powershell
DnsClientX.Cli --format json example.com
DnsClientX.Cli --format raw --question --answer --authority --additional example.com
DnsClientX.Cli --short example.com
DnsClientX.Cli --txt-concat --type TXT example.com
DnsClientX.Cli --reverse 1.1.1.1
```

Zone transfer convenience:

```powershell
DnsClientX.Cli --axfr --transfer-summary example.com
DnsClientX.Cli --axfr --format json example.com
```

Resolver import, probe, benchmark, and score reuse:

```powershell
DnsClientX.Cli --resolver-validate --resolver-file .\resolvers.txt
DnsClientX.Cli --resolver-validate --resolver-file .\resolvers.txt --format json
Test-DnsResolverCatalog -ResolverEndpointFile .\resolvers.txt
DnsClientX.Cli --probe --resolver-file .\resolvers.txt --probe-save .\probe.json
DnsClientX.Cli --benchmark --resolver-url https://example.com/resolvers.txt --benchmark-save .\benchmark.json
DnsClientX.Cli --resolver-select .\probe.json
DnsClientX.Cli --resolver-use .\probe.json --short example.com
```

DNS stamp inspection without querying DNS:

```powershell
DnsClientX.Cli --stamp-info 'sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk'
DnsClientX.Cli --stamp-info 'sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk' --format json
ConvertFrom-DnsStamp -Stamp 'sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk'
```

## Opt-In Real Modern Transport Checks

The test suite includes limited live checks for DoH3 and DoQ. They are opt-in so default CI stays stable.

Set both environment variables before running tests:

```powershell
$env:DNSCLIENTX_RUN_REAL_DNS_TESTS = '1'
$env:DNSCLIENTX_RUN_MODERN_TRANSPORT_TESTS = '1'
dotnet test .\DnsClientX.Tests\DnsClientX.Tests.csproj --filter FullyQualifiedName~ModernTransportRealTests
```

These live tests only run on .NET 8 and later and stay focused on a small set of real endpoints.

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
| Google              | `dns.google`                          | RFC 8484 wire  |
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

## System DNS Discovery

`DnsEndpoint.System` and `DnsEndpoint.SystemTcp` query the DNS servers exposed by the operating system. Discovery returns an immutable `SystemDnsConfiguration` containing the ordered server list, search suffixes, `ndots`, discovery source, effective Windows NRPT rules, and separate resolver/policy discovery errors.

- Windows and other .NET platforms enumerate every active interface. Interfaces are ordered by the native Windows interface metric when available, then by gateway presence and a stable interface order.
- Unix-like systems can fall back to `/etc/resolv.conf`, including `nameserver`, `search`/`domain`, and `options ndots:n`.
- Configured loopback and link-local resolvers are preserved because local forwarding stubs and IPv6 scoped DNS servers are valid. Unspecified and multicast addresses are rejected.
- UDP clients reuse healthy connected sockets and automatically retry a truncated response over TCP when `UseTcpFallback` is enabled. TCP and DoT clients reuse persistent RFC 7766 connections, pipeline bounded concurrent queries, and dispatch out-of-order responses by transaction ID.
- On Windows, dependency-free NRPT discovery honors the Group Policy-over-local-policy precedence rule. Supported suffix, prefix, FQDN, and catch-all matches route through `GenericDNSServers`; `DNSSECValidationRequired` triggers local chain validation. Punycode IDN policy is supported, while Windows-only UTF-8 IDN modes, conflicts, DirectAccess, IPsec, wildcard/subnet, and auto-trigger VPN behavior fail explicitly instead of bypassing policy.
- There is no silent public-resolver substitution. Missing system DNS is an explicit configuration error unless the caller opts in to `SystemDnsFallback.PublicResolvers`.

```csharp
using var systemClient = new ClientX(new Configuration(DnsEndpoint.System));
var response = await systemClient.Resolve("intranet", DnsRecordType.A);

// Explicit opt-in for applications that prefer availability over preserving
// the local DNS trust and routing boundary.
var configuration = new Configuration(
    DnsEndpoint.System,
    DnsSelectionStrategy.First,
    SystemDnsFallback.PublicResolvers);
using var fallbackClient = new ClientX(configuration);

var discovered = SystemInformation.GetDnsConfiguration(refresh: true);
Console.WriteLine($"Source: {discovered.Source}; ndots: {discovered.Ndots}");
Console.WriteLine(string.Join(", ", discovered.DnsServers));
Console.WriteLine(string.Join(", ", discovered.SearchDomains));
foreach (var rule in discovered.PolicyRules) {
    Console.WriteLine($"NRPT {rule.Id}: {string.Join(", ", rule.Namespaces)} -> {string.Join(", ", rule.NameServers)}");
}
```

Search expansion is enabled by default for system endpoints and can be disabled with `Configuration.UseSystemSearchDomains`. A trailing dot is always treated as absolute, and PTR inputs are never search-expanded.

NRPT enforcement is enabled by default for system endpoints and can be deliberately disabled with `Configuration.UseSystemDnsPolicies`. The applied match is available as `DnsResponse.AppliedSystemDnsPolicy`. DnsClientX reads policy directly and does not emulate Windows DirectAccess, VPN activation, or IPsec state; use the operating-system resolver API when those services own the policy.

Persistent TCP/DoT reuse is enabled by default. Set `EnableTcpConnectionReuse = false` only for compatibility diagnostics with a broken server. `MaxTcpQueriesPerConnection` bounds in-flight queries on one connection and defaults to 128. The configured timeout covers capacity, connection, transaction-ID reservation, write, and response waits. Caller cancellation removes only that transaction; late responses are consumed without completing another query.

This direct DNS client does not delegate name resolution to the operating-system resolver service and does not claim parity with every platform-specific resolver feature. Configure a `Configuration` instance before issuing concurrent queries; each query takes a defensive snapshot, but concurrent mutation of shared configuration is not a supported control plane.

## TO DO

> [!IMPORTANT]
> This library is still in development and there are things that need to be done, tested and fixed.
> If you would like to help, please do so by opening an issue or a pull request.
> The public API may change while protocol coverage and RFC conformance continue to mature.

- [ ] Complete the concrete stabilization, transport, cache, system-policy, zone, and DNSSEC work tracked in [PLAN.md](PLAN.md)
- [ ] Build specialized cryptography and privacy protocols as separately reviewed optional packages; keep the core dependency-light

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
    .WithUserAgent("MyApp/1.0")
    .Build();

var response = await client.Resolve("example.com", maxRetries: 3);
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
using var client = new ClientXBuilder()
    .WithBaseUri(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON)
    .Build();
```
Custom endpoints should be provided at construction time (builder/overloads) so the underlying HTTP client is configured correctly.
You can also preconfigure a `Configuration` and pass it into `ClientX`:
```csharp
var config = new Configuration(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON);
using var client = new ClientX(config);
```

#### Concurrency Control
When resolving arrays of names with the helper methods, you can optionally cap in-flight requests:

```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
client.EndpointConfiguration.MaxConcurrency = Environment.ProcessorCount; // or any positive integer

// Caps parallelism for array-based helpers like Resolve/ResolveFilter
var responses = await client.Resolve(new[] { "example.com", "google.com" }, DnsRecordType.A);
```
By default (`null`), DnsClientX keeps existing behavior and does not impose an explicit concurrency cap.

## Multi-Resolver

DnsClientX includes a flexible multi-resolver that can query multiple endpoints using different strategies.

- Strategies: FirstSuccess, FastestWins, SequentialFallback, RoundRobin
- Transports: UDP, TCP, DoT, DoH, DoH3, and DoQ where supported by the target runtime and endpoint
- Behaviors: per-query timeout, cancellation propagation, UDP→TCP fallback on truncation, TTL metrics

Usage example:

```csharp
var endpoints = EndpointParser.TryParseMany(new []{
    "1.1.1.1:53",
    "8.8.8.8:53",
    "https://dns.google/dns-query"
}, out var errors);

var options = new MultiResolverOptions {
    Strategy = MultiResolverStrategy.FirstSuccess,
    MaxParallelism = 4,
    RespectEndpointTimeout = true
};

using var mr = new DnsMultiResolver(endpoints, options);
var response = await mr.QueryAsync("example.com", DnsRecordType.A);
Console.WriteLine($"RTT: {response.RoundTripTime.TotalMilliseconds} ms via {response.UsedTransport} @ {response.UsedEndpoint}");

// Batch API (preserves input order, isolates failures)
var results = await mr.QueryBatchAsync(new [] { "a.com", "b.com", "c.com" }, DnsRecordType.A);

// Consensus/diagnostic API (one response per endpoint, in endpoint order)
var allResponses = await mr.QueryAllAsync("example.com", DnsRecordType.DNSKEY);
```

Notes:
- DoH endpoints must be provided as HTTPS URLs (e.g. https://dns.google/dns-query).
- When a UDP response is truncated and `AllowTcpFallback` is true, TCP is used automatically.
- `Response.TtlMin` and `Response.TtlAvg` expose TTL metrics derived from the answer set.

### Strategies

- FirstSuccess: Races a bounded set of endpoints and returns the first success. Cancels losers.
- FastestWins: Warms endpoints and prefers the one that produced the fastest successful response, caching the choice for a duration.
- SequentialFallback: Tries endpoints in order; returns the first terminal response or best transport error.
- RoundRobin: Distributes queries across endpoints to balance load. On failure, falls back to the first (or second) endpoint. Combine with MaxParallelism (global cap) and PerEndpointMaxInFlight (per-endpoint cap).

### Concurrency Control

- `MaxParallelism`: Caps total in-flight queries issued by the multi-resolver.
- `PerEndpointMaxInFlight`: Caps concurrent queries per endpoint (e.g., limit to 4 per DNS server).

### Response Caching

- Enable TTL-aware response caching (off by default in multi-resolver) to avoid repeat lookups.
- C#:

```csharp
var opts = new MultiResolverOptions {
  EnableResponseCache = true,
  MaxCacheTtl = TimeSpan.FromMinutes(60)
};
```

Positive answers use the lowest authoritative answer TTL. Negative `NoError`/`NXDomain` answers use the RFC 2308 minimum of the SOA TTL and SOA MINIMUM field. Responses without an authoritative cache lifetime are not cached; `MaxCacheTtl` can shorten, but never extend, that lifetime.

- PowerShell:

```powershell
Resolve-Dns -Name 'example.com' -Type A -DnsProvider Cloudflare,Google `
  -ResolverStrategy FirstSuccess -ResponseCache -MaxCacheTtlSeconds 3600
```

### PowerShell Examples

Balance across providers with caps:

```powershell
Resolve-Dns -Name @('a.com','b.com') -Type A `
  -DnsProvider System,Cloudflare,Quad9 `
  -ResolverStrategy RoundRobin -MaxParallelism 32 -PerEndpointMaxInFlight 4
```

First success across mixed endpoints:

```powershell
Resolve-Dns -Name 'example.com' -Type A `
  -ResolverEndpoint '1.1.1.1:53','https://dns.google/dns-query' -ResolverStrategy FirstSuccess
```

EDNS client subnet and DNSSEC details:

```powershell
Resolve-Dns -Name 'example.com' -Type A `
  -ResolverEndpoint 'https://dns.google/dns-query' `
  -EnableEdns -ClientSubnet '192.0.2.0/24' -RequestDnsSec -ValidateDnsSec -FullResponse
```

### Managing FastestWins Cache

- Clear for all:

```powershell
Clear-DnsMultiResolverCache
```

- Clear for specific providers or endpoints:

```powershell
Clear-DnsMultiResolverCache -ResolverDnsProvider Cloudflare,Google
Clear-DnsMultiResolverCache -ResolverEndpoint '1.1.1.1:53','https://dns.google/dns-query'
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
Console.WriteLine($"Authentic Data (AD): {response.AuthenticData}");
Console.WriteLine($"Local DNSSEC status: {response.DnsSecValidationStatus}");
Console.WriteLine(response.DnsSecValidationMessage);
```

The resolver-provided `AuthenticData` flag and local validation are deliberately separate. `Secure` means DnsClientX built and verified a chain to a configured root trust anchor; `Insecure` means a secure parent authenticated an unsigned delegation; `Bogus` is a cryptographic or proof failure; and `Indeterminate` means the response, trust state, or supported algorithms were insufficient. The dependency-free validator supports RSA/SHA-1 (algorithms 5 and 7, for compatibility), RSA/SHA-256, and RSA/SHA-512 on every target. The .NET 8 and .NET 10 assets also support ECDSA P-256/SHA-256 and ECDSA P-384/SHA-384. Ed25519 and Ed448 are supplied by the optional package below rather than claimed by the core.

Install `DnsClientX.DnsSec.EdDsa` when RFC 8080 algorithms 15 and 16 are required. It is separately versioned and is the only DnsClientX package that depends on `BouncyCastle.Cryptography`:

```csharp
using DnsClientX.DnsSec.EdDsa;

using var client = new ClientX(DnsEndpoint.RootServer);
client.EndpointConfiguration.UseEdDsaDnsSec();

DnsResponse response = await client.Resolve("signed.example", DnsRecordType.A,
    requestDnsSec: true, validateDnsSec: true);
```

The extension verifier participates in the same chain validation, cache isolation, and `Secure`/`Bogus`/`Indeterminate` semantics as the built-in algorithms. Merely referencing the package does not enable it; configuration is explicit per client.

The root-server profile enables RFC 9156 QNAME minimization by default. It asks for one delegation label at a time with type `NS`, continues through authoritative NODATA responses without revealing the final name, and sends the complete name and requested type only after reaching the authoritative zone. `DnsResponse.QNameMinimizedQueryCount` and `QNameMinimizationFallbackCount` make both the privacy protection and any compatibility downgrade visible. Set `Configuration.EnableQNameMinimization = false` only for controlled compatibility diagnostics.

RFC 5011 trust-anchor maintenance is opt-in because durable state belongs to the application. Configure a private, durable file and schedule explicit refreshes:

```csharp
using var client = new ClientX(DnsEndpoint.RootServer);
client.EndpointConfiguration.Rfc5011TrustAnchorStorePath = "dnssec/root-anchors.json";

DnsSecTrustAnchorRefreshResult refresh = await client.RefreshRootTrustAnchorsAsync();
if (!refresh.Succeeded) throw new InvalidOperationException(refresh.Response.DnsSecValidationMessage);
Console.WriteLine($"Refresh again by {refresh.Snapshot!.NextRefreshUtc:O}");
```

The state machine enforces the 30-day add hold-down plus original DNSKEY TTL, requires a validated post-deadline observation, resets absent pending keys, preserves missing active keys, immediately and permanently honors a key's verified self-revocation, and retains revoked tombstones through the 30-day remove hold-down. State writes replace a same-directory temporary file atomically; malformed state and clock rollback fail closed. No background timer is hidden inside `ClientX`: the application must call `RefreshRootTrustAnchorsAsync` by `NextRefreshUtc` and retry failed refreshes according to its scheduler. Without a configured store, the immutable IANA public-key anchors bundled with the package remain the validation boundary.

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
var providers = new[] {
    DnsEndpoint.Cloudflare,
    DnsEndpoint.Google,
    DnsEndpoint.Quad9
};
var endpoints = DnsResolverEndpointFactory.From(providers);
using var resolver = new DnsMultiResolver(endpoints, new MultiResolverOptions {
    Strategy = MultiResolverStrategy.FirstSuccess,
    MaxParallelism = 3
});
var response = await resolver.QueryAsync("google.com", DnsRecordType.A);
response.DisplayTable();
```

#### Streaming Queries
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
var domains = new[] { "google.com", "github.com", "microsoft.com" };
var recordTypes = new[] { DnsRecordType.A, DnsRecordType.AAAA };

await foreach (var response in client.ResolveStream(domains, recordTypes)) {
    var question = response.Questions.Length > 0 ? response.Questions[0] : null;
    Console.WriteLine(question is null
        ? "Resolved response"
        : $"Resolved: {question.Name} ({question.Type})");
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

Browser/WASM and dependency-injected hosts can keep ownership of their HTTP handler while reusing DnsClientX parsing and response semantics:

```csharp
var response = await DnsJsonQueryClient.QueryAsync(
    httpClient,
    new Uri("https://dns.google/resolve"),
    "google.com",
    DnsRecordType.A,
    requestDnsSec: true,
    cancellationToken: cancellationToken);
```

#### DNS over QUIC (DoQ)
```csharp
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.Quad9Quic);
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

For authoritative, CHAOS-class, and EDNS diagnostics, construct the message explicitly and keep the shared transport and validation rules:

```csharp
var query = new DnsMessage("version.bind", DnsRecordType.TXT, new DnsMessageOptions(
    RecursionDesired: false,
    QueryClass: 3)); // CHAOS
var result = await DnsWireQueryClient.QueryUdpAsync("192.0.2.53", 53, query);
Console.WriteLine($"{result.Response.WireMessageLength} response bytes");
```

`DnsWireQueryClient` validates the source peer, transaction ID, QR/opcode, and echoed question before returning. It preserves the exact request/response bytes for protocol diagnostics while avoiding a second DNS transport implementation in consumers.

#### Root Server Queries
```csharp
var response = await ClientX.QueryDns("google.com", DnsRecordType.A,
    DnsEndpoint.RootServer);
```

`RootServer` performs non-recursive iteration from the configured A-M root hints. It follows the closest delegation, applies glue according to the responding authority's bailiwick, resolves missing name-server addresses independently, and follows bounded CNAME/DNAME chains. With `validateDnsSec: true`, validation fetches DNSKEY/DS material through the same iterative path and anchors it at the bundled root trust anchors.

The profile goes through the normal `Resolve` retry, cache, audit, answer-projection, typed-record, PTR, and IDN pipeline. Referral and alias traversal has its own bound, independent of retry attempts:

```csharp
using var client = new ClientX(DnsEndpoint.RootServer);
client.EndpointConfiguration.IterativeMaxHops = 40;
var response = await client.Resolve("www.sidn.nl", validateDnsSec: true, typedRecords: true);
```

When an alias crosses authoritative packets or zones, each wire response is validated separately. DnsClientX does not combine signatures and RDATA offsets from different DNS messages or claim that an incomplete alias chain is secure.

### Performance and Reliability Features

#### Latency Measurement
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare);
var latency = await client.MeasureLatencyAsync();
Console.WriteLine($"Latency: {latency.TotalMilliseconds}ms");
```

#### Retry Configuration
```csharp
using var client = new ClientX(DnsEndpoint.Cloudflare, timeOutMilliseconds: 5000);
var response = await client.Resolve(
    "example.com",
    maxRetries: 3,
    retryDelayMs: 100);
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

// RFC 6763 meta-query: discover the advertised service types first.
var serviceTypes = await client.DiscoverServiceTypesAsync("example.com");

// Browse PTR instances of one service type, then resolve instance SRV/TXT data.
var services = await client.BrowseServicesAsync("http", "tcp", "example.com");
foreach (var service in services) {
    Console.WriteLine($"Instance: {service.InstanceName}");
    Console.WriteLine($"Type: {service.ServiceType} -> {service.Target}:{service.Port}");
}

// Return SRV targets in RFC 2782 priority and weighted-random connection order.
var srvRecords = await client.ResolveServiceAsync("ldap", "tcp", "example.com", resolveHosts: true);
foreach (var srv in srvRecords) {
    Console.WriteLine($"Server: {srv.Target}:{srv.Port} (Priority: {srv.Priority})");
    if (srv.Addresses != null) {
        Console.WriteLine($"IPs: {string.Join(", ", srv.Addresses)}");
    }
}
```

#### Zone Transfer (AXFR, IXFR, XFR-over-TLS, and ZONEMD)
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

// Atomic RFC 1995 incremental transfer. The server can return no change,
// ordered delete/add deltas, or a complete AXFR-style fallback.
var changes = await client.IncrementalZoneTransferAsync("example.com", currentSerial: 2026072100);

// Validate an RFC 8976 SIMPLE-SCHEME SHA-384/SHA-512 digest over the complete AXFR.
var digest = ZoneDigestValidator.Validate("example.com", zoneRecords);
Console.WriteLine($"{digest.Status}: {digest.Message}");
```

AXFR and IXFR accept only `DnsOverTCP` or `DnsOverTLS` endpoints and enforce configurable message, record, byte, and per-I/O time limits. Streaming AXFR retries only before exposing the first chunk, so a failed transfer cannot replay a duplicate prefix. IXFR is returned only after the complete serial chain is validated and is therefore safe for an application to apply atomically.

On .NET 8 or newer, `DnsOverTLS` zone transfers enforce RFC 9103 TLS 1.3 and the `dot` ALPN value. An IP endpoint requires `TlsServerName`; `IgnoreCertificateErrors` is rejected. Mutual TLS is available through `ZoneTransferClientCertificate`, and a narrowly scoped certificate callback can be supplied through `ZoneTransferServerCertificateValidationCallback` for private PKI or pinned lab endpoints. XFR-over-TLS is explicitly unsupported on older targets rather than downgraded to plaintext.

On macOS, XFR-over-TLS requires .NET 10 or newer and the Network.framework TLS client. Enable it before the process performs any TLS work:

```csharp
AppContext.SetSwitch("System.Net.Security.UseNetworkFramework", true);
```

DnsClientX does not change this process-wide setting on the host's behalf. Check `DnsTransportCapabilities.SupportsZoneTransferOverTls` before selecting XFR-over-TLS.

`ZoneDigestValidator` operates on canonical wire records preserved by `ZoneTransferAsync`; presentation-only `ZoneTransferResult` values are rejected. A matching digest establishes zone-data integrity, not origin authenticity. Authenticate the apex ZONEMD RRset separately with DNSSEC or use an authenticated transfer channel. Multi-message TSIG chaining for AXFR/IXFR is not implemented, so configuring the update-oriented `TsigKey` for a transfer fails explicitly instead of implying protection it does not provide.

#### Zone Master Files

```csharp
var parsed = DnsZoneFileParser.ParseFile("example.zone", new DnsZoneFileParseOptions {
    Origin = "example.com",
    AllowIncludes = true
});

foreach (var diagnostic in parsed.Diagnostics) {
    Console.WriteLine(diagnostic);
}

if (parsed.Success) {
    foreach (var record in parsed.Records) Console.WriteLine(record);
}
```

The parser supports owner inheritance, `$ORIGIN`, compound TTL values, multiline records, quoted/comment-aware tokens, scoped relative `$INCLUDE`, common `$GENERATE` forms, and RFC 3597 `TYPE####` records. Includes are disabled by default; enabling them permits only link-free paths beneath the top-level zone directory (or `IncludeRootDirectory`). Rooted, out-of-root, symbolic-link, and reparse-point paths additionally require `AllowUnsafeIncludePaths = true` and should be used only with trusted zone files. Unsupported classes, excessive generation, overflow, and malformed input are reported as structured diagnostics instead of being silently reinterpreted. `BindFileParser` remains a compatibility adapter over this public parser.

#### DNS Updates
```csharp
var configuration = new Configuration("192.0.2.53", DnsRequestFormat.DnsOverTCP) {
    TsigKey = TsigKey.FromBase64("update-key.example.com", "AQIDBAUGBwg=")
};
using var client = new ClientX(configuration);

// Add a record
await client.UpdateRecordAsync(
    "example.com", "www.example.com", DnsRecordType.A, "192.0.2.1", ttl: 300);

// Delete the complete A RRset
await client.DeleteRecordAsync("example.com", "www.example.com", DnsRecordType.A);
```

RFC 2136 wire updates are accepted only for UDP/TCP-configured authoritative targets and are sent over TCP. DoH, DoT, DoQ, mDNS, and built-in recursive profiles are not silently reinterpreted as plaintext update endpoints. When a `TsigKey` is configured, DnsClientX signs the request and requires a valid chained TSIG on the response. The separate JSON POST update mode is a proprietary custom-endpoint API and does not support TSIG.

### Multicast DNS (mDNS)
```csharp
using var client = new ClientX("224.0.0.251", DnsRequestFormat.Multicast) {
    EndpointConfiguration = {
        Port = 5353,
        TimeOut = 1500,
        MulticastInterfaceIndex = 12 // optional IPv6/IPv4 interface selection
    }
};
var responses = await client.ResolveMulticastAllAsync("printer.local", DnsRecordType.A);
```

The mDNS query uses transaction ID zero, clears RD, omits EDNS, and collects distinct valid responder messages for the configured timeout window. It is a bounded one-shot query, not a continuous RFC 6762 browser. `Resolve` is still available when a merged single response is more convenient.

### Source Binding and Telemetry

```csharp
var configuration = new Configuration("192.0.2.53", DnsRequestFormat.DnsOverUDP) {
    LocalEndPoint = new IPEndPoint(IPAddress.Parse("192.0.2.10"), 0)
};
using var client = new ClientX(configuration);

DnsClientTelemetry.QueryCompleted += (_, query) =>
    Console.WriteLine($"{query.Name} {query.Type}: {query.Status} in {query.Duration}");
```

`LocalEndPoint` is supported for UDP, TCP, DoT, and DoQ queries and for RFC 2136 updates through the shared TCP engine. HTTP-based transports reject it explicitly because `HttpClient` does not expose a portable per-query source binding. Modern targets also emit `ActivitySource` spans and `Meter` counters under the name `DnsClientX`; when no event, activity, or meter listener is attached, query telemetry does not allocate a scope.

When response caching is enabled, `DnsResponse.ResponseSource` reports `Network`, `Cache`, or `CoalescedNetwork`. Exact-key concurrent misses share one in-flight query, but each caller receives an independent response clone. Canceling one waiter does not cancel the shared fetch.

### Library Comparison Benchmark

`DnsClientX.Benchmarks` contains separate BenchmarkDotNet suites for the full wire parser, cache hit/miss paths, and a client comparison with DnsClient.NET. The client comparison uses a controlled loopback DNS responder by default so client overhead is not confused with Internet or resolver latency.

Run benchmarks one process at a time so BenchmarkDotNet build artifacts do not contend with each other:

```powershell
# Discover or smoke-test the parser and cache suites.
dotnet run -c Release --project .\DnsClientX.Benchmarks -- --list flat
dotnet run -c Release --project .\DnsClientX.Benchmarks -- --filter '*DnsWireParserBenchmark*' --job Dry
dotnet run -c Release --project .\DnsClientX.Benchmarks -- --filter '*DnsResponseCacheBenchmark*' --job Dry

# Run the controlled loopback library comparison.
dotnet run -c Release --project .\DnsClientX.Benchmarks -- --filter '*DnsLibraryNetworkBenchmark*'
```

To run the comparison against a resolver you control:

```powershell
$env:DNS_BENCHMARK_SERVER = '192.0.2.53'
$env:DNS_BENCHMARK_PORT = '53'
$env:DNS_BENCHMARK_NAME = 'example.com'
dotnet run -c Release --project .\DnsClientX.Benchmarks -- --filter '*DnsLibraryNetworkBenchmark*'
```

Both clients disable caching and retries, reuse their client instances, and perform one query per benchmark invocation. Treat lab results as environment-specific; use the loopback result for client overhead and the lab result only to detect material regressions under realistic network conditions.

For concurrent scenarios, use the dependency-free `DnsClientX.LoadTests` runner rather than BenchmarkDotNet. It reports throughput, p50/p95/p99 latency, and failures for each requested concurrency level and can emit JSON for comparisons over time:

```powershell
dotnet run -c Release -f net10.0 --project .\DnsClientX.LoadTests -- `
  --server 192.0.2.53 --name example.com --transport udp `
  --concurrency 1,32,128 --requests 1000 --warmup 16 `
  --json .\artifacts\dns-load.json
```

Use a resolver you own for sustained tests. The runner disables caching and retries, creates one reusable client per concurrency scenario, and returns a nonzero exit code when any request fails. A `NoError` RCODE counts as success only when no response error is present and the complete pre-projection answer contains the requested RRset; missing or dropped answers are reported separately. Results compare complete query behavior; parser-only and cache-only costs belong in the microbenchmark suites above.

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

    if (!string.IsNullOrEmpty(response.Error)) {
        Console.WriteLine($"DNS Error: {response.Error}");
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
`Resolve-Dns` covers the main query surface, including built-in providers, explicit server transports, resolver endpoint strategies, DNSSEC, EDNS/ECS, and typed records.

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
| `Test-DnsBenchmark`|                       | Benchmark multiple providers or endpoints |
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

#### Benchmarking and Resolver Comparison
```powershell
# Benchmark built-in providers and return ranked candidate rows
Test-DnsBenchmark -Name 'example.com' -DnsProvider Cloudflare,Google,Quad9 -Attempts 3 |
    Sort-Object Rank | Format-Table Target, SuccessPercent, AverageMs, Rank, IsRecommended

# Benchmark explicit resolver endpoints across domains and record types
Test-DnsBenchmark -Name 'example.com', 'microsoft.com' -Type A, AAAA `
    -ResolverEndpoint 'udp@1.1.1.1:53', 'tcp@9.9.9.9:53' `
    -Attempts 2 -MaxConcurrency 4 | Format-Table

# Add one run-level summary object after the candidate rows
Test-DnsBenchmark -Name 'example.com' -DnsProvider Cloudflare,Google `
    -Attempts 3 -IncludeSummary

# Return only the run-level summary object for automation
Test-DnsBenchmark -Name 'example.com' -DnsProvider Cloudflare,Google `
    -Attempts 3 -MinSuccessPercent 90 -SummaryOnly | Format-List

# Return only summary metadata that scripts can inspect directly
Test-DnsBenchmark -Name 'example.com' -DnsProvider Cloudflare,Google `
    -Attempts 3 -MinSuccessPercent 90 -SummaryOnly |
    Select-Object RecommendedTarget, RecommendationAvailable, OverallSuccessPercent, PolicyPassed
```

#### System DNS Queries
```powershell
# Use system-configured DNS servers
Resolve-Dns -Name 'google.com' -Type A -DnsProvider System -Verbose | Format-Table

# Query with custom DNS server over classic UDP
Resolve-Dns -Name 'google.com' -Type A -Server '8.8.8.8' | Format-Table

# Query a named resolver over DoH with explicit transport settings
Resolve-Dns -Name 'google.com' -Type A -Server 'dns.google' `
    -RequestFormat DnsOverHttps -Port 443 -UserAgent 'DnsClientX/PowerShell' | Format-Table

# Multiple servers with fallback
Resolve-Dns -Name 'google.com' -Type A -Server '1.1.1.1', '8.8.8.8' -Fallback | Format-Table
```

### Advanced Query Options

#### DNSSEC Validation
```powershell
# Request and validate DNSSEC with a built-in provider
Resolve-Dns -Name 'google.com' -Type A -DnsProvider Cloudflare -RequestDnsSec -ValidateDnsSec | Format-Table

# Request and validate DNSSEC across explicit resolver endpoints
Resolve-Dns -Name 'google.com' -Type A `
    -ResolverEndpoint 'https://dns.google/dns-query', 'tcp@9.9.9.9:53' `
    -RequestDnsSec -ValidateDnsSec -FullResponse | Format-List

# Query DNSSEC-specific records
Resolve-Dns -Name 'google.com' -Type DS, DNSKEY -DnsProvider Cloudflare | Format-Table
```

#### EDNS and Client Subnet
```powershell
# Enable EDNS and send ECS (client subnet)
Resolve-Dns -Name 'example.com' -Type A -DnsProvider Quad9ECS `
    -EnableEdns -ClientSubnet '192.0.2.0/24' | Format-Table

# Request resolver NSID metadata
Resolve-Dns -Name 'example.com' -Type A -ResolverEndpoint 'https://dns.google/dns-query' `
    -EnableEdns -RequestNsid -FullResponse | Format-List
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
$Response.Authorities | Format-Table
$Response.Additional | Format-Table

# Check response metadata
Write-Host "Response Time: $([math]::Round($Response.RoundTripTime.TotalMilliseconds, 2))ms"
Write-Host "Server: $($Response.ServerAddress)"
Write-Host "Authentic Data (AD): $($Response.AuthenticData)"
Write-Host "DNSSEC Error: $(if([string]::IsNullOrEmpty($Response.Error)) { 'None' } else { $Response.Error })"
```

#### Timeout and Retry Configuration
```powershell
# Custom timeout (in milliseconds)
Resolve-Dns -Name 'google.com' -Type A -TimeOut 5000 | Format-Table

# Limit per-client concurrency and HTTP connections
Resolve-Dns -Name 'google.com', 'microsoft.com' -Type A -DnsProvider Cloudflare `
    -MaxConcurrency 8 -MaxConnectionsPerServer 20 | Format-Table

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

# Find services using the alternate cmdlet name
Find-DnsService -Domain 'example.com' | Where-Object ServiceName -like '*http*' | Format-Table
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
        $Result = Resolve-Dns -Name 'google.com' -Type A -Server $Server -TimeOut 2000 -FullResponse
        Write-Host "✓ $Server responded in $([math]::Round($Result.RoundTripTime.TotalMilliseconds, 2))ms" -ForegroundColor Green
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
$DnssecResult = Resolve-Dns -Name $Domain -Type A -DnsProvider Cloudflare -ValidateDnsSec -FullResponse
Write-Host "Authentic Data (AD): $($DnssecResult.AuthenticData)" -ForegroundColor $(if($DnssecResult.AuthenticData) { 'Green' } else { 'Yellow' })
Write-Host "Local DNSSEC status: $($DnssecResult.DnsSecValidationStatus)"
Write-Host "Validation details: $($DnssecResult.DnsSecValidationMessage)"

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
# Simple A record query using the default System endpoint
DnsClientX.exe google.com

# Query with a specific built-in endpoint
DnsClientX.exe --endpoint Cloudflare google.com

# Query a specific record type
DnsClientX.exe --type MX google.com

# Explain which resolver/transport path was used
DnsClientX.exe --endpoint Cloudflare --type A --explain google.com

# Include per-attempt diagnostics
DnsClientX.exe --endpoint Cloudflare --type A --trace google.com
```

### Probe and Benchmark

```bash
# Probe a built-in endpoint family
DnsClientX.exe --probe --endpoint Cloudflare example.com

# Probe custom endpoints
DnsClientX.exe --probe --probe-endpoint udp@1.1.1.1:53,tcp@9.9.9.9:53 example.com

# Fail when responders disagree
DnsClientX.exe --probe --probe-endpoint udp@1.1.1.1:53,tcp@9.9.9.9:53 \
  --probe-require-consensus example.com

# Benchmark built-in endpoints across repeated queries
DnsClientX.exe --benchmark --endpoint Cloudflare,Google,Quad9 \
  --domain example.com --type A --benchmark-attempts 5

# Benchmark a custom endpoint matrix across domains and record types
DnsClientX.exe --benchmark \
  --probe-endpoint udp@1.1.1.1:53,tcp@9.9.9.9:53 \
  --domain example.com,microsoft.com \
  --type A,AAAA \
  --benchmark-attempts 3 \
  --benchmark-concurrency 4

# Add benchmark policy gates for automation
DnsClientX.exe --benchmark --endpoint Cloudflare,Google,Quad9 \
  --domain example.com \
  --type A \
  --benchmark-attempts 3 \
  --benchmark-min-success-percent 90 \
  --benchmark-min-successful-candidates 2 \
  --benchmark-summary-line

# Keep CI logs compact while still emitting a machine-readable summary line
DnsClientX.exe --benchmark --endpoint Cloudflare,Google,Quad9 \
  --domain example.com \
  --type A \
  --benchmark-attempts 3 \
  --benchmark-summary-only \
  --benchmark-summary-line
```

### Other Useful Options

```bash
# Request and validate DNSSEC data
DnsClientX.exe --endpoint Cloudflare --dnssec --validate-dnssec google.com

# Use DoH wire POST when supported
DnsClientX.exe --endpoint CloudflareWireFormatPost --wire-post google.com

# Send a dynamic DNS update
DnsClientX.exe --update example.com www A 192.0.2.10 --ttl 300
```

### Scripting Examples

#### Bash/PowerShell Scripts
```bash
#!/bin/bash
# Benchmark several providers and capture the one-line machine-readable summary
DnsClientX.exe --benchmark --endpoint Cloudflare,Google,Quad9 \
  --domain example.com \
  --type A \
  --benchmark-attempts 3 \
  --benchmark-summary-line
```

```powershell
# PowerShell script for quick probe comparison
& DnsClientX.exe --probe `
    --probe-endpoint 'udp@1.1.1.1:53,tcp@9.9.9.9:53' `
    --probe-summary-line `
    example.com
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
