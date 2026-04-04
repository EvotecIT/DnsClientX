Clear-Host

Import-Module $PSScriptRoot\..\DnsClientX.psd1 -Force -Verbose

$Domains = 'sip2sip.info' #, 'sip5060.net'

foreach ($Domain in $Domains) {
    Resolve-Dns -Type NAPTR -Name $Domain -DnsProvider Cloudflare | Format-Table
    Resolve-Dns -Type NAPTR -Name $Domain -DnsProvider Google | Format-Table
    Resolve-Dns -Type NAPTR -Name $Domain -DnsProvider GoogleWireFormat | Format-Table
    Resolve-Dns -Type NAPTR -Name $Domain -DnsProvider OpenDNS | Format-Table
    Resolve-Dns -Type NAPTR -Name $Domain -DnsProvider CloudflareWireFormat | Format-Table
    Resolve-Dns -Type NAPTR -Name $Domain -DnsProvider CloudflareWireFormatPost | Format-Table
}
return


Resolve-Dns -Name 'www.bücher.de' -Type A | Format-Table
Resolve-Dns -Name 'evotec.pl' -Type A | Format-Table
Resolve-Dns -Name 'evotec.pl' -Type A -DnsProvider Cloudflare -Verbose | Format-Table
Resolve-Dns -Name 'evotec.pl' -Type TXT -DnsProvider System -Verbose | Format-Table
Resolve-Dns -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT -DnsProvider System -Verbose | Format-Table
Resolve-Dns -Name 'github.com', 'evotec.pl', 'google.com' -Type TXT -DnsProvider System -Verbose #-TimeOut 5000
# Should fail next two
Resolve-Dns -Name 'evotec.pl' -Type SOA -Server 8.8.4.1 -Verbose -TimeOut 500 | Format-Table
Resolve-Dns -Name 'evotec.pl' -Type SOA -Server a1.net -Verbose -TimeOut 500 | Format-Table

# Request and validate DNSSEC
Resolve-Dns -Name 'example.com' -Type A -DnsProvider Cloudflare -RequestDnsSec -ValidateDnsSec | Format-Table
