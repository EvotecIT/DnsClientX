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

    It 'queries an optional transport through the public configuration without taking provider ownership' {
        $provider = [DnsClientX.PowerShell.Tests.ConfigurationWireTransport]::new()
        $configuration = [DnsClientX.Configuration]::new([uri]'https://relay.example/dns-query', [DnsClientX.DnsRequestFormat]::ObliviousDnsOverHttps)
        $configuration.WireTransport = $provider
        $configuration.TimeOut = 3000
        try {
            $rows = @(Resolve-Dns -Name 'example.com' -Type A -Configuration $configuration -FullResponse -RetryCount 1 -ErrorAction Stop)
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
