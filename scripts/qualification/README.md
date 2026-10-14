# DNS interoperability and network qualification

These harnesses use public DnsClientX operations against independent servers and native platform transports. They are test tools; users do not need BIND, Python or ASP.NET Core to install the library.

## Independent authoritative server

Install BIND test tools (`named`, `dig`, `dnssec-keygen`, `dnssec-signzone`, `dnssec-dsfromkey`), then run:

```sh
python3 scripts/qualification/qualify-bind.py --framework net10.0 --output /path/to/task-output/bind
```

On macOS, add `--tool-directory "$(brew --prefix bind)/bin"`. On confined Linux hosts, use `--lab-root` with a dedicated writable directory under the existing BIND data location (for example `/var/lib/bind/dnsclientx-qualification`). This keeps AppArmor enforcement intact. The script creates private `.test` zones, software signing keys and a synthetic TSIG key in a temporary child of that lab root, or the output directory when no lab root is supplied. BIND runs in the foreground on loopback and an unprivileged port. The script stops the server and removes the keys in `finally`; it retains producer logs and TRX results. No system service or resolver configuration changes.

The cases cover RSA signatures anchored by private DS records, NSEC/NSEC3 denial, CNAME/DNAME, wrong and expired anchors, atomic UPDATE prerequisites, multi-frame authenticated AXFR, incremental IXFR and no-change responses. This provides independent implementation evidence; it is not an independently operated deployment or a general server-compatibility claim.

## Native encrypted transports

On Windows, the .NET runtime supplies MsQuic. Linux requires `libmsquic`. macOS uses Homebrew `libmsquic`; .NET 8 also needs an available OpenSSL 3 library:

```sh
DYLD_FALLBACK_LIBRARY_PATH="$(brew --prefix)/lib:$(brew --prefix openssl@3)/lib" \
DNSCLIENTX_REQUIRE_QUIC=1 dotnet test DnsClientX.Tests/DnsClientX.Tests.csproj \
  -c Release -f net8.0 \
  --filter 'FullyQualifiedName~DnsQuicConnectionPool|FullyQualifiedName~NativeEncryptedDns'
```

Omit the macOS library variable on Windows/Linux. `DNSCLIENTX_REQUIRE_QUIC=1` makes an unavailable runtime fail the dedicated qualification job. Tests use ephemeral certificates, isolated trust roots and real DoQ/HTTP/3 peers. They check certificate rejection, answers, native protocol use, deadlines, caller cancellation, reuse and disposal. Direct HTTP queries retain their HttpClient deadline exception contract; query workflows classify those failures in their result metadata.

## Isolated Linux OS network

Build before entering the namespaces so restore does not need network access inside the lab:

```sh
dotnet build DnsClientX.Tests/DnsClientX.Tests.csproj -c Release -f net10.0 --artifacts-path artifacts/build
host_network=$(readlink /proc/self/ns/net)
host_mount=$(readlink /proc/self/ns/mnt)
sudo unshare --net --mount --propagation private --fork \
  python3 scripts/qualification/qualify-linux-network.py \
    --host-network "$host_network" --host-mount "$host_mount" \
    --framework net10.0 --artifacts "$PWD/artifacts/build" --output "$PWD/artifacts/network"
```

Use a disposable Linux environment with BIND, `iproute2` and `util-linux`. The harness refuses the supplied host network/mount namespaces and requires an initially empty network containing only loopback. It mounts a fresh sysfs inside that private mount namespace so interface discovery describes the isolated network. It creates two virtual adapters and binds its own resolver file over `/etc/resolv.conf` only in the private mount namespace. Tests exercise real OS discovery, suffix fallback, absolute names, IPv6 unicast DNS, multicast interface routing and explicit rediscovery after an adapter/configuration change. The server and resolver bind mount are removed on exit; the kernel removes the private network with the process.

The Windows live-DNS harness separately exercises NRPT routing on disposable hosted runners. Physical cross-host mDNS, physical link/VPN changes and macOS scoped resolver routing remain qualification gaps recorded in `PLAN.md`.

## Live providers and Windows NRPT

This non-packable runner exercises public DnsClientX APIs against live DNS providers and native resolver settings. It records signed and deliberately broken DNSSEC outcomes, OS-selected resolver behavior, and DNS-over-QUIC/HTTP/3 answers. Failures retain the native error and runtime support state. Results describe the measured host, network and provider at that time; they are separate from deterministic correctness suites and package-release evidence.

The `Qualify live DNS` workflow runs on disposable Linux and Windows hosts when this runner changes or when dispatched manually. Windows additionally creates one `.qualification.invalid` NRPT rule directed to a private loopback responder, verifies discovery and actual routing, then removes that exact rule and stops the responder in `finally`. The NRPT script refuses to run outside a GitHub-hosted Actions runner. It never changes a workstation, domain policy or production resolver.

Run the read-only live probes locally with:

```sh
dotnet run --project scripts/qualification/Qualification.csproj -c Release -f net10.0
```

QUIC and HTTP/3 require the platform's supported MsQuic runtime. The Linux workflow installs that existing platform dependency on its disposable host. Live provider availability or network restrictions can prevent qualification without identifying a product defect.

The DoQ endpoint follows [AdGuard's published provider configuration](https://adguard-dns.io/kb/general/dns-providers/); HTTP/3 uses [Cloudflare's documented DoH endpoint](https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/). `--deadline-ms=45000` sets each probe's cancellation bound. Exceptions are recorded per case so one failed endpoint does not discard earlier evidence or stop the remaining probes.
