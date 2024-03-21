Import-Module .\PowerDNSClient.psd1 -Force -Verbose

Resolve-DnsQuery -Name 'evotec.pl' -Type A -DnsProvider Cloudflare | Format-Table

Resolve-DnsQuery -Name 'evotec.pl' -Type TXT -DnsProvider Cloudflare | Format-Table

Resolve-DnsQuery -Name 'github.com' -Type TXT -DnsProvider Cloudflare | Format-Table