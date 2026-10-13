Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Configuration, privacy and discovery workflows' {
    BeforeAll {
        if (-not ('DnsClientX.PowerShell.Tests.ConfigurationWireTransport' -as [type])) {
            $references = @([DnsClientX.ClientX].Assembly.Location)
            if ($PSVersionTable.PSEdition -eq 'Core') {
                $references += @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' -File | Select-Object -ExpandProperty FullName)
            }
            $fixtureParameters = @{
                Path = "$PSScriptRoot/Fixtures/ConfigurationWireTransport.cs"
                ReferencedAssemblies = $references
            }
            if ($PSVersionTable.PSEdition -eq 'Core') {
                # PowerShell may run on newer .NET than the net8 module. Suppress only this compile-time unification warning.
                $fixtureParameters.CompilerOptions = '/nowarn:1701'
            }
            Add-Type @fixtureParameters
        }
    }

    It 'queries an optional transport through the public configuration without taking provider ownership (Stream=<Stream>)' -TestCases @(
        @{ Stream = $false }
        @{ Stream = $true }
    ) {
        param($Stream)
        $provider = [DnsClientX.PowerShell.Tests.ConfigurationWireTransport]::new()
        $configuration = [DnsClientX.Configuration]::new([uri]'https://relay.example/dns-query', [DnsClientX.DnsRequestFormat]::ObliviousDnsOverHttps)
        $configuration.WireTransport = $provider
        $configuration.TimeOut = 3000
        try {
            $rows = @(Resolve-Dns -Name 'example.com' -Type A -Configuration $configuration -Stream:$Stream -FullResponse -RetryCount 1 -ErrorAction Stop)
            $rows.Count | Should -Be 1
            $rows[0].Answers[0].Data | Should -Be '127.0.0.5'
            $rows[0].UsedTransport | Should -Be ([DnsClientX.Transport]::Doh)
            $provider.Queries | Should -Be 1
            $provider.Disposed | Should -BeFalse
            $configuration.TimeOut | Should -Be 3000
        } finally {
            $provider.Dispose()
        }
    }

    It 'retains pattern expansion and explicit timeout overrides for supplied configurations' {
        $provider = [DnsClientX.PowerShell.Tests.ConfigurationWireTransport]::new()
        $configuration = [DnsClientX.Configuration]::new([uri]'https://relay.example/dns-query', [DnsClientX.DnsRequestFormat]::ObliviousDnsOverHttps)
        $configuration.WireTransport = $provider
        $configuration.TimeOut = 1
        try {
            @(Resolve-Dns -Pattern 'host[1-2].example.com' -Type A -Configuration $configuration -TimeOut 2000 -FullResponse -RetryCount 1 -ErrorAction Stop).Count | Should -Be 2
            $configuration.TimeOut | Should -Be 1
            { Resolve-Dns -Name 'example.com' -Configuration $configuration -TimeOut 0 -ErrorAction Stop } | Should -Throw -ExceptionType ([System.ArgumentOutOfRangeException])
        } finally {
            $provider.Dispose()
        }
    }

    It 'caches configured provider queries and expires the requested TTL cap (Stream=<Stream>, Pattern=<Pattern>)' -TestCases @(
        @{ Stream = $false; Pattern = $false }
        @{ Stream = $true; Pattern = $false }
        @{ Stream = $false; Pattern = $true }
        @{ Stream = $true; Pattern = $true }
    ) {
        param($Stream, $Pattern)
        $provider = [DnsClientX.PowerShell.Tests.ConfigurationWireTransport]::new()
        $configuration = [DnsClientX.Configuration]::new([uri]'https://relay.example/dns-query', [DnsClientX.DnsRequestFormat]::ObliviousDnsOverHttps)
        $configuration.WireTransport = $provider
        $configuration.MaxConcurrency = 1
        $target = if ($Pattern) { @{ Pattern = 'cache[1-2].example.test' } } else { @{ Name = 'cache.example.test' } }
        $expected = if ($Pattern) { 2 } else { 1 }
        Clear-DnsCache | Out-Null
        try {
            $first = @(Resolve-Dns @target -Type A -Configuration $configuration -Stream:$Stream -ResponseCache -MaxCacheTtlSeconds 1 -FullResponse -RetryCount 1 -ErrorAction Stop)
            $queriesAfterFirst = $provider.Queries
            $cached = @(Resolve-Dns @target -Type A -Configuration $configuration -Stream:$Stream -ResponseCache -MaxCacheTtlSeconds 1 -FullResponse -RetryCount 1 -ErrorAction Stop)
            $first.Count | Should -Be $expected
            $queriesAfterFirst | Should -Be $expected
            foreach ($response in $first) {
                $response.Answers[0].Data | Should -Be '127.0.0.5'
                $response.ResponseSource | Should -Be ([DnsClientX.DnsResponseSource]::Network)
            }

            $cached.Count | Should -Be $expected
            $provider.Queries | Should -Be $expected
            foreach ($response in $cached) {
                $response.Answers[0].Data | Should -Be '127.0.0.5'
                $response.ResponseSource | Should -Be ([DnsClientX.DnsResponseSource]::Cache)
            }

            Start-Sleep -Milliseconds 1100
            $expired = @(Resolve-Dns @target -Type A -Configuration $configuration -Stream:$Stream -ResponseCache -MaxCacheTtlSeconds 1 -FullResponse -RetryCount 1 -ErrorAction Stop)
            $expired.Count | Should -Be $expected
            $provider.Queries | Should -Be ($expected * 2)
            foreach ($response in $expired) { $response.ResponseSource | Should -Be ([DnsClientX.DnsResponseSource]::Network) }
            $configuration.MaxConcurrency | Should -Be 1
            $provider.Disposed | Should -BeFalse
        } finally {
            Clear-DnsCache | Out-Null
            $provider.Dispose()
        }
    }

    It 'rejects another resolver source or transport override before invoking the supplied provider' {
        $provider = [DnsClientX.PowerShell.Tests.ConfigurationWireTransport]::new()
        $configuration = [DnsClientX.Configuration]::new([uri]'https://relay.example/dns-query', [DnsClientX.DnsRequestFormat]::ObliviousDnsOverHttps)
        $configuration.WireTransport = $provider
        try {
            { Resolve-Dns -Name 'example.com' -Configuration $configuration -DnsProvider Cloudflare -ErrorAction Stop } | Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
            { Resolve-Dns -Name 'example.com' -Configuration $configuration -RequestFormat DnsOverUDP -ErrorAction Stop } | Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
            $provider.Queries | Should -Be 0
        } finally {
            $provider.Dispose()
        }
    }

    It 'exposes discovery and continuous browse as cancellable typed core workflows' {
        (Get-Command Get-DnsDesignatedResolver -ErrorAction Stop).OutputType.Type | Should -Contain ([DnsClientX.DnsDesignatedResolver])
        (Get-Command Receive-DnsService -ErrorAction Stop).OutputType.Type | Should -Contain ([DnsClientX.DnsServiceBrowseEvent])
        $invalid = [DnsClientX.Configuration]::new('dns.example', [DnsClientX.DnsRequestFormat]::DnsOverTLS)
        { Get-DnsDesignatedResolver -Configuration $invalid -ErrorAction Stop } | Should -Throw -ExpectedMessage '*IP-based DDR*'
        { Watch-DnsService -Service http -Protocol tcp -Configuration $invalid -ErrorAction Stop } | Should -Throw -ExpectedMessage '*multicast DNS*'
    }

    It 'cancels and drains a provider query when the PowerShell invocation is stopped' {
        $provider = [DnsClientX.PowerShell.Tests.ConfigurationWireTransport]::new()
        $provider.Delay = $true
        $configuration = [DnsClientX.Configuration]::new([uri]'https://relay.example/dns-query', [DnsClientX.DnsRequestFormat]::ObliviousDnsOverHttps)
        $configuration.WireTransport = $provider
        $binary = [DnsClientX.PowerShell.CmdletResolveDnsQuery].Assembly.Location
        $shell = [System.Management.Automation.PowerShell]::Create()
        try {
            [void]$shell.AddScript('param($binary,$configuration) Import-Module $binary -Force; Resolve-Dns -Name example.com -Configuration $configuration -TimeOut 10000 -RetryCount 1 -FullResponse').AddArgument($binary).AddArgument($configuration)
            $pending = $shell.BeginInvoke()
            $clock = [System.Diagnostics.Stopwatch]::StartNew()
            while ($provider.Active -eq 0 -and $clock.Elapsed.TotalSeconds -lt 5) { Start-Sleep -Milliseconds 10 }
            $provider.Active | Should -Be 1
            $shell.Stop()
            try { [void]$shell.EndInvoke($pending) } catch [System.Management.Automation.PipelineStoppedException] { }
            $provider.Active | Should -Be 0
            $provider.Canceled | Should -Be 1
            $provider.Disposed | Should -BeFalse
        } finally {
            $shell.Dispose()
            $provider.Dispose()
        }
    }

    It 'drains a native multicast browse receiver before Stop returns' {
        $probe = [System.Net.Sockets.UdpClient]::new(0)
        $port = $probe.Client.LocalEndPoint.Port
        $probe.Dispose()
        $configuration = [DnsClientX.Configuration]::new('224.0.0.251', [DnsClientX.DnsRequestFormat]::Multicast)
        $configuration.Port = $port
        $binary = [DnsClientX.PowerShell.CmdletReceiveDnsService].Assembly.Location
        $shell = [System.Management.Automation.PowerShell]::Create()
        try {
            [void]$shell.AddScript('param($binary,$configuration) Import-Module $binary -Force; Watch-DnsService -Service http -Protocol tcp -Configuration $configuration').AddArgument($binary).AddArgument($configuration)
            $pending = $shell.BeginInvoke()
            $clock = [System.Diagnostics.Stopwatch]::StartNew()
            do {
                $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveUdpListeners()
                $listening = @($listeners | Where-Object Port -eq $port).Count -gt 0
                if (-not $listening) { Start-Sleep -Milliseconds 10 }
            } while (-not $listening -and $clock.Elapsed.TotalSeconds -lt 5)
            $listening | Should -BeTrue
            $shell.Stop()
            try { [void]$shell.EndInvoke($pending) } catch [System.Management.Automation.PipelineStoppedException] { }
            $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveUdpListeners()
            @($listeners | Where-Object Port -eq $port).Count | Should -Be 0
        } finally {
            $shell.Dispose()
        }
    }

}
