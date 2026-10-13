# DnsClientX.DnsCrypt

This optional package sends DNS queries using DNSCrypt v2. It verifies resolver certificates against an explicitly trusted Ed25519 provider key and authenticates each encrypted response before returning it to DnsClientX.

Install `DnsClientX.DnsCrypt` alongside `DnsClientX`. Obtain the resolver IP address, port, provider name, and provider public key through a trusted source. The package includes its libsodium native dependency through NuGet.

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

Use this provider with a recursive resolver. Root iteration rejects optional privacy transports.

To use Anonymized DNSCrypt, set `RelayEndpoint` to the relay's IP endpoint before constructing the provider. Both certificate discovery and encrypted requests go through that relay. The configured resolver endpoint remains the authenticated destination. A relay still sees the client address and selected resolver; traffic timing can identify related exchanges. A truncated reply through a relay may remain truncated because the relay's upstream transport is UDP.

Certificate discovery sends only the configured provider TXT question without application encryption. Returned certificates must have a valid signature and validity interval. Refresh happens periodically and during exchange recovery. Failure never enables cleartext application DNS.

The dynamic native backend targets desktop Windows, Linux, and macOS architectures supplied by the libsodium package. .NET Framework consumers receive Windows native assets in `runtimes/win-*/native`; the provider loads the asset matching the running process. Apple mobile static libraries, post-quantum DNSCrypt, DNS stamps, and DNSSEC validation are separate capabilities.
