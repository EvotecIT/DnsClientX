---
title: "DnsClientX Overview"
description: "How DnsClientX fits DNS lookup and resolver testing workflows."
layout: docs
---

DnsClientX is useful when you need DNS lookups that are explicit about provider, transport, caching, and result shape.

## Common fit

- compare results from different public DNS providers
- test DNS over HTTPS, TLS, HTTP/3, or QUIC support
- return typed DNS records in .NET code
- run multi-record lookups from PowerShell without shelling out to platform-specific tools

## Good operating pattern

Start with `example.com` or another safe public test domain. Move to internal zones only after you know which resolver and transport settings you want to validate.

