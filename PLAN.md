# PLAN

## Purpose

This roadmap tracks the next library-aligned work for DnsClientX after the recent cleanup wave.

The project identity remains:

- library first
- cross-platform core
- dependency-light main package
- CLI and PowerShell surfaces built on the same engine
- advanced DNS protocol features without drifting into unrelated network product areas

## Principles

### Keep The Core Focused

The main package should stay centered on DNS resolution, DNS transport handling, diagnostics, DNSSEC, updates, zone transfer workflows, resolver selection, and protocol parsing.

### Prefer Additive Depth Over Product Sprawl

The best next features deepen the resolver, parser, diagnostics, CLI, and automation story. Avoid turning the main package into a proxy, desktop security suite, operating system manager, or broad network-control product.

### Protect The No-Dependency Baseline

Modern targets should remain dependency-free where practical. If a feature needs specialized dependencies, unusual crypto, a different support contract, or a much larger maintenance surface, it should move into an optional package.

### Keep Runtime-Native Modern Transports In Core

DoH3 and DoQ belong in the core package when the runtime provides the transport support without extra package weight. Older targets should report clear unsupported behavior instead of pulling in compatibility stacks.

### Build Through Existing Surfaces

New capabilities should use the current architecture:

- core library for protocol, parsing, resolver, and diagnostics behavior
- CLI for operator workflows and scripting
- PowerShell for automation and administration

## Completed Cleanup Wave

The previous roadmap items below are now implemented and should be treated as maintained surfaces rather than future work:

- CLI query output modes: `pretty`, `json`, `raw`, and `short`
- CLI section toggles for question, answer, authority, and additional output
- reverse lookup shortcut support in the CLI
- TXT concatenation output support
- EDNS padding and cookie option support
- recursive AXFR convenience flow in the CLI
- explicit resolver endpoint syntax shared by library, CLI, and PowerShell workflows
- resolver import from files and URLs for probe and benchmark workflows
- persisted probe and benchmark score snapshots
- resolver selection and resolver reuse from saved score snapshots
- runtime transport capability reporting
- DNS stamp parsing and generation for plain DNS, DoH, DoT, and DoQ endpoint models
- no-network DNS stamp inspection in the CLI and PowerShell
- no-network resolver catalog validation in the CLI and PowerShell
- resolver score snapshot schema versioning and future-version compatibility checks
- release sanity coverage for version alignment and CLI help/README parity

## Current Direction

The project already has strong foundations:

- broad DNS transport support
- DNSSEC and root validation work
- typed records
- AXFR and dynamic updates
- multi-resolver strategies
- probe, benchmark, scoring, and resolver reuse workflows
- cross-platform targeting

The next phase should focus on protocol completeness, sharper resolver operations, and a small DNS-only policy model.

## Roadmap

## Phase 1: Protocol Completeness

Goal: improve protocol coverage in the core package without adding dependency pressure.

### Scope

- cleaner NSID request and response convenience paths
- stronger validation for EDNS option combinations and public request models
- more focused tests around fully-qualified names, root labels, and wire serialization edge cases

### Primary File Areas

- `DnsClientX/Edns/*`
- `DnsClientX/EdnsOptions.cs`
- `DnsClientX/DnsMessageOptions.cs`
- `DnsClientX/ResolveDnsRequest.cs`
- `DnsClientX/EndpointParser.cs`
- `DnsClientX/ProtocolDnsWire/DnsMessage.cs`
- `DnsClientX.Tests/*Edns*`
- `DnsClientX.Tests/*EndpointParser*`

### Acceptance Criteria

- supported DNS stamps remain round-trippable through the same `DnsResolverEndpoint` model as explicit endpoint syntax
- NSID can be requested through reusable request models without hand-building EDNS options
- wire-format tests cover trailing-dot and root-label edge cases

## Phase 2: Resolver Operations

Goal: make resolver selection workflows more repeatable and operator-friendly.

### Scope

- bootstrap resolver control for hostname-based DoH, DoT, DoH3, DoQ, and gRPC endpoints
- resolver catalog import as a first-class library API, not only CLI plumbing
- score snapshot migration guardrails for any future schema changes
- optional resolver health profile files for repeated probe and benchmark runs
- richer explain output for why a resolver was selected or rejected

### Primary File Areas

