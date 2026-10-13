# DnsClientX engineering and qualification plan

DnsClientX provides one DNS engine for C#, PowerShell and command-line users. The core owns protocol handling, record parsing, certificate policy, DNSSEC, caching and resolver operations. Optional packages own specialized cryptography and native protocol requirements; the user surfaces call those owners.

This working plan covers the local combined candidate for [PR #581](https://github.com/EvotecIT/DnsClientX/pull/581) and [PR #582](https://github.com/EvotecIT/DnsClientX/pull/582). Both PRs retain their existing review and release gates. The combined candidate is local: neither a local merge nor a green local package establishes merged PRs, combined hosted CI, native Windows execution, or public NuGet/PSGallery availability.

## Maintained capabilities

- UDP with TCP fallback, TCP, TLS, HTTPS wire and JSON, HTTP/2, HTTP/3, QUIC and gRPC, with runtime capability reporting. Modern transports depend on the target runtime and native platform support.
- Typed records with owner, class, remaining TTL and wire metadata; negative cache lifetimes bound by SOA, alias and denial evidence; response single-flight and process-wide cache management.
- Reusable query options, ordered and completion-order bounded streams, cancellation/drain, resolver catalogs, probe/benchmark policies, saved scores, health profiles and structured explanations.
- Local DNSSEC validation, authenticated denial, iterative root resolution, QNAME minimization and RFC 5011 anchor lifecycle. Explicit zone-qualified DS anchors bind private DNSKEY verification, descendant trust and authenticated cache lifetimes. Portable ECDSA and EdDSA verification belongs to the optional DNSSEC provider.
- System resolver discovery, supported Windows NRPT routing, explicit bootstrap resolvers, DNS stamps and EDNS options.
- AXFR, IXFR, transfer over TLS, ZONEMD and TSIG multi-message authentication, including authenticated buffering and required first/final signatures.
- Atomic RFC 2136 UPDATE batches with additions, RRset/value/name deletions, name/RRset prerequisites and complete RRset comparisons. PowerShell confirmation happens before network activity.
- SVCB encrypted-DNS selection and DDR pinned to the original resolver, with bounded candidates, certificate verification, and dependency-free DoH URI templates that preserve HTTPS authority.
- Persistent multicast DNS-SD browsing with bounded state, additions, updates, goodbyes, expiration, cache-flush rescue, refresh queries, known-answer suppression and cancellation/disposal drain.
- Optional `DnsClientX.Odoh` for RFC 9230 HPKE, authenticated target configuration, per-query response binding and HTTPS relay routing; optional `DnsClientX.DnsCrypt` for signed resolver certificates, NaCl XSalsa20/XChaCha20 suites, encrypted UDP/TCP and relay routing.
- Thin PowerShell administration, discovery, streaming and configuration surfaces, packaged type/format assets, exported commands and generated help.

The modern core has no new production packages. The approved optional dependencies are BouncyCastle.Cryptography for ODoH and libsodium for DNSCrypt. Optional providers remain caller-owned and must stay alive while clients use their configurations.

## Local combined candidate checklist

- [x] Select the portable native module builder and retain both branches' PowerShell contracts, including atomic-batch WhatIf and individual updates.
- [x] Reconcile core and cmdlet integration, including configured-provider streaming without plaintext fallback and private-anchor DNSSEC material-cache binding; prove the reachable regressions before packaging.
- [x] Keep one packaged CI producer/consumer lane with native PowerShell, minimum PowerShell 7.4 and three-OS NuGet consumers; consolidate strict package, asset and Pester-result checks in one helper.
- [x] Regenerate the manifest, command Markdown and external help through PowerForge from the frozen merged C# sources.
- [x] Build one unsigned candidate through the native entrypoint with exact source versions, version updates disabled, no signing/publication and no local installation.
- [x] Import the actual module ZIP in a fresh process, run the combined PowerShell suite, verify loaded assembly locations and type/format/export assets, and prove rejection of teardown/discovery failures, empty suites and stale assemblies.
- [x] Complete the combined broad .NET 8/.NET 10 proof and one bounded independent read-only review; configured streaming retains providers, private anchors, cache controls and concurrency limits.
- [ ] Complete the authenticated DoT execution regression and repeat affected combined package/legacy proof after the transport candidate is frozen.
- [x] Remove superseded build/package extraction output and retain one current ZIP with concise proof and cleanup receipts.

## Existing proof and remaining qualification

The separate candidates have controlled .NET 8/.NET 10 regressions, loopback discovery and multicast peers, ephemeral certificate fixtures, private-zone DNSSEC and atomic UPDATE wire fixtures, and local .NET Framework 4.7.2 compilation. Independent Go producers add TSIG AXFR/IXFR, ODoH target/relay and DNSCrypt responder evidence. Local fresh package consumers and unsigned module ZIP imports cover their tested hosts. The combined source must repeat the affected proof; these earlier results do not qualify the merged candidate.

Live public DDR observations bind trusted certificates to the designating resolver's IP and preserve the original HTTPS authority during alternate-address queries. They describe the observed resolver/network environment, not all resolvers or general performance.

Remaining release/platform gates:

- [ ] Complete current-head CI and repository review settlement for each published PR; address validated findings and recheck delayed feedback.
- [ ] Qualify private-zone DNSSEC and UPDATE prerequisites against an independently operated authoritative lab.
- [ ] Execute combined packaged workflows on Windows PowerShell 5.1, minimum PowerShell 7.4 and supported modern PowerShell hosts.
- [ ] Verify DNSCrypt native assets and actual provider execution on Windows/Linux in addition to macOS ARM64.
- [ ] Qualify DoQ and HTTP/3 on supported Windows/Linux/macOS native runtimes, including certificate errors, cancellation and connection reuse.
- [ ] Exercise IPv6, split DNS and search-domain routing on representative hosts.
- [ ] Exercise CLI archives on their native x64/ARM64 architectures through the shared PowerForge publish configuration.

Local source, compiled assemblies, isolated packed candidates, hosted CI, merged source, public packages and installed versions remain separate delivery states. Coordinated release must publish the core and optional providers before downstream users rely on public package availability. Signing, public publication and deployment follow their normal authorized release process.

## Qualification limits

- QUIC execution requires runtime APIs and native host support; unsupported paths report that state explicitly. Local legacy compilation does not establish Windows runtime behavior.
- Isolated custom TLS roots and HTTP/2 ALPN verification require modern .NET. HTTP/1.1 or HTTP/2 with a different connection address and the original authority requires .NET 8 or later. Alternate-address HTTP/3 DDR reports `Unsupported` for GET and POST; same-address HTTP/3 and alternate-address DoQ retain QUIC capability checks.
- DDR is per resolver/network. Unverified or expired designations cannot create encrypted configurations, and discovery does not replace global DNS settings.
- Slow browse consumers apply bounded backpressure; UDP announcements may be lost. Applications should rediscover after network/interface changes.
- ODoH does not prevent relay/target collusion or traffic analysis. Supplied HTTP handlers must preserve documented privacy settings.
- DNSCrypt native packaging covers supported desktop architectures. Mobile static assets, post-quantum DNSCrypt and privacy-protocol stamp integration remain separate work.

## Next product work

- [ ] Expand independently produced protocol evidence for signed aliases, compact denial, transfers, updates and service discovery.
- [ ] Record a complete ordered delegation/trust graph before claiming a full resolution or DNSSEC chain; bind resolver-selection explanations to persisted score, policy and capability evidence.
- [ ] Distinguish endpoint identities throughout benchmark reports before allowing same-label variants in one profile. Publish performance claims only from repeatable scenarios that separate allocations, throughput and network latency.
- [ ] Design portable CLI profile overrides with explicit precedence when demonstrated callers need them.
- [ ] Define one deterministic DNS-only policy evaluator for blocking, static answers and resolver selection; keep packet, browser, proxy and OS startup management outside this library.
- [ ] Add hosting or rule packages only for demonstrated consumers with a distinct support and approved dependency contract.
