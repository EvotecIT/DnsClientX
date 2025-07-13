Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Invoke-DnsUpdate cmdlet' {
    It 'Cmdlet is available' {
        Get-Command Invoke-DnsUpdate | Should -Not -BeNullOrEmpty
    }

    It 'Fails when server is unreachable' {
        { Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '1.1.1.1' -Port 1 -ErrorAction Stop } | Should -Throw
    }

    It 'Fails when Ttl is less than or equal to zero' {
        { Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '1.1.1.1' -Ttl 0 -ErrorAction Stop } | Should -Throw -ExceptionType System.Management.Automation.ParameterBindingException
    }
}
