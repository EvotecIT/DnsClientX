Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Benchmark connection modes' {
    It 'rejects reporting-only mode <Mode> before querying' -TestCases @(@{Mode='Mixed'}, @{Mode=2}) {
        param($Mode)
        $observed = $null
        try { Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare -ConnectionMode $Mode -ErrorAction Stop | Out-Null }
        catch { $observed = $_ }
        $observed | Should -Not -BeNullOrEmpty
        $observed.FullyQualifiedErrorId | Should -BeLike 'ParameterArgumentValidationError*'
    }

    It 'accepts executable mode <Mode> through normal input validation' -TestCases @(@{Mode='Cold'}, @{Mode='Warm'}) {
        param($Mode)
        $missingProvider = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '.dll')
        $observed = $null
        try { Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare -ConnectionMode $Mode -DnsSecVerifierPath $missingProvider -ErrorAction Stop | Out-Null }
        catch { $observed = $_ }
        $observed | Should -Not -BeNullOrEmpty
        $observed.FullyQualifiedErrorId | Should -BeLike 'DnsBenchmarkInvalidInput*'
    }
}
