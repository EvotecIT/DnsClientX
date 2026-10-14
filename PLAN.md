# DnsClientX engineering and qualification plan

DnsClientX provides one DNS engine for C#, PowerShell and command-line users. The core owns protocol handling, record parsing, certificate policy, DNSSEC, caching and resolver operations. Optional packages own specialized cryptography and native protocol requirements; the user surfaces call those owners.

This plan records implemented capabilities, their qualification limits and remaining product work. Changes merge after current-head CI and review settlement. Signing, public package publication and deployment remain separate release steps.

## Maintained capabilities

- UDP with TCP fallback, TCP, TLS, HTTPS wire and JSON, HTTP/2, HTTP/3, QUIC and gRPC, with runtime capability reporting. Modern transports depend on the target runtime and native platform support.
- Typed records with owner, class, remaining TTL and wire metadata; negative cache lifetimes bound by SOA, alias and denial evidence; response single-flight and process-wide cache management.
- Reusable query options, ordered and completion-order bounded streams, cancellation/drain, resolver catalogs, probe/benchmark policies, saved scores, health profiles and structured explanations. Stable configured candidate identities distinguish same-label endpoint variants through scoring, reports, comparison and saved selection; schema 1/2 scores remain readable.
- Local DNSSEC validation, authenticated denial, iterative root resolution, QNAME minimization and RFC 5011 anchor lifecycle. Explicit zone-qualified DS anchors bind private DNSKEY verification, descendant trust and authenticated cache lifetimes. Portable ECDSA and EdDSA verification belongs to the optional DNSSEC provider.
- System resolver discovery, supported Windows NRPT routing, explicit bootstrap resolvers, DNS stamps and EDNS options.
- AXFR, IXFR, transfer over TLS, ZONEMD and TSIG multi-message authentication, including authenticated buffering and required first/final signatures.
- Atomic RFC 2136 UPDATE batches with additions, RRset/value/name deletions, name/RRset prerequisites and complete RRset comparisons. PowerShell confirmation happens before network activity.
- SVCB encrypted-DNS selection and DDR pinned to the original resolver, with bounded candidates, certificate verification, and dependency-free DoH URI templates that preserve HTTPS authority.
- Persistent multicast DNS-SD browsing with bounded state, additions, updates, goodbyes, expiration, cache-flush rescue, refresh queries, known-answer suppression and cancellation/disposal drain.
- Optional `DnsClientX.Odoh` for RFC 9230 HPKE, authenticated target configuration, per-query response binding and HTTPS relay routing; optional `DnsClientX.DnsCrypt` for signed resolver certificates, NaCl XSalsa20/XChaCha20 suites, encrypted UDP/TCP and relay routing.
- Thin PowerShell administration, discovery, streaming and configuration surfaces, packaged type/format assets, exported commands and generated help.

The modern core has no new production packages. The approved optional dependencies are BouncyCastle.Cryptography for ODoH and libsodium for DNSCrypt. Optional providers remain caller-owned and must stay alive while clients use their configurations.

## Existing proof and remaining qualification

The implementation has controlled .NET 8/.NET 10 regressions, loopback discovery and multicast peers, ephemeral certificate fixtures, private-zone DNSSEC and atomic UPDATE wire fixtures, and .NET Framework 4.7.2 compilation. Independent Go producers add TSIG AXFR/IXFR, ODoH target/relay and DNSCrypt responder evidence. Source, fresh package-consumer, actual module ZIP and legacy-compilation checks cover the integrated implementation. Focused regressions cover private-anchor DNAME boundaries, split-frame authenticated IXFR, serial wraparound, mDNS link admission and server fallback after transport deadlines.

An isolated BIND lab independently signs and serves private NSEC/NSEC3 zones over IPv4 and IPv6. It qualifies configured DS anchors, signed CNAME/DNAME answers, authenticated denial, wrong/expired-anchor rejection, complete-RRset UPDATE prerequisites, atomic rejection, and TSIG multi-frame AXFR and journal-backed IXFR. BIND and its generated keys are test inputs; no system DNS service or persistent trust store is changed.

