Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'DnsResponse RetryCount property' {
    It 'Response includes RetryCount property' {
        $res = Resolve-Dns -Name 'example.com' -FullResponse -ErrorAction SilentlyContinue
        ($res[0].PSObject.Properties.Name) -contains 'RetryCount' | Should -BeTrue
    }
}
