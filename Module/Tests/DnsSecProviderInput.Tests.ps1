Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'DNSSEC provider input errors' {
    $cases = foreach ($command in @('Resolve-Dns', 'Test-DnsProbe', 'Test-DnsBenchmark')) {
        foreach ($fixture in @('missing', 'blank', 'malformed')) {
            @{
                Command = $command
                Fixture = $fixture
                ExpectedId = switch ($command) {
                    'Resolve-Dns' { 'ResolveDnsInvalidInput' }
                    'Test-DnsProbe' { 'DnsProbeInvalidInput' }
                    'Test-DnsBenchmark' { 'DnsBenchmarkInvalidInput' }
                }
            }
        }
    }

    It 'classifies <Fixture> provider input for <Command> before querying' -TestCases $cases {
        param($Command, $Fixture, $ExpectedId)
        $path = Join-Path $TestDrive ($Fixture + '-' + [guid]::NewGuid().ToString('N') + '.dll')
        if ($Fixture -eq 'blank') { $path = ' ' }
        if ($Fixture -eq 'malformed') { Set-Content -LiteralPath $path -Value 'Synthetic invalid DLL fixture.' }
        $parameters = @{ Name = 'example.com'; DnsProvider = 'Cloudflare'; DnsSecVerifierPath = $path; ErrorAction = 'Stop' }
        if ($Command -eq 'Test-DnsBenchmark') { $parameters.Attempts = 1 }
        $observed = $null
        try { & $Command @parameters | Out-Null }
        catch { $observed = $_ }
        $observed | Should -Not -BeNullOrEmpty
        $observed.FullyQualifiedErrorId | Should -BeLike ($ExpectedId + '*')
        $observed.CategoryInfo.Category | Should -Be 'InvalidArgument'
    }
}
