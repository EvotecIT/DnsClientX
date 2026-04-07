## Unreleased
- Expanded `Resolve-Dns` PowerShell parity with the C# client for EDNS, ECS, NSID, DNSSEC, resolver selection, server transport selection, and advanced HTTP/proxy/concurrency settings.
- Unified `Resolve-Dns` parameter availability across `-DnsProvider`, `-ResolverEndpoint`, `-ResolverDnsProvider`, and `-Server` scenarios so advanced query options are available where they are expected.
- Fixed multi-resolver handling for DNSSEC validation and preserved resolver endpoint variants more accurately for provider-specific hosts and request formats.
- Refreshed the README, PowerShell examples, manifest exports, and XML-help examples so the documented usage matches the shipped cmdlet surface.

## 0.3.0 - 2024.09.19
- Added IDN conversion (punycode) for domain names
- Added timeout for UDP requests
- Added error handling for UDP requests
