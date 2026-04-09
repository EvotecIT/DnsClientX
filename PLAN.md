# PLAN

## Purpose

This plan turns the current assessment into a library-aligned roadmap.

It is written to support the current identity of the project:

- library first
- cross-platform core
- minimal dependencies in the main package
- strong CLI and PowerShell surfaces built on the same engine
- advanced DNS features without drifting into unrelated network product areas

## Principles

### Keep the core focused

The main package should remain centered on DNS resolution, DNS transport handling, DNS diagnostics, DNSSEC, updates, and related protocol features.

### Prefer additive features over product sprawl

The best next features are the ones that deepen the resolver, parser, diagnostics, CLI, and automation story without turning the project into a proxy, desktop security suite, or operating system management tool.

### Protect the no-dependency baseline

For modern targets, the main package should stay dependency-free where practical. If a feature is valuable but pushes the core too far, it should move into an optional package instead of reshaping the main library.

### Keep runtime-native modern transports in core

If a transport can be implemented with no additional NuGet dependency on the modern target line, it belongs in the main package even if older targets cannot support it fully.

That means:

- modern runtime-native transports should live in the core package for `net8+`
- older targets may degrade gracefully instead of matching feature parity
- optional packages should be reserved for features that genuinely add dependency graph weight or separate maintenance burden

### Build through existing surfaces

New capabilities should be aligned with the current architecture:

- core library for protocol and parsing features
- CLI for diagnostics, scripting, and operator workflows
- PowerShell for automation and administration

## Proposed Structure

The roadmap is organized into four layers:

1. Core Enhancements
2. CLI and PowerShell Experience
3. Advanced Policy and Resolver Workflows
4. Optional Package Candidates

This structure keeps the project aligned with what it already does well and makes it easier to phase work without creating architectural debt.

## Current Direction

The project already has a strong foundation:

- broad transport support
- DNSSEC support
- typed records
- AXFR and updates
- multi-resolver strategies
- benchmark and probe workflows
- cross-platform support

Because of that, the next phase should not be about reinventing the resolver. It should be about making the existing engine more complete, more diagnosable, and easier to use in automation.

## What To Add First

The best first additions are the ones that are:

- high value
- low to medium implementation risk
- aligned with the current library architecture
- realistic without adding dependencies

### First Wave

- richer CLI output modes: `json`, `raw`, `short`, and section toggles
- reverse lookup shortcut support in the CLI
- TXT concatenation as an output or parsing option
- EDNS padding support
- EDNS cookie support
- NSID-focused convenience modes
- recursive AXFR helpers in CLI and PowerShell
- resolver import from files and URLs for benchmark and probe workflows

These are the most natural next steps because they improve usability, scripting, and protocol completeness without requiring a major redesign.

## Roadmap

## Phase 1: Strengthen Existing Surfaces

Goal: improve the value of the existing library, CLI, and PowerShell surfaces without changing the project's architectural identity.

### Core

- add EDNS padding support
- add EDNS cookie support
- expose a cleaner NSID request path for reusable request models
- add stamp parsing for standard endpoint formats that map naturally onto existing transports

### CLI

- add `json` output
- add `raw` output
- add `short` output
- add question, authority, and additional section toggles
- add human-friendly TTL formatting
- add reverse lookup shortcut support
- add TXT concatenation support
- add recursive AXFR convenience commands

### PowerShell

- expose the same recursive AXFR convenience surface
- support resolver import for benchmark and probe scenarios
- keep parameter names aligned with existing request models

### Why This Comes First

- strong user value
- low architectural risk
- no need to widen the dependency surface
- direct alignment with the current strengths of the library

## Phase 2: Improve Resolver Operations

Goal: build better operator workflows on top of the existing resolver and benchmark engine.

### Additions

- bootstrap resolver control for hostname-based transports
- import resolver catalogs from local files
- import resolver catalogs from remote URLs
- persist resolver scoring and health summaries from benchmark and probe runs
- add a simple working-resolver selection workflow built on existing benchmark and probe logic

### Why This Matters

The project already knows how to resolve, benchmark, and probe. The next useful step is to make those results reusable, so the CLI and automation layers can evolve from one-off checks into repeatable resolver selection workflows.

## Phase 3: Add a DNS-Only Policy Layer

Goal: introduce domain-aware policy without drifting into unrelated product areas.

### Initial Scope

- block by domain
- static override by domain
- custom resolver selection by domain
- rule explain and diagnostics output

### Scope Guardrails

This layer should remain DNS-focused. It should not include:

- proxy orchestration
- packet fragmentation
- SNI rewriting
- browser or system traffic manipulation

