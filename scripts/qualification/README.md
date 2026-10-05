# Native DNS qualification

This non-packable runner exercises public DnsClientX APIs against live DNS providers and native resolver settings. It records signed and deliberately broken DNSSEC outcomes, OS-selected resolver behavior, and DNS-over-QUIC/HTTP/3 answers. Failures retain the native error and runtime support state. Results describe the measured host, network and provider at that time; they are separate from deterministic correctness suites and package-release evidence.

The `Qualify live DNS` workflow runs on disposable Linux and Windows hosts when this runner changes or when dispatched manually. Windows additionally creates one `.qualification.invalid` NRPT rule directed to a private loopback responder, verifies discovery and actual routing, then removes that exact rule and stops the responder in `finally`. The NRPT script refuses to run outside a GitHub-hosted Actions runner. It never changes a workstation, domain policy or production resolver.

Run the read-only live probes locally with:

```sh
dotnet run --project scripts/qualification/Qualification.csproj -c Release -f net10.0
```

QUIC and HTTP/3 require the platform's supported MsQuic runtime. The Linux workflow installs that existing platform dependency on its disposable host. Live provider availability or network restrictions can prevent qualification without identifying a product defect.

The DoQ endpoint follows [AdGuard's published provider configuration](https://adguard-dns.io/kb/general/dns-providers/); HTTP/3 uses [Cloudflare's documented DoH endpoint](https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/). `--deadline-ms=45000` sets each probe's cancellation bound. Exceptions are recorded per case so one failed endpoint does not discard earlier evidence or stop the remaining probes.
