Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Invoke-DnsUpdate cmdlet' {
    It 'Cmdlet is available' {
        Get-Command Invoke-DnsUpdate | Should -Not -BeNullOrEmpty
    }

    It 'Fails when server is unreachable' {
        { Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '1.1.1.1' -Port 1 -ErrorAction Stop } | Should -Throw
    }

    It 'Allows zero Ttl and rejects negative values at binding' {
        $ttl = (Get-Command Invoke-DnsUpdate).Parameters['Ttl']
        $range = $ttl.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateRangeAttribute] }
        $range.MinRange | Should -Be 0
        { Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '1.1.1.1' -Ttl -1 -ErrorAction Stop } | Should -Throw -ExceptionType System.Management.Automation.ParameterBindingException
    }
}
