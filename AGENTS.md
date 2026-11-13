# DnsClientX

Async C# DNS client library supporting UDP, TCP, DNS over HTTPS, TLS and QUIC,
DNSSEC validation and typed records (SPF, DMARC, DKIM and more), with a
PowerShell module and a CLI.

## Layout

- `DnsClientX/`: the library. `ProtocolDns*/` per transport, `DnsRecords/` for
  record parsing, `Linq/` (query provider), `MultiResolver/`, `Network/`, and
  `Security/` (DNSSEC validation and root trust anchors).
- `DnsClientX.PowerShell/` and `Module/`: the binary cmdlets and the module
  manifest, build and Pester tests.
- `DnsClientX.Cli/`: command-line tool.
- `DnsClientX.Tests/`: xUnit tests. `DnsClientX.LoadTests/`,
  `DnsClientX.Benchmarks/`, `DnsClientX.AotSmoke/`: load, benchmark and AOT checks.
- Optional protocol packages: `DnsClientX.DnsCrypt/`, `DnsClientX.Odoh/`,
  `DnsClientX.DnsSec.EdDsa/` (see `OPTIONAL-PROTOCOLS.md`).
- `Website/`: project site.

## Build and test

```powershell
dotnet build DnsClientX.sln -c Release
dotnet test DnsClientX.sln -c Release
dotnet test DnsClientX.Tests/DnsClientX.Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"
./Module/Build/Build-Module.ps1                  # PowerShell module
./Module/DnsClientX.Tests.ps1                    # module Pester tests
./Build/Build-Project.ps1                        # NuGet packages (see Build/project.build.json)
```

Targets: `netstandard2.0`, `net472` (Windows only), `net8.0` and `net10.0`.
DNS over QUIC needs `net8.0` or later.

## Tests

- Live DNS and load workloads are opt-in. Set `DNSCLIENTX_RUN_REAL_DNS_TESTS=1`
  for live resolver tests and `DNSCLIENTX_RUN_MODERN_TRANSPORT_TESTS=1` for live
  DoT, DoH3 and DoQ. Keep deterministic loopback protocol and lifetime tests in
  the ordinary suite.
- `DNSCLIENTX_DEBUG_SYSTEMDNS=1` traces system resolver discovery.

## Generated files

`Module/Docs/*.md` and the module's external help come from the cmdlet XML
documentation. Run `./Module/Build/Build-Module.ps1 -RunMode Documentation`
after changing cmdlet docs; don't edit the generated Markdown.
`Build/Update-ProviderDocumentation.ps1` regenerates the provider list.

## Conventions

- C#: braces on every block, file-scoped namespaces, follow `.editorconfig`.
- PowerShell: K&R/OTBS braces, help inside the function.
- DNSSEC root trust anchors are code in `DnsClientX/Security/RootTrustAnchors.cs`,
  covered by the trust-anchor tests. Change them only with a source for the new
  anchor.
