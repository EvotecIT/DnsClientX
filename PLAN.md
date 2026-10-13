# DNS administration and privacy qualification

This plan tracks advanced DNS administration, service discovery, and optional privacy transports. The [README](README.md) describes the public workflows; this document tracks the remaining proof and delivery work.

The core owns DNS messages, record parsing, response correlation, certificate policy, DNSSEC, and resolver configuration. Optional packages own specialized cryptography and native protocol requirements. CLI and PowerShell use those APIs without duplicating protocol engines.

## Current implementation

- [x] Atomic RFC 2136 UPDATE batches with additions, RRset/value/name deletions, name/RRset prerequisites, and complete RRset comparisons.
- [x] TSIG query signing and multi-message AXFR/IXFR authentication, including authenticated buffering of unsigned intermediate frames and mandatory first/final signatures.
- [x] Explicit zone-qualified DNSSEC DS anchors with most-specific trust boundaries, private DNSKEY authentication, descendant delegation validation, and anchor-bound cache identities/lifetimes.
- [x] Dependency-free SVCB encrypted-DNS selection and DDR queries pinned to the original resolver, with bounded aliases/candidates and explicit certificate verification outcomes.
- [x] Dependency-free DoH URI-template parsing and scalar expansion, retaining the original resolver's IP certificate identity and HTTPS origin when the connection address differs.
- [x] Persistent multicast DNS-SD browsing with additions, updates, goodbyes, expirations, cache-flush rescue, refresh queries, known-answer suppression, bounded state, and cancellation/disposal drain.
- [x] Optional `DnsClientX.Odoh` provider with portable RFC 9230 HPKE, authenticated target-configuration acquisition/refresh, per-query response binding, and HTTPS relay routing.
- [x] Optional `DnsClientX.DnsCrypt` provider with signed resolver certificates, NaCl XSalsa20/XChaCha20 suites, encrypted UDP/TCP exchanges, relay routing, and certificate refresh.
- [x] Thin PowerShell administration/discovery/configuration surfaces, module exports, and generated help; unsigned packaged workflows pass locally.

The modern core remains free of new production packages. The approved optional dependencies are BouncyCastle.Cryptography for ODoH and libsodium for DNSCrypt. Optional providers remain caller-owned and must stay alive while clients use their configurations.

## Interoperability and lifecycle proof

- [x] Focused local discovery tests on .NET 8 and .NET 10, including actual UDP resolver queries, real loopback multicast browsing, ephemeral certificate fixtures, trusted/untrusted DDR, TTL expiry, and socket release under backpressure.
- [x] Live public DDR: system-trusted certificates bind both the original and alternate connection addresses to the designating resolver's IP; an ordinary DoH query succeeds through the alternate address with the original HTTPS authority preserved.
- [x] Independent Go ODoH target/relay and DNSCrypt responder exchanges, covering authenticated responses, tamper rejection, cancellation, both DNSCrypt suites, and UDP/TCP paths.
- [x] Local .NET Framework 4.7.2 compilation for core, optional providers, CLI, and tests. Compilation does not establish Windows runtime behavior.
- [x] Independent Go TSIG AXFR/IXFR exchanges, controlled atomic UPDATE wire/prerequisite failures, and actual signed private-zone UDP responses.
- [ ] Qualify private-zone DNSSEC and update prerequisites against an independently operated authoritative lab.
- [x] Stable broad .NET 8/.NET 10 regression suites and local legacy compilation.
- [x] Remediate the first platform CI findings with portable TLS fixtures, deployment-bound Framework native loading, normalized package paths, and deadline tests that observe completion rather than machine speed.
- [ ] Complete required native/platform CI.
- [x] Isolated packed NuGet consumer and actual unsigned PowerShell package proof on the local host.
- [x] Repeat affected packaged proof after remediation: the final unsigned module passes all 61 PowerShell cases and fresh isolated NuGet consumers run on .NET 8 and .NET 10.
- [ ] Verify DNSCrypt native assets and real provider execution on Windows and Linux in addition to the locally tested macOS ARM64 host.
- [ ] Verify packaged PowerShell workflows on Windows PowerShell 5.1 and the supported PowerShell 7 hosts.
- [x] Add one unsigned module ZIP producer and four isolated packaged PowerShell consumers to CI, preserving the existing source-test and transport checks.
- [x] Complete one independent read-only risk review and remediate trust, timeout, and transport-capability findings with regression proof.
- [x] Complete a bounded platform/package closure review; packaged PowerShell validation rejects teardown and discovery failures as well as failed or unexecuted tests.

Controlled fixtures prove the paths they exercise. Independent producers add interoperability evidence. Native runtime, packaged-consumer, external CI, and public-release proof remain separate gates. Live resolver/network observations are environment evidence, not general performance guarantees.

## Candidate delivery

- [x] Publish one complete ready-for-review PR for the administration/privacy outcome: [PR #582](https://github.com/EvotecIT/DnsClientX/pull/582).
- [ ] Require current-head CI and repository review settlement, address validated findings, and resolve fixed conversations.
- [ ] Recheck delayed reviewer feedback before declaring readiness.
- [x] Remove superseded task output and retain only concise proof and the final private package/module candidate through review settlement.

The separate resolver-workflow work in [PR #581](https://github.com/EvotecIT/DnsClientX/pull/581) retains its own review and release boundary. Source implementation and isolated candidate packages do not establish public NuGet or PSGallery availability. Release signing, public package publication, and deployment follow their normal release process.

## Qualification limits

- QUIC discovery and transport execution depend on both runtime APIs and native host support. An unsupported runtime reports that state explicitly.
- Custom isolated TLS roots and HTTP/2 ALPN verification require modern .NET. HTTP/1.1 and HTTP/2 with a different connection address and the original authority require .NET 8 or later. HTTP/3 DDR with a different connection address reports `Unsupported` for both GET and POST; same-address HTTP/3 and alternate-address DoQ retain their existing QUIC capability checks.
- DDR is per resolver and per network. Discovery does not replace global DNS configuration, and an unverified or expired designation cannot create an encrypted client configuration.
- A slow browse consumer applies bounded backpressure; UDP announcements can be lost. Applications should rediscover after network/interface changes.
- ODoH does not prevent a colluding relay and target or traffic analysis. A supplied HTTP handler must preserve its documented privacy settings.
- DNSCrypt native packaging targets supported desktop architectures. Mobile static assets, post-quantum DNSCrypt, and privacy-protocol stamp integration remain separate work.

## Future DNS-only work

A domain policy API for blocking, static answers, and resolver routing remains a separate design task. It should preserve explainable DNS decisions and avoid system-wide proxy, packet, browser, or operating-system management behavior.
