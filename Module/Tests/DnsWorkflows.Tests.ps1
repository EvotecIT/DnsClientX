BeforeAll {
    Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force
    if (-not ('DnsWorkflowServer' -as [type])) {
        Add-Type -Path "$PSScriptRoot/Fixtures/DnsWorkflowServer.cs"
    }
}

Describe 'PowerShell DNS workflows against a loopback peer' {
    BeforeEach { $script:dnsPeer = [DnsWorkflowServer]::new() }
    AfterEach { $script:dnsPeer.Dispose() }

    It 'binds names by pipeline value and property and emits individual answer rows' {
        $rows = @('rows.test' | Resolve-Dns -Server 127.0.0.1 -Port $dnsPeer.Port -RetryCount 1)
        $rows.Count | Should -Be 2
        $rows[0].GetType().FullName | Should -Be 'DnsClientX.DnsAnswerMinimal'
        $filtered = @($rows | Where-Object Data -EQ '192.0.2.10')
        $filtered.Count | Should -Be 1
        $filtered[0].Data | Should -Be '192.0.2.10'
        $response = [pscustomobject]@{ DnsName = 'property.test' } |
            Resolve-Dns -Server 127.0.0.1 -Port $dnsPeer.Port -FullResponse -RetryCount 1
        $response.GetType().FullName | Should -Be 'DnsClientX.DnsResponse'
        $response.Questions[0].Name.TrimEnd('.') | Should -Be 'property.test'
    }

    It 'returns typed values with their record metadata when requested' {
        $rows = @(Resolve-Dns rows.test -Server 127.0.0.1 -Port $dnsPeer.Port -TypedRecordMetadata -RetryCount 1)
        $rows.Count | Should -Be 2
        $rows[0].Name.TrimEnd('.') | Should -Be 'rows.test'
        $rows[0].TTL | Should -Be 120
        $rows[0].Value.GetType().FullName | Should -Be 'DnsClientX.ARecord'
        $rows[0].Record.Data | Should -Be '192.0.2.10'
    }

    It 'returns individual typed payloads and preserves full-response precedence' {
        $rows = @(Resolve-Dns rows.test -Server 127.0.0.1 -Port $dnsPeer.Port -TypedRecords -RetryCount 1)
        $rows.Count | Should -Be 2
        $rows[0].GetType().FullName | Should -Be 'DnsClientX.ARecord'
        $rows[0].Address.IPAddressToString | Should -Be '192.0.2.10'
        $response = Resolve-Dns rows.test -Server 127.0.0.1 -Port $dnsPeer.Port -FullResponse -TypedRecordMetadata -RetryCount 1
        $response.GetType().FullName | Should -Be 'DnsClientX.DnsResponse'
        $response.TypedAnswers.Count | Should -Be 2
    }

    It 'streams batches with input ordering and optional completion ordering' {
        $ordered = @(Resolve-Dns slow.test,fast.test -Server 127.0.0.1 -Port $dnsPeer.Port -Stream -FullResponse -MaxConcurrency 2 -RetryCount 1)
        $ordered[0].Questions[0].Name.TrimEnd('.') | Should -Be 'slow.test'
        $completed = @(Resolve-Dns slow.test,fast.test -Server 127.0.0.1 -Port $dnsPeer.Port -Stream -CompletionOrder -FullResponse -MaxConcurrency 2 -RetryCount 1)
        $completed[0].Questions[0].Name.TrimEnd('.') | Should -Be 'fast.test'
        $completed.Count | Should -Be 2
        { Resolve-Dns fast.test -CompletionOrder -ErrorAction Stop } | Should -Throw '*requires Stream*'
    }

    It 'stops a bulk stream after the pipeline consumer takes its first result' {
        $names = @('fast.test') + @(1..11 | ForEach-Object { 'slow.test' })
        $first = Resolve-Dns -Name $names -Server 127.0.0.1 -Port $dnsPeer.Port -Stream -CompletionOrder -FullResponse -MaxConcurrency 2 -RetryCount 1 |
            Select-Object -First 1
        $first.Questions[0].Name.TrimEnd('.') | Should -Be 'fast.test'
        $receivedAtStop = $dnsPeer.Queries.Count
        $receivedAtStop | Should -BeLessThan $names.Count
        Start-Sleep -Milliseconds 350
        $dnsPeer.Queries.Count | Should -Be $receivedAtStop
    }

    It 'inspects and selectively invalidates the process-wide response cache with WhatIf support' {
        Clear-DnsCache | Out-Null
        try {
            Resolve-Dns cache.test -Type A,AAAA -Server 127.0.0.1 -Port $dnsPeer.Port -ResponseCache -RetryCount 1 | Out-Null
            (Get-DnsCache).EntryCount | Should -Be 2
            Clear-DnsCache cache.test -Type A -WhatIf
            (Get-DnsCache).EntryCount | Should -Be 2
            (Clear-DnsCache cache.test -Type A) | Should -Be 1
            (Get-DnsCache).EntryCount | Should -Be 1
            (Clear-DnsCache cache.test) | Should -Be 1
            (Get-DnsCache).EntryCount | Should -Be 0
        } finally { Clear-DnsCache | Out-Null }
    }

    It 'streams services so filtering never leaks an unmatched service' {
        $services = @('services.test' | Find-DnsService -ResolverEndpoint $dnsPeer.Endpoint)
        $services.Count | Should -Be 2
        $services[0].GetType().FullName | Should -Be 'DnsClientX.DnsService'
        $http = @($services | Where-Object ServiceType -Like '_http._tcp.*')
        $http.Count | Should -Be 1
        $http[0].Port | Should -Be 8080
        $http[0].Metadata['path'] | Should -Be '/status'
        @(Get-DnsService empty.test -ResolverEndpoint $dnsPeer.Endpoint).Count | Should -Be 0
        @(Find-DnsService empty.test -ResolverEndpoint $dnsPeer.Endpoint).Count | Should -Be 0
    }

    It 'lists service types, browses a selected type and resolves host addresses' {
        $types = @(Find-DnsService services.test -ServiceTypes -ResolverEndpoint $dnsPeer.Endpoint)
        $types | Should -Contain '_http._tcp.services.test'
        $types | Should -Contain '_ssh._tcp.services.test'
        $services = @(Get-DnsService services.test -Service http -ResolverEndpoint $dnsPeer.Endpoint)
        $services.Count | Should -Be 1
        $services[0].ServiceType | Should -Be '_http._tcp.services.test'
        $resolved = @(Get-DnsService services.test -Service http -Resolve -ResolveHosts -ResolverEndpoint $dnsPeer.Endpoint)
        $resolved.Count | Should -Be 1
        $resolved[0].Addresses.Count | Should -Be 2
        $resolved[0].Addresses.IPAddressToString | Should -Contain '192.0.2.10'
        $resolved[0].Addresses.IPAddressToString | Should -Contain '2001:db8::10'
    }

    It 'supports automation policy errors while retaining warning-only defaults' {
        $summary = Test-DnsProbe fail.test -ResolverEndpoint $dnsPeer.Endpoint -MinSuccessCount 1 -SummaryOnly -WarningAction SilentlyContinue
        $summary.PolicyPassed | Should -BeFalse
        { Test-DnsProbe fail.test -ResolverEndpoint $dnsPeer.Endpoint -MinSuccessCount 1 -FailOnPolicyViolation -WarningAction SilentlyContinue -ErrorAction Stop } |
            Should -Throw '*Probe policy failed*'
        { Test-DnsBenchmark fail.test -ResolverEndpoint $dnsPeer.Endpoint -Attempts 1 -MinSuccessfulCandidates 1 -FailOnPolicyViolation -WarningAction SilentlyContinue -ErrorAction Stop } |
            Should -Throw '*Benchmark policy failed*'
    }

    It 'runs a saved health profile and accepts typed profiles through the pipeline' {
        $profile = [DnsClientX.ResolverHealthProfile]@{
            Name = 'loopback-health'
            ResolverEndpoints = @($dnsPeer.Endpoint)
            Names = @('health.test')
            AttemptsPerCombination = 1
            ProbePolicy = [DnsClientX.ResolverProbePolicy]@{ MinSuccessCount = 1 }
        }
        $profilePath = Join-Path $TestDrive 'health.json'
        [DnsClientX.ResolverHealthProfileStore]::Save($profilePath, $profile)
        $report = Test-DnsHealth -Path $profilePath -IncludeBenchmark -ErrorAction Stop
        $report.ProfileName | Should -Be 'loopback-health'
        $report.PolicyPassed | Should -BeTrue
        $report.Queries.Count | Should -Be 1
        $report.Benchmark.Summary.OverallSuccessCount | Should -Be 1
        $profile.Names = @('fail.test')
        { $profile | Test-DnsHealth -FailOnPolicyViolation -ErrorAction Stop } | Should -Throw '*health profile*failed*'
    }

    It 'compares saved snapshots from the same query scope and retains answer changes' {
        $baselinePath = Join-Path $TestDrive 'baseline.json'
        $currentPath = Join-Path $TestDrive 'current.json'
        Test-DnsProbe change.test -ResolverEndpoint $dnsPeer.Endpoint -SavePath $baselinePath | Out-Null
        $dnsPeer.Address = '192.0.2.20'
        Test-DnsProbe change.test -ResolverEndpoint $dnsPeer.Endpoint -SavePath $currentPath | Out-Null
        $comparison = Compare-DnsResolverSnapshot -BaselinePath $baselinePath -CurrentPath $currentPath
        $comparison.ScopeComparable | Should -BeTrue
        $comparison.Answers.Count | Should -BeGreaterThan 0
        $comparison.Answers[0].Changed | Should -BeTrue
        $comparison.HealthRegression | Should -BeFalse
        $baseline = [DnsClientX.ResolverScoreStore]::Load($baselinePath)
        $current = [DnsClientX.ResolverScoreStore]::Load($currentPath)
        ($current | Compare-DnsResolverSnapshot -Baseline $baseline).ScopeComparable | Should -BeTrue
    }

    It 'explains captured response evidence without issuing another DNS query' {
        $response = Resolve-Dns explain.test -Server 127.0.0.1 -Port $dnsPeer.Port -FullResponse -RetryCount 1
        $before = $dnsPeer.Queries.Count
        $explanation = $response | Get-DnsQueryExplanation
        $explanation.Status | Should -Be ([DnsClientX.DnsResponseCode]::NoError)
        $explanation.Transport | Should -Be ([DnsClientX.Transport]::Udp)
        $explanation.ResponseSource | Should -Be ([DnsClientX.DnsResponseSource]::Network)
        $explanation.UnavailableEvidence.Count | Should -BeGreaterThan 0
        $dnsPeer.Queries.Count | Should -Be $before
    }

    It 'cancels service discovery when the PowerShell invocation stops' {
        $runner = [powershell]::Create()
        try {
            $module = (Resolve-Path "$PSScriptRoot/../DnsClientX.psd1").Path
            $null = $runner.AddScript('param($module,$endpoint) Import-Module $module -Force; Find-DnsService cancel.test -ResolverEndpoint $endpoint -TimeOut 30000').AddArgument($module).AddArgument($dnsPeer.Endpoint)
            $invocation = $runner.BeginInvoke()
            $deadline = [DateTime]::UtcNow.AddSeconds(5)
            while ($dnsPeer.Queries.Count -lt 2 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 20 }
            $dnsPeer.Queries.Count | Should -Be 2
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $runner.Stop()
            $watch.Elapsed.TotalSeconds | Should -BeLessThan 3
            $runner.InvocationStateInfo.State | Should -Be ([System.Management.Automation.PSInvocationState]::Stopped)
        } finally { $runner.Dispose() }
    }
}
