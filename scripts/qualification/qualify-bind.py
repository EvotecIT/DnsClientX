#!/usr/bin/env python3
"""Run independent BIND qualification in a disposable, loopback-only lab.

BIND is a test tool, never a shipped dependency or a system service. The caller
owns the output directory; ephemeral signing and TSIG keys stay in its lab child.
"""
import argparse
import base64
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--lab-root", type=Path, help="Existing writable directory allowed by the host BIND confinement policy")
    parser.add_argument("--framework", choices=("net8.0", "net10.0"), required=True)
    parser.add_argument("--tool-directory", type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    tools = {}
    for name in ("named", "dnssec-keygen", "dnssec-signzone", "dnssec-dsfromkey", "dig"):
        tool = str(args.tool_directory / name) if args.tool_directory else shutil.which(name)
        if args.tool_directory and not Path(tool).is_file():
            tool = str(args.tool_directory.parent / "sbin" / name)
        if not tool or not Path(tool).is_file():
            raise RuntimeError(f"BIND test tool is missing: {name}")
        tools[name] = tool
    repo = Path(__file__).resolve().parents[2]
    with tempfile.TemporaryDirectory(prefix="bind-lab-", dir=args.lab_root.resolve() if args.lab_root else output) as temporary:
        lab = Path(temporary)
        with socket.socket() as reservation:
            reservation.bind(("127.0.0.1", 0))
            port = reservation.getsockname()[1]
        secret = base64.b64encode(os.urandom(32)).decode()
        zones = []
        with (output / "bind-producer.log").open("w") as log:
            def run(name, *arguments):
                result = subprocess.run([tools[name], *map(str, arguments)], cwd=lab,
                                        text=True, capture_output=True, check=True, timeout=90)
                log.write(result.stdout + result.stderr)
                log.flush()
                return result.stdout.strip()

            version = run("named", "-v")
            for prefix in ("nsec", "nsec3"):
                zone = f"{prefix}.qualification.test"
                directory = lab / prefix
                directory.mkdir()
                source = directory / "zone.db"
                source.write_text(f"""$ORIGIN {zone}.
$TTL 120
@ IN SOA ns hostmaster (1 60 60 3600 120)
  IN NS ns
ns IN A 127.0.0.1
www IN A 192.0.2.9
www IN AAAA 2001:db8::9
alias IN CNAME www
old IN DNAME new
host.new IN A 192.0.2.10
""")
                ksk = run("dnssec-keygen", "-q", "-a", "RSASHA256", "-b", "2048", "-n", "ZONE", "-f", "KSK", "-K", directory, zone)
                run("dnssec-keygen", "-q", "-a", "RSASHA256", "-b", "2048", "-n", "ZONE", "-K", directory, zone)
                signed = directory / "zone.signed"
                options = ["-3", "A1B2C3D4", "-H", "1"] if prefix == "nsec3" else []
                run("dnssec-signzone", "-n", "1", "-S", "-K", directory, "-o", zone, "-f", signed, *options, source)
                ds = run("dnssec-dsfromkey", "-2", directory / (ksk + ".key"))
                fields = ds.split()
                zones.append({"name": zone, "keyTag": int(fields[3]), "algorithm": int(fields[4]),
                              "digestType": int(fields[5]), "digest": "".join(fields[6:])})
            (lab / "updates.db").write_text("""$ORIGIN updates.qualification.test.
$TTL 120
@ IN SOA ns hostmaster (1 60 60 3600 120)
  IN NS ns
ns IN A 127.0.0.1
existing IN A 192.0.2.1
existing IN A 192.0.2.2
""")
            def quoted(path):
                return json.dumps(str(path))

            configuration = f"""options {{
 directory {quoted(lab)};
 listen-on port {port} {{ 127.0.0.1; }};
 listen-on-v6 port {port} {{ ::1; }};
 recursion no; dnssec-validation no; transfer-format one-answer;
 pid-file {quoted(lab / 'named.pid')};
 session-keyfile {quoted(lab / 'session.key')};
 managed-keys-directory {quoted(lab)};
 allow-query {{ 127.0.0.1; ::1; }};
}};
controls {{ }};
key "qualification-key" {{ algorithm hmac-sha256; secret "{secret}"; }};
"""
            for zone in zones:
                prefix = zone["name"].split(".")[0]
                configuration += f'zone "{zone["name"]}" {{ type primary; file {quoted(lab / prefix / "zone.signed")}; }};\n'
            configuration += f"""zone "updates.qualification.test" {{
 type primary; file {quoted(lab / 'updates.db')};
 allow-update {{ key qualification-key; }};
 allow-transfer {{ key qualification-key; }};
 ixfr-from-differences yes;
}};
"""
            config = lab / "named.conf"
            config.write_text(configuration)
            manifest = lab / "lab.json"
            manifest.write_text(json.dumps({"port": port, "secret": secret, "zones": zones}))
            with (output / "named.log").open("w") as server_log:
                server = subprocess.Popen([tools["named"], "-g", "-n", "1", "-c", str(config)],
                                          cwd=lab, stdout=server_log, stderr=subprocess.STDOUT)
                try:
                    ready = False
                    for _ in range(100):
                        if server.poll() is not None:
                            raise RuntimeError("BIND exited during startup; see named.log")
                        probe = subprocess.run([tools["dig"], "@127.0.0.1", "-p", str(port), zones[0]["name"],
                                                "SOA", "+short", "+time=1", "+tries=1"], text=True, capture_output=True, timeout=3)
                        if probe.returncode == 0 and "hostmaster" in probe.stdout:
                            ready = True
                            break
                        time.sleep(0.1)
                    if not ready:
                        raise RuntimeError("BIND did not become ready")
                    # Record independently decoded signed/denial envelopes, not only client verdicts.
                    for zone in zones:
                        run("dig", "@127.0.0.1", "-p", port, "absent." + zone["name"], "A", "+dnssec", "+noall", "+comments", "+authority")
                    environment = os.environ.copy()
                    environment["DNSCLIENTX_BIND_MANIFEST"] = str(manifest)
                    subprocess.run(["dotnet", "test", str(repo / "DnsClientX.Tests/DnsClientX.Tests.csproj"),
                                    "-c", "Release", "-f", args.framework, "--artifacts-path", str(output / "artifacts"),
                                    "--filter", "FullyQualifiedName~BindAuthoritative", "--logger", "trx;LogFileName=bind.trx",
                                    "--results-directory", str(output / "results")], cwd=repo, env=environment, check=True)
                    (output / "producer.json").write_text(json.dumps({"producer": version, "framework": args.framework,
                        "transport": "loopback IPv4 and IPv6", "zones": [zone["name"] for zone in zones]}, indent=2))
                finally:
                    server.terminate()
                    try:
                        server.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        server.kill()
                        server.wait(timeout=10)


if __name__ == "__main__":
    main()
