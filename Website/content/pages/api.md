---
title: "API Reference"
description: "Browse generated API reference for the DnsClientX .NET library and the DnsClientX PowerShell module."
layout: page
slug: api
---

## Reference Areas

The DnsClientX website ships two generated API surfaces:

- The `.NET` API generated from the compiled `DnsClientX` assembly and XML documentation.
- The PowerShell cmdlet reference generated from synced help XML, module metadata, and example scripts.

<div class="imo-api-overview">
  <section class="imo-api-overview__panel">
    <div>
      <span class="imo-api-overview__eyebrow">Choose your entry point</span>
      <h2 class="imo-api-overview__title">Start with the reference when you know the API, or the guide when you are still shaping the workflow</h2>
      <p class="imo-api-overview__copy">The generated reference is ideal when you already know the type, cmdlet, or namespace you need. If you are still exploring how the workflow should look, the conceptual guides and the playground are faster starting points.</p>
    </div>
    <div class="imo-api-overview__actions">
      <a href="/docs/" class="imo-btn imo-btn-primary">Open Documentation</a>
      <a href="/playground/" class="imo-btn imo-btn-ghost">Open Playground</a>
    </div>
  </section>

  <section class="imo-api-overview__group" aria-labelledby="api-group-dotnet">
    <div class="imo-api-overview__group-header">
      <div>
        <span class="imo-api-overview__eyebrow">.NET library</span>
        <h2 id="api-group-dotnet">DnsClientX .NET API</h2>
      </div>
      <p>Generated during website CI from the current build outputs, with source links back to the repository.</p>
    </div>
    <div class="imo-api-card-grid imo-api-card-grid--single">
      <article class="imo-api-card" style="--api-accent: #38bdf8;">
        <div class="imo-api-card__header">
          <h3>DnsClientX</h3>
          <span class="imo-badge">Core library</span>
        </div>
        <p class="imo-api-card__desc">Async DNS queries, typed record parsing, resolver transport selection, DNSSEC-aware flows, multi-resolver support, and EDNS customization.</p>
        <p class="imo-api-card__best">Best for: app code, services, CLI tools, diagnostics, and products that want a reusable DNS engine.</p>
        <div class="imo-api-card__links">
          <a href="/api/dnsclientx/" class="imo-api-card__primary">Open .NET API Reference</a>
          <a href="/docs/csharp/" class="imo-api-card__secondary">C# guides</a>
        </div>
      </article>
    </div>
  </section>

  <section class="imo-api-overview__group" aria-labelledby="api-group-powershell">
    <div class="imo-api-overview__group-header">
      <div>
        <span class="imo-api-overview__eyebrow">PowerShell automation</span>
        <h2 id="api-group-powershell">DnsClientX PowerShell cmdlets</h2>
      </div>
      <p>Generated from XmlDoc2CmdletDoc output, synced module manifest data, and checked-in example scripts from the repository.</p>
    </div>
    <div class="imo-api-card-grid imo-api-card-grid--single">
      <article class="imo-api-card" style="--api-accent: #f59e0b;">
        <div class="imo-api-card__header">
          <h3>DnsClientX PowerShell</h3>
          <span class="imo-badge">Module</span>
        </div>
        <p class="imo-api-card__desc">Resolve DNS records, compare providers, use multi-resolver strategies, inspect full responses, and automate DNS checks from scripts.</p>
        <p class="imo-api-card__best">Best for: scripts, build pipelines, operations diagnostics, and repeatable DNS validation tasks.</p>
        <div class="imo-api-card__links">
          <a href="/api/powershell/" class="imo-api-card__primary">Open Cmdlet Reference</a>
          <a href="/docs/powershell/" class="imo-api-card__secondary">PowerShell guides</a>
        </div>
      </article>
    </div>
  </section>
</div>

## How These Pages Are Generated

- The `.NET` reference is generated from the `DnsClientX` assembly and XML docs produced during CI.
- The PowerShell reference is generated from the module help XML, manifest, and examples synced from the repository.
- The merged xref map is fed back into the site build so guides and API pages can cross-link cleanly.

## Need A Practical Starting Point?

- [Getting Started](/docs/getting-started/) for installation and first queries.
- [C# Usage](/docs/csharp/) when you want application code.
- [PowerShell Usage](/docs/powershell/) when the workflow belongs in scripts.
- [Playground](/playground/) when you want to explore live answers in the browser first.
