# DNS administration and privacy qualification

This plan tracks advanced DNS administration, service discovery, and optional privacy transports. The [README](README.md) describes the public workflows; this document tracks the remaining proof and delivery work.

The core owns DNS messages, record parsing, response correlation, certificate policy, DNSSEC, and resolver configuration. Optional packages own specialized cryptography and native protocol requirements. CLI and PowerShell use those APIs without duplicating protocol engines.

## Current implementation

- Atomic RFC 2136 UPDATE batches with additions, RRset/value/name deletions, name/RRset prerequisites, and complete RRset comparisons.
- TSIG query signing and multi-message AXFR/IXFR authentication, including authenticated buffering of unsigned intermediate frames and mandatory first/final signatures.
- Explicit zone-qualified DNSSEC DS anchors with most-specific trust boundaries, private DNSKEY authentication, descendant delegation validation, and anchor-bound cache identities/lifetimes.
- Dependency-free SVCB encrypted-DNS selection and DDR queries pinned to the original resolver, with bounded aliases/candidates and explicit certificate verification outcomes.
- Dependency-free DoH URI-template parsing and scalar expansion, retaining the original resolver's IP certificate identity and HTTPS origin when the connection address differs.
- Persistent multicast DNS-SD browsing with additions, updates, goodbyes, expirations, cache-flush rescue, refresh queries, known-answer suppression, bounded state, and cancellation/disposal drain.
- Optional `DnsClientX.Odoh` provider with portable RFC 9230 HPKE, authenticated target-configuration acquisition/refresh, per-query response binding, and HTTPS relay routing.
- Optional `DnsClientX.DnsCrypt` provider with signed resolver certificates, NaCl XSalsa20/XChaCha20 suites, encrypted UDP/TCP exchanges, relay routing, and certificate refresh.
- Thin PowerShell administration/discovery/configuration surfaces, module exports, and generated help.

The modern core remains free of new production packages. The approved optional dependencies are BouncyCastle.Cryptography for ODoH and libsodium for DNSCrypt. Optional providers remain caller-owned and must stay alive while clients use their configurations.

## Interoperability and lifecycle proof

Controlled regression suites cover discovery, multicast browsing, trust and private-zone boundaries, TTL expiry, cancellation and socket release on .NET 8 and .NET 10. The legacy test/reference graph and PowerShell surface compile for .NET Framework 4.7.2; Windows CI also executes the legacy suite.

Independent Go peers exercise ODoH target/relay exchanges, both DNSCrypt suites and UDP/TCP paths, TSIG AXFR/IXFR, and ordinary DoT plus configurations returned by DDR. The strict DoT peer requires `dot` ALPN and verifies TLS 1.3 and the expected identity. The Windows Server 2025 TLS 1.3 test executes on .NET 8 and .NET 10 and asserts a DNS answer, SNI and `dot` negotiation with certificate checking enabled.

Live public DDR observations bind system-trusted certificates to the original resolver's IP and retain the original HTTPS authority during an alternate-address DoH query. Controlled atomic UPDATE fixtures verify prerequisites and wire failures; signed private-zone fixtures exercise explicit anchors. These fixtures do not replace independent authoritative-lab qualification.

Native package CI verifies DNSCrypt assets and real provider execution on Windows, Linux and macOS, including a Windows .NET Framework consumer. The shared PowerForge entrypoint builds one unsigned module ZIP without version updates, signing, publication or installation. Fresh consumers test that actual artifact on Windows PowerShell 5.1, Windows PowerShell 7, Linux and macOS. Package guards reject preloaded/development assemblies, empty or unexecuted suites, and setup, teardown or discovery failures.

Independent reviews covered protocol/trust/lifetime contracts, native deployment and packaged validation. Validated findings have regression proof; these reviews supplement current-head CI and repository review.

Controlled fixtures prove the paths they exercise. Independent producers add interoperability evidence. Native runtime, packaged-consumer, external CI, and public-release proof remain separate gates. Live resolver/network observations are environment evidence, not general performance guarantees.

## Candidate delivery

[PR #582](https://github.com/EvotecIT/DnsClientX/pull/582) contains the administration/privacy implementation. Current-head CI, repository review, delayed feedback and addressed-thread settlement gate readiness and must be rechecked after any publication.

The local integration with [PR #581](https://github.com/EvotecIT/DnsClientX/pull/581) resolves the overlapping core/module changes and preserves configured providers, private anchors, cache controls, PowerShell binding and streaming limits. Its broad suites pass on .NET 8 and .NET 10, the actual combined ZIP passes 84 PowerShell tests, fresh package consumers execute on both modern runtimes, and the full legacy graph compiles. This combined candidate remains local and needs its own hosted/native acceptance before release. Superseded build output is removed; the current private candidates and concise evidence are retained through settlement.

The separate resolver-workflow work in [PR #581](https://github.com/EvotecIT/DnsClientX/pull/581) retains its own review and release boundary. Source implementation and isolated candidate packages do not establish public NuGet or PSGallery availability. Release signing, public package publication, and deployment follow their normal release process.

## Qualification limits

- QUIC discovery and transport execution depend on both runtime APIs and native host support. An unsupported runtime reports that state explicitly.
- Modern pooled DoT offers `dot` ALPN. Legacy targets retain the previous TLS overload and are not qualified against servers that require ALPN.
- Custom isolated TLS roots and HTTP/2 ALPN verification require modern .NET. HTTP/1.1 and HTTP/2 with a different connection address and the original authority require .NET 8 or later. HTTP/3 DDR with a different connection address reports `Unsupported` for both GET and POST; same-address HTTP/3 and alternate-address DoQ retain their existing QUIC capability checks.
- DDR is per resolver and per network. Discovery does not replace global DNS configuration, and an unverified or expired designation cannot create an encrypted client configuration.
- A slow browse consumer applies bounded backpressure; UDP announcements can be lost. Applications should rediscover after network/interface changes.
- ODoH does not prevent a colluding relay and target or traffic analysis. A supplied HTTP handler must preserve its documented privacy settings.
- DNSCrypt native packaging targets supported desktop architectures. Mobile static assets, post-quantum DNSCrypt, and privacy-protocol stamp integration remain separate work.

## Future DNS-only work

- [ ] Qualify private-zone DNSSEC and UPDATE prerequisites against an independently operated authoritative lab.
- [ ] Expand independent native DNS-SD producer and live QUIC DDR evidence, including representative IPv6 and split-DNS environments.
- [ ] Add privacy-protocol stamp workflows, mobile native assets and optional post-quantum DNSCrypt under their own compatibility and dependency contracts.
- [ ] Design a domain policy API for blocking, static answers and resolver routing. Preserve explainable DNS decisions and keep system-wide proxy, packet, browser and operating-system management outside this library.
