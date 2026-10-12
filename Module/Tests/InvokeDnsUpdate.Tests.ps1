Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Invoke-DnsUpdate cmdlet' {
    It 'Cmdlet is available' {
        Get-Command Invoke-DnsUpdate | Should -Not -BeNullOrEmpty
    }

    It 'Fails when server is unreachable' {
        { Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '1.1.1.1' -Port 1 -ErrorAction Stop } | Should -Throw
    }

    It 'previews additions and both deletion modes without contacting the server' {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start()
        try {
            $port = $listener.LocalEndpoint.Port
            foreach ($operation in @(@{}, @{ Delete = $true }, @{ DeleteValue = $true })) {
                $output = @(Invoke-DnsUpdate -Zone example.test -Server 127.0.0.1 -Port $port -Name www -Type A -Data 192.0.2.10 @operation -WhatIf -ErrorAction Stop)
                $output.Count | Should -Be 0
            }
            $listener.Pending() | Should -BeFalse
            (Get-Command Invoke-DnsUpdate).Parameters.Keys | Should -Contain 'Confirm'
        } finally { $listener.Stop() }
    }

    It 'Allows zero Ttl and rejects negative values at binding' {
        $ttl = (Get-Command Invoke-DnsUpdate).Parameters['Ttl']
        $range = $ttl.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateRangeAttribute] }
        $range.MinRange | Should -Be 0
        { Invoke-DnsUpdate -Zone 'example.com' -Server '127.0.0.1' -Name 'www' -Type A -Data '1.1.1.1' -Ttl -1 -ErrorAction Stop } | Should -Throw -ExceptionType System.Management.Automation.ParameterBindingException
    }
}
