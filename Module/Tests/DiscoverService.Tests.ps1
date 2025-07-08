Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Get-DnsService cmdlet' {
    It 'Returns empty array when no services are discovered' {
        $result = Get-DnsService -Domain 'example.com'
        ($null -ne $result) | Should -BeTrue
        $result.GetType().FullName | Should -Be 'DnsClientX.DnsService[]'
        $result.Count | Should -Be 0
    }
}
