# Optional encrypted DNS packages

DnsClientX core remains dependency-free. DNSCrypt v2 and Oblivious DoH are cryptographic protocols, not aliases for existing DNS transports, so they must not be represented as supported until dedicated packages pass protocol-vector and interoperability testing.

## `DnsClientX.DnsCrypt`

The first optional package should target modern .NET only and implement the DNSCrypt v2 X25519-XChaCha20-Poly1305 construction. The package should reference DnsClientX plus [NSec.Cryptography](https://nsec.rocks/), which supplies maintained libsodium-backed X25519, Ed25519, and XChaCha20-Poly1305 primitives under the MIT license.

Because the current NSec 26.4.0 package targets .NET 9 or later while DnsClientX also supports .NET 8, a multi-target package should use NSec 25.4.0 for `net8.0` and NSec 26.4.0 for `net10.0`, or wait until a current NSec release again covers both targets. The package must not copy or reimplement those primitives.

The initial supported contract should include:

- DNS stamp or explicit resolver/provider configuration; no misleading built-in endpoint names before they interoperate.
- Provider-certificate TXT discovery, Ed25519 signature verification, validity/serial selection, and certificate refresh before expiry.
- X25519 shared-key derivation and DNSCrypt certificate version `0x0002` only.
- XChaCha20-Poly1305 query/response framing, unique nonce handling, required padding validation, UDP, TCP, truncation/fallback, cancellation, and bounded resource use.
- Certificate, malformed-packet, replay/nonce, known-answer, and official-resolver interoperability tests. Captured secrets or private keys must never enter fixtures or logs.
- Capability reporting that distinguishes unsupported crypto suites from endpoint or transport failure.

The older XSalsa20-Poly1305 suite must be rejected explicitly unless a maintained, reviewed provider is selected. A signed public-resolver catalog can be a later feature, but its Minisign verification must also use a maintained implementation or receive a separate cryptographic review; parsing an unsigned downloaded catalog as trusted configuration is not acceptable.

## `DnsClientX.ObliviousDns`

ODoH remains deferred. [RFC 9230](https://www.rfc-editor.org/rfc/rfc9230.html) requires HPKE from [RFC 9180](https://www.rfc-editor.org/rfc/rfc9180.html), including the mandatory X25519/HKDF-SHA-256 KEM, HKDF-SHA-256 KDF, and AES-128-GCM AEAD suite. Supplying those primitives individually is not equivalent to a reviewed HPKE implementation.

The currently published `HPKE` 0.0.1 NuGet package describes itself as work in progress, dates from 2023, and has negligible ecosystem adoption. It is not an acceptable security dependency. DnsClientX should not compose HPKE itself from low-level primitives merely to claim ODoH support.

When a maintained, tested, and reviewable RFC 9180 dependency is available, an optional ODoH package still needs:

- strict target configuration/key parsing, selection, refresh, and key-id derivation;
- RFC 6570 proxy templates containing exactly `targethost` and `targetpath`;
- `application/oblivious-dns-message` request/response validation, no-cache behavior, and redirect handling that never bypasses the proxy;
- fresh query context and response-secret derivation, padding, response binding, and negative/adversarial vectors;
- at least two independent target/proxy interoperability environments and a documented non-collusion trust model.

Until those gates are met, the core endpoint and stamp parsers should continue to reject ODoH and DNSCrypt requests with explicit unsupported-protocol errors.
