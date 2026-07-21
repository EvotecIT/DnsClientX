# DnsClientX.DnsSec.EdDsa

Optional RFC 8080 Ed25519 and Ed448 DNSSEC signature verification for DnsClientX. The package uses `BouncyCastle.Cryptography`; the main `DnsClientX` package remains free of that dependency.

```csharp
using DnsClientX;
using DnsClientX.DnsSec.EdDsa;

using var client = new ClientX(DnsEndpoint.RootServer);
client.EndpointConfiguration.UseEdDsaDnsSec();

DnsResponse response = await client.Resolve(
    "signed.example",
    DnsRecordType.A,
    requestDnsSec: true,
    validateDnsSec: true);
```

The verifier handles only DNSSEC algorithms 15 (Ed25519) and 16 (Ed448). DnsClientX core continues to handle its built-in RSA and ECDSA algorithms.