Native transport qualification exercises DoQ and HTTP/3 certificate rejection, bounded deadlines, caller cancellation, connection reuse and disposal through public clients. Dedicated jobs cover Windows, Linux and macOS on .NET 8/10. macOS needs MsQuic and, for .NET 8, a compatible OpenSSL library path. Active-interface multicast tests verify selected outbound routing and socket drain, including receives scheduled while disposal closes the socket. Isolated Linux network/mount namespaces exercise OS resolver discovery, IPv6 DNS, search order, multiple virtual adapters and explicit rediscovery after network changes. Virtual interfaces and same-host multicast do not establish cross-host physical-network acceptance.

The administration/privacy candidate also has native Windows TLS 1.3 execution and actual packaged PowerShell tests on Windows PowerShell 5.1/7, Linux and macOS. Hosted consumer jobs qualify the combined module on Windows PowerShell 5.1, minimum PowerShell 7.4 and modern Windows/Linux/macOS hosts. DNSCrypt qualification includes PackageReference consumers and a non-SDK packages.config Windows consumer that verifies native asset delivery and loading.

Live public DDR observations bind trusted certificates to the designating resolver's IP and preserve the original HTTPS authority during alternate-address queries. They describe the observed resolver/network environment, not all resolvers or general performance.

Remaining independent and platform qualification:

- [ ] Qualify against an independently operated authoritative deployment in addition to the reproducible BIND lab, including server/version diversity and delegated private zones.
- [ ] Qualify multicast discovery between physical hosts over IPv4/IPv6, multiple physical adapters and real link/VPN changes; a configured authenticated LAN peer is required.
- [ ] Extend native split-DNS/search-domain evidence beyond Windows NRPT and isolated Linux resolver files, especially macOS scoped resolvers and representative VPN configurations. Record unsupported platform routing explicitly.
- [ ] Exercise CLI archives on their native x64/ARM64 architectures through the shared PowerForge publish configuration.

Local source, compiled assemblies, isolated packed candidates, hosted CI, merged source, public packages and installed versions remain separate delivery states. Coordinated release must publish the core and optional providers before downstream users rely on public package availability. Signing, public publication and deployment follow their normal authorized release process.

## Qualification limits

- QUIC execution requires runtime APIs and native host support; unsupported paths report that state explicitly. Local legacy compilation does not establish Windows runtime behavior.
- Modern pooled DoT offers `dot` ALPN. Legacy targets retain the previous TLS overload and are not qualified against servers that require ALPN.
- Isolated custom TLS roots and HTTP/2 ALPN verification require modern .NET. HTTP/1.1 or HTTP/2 with a different connection address and the original authority requires .NET 8 or later. Alternate-address HTTP/3 DDR reports `Unsupported` for GET and POST; same-address HTTP/3 and alternate-address DoQ retain QUIC capability checks.
- DDR is per resolver/network. Unverified or expired designations cannot create encrypted configurations, and discovery does not replace global DNS settings.
- Slow browse consumers apply bounded backpressure; UDP announcements may be lost. Applications should rediscover after network/interface changes.
- ODoH does not prevent relay/target collusion or traffic analysis. Supplied HTTP handlers must preserve documented privacy settings.
- DNSCrypt native packaging covers supported desktop architectures. Mobile static assets, post-quantum DNSCrypt and privacy-protocol stamp integration remain separate work.

## Next product work

- [ ] Expand independently produced protocol evidence for compact denial, delegated private zones and service discovery.
- [ ] Add privacy-protocol stamp workflows, mobile native assets and optional post-quantum DNSCrypt under their own compatibility and dependency contracts.
- [ ] Record a complete ordered delegation/trust graph before claiming a full resolution or DNSSEC chain; bind resolver-selection explanations to persisted score, policy and capability evidence.
- [ ] Publish performance claims only from repeatable scenarios that separate allocations, throughput and network latency.
- [ ] Design portable CLI profile overrides with explicit precedence when demonstrated callers need them.
- [ ] Define one deterministic DNS-only policy evaluator for blocking, static answers and resolver selection; keep packet, browser, proxy and OS startup management outside this library.
- [ ] Add hosting or rule packages only for demonstrated consumers with a distinct support and approved dependency contract.