### Why This Phase Is Valuable

A DNS-only policy layer would be one of the strongest differentiators for the library, but it is large enough that it should arrive only after the transport, parser, CLI, and resolver workflow improvements are stable.

## Phase 4: Optional Packages

Goal: keep the main library lean while still leaving room for higher-complexity protocol features.

### Best Candidates

- `DnsClientX.DnsCrypt`
- `DnsClientX.Odoh`
- `DnsClientX.Rules`
- `DnsClientX.Server`

### Rationale

These features may be valuable, but they should not force the main package to absorb significant complexity, new dependencies, or a broader maintenance burden than the core library needs.

### Packaging Policy

- keep `DoH3` in the core package when it remains dependency-free on modern targets
- keep `DoQ` in the core package when it remains dependency-free on modern targets
- do not add compatibility dependencies just to backport `DoH3` or `DoQ` to older targets
- return clear non-support behavior on older frameworks when the runtime cannot provide the transport
- create optional packages only when a protocol requires external dependencies, specialized crypto, or a materially different support contract

## Difficulty And Fit

### High-fit, low-to-medium complexity

- CLI output improvements
- reverse lookup CLI support
- TXT concatenation support
- EDNS padding
- EDNS cookie
- NSID convenience support
- recursive AXFR helper workflows
- resolver import from file or URL

These should be prioritized first.

### High-fit, medium-to-high complexity

- stamp parsing and generation
- bootstrap resolver control
- persisted resolver scoring
- DNS-only policy rules

These are worth doing, but they should follow the first wave.

### Valuable, but better outside the main package

- full DNSCrypt implementation
- full ODoH implementation
- local DNS or DoH server stacks if they grow large
- any feature that requires broad protocol-specific dependencies or native helpers

## Explicit Non-Goals For The Main Package

The main library should avoid absorbing features that move it into a different product category.

That includes:

- DPI bypass workflows
- packet fragmentation features
- fake SNI workflows
- proxy server product features
- operating system startup management
- large Windows-only orchestration features

These may be useful in other tools, but they are not the right center of gravity for this library.

## Recommended Execution Order

1. CLI output modes and section controls
2. EDNS padding and cookie support
3. reverse lookup and TXT convenience features
4. recursive AXFR convenience flows
5. resolver import from file and URL
6. bootstrap resolver control
7. persisted resolver scoring
8. DNS-only policy layer
9. optional packages for higher-complexity protocols

## Success Criteria

The plan is succeeding if:

- the main package remains dependency-light
- new features build on existing abstractions instead of bypassing them
- CLI and PowerShell become more useful for diagnostics and automation
- the library becomes more complete at the DNS protocol layer
- policy features remain DNS-focused
- higher-complexity protocol work is isolated into optional packages when needed

## Concrete Backlog

### Core backlog

- add EDNS padding option type and wire serialization support
- add EDNS cookie option type and wire serialization support
- extend reusable request models to express padding, cookie, and richer EDNS intent
- add endpoint stamp parsing for supported built-in transport types
- add bootstrap resolver configuration for hostname-based transports
- add resolver import primitives from file and URL sources
- add persisted resolver score model for benchmark and probe outputs
- design a DNS-only rule model for block, override, and custom resolver selection

### CLI backlog

- add `--format json`
- add `--format raw`
- add `--short`
- add section switches for question, answer, authority, and additional
- add pretty TTL formatting options
- add reverse lookup shortcut
- add TXT concatenation option
- add recursive AXFR mode
- add resolver import input options
- add stable machine-readable summary output for imported-resolver workflows

### PowerShell backlog

- add recursive AXFR convenience cmdlet surface or parameter set
- add resolver import support for benchmark and probe commands
- add parameter coverage for richer EDNS controls
- add rule explain support once a policy layer exists

### Diagnostics and testing backlog

- add unit tests for EDNS padding and cookie serialization
- add parser tests for stamp parsing
- add CLI tests for new output modes and flags
- add probe and benchmark tests for imported resolver sources
- add tests for persisted resolver score read and write behavior
- add rule engine tests before any public policy API is finalized

## Milestones

### Milestone 1: Protocol Completeness

Outcome:

- richer EDNS support
- cleaner NSID path
- stamp parsing for supported transport types

Scope:

- EDNS padding
- EDNS cookie
- request-model support for richer EDNS options
- tests for wire generation and parser behavior

Primary file areas:

- `DnsClientX/Edns/*`
- `DnsClientX/EdnsOptions.cs`
- `DnsClientX/DnsMessageOptions.cs`
- `DnsClientX/ResolveDnsRequest.cs`
- `DnsClientX/ProtocolDnsWire/DnsMessage.cs`
- `DnsClientX.Tests/*Edns*`

