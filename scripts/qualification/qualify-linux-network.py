#!/usr/bin/env python3
"""Qualify OS DNS discovery and adapter changes in private Linux namespaces.

Build the test project first. Invoke this script through unshare with fresh
network AND mount namespaces, passing the parent's namespace identities. The
script refuses the host namespaces and never edits the host's resolver file.
This is virtual kernel-network evidence, not physical cross-host acceptance.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host-network", required=True)
    parser.add_argument("--host-mount", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--lab-root", type=Path, help="Existing writable directory allowed by the host BIND confinement policy")
    parser.add_argument("--artifacts", type=Path, required=True)
    parser.add_argument("--framework", choices=("net8.0", "net10.0"), required=True)
    args = parser.parse_args()
    namespaces = {name: os.readlink(f"/proc/self/ns/{name}") for name in ("net", "mnt")}
    if os.geteuid() != 0 or namespaces["net"] == args.host_network or namespaces["mnt"] == args.host_mount:
        raise RuntimeError("Fresh private Linux network and mount namespaces are required")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    repo = Path(__file__).resolve().parents[2]

    def run(*arguments):
        return subprocess.run(list(map(str, arguments)), check=True, text=True, capture_output=True, timeout=20)

    # The inherited sysfs still describes the parent's network namespace.
    # Only after both namespace identities are verified may mounts be changed.
    run("mount", "--make-rprivate", "/")
    run("mount", "-t", "sysfs", "sysfs", "/sys")
    if set(os.listdir("/sys/class/net")) != {"lo"}:
        raise RuntimeError("Expected a fresh network namespace containing only loopback")

    with tempfile.TemporaryDirectory(prefix="network-lab-", dir=args.lab_root.resolve() if args.lab_root else output) as temporary:
        lab = Path(temporary)
        run("ip", "link", "set", "lo", "up")
        for number in (1, 2):
            name = f"dnsqual{number}"
            run("ip", "link", "add", name, "type", "dummy")
            run("ip", "address", "add", f"10.53.{number}.1/24", "dev", name)
            run("ip", "-6", "address", "add", f"fd53:{number}::1/64", "dev", name, "nodad")
            run("ip", "link", "set", name, "multicast", "on", "up")
        resolver = lab / "resolv.conf"
        resolver.write_text("nameserver 10.53.1.1\nsearch missing.search.test first.search.test\noptions ndots:1\n")
        target = Path("/etc/resolv.conf").resolve()
        run("mount", "--bind", resolver, target)
        process = None
        try:
            configuration = f'''options {{
 directory {json.dumps(str(lab))};
 listen-on port 53 {{ 10.53.1.1; 10.53.2.1; }};
 listen-on-v6 port 53 {{ fd53:1::1; fd53:2::1; }};
 recursion no; dnssec-validation no;
 pid-file {json.dumps(str(lab / 'named.pid'))};
 session-keyfile {json.dumps(str(lab / 'session.key'))};
}};
controls {{ }};
zone "search.test" {{ type primary; file "search.db"; }};
'''
            (lab / "named.conf").write_text(configuration)
            (lab / "search.db").write_text('''$ORIGIN search.test.
$TTL 30
@ IN SOA ns hostmaster (1 60 60 3600 30)
  IN NS ns
ns IN A 10.53.1.1
host.first IN A 192.0.2.53
host.first IN AAAA 2001:db8::53
host.second IN A 192.0.2.54
host.second IN AAAA 2001:db8::54
''')
            manifest = lab / "network.json"
            manifest.write_text(json.dumps({"resolverFile": str(resolver), "networkNamespace": namespaces["net"],
                                            "mountNamespace": namespaces["mnt"]}))
            with (output / "named.log").open("w") as log:
                process = subprocess.Popen(["named", "-g", "-n", "1", "-c", str(lab / "named.conf")],
                                           cwd=lab, stdout=log, stderr=subprocess.STDOUT)
                for _ in range(50):
                    if process.poll() is not None:
                        raise RuntimeError("Isolated BIND exited during startup")
                    probe = subprocess.run(["dig", "@10.53.1.1", "search.test", "SOA", "+short", "+time=1", "+tries=1"],
                                           text=True, capture_output=True, timeout=3)
                    if probe.returncode == 0 and "hostmaster" in probe.stdout:
                        break
                    time.sleep(0.1)
                else:
                    raise RuntimeError("Isolated BIND did not become ready")
                environment = os.environ.copy()
                environment["DNSCLIENTX_NETWORK_MANIFEST"] = str(manifest)
                subprocess.run(["dotnet", "test", str(repo / "DnsClientX.Tests/DnsClientX.Tests.csproj"), "-c", "Release",
                                "-f", args.framework, "--no-build", "--no-restore", "--artifacts-path", str(args.artifacts.resolve()),
                                "--filter", "FullyQualifiedName~IsolatedLinuxNetwork", "--logger", "trx;LogFileName=network.trx",
                                "--results-directory", str(output / "results")], cwd=repo, env=environment, check=True)
                (output / "namespace-receipt.json").write_text(json.dumps({"namespaces": namespaces,
                    "hostNetwork": args.host_network, "hostMount": args.host_mount,
                    "interfaces": json.loads(run("ip", "-json", "address", "show").stdout),
                    "sysfsRemounted": True, "evidenceKind": "isolated Linux kernel network; no physical peer"}, indent=2))
        finally:
            if process is not None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)
            run("umount", target)


if __name__ == "__main__":
    main()
