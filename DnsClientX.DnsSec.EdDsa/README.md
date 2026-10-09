# DnsClientX.DnsSec.EdDsa

Optional portable RFC 6605 ECDSA and RFC 8080 Ed25519/Ed448 DNSSEC signature verification for DnsClientX. The package uses `BouncyCastle.Cryptography`; the main `DnsClientX` package remains free of that dependency.

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

The verifier handles algorithms 13 (ECDSA P-256), 14 (ECDSA P-384), 15 (Ed25519), and 16 (Ed448) on every supported target, including .NET Framework 4.7.2. When configured, it owns verification for those algorithms; core handles its other built-in algorithms. `client.EndpointConfiguration.SupportedDnsSecAlgorithms` lists the effective capabilities.

The CLI and PowerShell module can load the optional assembly explicitly. Place the matching provider and `BouncyCastle.Cryptography.dll` together in a local folder, using the provider build for the active runtime:

```powershell
Resolve-Dns -Name example.com -Type SOA -DnsProvider Cloudflare -ValidateDnsSec -DnsSecVerifierPath './provider/DnsClientX.DnsSec.EdDsa.dll'
```

```text
dnsclientx example.com -t SOA --validate-dnssec --dnssec-verifier ./provider/DnsClientX.DnsSec.EdDsa.dll
```

`Test-DnsProbe` and `Test-DnsBenchmark` accept the same `DnsSecVerifierPath` parameter. Loading requires the assembly's single public, parameterless `IDnsSecSignatureVerifier` implementation; missing dependencies or ambiguous providers fail explicitly. The CLI and module do not install or download cryptographic providers.
