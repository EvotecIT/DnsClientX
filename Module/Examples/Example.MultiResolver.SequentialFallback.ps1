# SequentialFallback tries endpoints in order until a terminal response
Resolve-Dns -Name 'example.com' -Type A -DnsProvider Cloudflare, Google -ResolverStrategy SequentialFallback | Format-Table
