Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Get-DnsZoneTransfer cmdlet' {
    It 'Cmdlet is available' {
        Get-Command Get-DnsZone | Should -Not -BeNullOrEmpty
    }

    It 'Fails when server does not allow transfer' {
        { Get-DnsZoneTransfer -Zone 'example.com' -Server '127.0.0.1' -Port 1 -ErrorAction Stop } | Should -Throw
    }
}
