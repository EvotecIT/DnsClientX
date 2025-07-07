Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Resolve-Dns pattern parameter' {
    It 'Fails when TimeOut is less than or equal to zero' {
        { Resolve-Dns -Pattern 'host[1-2].example.com' -TimeOut 0 -ErrorAction Stop } | Should -Throw -ExceptionType System.ArgumentOutOfRangeException
    }
}
