# DnsClientX.Odoh

This optional package sends DNS queries through an HTTPS relay using Oblivious DNS over HTTPS (RFC 9230). It encrypts each query for the target with fresh HPKE key material and authenticates the encrypted response before the core DNS client matches and parses it. It supports the RFC's required X25519/HKDF-SHA256/AES-128-GCM suite on all DnsClientX targets.

```csharp
using DnsClientX;
using DnsClientX.Odoh;

using var provider = new OdohClient(new OdohClientOptions(
    new Uri("https://relay.example/dns-query"),
    new Uri("https://target.example/dns-query")));
using var client = new ClientX(provider.CreateConfiguration());
var response = await client.Resolve("example.com", DnsRecordType.A);
```

Choose a relay and target that support ODoH. The default configuration URL is the target origin's `/.well-known/odohconfigs`; `ConfigurationUri` can select another authenticated HTTPS source. The provider refreshes expired keys, honors HTTP configuration freshness with a one-day upper bound, and fails if no supported configuration is offered. `OdohTargetConfiguration.Parse` accepts an already acquired configuration list with an explicit expiry. Set `InitialConfiguration` to use it until expiry.

The provider owns its default `HttpClient`, disables cookies and automatic redirects, and never retries a failed exchange through cleartext DNS or direct DoH. Each response is bound to its individual query. Query plaintext is padded to 128-byte blocks by default; `PaddingBlockSize = 0` disables padding. Concurrent queries and cancellation are supported. The caller owns the provider lifetime even when its configuration is used by `ClientX`.

A supplied `HttpClient` remains caller-owned unless `disposeHttpClient` is true. Its handler must disable automatic redirects, cookies, and default credentials, and must not inject identifying headers or change destinations. The provider cannot inspect these handler settings. Use the default client when those settings cannot be guaranteed. Default request headers other than `Accept` are rejected.

HTTPS authenticates key acquisition. HPKE authenticates the response under the acquired target key; it does not validate DNSSEC or protect against a colluding relay and target. Configuration discovery and traffic analysis policy are outside RFC 9230. This package uses BouncyCastle.Cryptography for portable X25519 and AES-GCM primitives; its public API exposes no Bouncy Castle types.

## Stamps and explicit provider setup

`OdohClientOptions.FromEndpoints(relay, target)` accepts HTTPS endpoints or role-specific ODoH stamps. A target stamp cannot be substituted for a relay stamp. The relay remains explicit; no default relay or direct-query fallback is selected.

```csharp
using var transport = new OdohTransportFactory().Create(targetStamp, relayStamp);
using var client = new ClientX(transport.CreateConfiguration());
var response = await client.Resolve("example.com", DnsRecordType.A);
```

The CLI loads a trusted deployed provider and uses the shared query, output and explanation workflow:

```text
DnsClientX --privacy-provider ./DnsClientX.Odoh.dll --privacy-resolver https://target.example/dns-query --privacy-relay https://relay.example/dns-query --explain example.com
```

Either endpoint can instead be its `sdns://` stamp. These are query options; they do not select probe, benchmark, update or zone-transfer modes. Dependencies must be deployed beside the selected DLL. Loading a DLL executes local code, so select only a trusted provider. The CLI disposes its provider after the query. PowerShell callers own the returned provider:

```powershell
$transport = New-DnsPrivacyTransport -ProviderPath ./DnsClientX.Odoh.dll -Resolver $targetStamp -Relay $relayStamp
try {
    Resolve-Dns example.com -Configuration $transport.CreateConfiguration() -FullResponse
} finally {
    $transport.Dispose()
}
```

The stamp importer rejects nonempty relay certificate hashes and explicit IP/bootstrap routing because the HTTPS provider cannot enforce those stamp policies. It never silently drops certificate pins or changes the relay/target role. `ConvertFrom-DnsStamp` and CLI `--stamp-info` inspect supported privacy stamps without loading a provider or contacting a server.
