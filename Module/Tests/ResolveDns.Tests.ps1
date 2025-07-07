Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Resolve-Dns cmdlet' {
    It 'Fails when TimeOut is less than or equal to zero' {
        { Resolve-Dns -Name 'example.com' -TimeOut 0 -ErrorAction Stop } | Should -Throw -ExceptionType System.ArgumentOutOfRangeException
    }
}
