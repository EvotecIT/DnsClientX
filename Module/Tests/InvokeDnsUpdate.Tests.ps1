Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Invoke-DnsUpdate cmdlet' {
    It 'Cmdlet is available' {
        Get-Command Invoke-DnsUpdate | Should -Not -BeNullOrEmpty
    }

    It 'Supports WhatIf for a complete atomic batch without contacting the server' {
        $operation = [DnsClientX.DnsUpdateOperation]::Add('www.example.test', [DnsClientX.DnsRecordType]::A, '192.0.2.1', 60)
        $prerequisite = [DnsClientX.DnsUpdatePrerequisite]::RrsetAbsent('www.example.test', [DnsClientX.DnsRecordType]::A)
        $batch = [DnsClientX.DnsUpdateBatch]::new('example.test', [DnsClientX.DnsUpdateOperation[]] @($operation), [DnsClientX.DnsUpdatePrerequisite[]] @($prerequisite))
        $result = Invoke-DnsUpdate -Batch $batch -Server '127.0.0.1' -Port 1 -WhatIf -ErrorAction Stop
        $result | Should -BeNullOrEmpty
    }

    It 'Rejects mixing a batch with individual record parameters before dispatch' {
        $batch = [DnsClientX.DnsUpdateBatch]::new('example.test', [DnsClientX.DnsUpdateOperation[]] @([DnsClientX.DnsUpdateOperation]::DeleteName('www.example.test')), $null)
        { Invoke-DnsUpdate -Batch $batch -Zone 'example.test' -Name www -Type A -Server '127.0.0.1' -ErrorAction Stop } | Should -Throw -ExceptionType System.Management.Automation.ParameterBindingException
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
