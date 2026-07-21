## Unreleased - 2026.07.20
### 2.1.0 DNS correctness, policy, transfer, and performance release

- Adds persistent pipelined DNS-over-TCP and DNS-over-TLS sessions, exact-key single-flight caching, controlled load tests, and allocation/latency benchmark scenarios.
- Adds Windows NRPT discovery and policy-aware system resolver routing.
- Adds bounded IXFR, RFC 9103 XFR-over-TLS, and RFC 8976 ZONEMD validation.
- Adds RFC 9156 QNAME minimization and opt-in RFC 5011 managed root trust anchors.
- Adds the separately versioned `DnsClientX.DnsSec.EdDsa` package for RFC 8080 Ed25519 and Ed448 validation without adding dependencies to the core package.

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
* Discover system resolvers without silent public fallback, preserve local forwarding/link-local servers, expose search domains and `ndots`, and apply system search expansion with an explicit opt-in public fallback
* Correct RFC 6763 DNS-SD browsing, RFC 2782 weighted SRV ordering, and bounded RFC 6762 multicast collection across IPv4/IPv6 interfaces
* Harden iterative referrals with closest-zone selection, bailiwick-constrained glue, missing nameserver resolution, and CNAME/DNAME loop limits
* Add a public diagnostic BIND master-file parser with `$ORIGIN`, `$INCLUDE`, `$GENERATE`, owner inheritance, multiline records, and RFC 3597 generic types
* Add UDP socket reuse, explicit UDP/TCP/DoT/DoQ source binding, zero-cost-when-disabled query telemetry, and a controlled DnsClient.NET comparison benchmark
* Document the exact DNSSEC algorithm boundary and the reviewed dependency gates for future optional DNSCrypt and ODoH packages
* Stop guessing TXT record boundaries from SPF/verification content, preserve complete TXT RRs during filtering, route the default Google profile through RFC 8484 wire format, and reject built-in JSON POST profiles that their providers do not publish
* Prevent malformed eleven-field LOC presentation data from reaching a twelfth-field parser read
* Make the advertised `RootServer` profile executable through every shared resolver workflow, preserve root-label DNSKEY lookups, and apply glue against the responding authority's bailiwick
* Reject encrypted/recursive resolver profiles as RFC 2136 update targets instead of silently sending plaintext DNS to their ports, and share TCP source binding with authenticated updates
* Validate RFC 6672 DNAME-synthesized unsigned CNAMEs through their signed DNAME RRset and validate cross-zone iterative alias packets independently
* Correlate mDNS packets by requested owner/type, retain only related DNS-SD records, and cap collected responses to resist unrelated multicast traffic and floods
* Make zone-file includes opt-in, root-confined, and link-safe by default, and turn TTL/`$GENERATE` overflow or hostile expansion into bounded diagnostics
* Keep source/interface/family/fallback selections out of one another's cache entries, continue system search lists after NODATA without skipping projected alias successes, and isolate telemetry subscriber failures
* Atomically claim client resources before asynchronous disposal, construct TCP/update/AXFR sockets for the resolved IPv4/IPv6 family, and enforce DoQ FIN, one-response, and EDNS keepalive rules
* Route the root profile through normal retry, cache, audit, projection, typed-record, PTR, and IDN behavior with an independent iterative-hop setting
* Rename the misleading `ResolveFromRoot(... maxRetries ...)` parameter to `maxHops`, keep `RetryCount` for actual repeated attempts, and retain ordinary RRsets for ANY queries

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
