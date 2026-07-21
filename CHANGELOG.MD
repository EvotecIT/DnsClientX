## Unreleased - 2026.07.20
### 2.0.0 breaking RFC and reliability release
* Enforce DNS response correlation and wire-format bounds, including transaction ID, QR/opcode/question validation, compression-loop protection, complete section parsing, and EDNS extended response codes
* Validate DoH HTTP status, media type, protocol version, and response body before DNS parsing so proxy and resolver error pages cannot masquerade as DNS responses
* Correct retry, bounded TTL cache, RFC 2308 negative-cache, immutable snapshot, and UDP peer-validation behavior
* Add dependency-free DNSSEC chain and denial validation for RSA and ECDSA using bundled IANA root trust anchors, delegation-cut-aware NSEC/NSEC3 proofs, and public trust-anchor/key-tag metadata
* Add RFC 8945 TSIG authentication for RFC 2136 updates, typed update RDATA, exact-value deletes, and correct zero-TTL update encoding
* Implement pooled DNS over QUIC for published DoQ endpoints and reject misleading provider, DNSCrypt v2, and ODoH declarations explicitly
* Make the shared multi-resolver pool clients, enforce a global parallelism bound, preserve terminal NXDOMAIN/NODATA responses, and provide bounded-worker batch queries
* Harden AXFR response correlation and opening/closing SOA validation
* Rename `SequentialAll` to `SequentialFallback` and remove dead asymmetric `SigningKey`/`ValidateRootDnsSec` APIs whose names did not describe implemented DNS protocols
* Add public response cloning/filtering helpers so consumers can preserve DNS metadata without their own resolver implementation
* Add a caller-owned-`HttpClient` DNS JSON client for browser/WASM and dependency-injected hosts, including HTTP and media-type validation
* Stop injecting the application icon into every consuming target framework while retaining package and assembly branding

### Earlier unreleased changes
* Fix custom endpoint docs and add `ClientX(Configuration ...)` constructor
* Improve UDP/Multicast timeout handling to rely on cancellation tokens
* Add tests for TXT record filtering and configuration constructor
* Add shared modern transport capability reporting for DoH3 and DoQ
* Add `doh3@...` and `doq@...` custom endpoint parsing across library, CLI, and PowerShell flows
* Add built-in Quad9 HTTP/3 and QUIC endpoints plus capability-aware diagnostics
* Persist runtime capability hints in saved probe and benchmark resolver score snapshots
* Add opt-in live DoH3 and DoQ verification tests that stay skipped by default
* Add cached DNS server hostname resolution with stale fallback, configurable TTLs, and tests
* Add LRU-style cache trimming, failure backoff options, and resolver deduplication tests

## 0.3.4 - 2024.11.15
### What's Changed
* Further changes to PTR (I'm pretty bad at this), changes default provider from Cloudflare to System by @PrzemyslawKlys in https://github.com/EvotecIT/DnsClientX/pull/14

**Full Changelog**: https://github.com/EvotecIT/DnsClientX/compare/v...v0.3.4

## 0.3.2/0.3.3 - 2024.11.15
### What's Changed
* Few improvements, fix PTR by @PrzemyslawKlys in https://github.com/EvotecIT/DnsClientX/pull/12
* Fix ptr & bump version by @PrzemyslawKlys in https://github.com/EvotecIT/DnsClientX/pull/13

**Full Changelog**: https://github.com/EvotecIT/DnsClientX/compare/v0.3.1...v0.3.2

## 0.3.1 - 2024.10.19

### What's Changed
* Bump System.Text.Json from 8.0.4 to 8.0.5 in /DnsClientX by @dependabot in https://github.com/EvotecIT/DnsClientX/pull/11

**Full Changelog**: https://github.com/EvotecIT/DnsClientX/compare/v0.3.0...v0.3.1

## 0.3.0 - 2024.09.19
- Added IDN conversion (punycode) for domain names
- Added timeout for UDP requests
- Added error handling for UDP requests

## 0.2.2 - 2024.07.21
- Update dll package

## 0.2.1 - 2024.04.27

### What's new
- Updated description of package

**Full Changelog**: https://github.com/EvotecIT/DnsClientX/compare/v0.2.0...v0.2.1

## 0.2.0 - 2024.04.27

### What's Changed
* Add TLSA record improvements & other by @PrzemyslawKlys in https://github.com/EvotecIT/DnsClientX/pull/7
  - Improves TLSA record processing, making sure it's the same output in all DnsProviders
  - Update dependency of System.Text.Json to 8.0.3
  - Other small updates
  - Removes old converting projects that won't be needed

**Full Changelog**: https://github.com/EvotecIT/DnsClientX/compare/v0.1.1...v0.2.0

## 0.1.1 - 2024.04.05
- Fixes packaging for NUGET to display proper readme

## 0.1.0 - 2024.04.05
- Initial release
