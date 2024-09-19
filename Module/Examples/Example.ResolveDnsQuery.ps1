Clear-Host

Import-Module $PSScriptRoot\..\DnsClientX.psd1 -Force

Resolve-DnsQuery -Name 'www.bücher.de' -Type A | Format-Table
Resolve-DnsQuery -Name 'evotec.pl' -Type A | Format-Table
Resolve-DnsQuery -Name 'evotec.pl' -Type A -DnsProvider Cloudflare -Verbose | Format-Table
Resolve-DnsQuery -Name 'evotec.pl' -Type TXT -DnsProvider System -Verbose | Format-Table
Resolve-DnsQuery -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT -DnsProvider System -Verbose | Format-Table
Resolve-DnsQuery -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT -DnsProvider System -Verbose #-TimeOut 5000
Resolve-DnsQuery -Name 'evotec.pl' -Type SOA -Server 8.8.4.1 -Verbose -TimeOut 500 | Format-Table
Resolve-DnsQuery -Name 'evotec.pl' -Type SOA -Server a1.net -Verbose -TimeOut 500 | Format-Table