# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DnsClientX is a modern, async C# library for DNS operations supporting multiple protocols (UDP, TCP, HTTPS/DoH, TLS/DoT, QUIC/DoQ). It includes both a .NET library and a PowerShell module.

## Build Commands

### .NET Library
```bash
# Full build
dotnet build DnsClientX.sln --configuration Release

# Debug build
dotnet build DnsClientX.sln --configuration Debug

# Clean and rebuild
dotnet clean DnsClientX.sln
dotnet restore DnsClientX.sln
dotnet build DnsClientX.sln --configuration Release
```

### PowerShell Module
```powershell
# Build the PowerShell module
./Module/Build/Build-Module.ps1

# Build only the module manifest
$env:RefreshPSD1Only = 'true'
./Module/Build/Build-Module.ps1
```

## Test Commands

### .NET Tests
```bash
# Run all tests
dotnet test DnsClientX.sln --configuration Release

# Run tests with coverage
dotnet test DnsClientX.Tests/DnsClientX.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --logger trx

# Run tests for specific framework
dotnet test --framework net8.0

# Debug system DNS issues
DNSCLIENTX_DEBUG_SYSTEMDNS=1 dotnet test DnsClientX.Tests/DnsClientX.Tests.csproj
```

### PowerShell Tests
```powershell
# Run PowerShell module tests
./Module/DnsClientX.Tests.ps1
```

### Run a Single Test
```bash
# Run specific test by name filter
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Run tests in a specific class
dotnet test --filter "ClassName=DnsClientX.Tests.SpecificTestClass"
```

## Architecture Overview

### Core Components

1. **DnsClientX/** - Main library
   - `Client/` - DNS client implementations
   - `Protocol*/` - Protocol-specific implementations (UDP, TCP, DoH, DoT, DoQ)
   - `Records/` - DNS record type definitions and parsers
   - `Typed/` - Strongly-typed record parsers (SPF, DMARC, DKIM, etc.)
   - `QueryEngine/` - LINQ provider for DNS queries
   - `Cache/` - Response caching system
   - `Dnssec/` - DNSSEC validation logic

2. **DnsClientX.PowerShell/** - PowerShell module
   - Cmdlets: `Resolve-Dns`, `Get-DnsService`, `Find-DnsService`, `Get-DnsZone`, `Invoke-DnsUpdate`
   - Cross-platform support for Windows PowerShell 5.1+ and PowerShell Core

3. **DnsClientX.Cli/** - Command-line interface
   - Supports multiple output formats (table, JSON, CSV, raw)

### Key Design Patterns

- **Protocol Abstraction**: Each protocol (UDP, TCP, DoH, etc.) implements a common interface
- **Factory Pattern**: Used for creating typed DNS records based on record type
- **Async/Await**: All DNS operations are async-first
- **Builder Pattern**: Used for constructing DNS queries with fluent API

### Multi-targeting Strategy

The project targets multiple .NET versions:
- .NET 8.0 and 9.0 (latest features)
- .NET Standard 2.0 (broad compatibility)
- .NET Framework 4.7.2 (legacy support)

Conditional compilation is used extensively to handle platform differences.

## Code Style Guidelines

### C# Code Style
- Always use braces even for single line statements
- Prefer file-scoped namespaces
- Follow .editorconfig rules in the repository

### PowerShell Code Style
- Use K&R/OTBS style
- Add documentation for functions inside the function block
- Module formatting is automatically applied during build

## Common Development Tasks

### Running Examples
```bash
dotnet run --project DnsClientX.Examples/DnsClientX.Examples.csproj
```

### Running CLI
```bash
dotnet run --project DnsClientX.Cli/DnsClientX.Cli.csproj
```

### Running Benchmarks
```bash
dotnet run --project DnsClientX.Benchmarks/DnsClientX.Benchmarks.csproj -c Release
```

### Updating Version
```powershell
./Build/UpdateVersion.ps1
```

## Important Files and Locations

- **Build Scripts**: `Module/Build/Build-Module.ps1`, `Build/*.ps1`
- **CI/CD**: `.github/workflows/test-*.yml`
- **Test Data**: `DnsClientX.Tests/TestData/`
- **Root Trust Anchors**: `DnsClientX/Dnssec/TrustAnchors.xml`
- **PowerShell Module Manifest**: `Module/DnsClientX.psd1`

## DNSSEC Development

When working with DNSSEC:
- Root trust anchors are in `DnsClientX/Dnssec/TrustAnchors.xml`
- Update instructions are in `DnsClientX/Dnssec/UpdateTrustAnchors.md`
- DNSSEC validation logic is in `DnsClientX/Dnssec/DnssecValidator.cs`

## Protocol-Specific Notes

- **DoH (DNS over HTTPS)**: Supports both JSON and wire format
- **DoT (DNS over TLS)**: Uses port 853 by default
- **DoQ (DNS over QUIC)**: Experimental, requires .NET 8.0+
- **Multicast DNS**: Uses 224.0.0.251:5353 for local network discovery

## Publishing

The project publishes to:
- NuGet.org (DnsClientX library)
- PowerShell Gallery (DnsClientX module)
- GitHub Releases (all packages)

Publishing is handled by PSPublishModule and requires API keys stored locally.