Describe 'Invoke-DnsZoneTransfer' {
    BeforeAll {
        Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force
    }

    It 'Cmdlet exists' {
        Get-Command Invoke-DnsZoneTransfer | Should -Not -BeNullOrEmpty
    }

    It 'Fails on unreachable server' {
        { Invoke-DnsZoneTransfer -Zone 'example.com' -Server '127.0.0.1' -Port 64000 -TimeOut 500 } | Should -Throw
    }
}
