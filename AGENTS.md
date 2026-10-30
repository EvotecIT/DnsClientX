# Working in DnsClientX

Resolve the shared procedures below against `CODEX_HOME` (default `~/.codex`).

Before adding or changing tests, apply the [test-audit authoring gate](skills/test-audit/SKILL.md#authoring-gate) and use [contract-focused proof design](skills/contract-focused-tests/SKILL.md). Deliberate pruning follows [campaign mode](skills/test-audit/CAMPAIGN.md). Coverage is local campaign evidence, never a CI or merge gate.

Keep live DNS and load workloads opt-in. Use the existing `DNSCLIENTX_RUN_REAL_DNS_TESTS` switch for live tests and `DNSCLIENTX_RUN_MODERN_TRANSPORT_TESTS` for modern live transports. Keep deterministic loopback protocol and lifetime fixtures in the ordinary suite, with supported target and platform coverage.
