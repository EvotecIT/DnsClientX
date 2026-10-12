# DnsClientX engineering plan

DnsClientX provides one DNS engine for C#, PowerShell and command-line users. The core owns protocol handling, record parsing, validation, caching and resolver operations. The surfaces expose that behavior without maintaining separate query engines.

## Maintained capabilities

- UDP with TCP fallback, TCP, TLS, HTTPS wire and JSON, HTTP/2, HTTP/3, QUIC and gRPC transports, with runtime capability reporting. Modern transports depend on the target runtime and native platform support.
- Typed records with owner, class, remaining TTL and raw record metadata; unknown payloads retain an explicit representation.
- Local DNSSEC validation, authenticated denial proofs, iterative root resolution, QNAME minimization and RFC 5011 trust-anchor lifecycle management. Portable ECDSA and EdDSA verification remains an optional package.
- Persistent TCP/DoT reuse, response correlation, cancellation and bounded recovery. Exact-policy response cache single-flight distinguishes network, coalesced and cached results.
- System resolver discovery and supported Windows NRPT routing, explicit bootstrap resolvers, DNS stamps, EDNS NSID, padding, cookies and client subnet.
- AXFR, IXFR, transfer over TLS, ZONEMD validation, authenticated updates and DNS service discovery.
- Resolver catalog validation, probe/benchmark policies, saved score snapshots and resolver selection/reuse.
- Bounded local health profiles, scope-aware snapshot comparisons and structured query explanations.

## Priorities 1–3: implemented contracts

These contracts provide the foundation for usable C#, PowerShell and CLI workflows:

- [x] Bound negative cache entries by SOA TTL/MINIMUM, aliases and denial proofs; age public TTLs while preserving receipt-time wire evidence.
- [x] Bound streaming admission and completed results; cancel and drain admitted work when the consumer stops, while preserving independent single-flight waiters.
- [x] Make transport capability reports complete and reject unsupported legacy gRPC and unknown transport values consistently.
- [x] Emit individual PowerShell answer/service rows, bind pipeline names, propagate cancellation and honor update confirmation before network activity.
- [x] Keep JSON CLI output parseable with explanation/trace, expose complete probe/benchmark reports and support direct custom resolver queries.
- [x] Add reusable client query options, typed metadata projections, ordered/completion-order streams and process-wide cache management.
- [x] Reuse DNSSEC material only after accepted validation, bound it by TTL/proof expiry and revalidate it for each answer.
- [x] Add repeatable health profiles, comparisons with explicit endpoint scope limitations and caller-supplied authoritative reference responses.
- [x] Preserve tested endpoint settings in saved recommendations; report ambiguous execution evidence instead of inferring a reusable endpoint.

Protocol fixtures and loopback peers establish controlled correctness. They do not establish independent resolver interoperability, native QUIC availability on every platform, public package publication or universal performance.

## Release and qualification gates

Before distributing a release, validate the actual core and optional-provider NuGet packages through isolated local-feed consumers, import the actual PowerShell package in fresh processes, and exercise CLI archives on their native architectures. The package matrix covers Windows PowerShell 5.1, PowerShell 7.4 and modern PowerShell on Windows, Linux and macOS. Runtime transport reports must describe unsupported paths clearly.

The optional DNSSEC provider must be published alongside the core through the coordinated release flow before consumers can rely on its public NuGet availability. Source integration, local packages, public NuGet/PSGallery packages and installed user versions are separate delivery states. CLI archives use the shared PowerForge publish configuration for x64 and ARM64; native execution qualifies only the tested host/RID.

## Next priorities

### Independent protocol qualification

- [ ] Expand repeatable interoperability against independently produced DNS messages and resolvers for signed aliases, compact denial, transfers, updates and service discovery.
- [ ] Qualify DoQ and HTTP/3 on supported Windows/Linux/macOS native runtimes, including certificate errors, timeout/cancellation and connection reuse; record unavailable platform paths.
- [ ] Exercise IPv6, split DNS and platform search-domain routing on representative hosts without treating controlled fixtures as native policy proof.
- [ ] Measure warm DNSSEC material reuse and bounded streaming with the existing controlled load runner. Separate allocations, throughput and network latency; publish only reproducible scenario claims.

### Diagnostic depth

- [ ] Record a complete ordered iterative delegation/trust graph before claiming that explanations show a full resolution or DNSSEC chain.
- [ ] Add resolver-selection explanations that bind the chosen endpoint to persisted score, policy and capability evidence.
- [ ] Distinguish endpoint execution identities throughout benchmark reports before allowing same-label variants in one benchmark profile. Probe-only profiles already preserve distinct settings; combined benchmark profiles reject ambiguous labels before execution.
- [ ] Design profile overrides for CLI users only when their precedence can remain explicit and portable. Typed C#/PowerShell profiles can already be modified before execution.

### DNS-only policy

- [ ] Define a deterministic, offline-testable rule model for domain blocking, static answers and resolver selection.
- [ ] Keep one canonical policy evaluator behind library, CLI and PowerShell explain workflows.
- [ ] Keep packet manipulation, browser traffic control, proxy orchestration and OS startup management outside this library.

### Optional protocol packages

- [ ] Assess DNSCrypt and complete ODoH interoperability as separately reviewed protocol work, including dependency approval and independent fixtures.
- [ ] Add server hosting or rule packages only for demonstrated consumers with a distinct support and dependency contract.

DoH3 and DoQ stay in core where the runtime supplies them. Specialized crypto and hosting dependencies belong in optional owners. A new protocol name or green synthetic test alone does not make a feature supported.
