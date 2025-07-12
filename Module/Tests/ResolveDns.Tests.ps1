Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Resolve-Dns cmdlet' {
    It 'Fails when TimeOut is less than or equal to zero' {
        { Resolve-Dns -Name 'example.com' -TimeOut 0 -ErrorAction Stop } | Should -Throw -ExceptionType System.ArgumentOutOfRangeException
    }

    It 'Removes duplicate servers before processing' {
        $result = Resolve-Dns -Name 'example.com' -Server @('127.0.0.1','127.0.0.1') -AllServers -FullResponse -TimeOut 10 -ErrorAction SilentlyContinue
        $result.Count | Should -Be 1
    }

    It 'Returns typed records when requested' {
        $result = Resolve-Dns -Name 'example.com' -Type A -TypedRecords -TimeOut 10 -ErrorAction SilentlyContinue | Select-Object -First 1
        $result.TypedAnswers | Should -Not -BeNullOrEmpty
    }
}
