# DnsClientX.DnsCrypt

This optional package sends DNS queries using DNSCrypt v2. It verifies resolver certificates against an explicitly trusted Ed25519 provider key and authenticates each encrypted response before returning it to DnsClientX.

`DnsClientX.DnsCrypt` is not published on NuGet and is not included in the core 2.2.0 release. Build this project from the same source revision as the core, then deploy its DLL and runtime dependencies together. Its libsodium dependency is restored by the source build. Obtain the resolver IP address, port, provider name, and provider public key through a trusted source.

```csharp
using DnsClientX;
using DnsClientX.DnsCrypt;

var options = new DnsCryptOptions(
    resolverAddress, 443, providerName, providerPublicKeyHex);
using var provider = new DnsCryptClient(options);
using var client = provider.CreateClient();
var answer = await client.Resolve("example.com", DnsRecordType.A);
```

The provider snapshots options at construction and supports concurrent queries. Keep it alive for every client using its configuration; `ClientX` does not dispose a caller-supplied provider. `CreateConfiguration()` also lets you use the standard client configuration path. Cancel requests with their cancellation token; disposing the provider cancels all its active exchanges.

`QueryAsync()` returns an authenticated DNS wire message directly. `QueryExchangeAsync()` also reports whether that particular exchange completed over UDP or TCP, including a TCP retry; the high-level client retains that information in `UsedTransport`.

`NetworkTransport` defaults to UDP with an encrypted TCP retry after timeout or an authenticated truncated response. Set it to `DnsCryptNetworkTransport.Tcp` to use TCP immediately. `EncryptionPolicy` defaults to preferring the NaCl XChaCha20 suite while allowing deployed XSalsa20 certificates. Set `RequireXChaCha20` or `RequireXSalsa20` to require one suite. These DNSCrypt constructions are not interchangeable with IETF ChaCha20-Poly1305 AEAD.

`Timeout` limits each exchange to five seconds by default. One overall deadline covers a provider query or explicit certificate refresh, including discovery, waiting for another refresh, TCP fallback, and one certificate recovery. `OverallTimeout` can set that deadline explicitly. Otherwise it allows eight exchange timeouts for UDP with fallback (40 seconds by default), or four for TCP only (20 seconds). Very large derived deadlines are capped at `Int32.MaxValue` milliseconds. `CreateConfiguration()` uses the same overall budget for `TimeOut`; assigning a shorter `TimeOut` or canceling the caller's token stops the operation before further provider retries. High-level transient retries follow the client's retry policy; a cancellation-token deadline can bound the complete high-level request.

Use this provider with a recursive resolver. Root iteration rejects optional privacy transports.

To use Anonymized DNSCrypt, set `RelayEndpoint` to the relay's IP endpoint before constructing the provider. Both certificate discovery and encrypted requests go through that relay. The configured resolver endpoint remains the authenticated destination. A relay still sees the client address and selected resolver; traffic timing can identify related exchanges. A truncated reply through a relay may remain truncated because the relay's upstream transport is UDP.

Certificate discovery sends only the configured provider TXT question without application encryption. Returned certificates must have a valid signature and validity interval. Refresh happens periodically and during exchange recovery. Failure never enables cleartext application DNS.

The dynamic native backend targets desktop Windows, Linux, and macOS architectures supplied by the libsodium package. .NET Framework consumers using either `PackageReference` or `packages.config` receive Windows native assets in `runtimes/win-*/native`. Legacy consumers use the libsodium version installed in their `packages.config`. The provider loads the asset matching the running process from the original provider assembly directory or the application's deployment directory, including when the managed assembly is shadow copied. Apple mobile static libraries, post-quantum DNSCrypt, and DNSSEC validation are separate capabilities.

## Trusted resolver stamps and command-line queries

`DnsCryptOptions.FromStamp(resolverStamp, relayStamp)` retains the resolver IP, certificate provider name and trusted public key. The optional second stamp selects an Anonymized DNSCrypt relay without replacing the authenticated resolver identity. Obtain stamps from a trusted resolver directory or the provider's own documentation.

```csharp
using var transport = new DnsCryptTransportFactory().Create(resolverStamp, relayStamp);
using var client = new ClientX(transport.CreateConfiguration());
var response = await client.Resolve("example.com", DnsRecordType.A);
```

The CLI can load the deployed provider explicitly, with its managed and native dependencies beside it:

```text
DnsClientX --privacy-provider ./DnsClientX.DnsCrypt.dll --privacy-resolver sdns://... --format json example.com
```

Add `--privacy-relay sdns://...` for an anonymizing relay. Query output, `--short`, `--explain` and cancellation use the normal shared query workflow; these options do not select probe, benchmark, update or zone-transfer modes. Loading a provider DLL executes local code, so select only a trusted deployment. The CLI owns and disposes that provider after the query completes.

```powershell
$transport = New-DnsPrivacyTransport -ProviderPath ./DnsClientX.DnsCrypt.dll -Resolver $resolverStamp -Relay $relayStamp
try {
    Resolve-Dns example.com -Configuration $transport.CreateConfiguration() -FullResponse
} finally {
    $transport.Dispose()
}
```

`ConvertFrom-DnsStamp` and CLI `--stamp-info` inspect privacy roles without loading a provider or contacting a server. Stamp property bits describe the directory's claims; they do not independently verify DNSSEC, logging or filtering behavior.
