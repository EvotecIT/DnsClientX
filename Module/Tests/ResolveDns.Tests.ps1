Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Resolve-Dns cmdlet' {
    It 'Fails when TimeOut is less than or equal to zero' {
        { Resolve-Dns -Name 'example.com' -TimeOut 0 -ErrorAction Stop } | Should -Throw -ExceptionType System.ArgumentOutOfRangeException
    }

    It 'Removes duplicate servers before processing' {
        $result = Resolve-Dns -Name 'example.com' -Server @('127.0.0.1','127.0.0.1') -AllServers -FullResponse -TimeOut 10 -ErrorAction SilentlyContinue
        $result.Count | Should -Be 1
    }
}
