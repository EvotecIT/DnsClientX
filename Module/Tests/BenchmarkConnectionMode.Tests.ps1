Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

BeforeAll {
    if (-not ('DnsWorkflowServer' -as [type])) {
        Add-Type -Path "$PSScriptRoot/Fixtures/DnsWorkflowServer.cs"
    }
}

Describe 'Benchmark connection modes' {
    It 'keeps same-label endpoint variants separate in results and saved recommendations' {
        $peer = [DnsWorkflowServer]::new()
        try {
            $stamps = foreach ($dnssec in @($false, $true)) {
                $endpoint = [DnsClientX.DnsResolverEndpoint]@{
                    Host = '127.0.0.1'; Port = $peer.Port; Transport = 'Udp'; DnsSecOk = $dnssec
                }
                [DnsClientX.DnsStamp]::Create($endpoint)
            }
            $path = Join-Path $TestDrive 'identity.json'
            $rows = @(Test-DnsBenchmark -Name 'fixture.test' -ResolverEndpoint $stamps -Attempts 1 -SavePath $path -ErrorAction Stop)
            $rows.Count | Should -Be 2
            @($rows.Target | Select-Object -Unique).Count | Should -Be 1
            @($rows.CandidateId | Select-Object -Unique).Count | Should -Be 2
            $rows[0].TotalQueries | Should -Be 1
            $rows[1].TotalQueries | Should -Be 1
            $saved = [DnsClientX.ResolverScoreStore]::Load($path)
            $saved.SchemaVersion | Should -Be 3
            $selection = Get-DnsResolverSelection -Path $path -ErrorAction Stop
            $selection.CandidateId | Should -Be $saved.Summary.RecommendedCandidateId
            $rows.CandidateId | Should -Contain $selection.CandidateId
            $peer.Queries.Count | Should -Be 2
            $probe = Test-DnsProbe -Name 'fixture.test' -ResolverEndpoint $stamps -SummaryOnly -ErrorAction Stop
            $probe.AnswerVariants.Count | Should -Be 1
            $probe.AnswerVariants[0].CandidateIds.Count | Should -Be 2
            $probe.AnswerVariants[0].CandidateIds | Should -Contain $selection.CandidateId
            $peer.Queries.Count | Should -Be 4
        } finally {
            $peer.Dispose()
        }
    }
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
