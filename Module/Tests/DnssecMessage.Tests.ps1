Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force
Add-Type -Path "$PSScriptRoot/../../DnsClientX/bin/Debug/net8.0/DnsClientX.dll"

Describe 'DnsMessage DO bit' {
    It 'Sets DO bit when requestDnsSec is true' {
        $msg = [DnsClientX.DnsMessage]::new('example.com',[DnsClientX.DnsRecordType]::A,$true)
        $bytes = $msg.SerializeDnsWireFormat()
        $offset = 12
        foreach($label in 'example.com'.Split('.')){ $offset += 1 + $label.Length }
        $offset += 1 + 2 + 2
        $ttl = ([int]$bytes[$offset+5] -shl 24) -bor ([int]$bytes[$offset+6] -shl 16) -bor ([int]$bytes[$offset+7] -shl 8) -bor [int]$bytes[$offset+8]
        ($ttl -band 0x00008000) | Should -Be 0x00008000
    }
}
