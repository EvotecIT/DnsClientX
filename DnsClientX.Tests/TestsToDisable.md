# Real DNS Integration Tests

## Why They Are Opt-In

The classes below intentionally query live DNS providers or rely on real network conditions.
They are useful for validation against real hosts, but they are not stable enough for default CI runs.

### Live-Host Test Groups
- `CompareProviders.cs`
- `CompareProvidersResolve.cs`
- `CompareProvidersResolveAll.cs`
- `CompareProvidersResolveFilter.cs`
- `CompareJsonWithDnsWire.cs`
- `Quad9ReliabilityFix.cs`
- `QueryDnsOverHttp3.cs`
- `ResolveFromRootTests.cs`
- `DnsMultiResolverBasicTests.cs`
- `SystemTcpIntegrationTests.cs`

### Default CI Coverage
- `QueryDnsByEndpoint.cs` - Tests basic functionality
- `QueryDnsByHostName.cs` - Tests core logic
- `QueryDnsByUri.cs` - Tests core logic
- `ResolveFirst.cs` - Tests core logic
- `ResolveAll.cs` - Tests core logic
- `ResolveSync.cs` - Tests core logic

## Why They Are Not In Default CI

1. **External Dependencies**: Tests depend on external DNS services (Google, OpenDNS, Cloudflare)
2. **Rate Limiting**: Providers block rapid automated requests
3. **Network Flakiness**: Internet connectivity issues cause false failures
4. **CDN Behavior**: Providers correctly return different IPs for CDN domains
5. **Environment Requirements**: Some tests require root-server access, HTTP/3 reachability, or system resolver behavior

## How To Run Them

- Local/manual: set `DNSCLIENTX_RUN_REAL_DNS_TESTS=1` before running `dotnet test`
- GitHub Actions: use `workflow_dispatch` on `Test .NET` and enable `run_real_dns_tests`

## Recommendation

Keep real-host tests as an opt-in integration suite and keep default CI focused on deterministic client behavior.