- `DnsClientX/EndpointParser.cs`
- `DnsClientX/ResolverExecutionTargetResolver.cs`
- `DnsClientX/ResolverScoreStore.cs`
- `DnsClientX/ResolverProbe*`
- `DnsClientX/ResolverBenchmark*`
- `DnsClientX.Cli/Program.cs`
- `DnsClientX.PowerShell/*Benchmark*`
- `DnsClientX.PowerShell/*Probe*`

### Acceptance Criteria

- hostname-based transports can use an explicit bootstrap path when needed
- resolver catalog loading and validation are reusable from library, CLI, and PowerShell
- future score snapshot schema changes have migration or compatibility guardrails
- explain output names policy, score, health, and capability factors

## Phase 3: DNS-Only Policy Layer

Goal: add domain-aware behavior while staying inside the DNS domain.

### Initial Scope

- block by domain
- static answer override by domain
- custom resolver selection by domain
- rule explain output
- testable policy decisions independent from network calls

### Scope Guardrails

This layer should not include:

- proxy orchestration
- packet fragmentation
- SNI rewriting
- browser or system traffic manipulation
- OS startup management

### Primary File Areas

- new policy-focused files under `DnsClientX/`
- `DnsClientX.QueryDnsRequest.cs`
- `DnsClientX.Cli/Program.cs`
- `DnsClientX.PowerShell/*`
- dedicated policy tests under `DnsClientX.Tests/`

### Acceptance Criteria

- policy decisions are deterministic and explainable
- rules can be evaluated without performing network I/O
- public APIs stay DNS-focused and do not imply system-wide traffic control

## Phase 4: Optional Packages

Goal: leave room for valuable higher-complexity protocols without weighing down the main package.

### Best Candidates

- `DnsClientX.DnsCrypt`
- `DnsClientX.Odoh`
- `DnsClientX.Rules`
- `DnsClientX.Server`

### Packaging Policy

- keep dependency-free DoH3 and DoQ support in the core package on modern targets
- do not add compatibility dependencies just to backport modern transports to older targets
- create optional packages when a feature needs specialized dependencies, crypto, server hosting, or a materially different maintenance contract

## Concrete Backlog

### Core

- expose NSID convenience options through `ResolveDnsRequest`
- add bootstrap resolver configuration to request and endpoint models
- add migration guardrails when score snapshot schemas change
- design a DNS-only rule model for block, override, and resolver selection

### CLI

- expose bootstrap resolver controls for explicit endpoints
- add explain output for resolver score and policy decisions
- keep help text and README feature lists aligned

### PowerShell

- expose bootstrap resolver controls
- add parameter coverage for NSID and stamp workflows
- add rule explain support once the policy model exists

### Diagnostics And Testing

- maintain release sanity tests for version alignment
- maintain CLI help and README parity checks for documented switches
- add parser tests for supported and unsupported stamps
- maintain snapshot schema compatibility tests
- add policy engine tests before public policy APIs are finalized

## Recommended Next 3 PRs

### PR 1: Bootstrap Resolver Control

Goal:

- let hostname-based transports use explicit bootstrap resolver behavior where needed

Acceptance criteria:

- bootstrap settings are available from reusable request models
- CLI and PowerShell expose consistent parameter names
- explain output shows when bootstrap behavior was used

### PR 2: NSID Request Convenience

Goal:

- expose NSID as a first-class request option instead of requiring hand-built EDNS options

Acceptance criteria:

- reusable request models can ask for NSID
- CLI and PowerShell expose matching NSID switches or parameters
- response parsing and output surfaces include returned NSID data when present

### PR 3: Resolver Health Profiles

Goal:

- make repeated probe and benchmark runs easier to configure and compare

Acceptance criteria:

- profile files can define resolver catalogs, domains, record types, and policy thresholds
- CLI and PowerShell can load a profile and still override selected fields
- reports include enough profile metadata to compare runs over time

## Success Criteria

The roadmap is succeeding if:

- the main package remains dependency-light
- new features build on existing abstractions
- CLI and PowerShell stay useful for diagnostics and automation
- protocol coverage improves without product sprawl
- policy features remain DNS-focused
- optional packages absorb higher-complexity protocols when needed

## Summary

DnsClientX should deepen along its current axis: protocol completeness, diagnostics, resolver workflows, and eventually DNS-only policy. The main package should avoid becoming a broader network-control product.
