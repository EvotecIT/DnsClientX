Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

BeforeAll {
    if (-not ('DnsWorkflowServer' -as [type])) {
        Add-Type -Path "$PSScriptRoot/Fixtures/DnsWorkflowServer.cs"
    }
}

Describe 'Resolve-Dns cmdlet' {
    It 'Throws when AllServers is used without Server' {
        { Resolve-Dns -Name 'example.com' -AllServers -ErrorAction Stop } | Should -Throw -ExceptionType System.InvalidOperationException
    }

    It 'Removes duplicate servers before processing' {
        $peer = [DnsWorkflowServer]::new()
        try {
            $result = @(Resolve-Dns -Name 'example.com' -Server @('127.0.0.1', '127.0.0.1') -Port $peer.Port -AllServers -FullResponse -RetryCount 1 -ErrorAction Stop)
            $result.Count | Should -Be 1
            $result[0].GetType().FullName | Should -Be 'DnsClientX.DnsResponse'
            $result[0].Status | Should -Be ([DnsClientX.DnsResponseCode]::NoError)
            $result[0].Answers[0].Data | Should -Be '192.0.2.10'
            $peer.Queries.Count | Should -Be 1
            $peer.Queries[0] | Should -Be 'example.com'
        } finally { $peer.Dispose() }
    }

}
