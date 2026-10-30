Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'DnsResponse RetryCount property' {
    It 'Response includes RetryCount property' -Tag 'RealDns' -Skip:($env:DNSCLIENTX_RUN_REAL_DNS_TESTS -notin @('1', 'true')) {
        $res = @(Resolve-Dns -Name 'example.com' -FullResponse -ErrorAction Stop)
        $res.Count | Should -Be 1
        $res[0].GetType().FullName | Should -Be 'DnsClientX.DnsResponse'
        $res[0].Status | Should -Be ([DnsClientX.DnsResponseCode]::NoError)
        $res[0].Answers.Count | Should -BeGreaterThan 0
        $res[0].PSObject.Properties.Name | Should -Contain 'RetryCount'
        $res[0].RetryCount.GetType() | Should -Be ([int])
        $res[0].RetryCount | Should -BeGreaterOrEqual 0
    }
}