### Milestone 2: CLI Diagnostics Upgrade

Outcome:

- much stronger operator and scripting UX

Scope:

- `json`, `raw`, and `short` output modes
- section toggles
- reverse lookup shortcut
- TXT concatenation

Primary file areas:

- `DnsClientX.Cli/Program.cs`
- `DnsClientX.Tests/Cli*`
- `DnsClientX.Tests/*Query*`

### Milestone 3: AXFR and Resolver Workflow Improvements

Outcome:

- better operational workflows for large-scale resolver handling

Scope:

- recursive AXFR convenience
- resolver import from file and URL
- benchmark and probe integration for imported resolvers

Primary file areas:

- `DnsClientX.Cli/Program.cs`
- `DnsClientX.PowerShell/*`
- `DnsClientX/EndpointParser.cs`
- `DnsClientX/Definitions/*`
- `DnsClientX.Tests/*Benchmark*`
- `DnsClientX.Tests/*Probe*`

### Milestone 4: Resolver State and Selection

Outcome:

- reusable resolver quality data and better repeatable operator workflows

Scope:

- persisted resolver scoring
- score reuse in benchmark and probe-driven selection flows
- optional cache or profile model for resolver health snapshots

Primary file areas:

- `DnsClientX.Cli/Program.cs`
- `DnsClientX.PowerShell/CmdletTestDnsBenchmark.cs`
- `DnsClientX/Definitions/*`
- `DnsClientX.Tests/*Benchmark*`

### Milestone 5: DNS-Only Policy Layer

Outcome:

- domain-aware behavior without leaving the DNS domain

Scope:

- domain block rules
- static answer override rules
- per-domain custom resolver rules
- explain output for rule decisions

Primary file areas:

- new policy-focused files under `DnsClientX/`
- `DnsClientX.QueryDnsRequest.cs`
- `DnsClientX.Cli/Program.cs`
- `DnsClientX.PowerShell/*`
- dedicated policy tests under `DnsClientX.Tests/`

## Next 3 PRs

### PR 1: Richer EDNS Support

Goal:

- add EDNS padding and cookie support in the core library, including reusable request-model support and tests

Why first:

- strong protocol value
- contained scope
- no dependency pressure
- unlocks later CLI and automation work

Suggested file targets:

- `DnsClientX/Edns/`
- `DnsClientX/EdnsOptions.cs`
- `DnsClientX/DnsMessageOptions.cs`
- `DnsClientX/ResolveDnsRequest.cs`
- `DnsClientX/ProtocolDnsWire/DnsMessage.cs`
- `DnsClientX.Tests/EdnsOptionsTests.cs`
- new focused EDNS tests if needed

Suggested acceptance criteria:

- padding and cookie options can be expressed through public request paths
- wire serialization includes the new EDNS options correctly
- existing EDNS behavior remains unchanged when the new options are unused

### PR 2: CLI Output Expansion

Goal:

- add `json`, `raw`, and `short` output modes plus section toggles

Why second:

- immediate user-visible value
- builds on existing response structures
- keeps work centered in the CLI layer

Suggested file targets:

- `DnsClientX.Cli/Program.cs`
- `DnsClientX.Tests/CliIntegrationTests.cs`
- `DnsClientX.Tests/CliExplainTraceTests.cs`
- new CLI output tests if needed

Suggested acceptance criteria:

- CLI can render structured JSON output
- CLI can render raw DNS-style output
- CLI can emit short answer-only output
- section flags correctly show or hide question, authority, and additional data

### PR 3: Resolver Import Workflow

Goal:

- support importing resolver inputs from files and URLs for benchmark and probe flows

Why third:

- high operational value
- aligns with existing benchmark and probe investment
- opens the door to persisted resolver scoring later

Suggested file targets:

- `DnsClientX.Cli/Program.cs`
- `DnsClientX.PowerShell/*`
- `DnsClientX/EndpointParser.cs`
- `DnsClientX.Tests/*Benchmark*`
- `DnsClientX.Tests/*Probe*`

Suggested acceptance criteria:

- benchmark and probe commands can consume resolver lists from file inputs
- remote resolver list import is supported with validation
- imported resolvers reuse the same parsing and endpoint validation path as direct inputs

## Summary

The project should deepen along its current axis rather than widen into unrelated areas.

The right next step is to improve protocol completeness, diagnostics, CLI output, resolver workflows, and eventually DNS-only policy support. The wrong next step is to grow the main package into a broader network-control product.
