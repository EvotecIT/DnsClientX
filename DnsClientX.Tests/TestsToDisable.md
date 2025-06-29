# Tests to Disable Due to External Dependencies

## Problem: Comparison Tests Are Unreliable

The following test classes test external DNS providers rather than DnsClientX logic, causing 10-15% test failure rates:

### ❌ Disable These Test Classes:
- `CompareProviders.cs` - Compares external DNS providers
- `CompareProvidersResolve.cs` - Compares external DNS providers
- `CompareProvidersResolveAll.cs` - Compares external DNS providers
- `CompareProvidersResolveFilter.cs` - Compares external DNS providers
- `CompareJsonWithDnsWire.cs` - Compares external DNS providers

### ✅ Keep These Test Classes:
- `QueryDnsByEndpoint.cs` - Tests basic functionality
- `QueryDnsByHostName.cs` - Tests core logic
- `QueryDnsByUri.cs` - Tests core logic
- `ResolveFirst.cs` - Tests core logic
- `ResolveAll.cs` - Tests core logic
- `ResolveSync.cs` - Tests core logic

## Reason for Disabling:

1. **External Dependencies**: Tests depend on external DNS services (Google, OpenDNS, Cloudflare)
2. **Rate Limiting**: Providers block rapid automated requests
3. **Network Flakiness**: Internet connectivity issues cause false failures
4. **CDN Behavior**: Providers correctly return different IPs for CDN domains
5. **Test Logic Issues**: Array index exceptions in comparison logic

## Recommendation:

Focus tests on DnsClientX client behavior, not external provider comparisons.