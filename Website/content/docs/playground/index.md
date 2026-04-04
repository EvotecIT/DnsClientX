---
title: "Playground Guide"
description: "Use the browser playground to inspect live DNS answers and turn them into DnsClientX code."
layout: docs
slug: playground
---

## What The Playground Does

The [DNS playground](/playground/) is a browser-first experience for exploring a name and record type combination before you commit the workflow to code or scripts.

It gives you:

- A record-type picker similar to public resolver tools.
- Live JSON responses from browser-friendly public DNS endpoints.
- Structured rendering of answers, authority, and additional sections.
- Equivalent DnsClientX snippets for both C# and PowerShell.

## How To Use It

1. Enter a domain or record owner name.
2. Pick the record type you want to inspect.
3. Toggle DNSSEC detail or disable validation when you want to compare resolver behavior.
4. Optionally supply an EDNS client subnet for the Google preview path.
5. Copy the generated C# or PowerShell snippet into your own workflow.

## Important Scope Note

The browser preview uses public DNS JSON resolver APIs because the page must work client-side without a custom backend. DnsClientX itself supports many more transports and richer application-side workflows than the browser can expose directly.
