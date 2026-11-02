Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Explicit privacy provider setup and stamp inspection' {
    It 'loads a local factory and preserves caller ownership through a real configured query' {
        # Framework LoadFrom keeps its DLL open until the qualification process exits.
        $dll = Join-Path $PSScriptRoot ("Fixtures/privacy-provider-" + $PSVersionTable.PSEdition + '.dll')
        $references = @([DnsClientX.ClientX].Assembly.Location)
        if ($PSVersionTable.PSEdition -eq 'Core') {
            $references += @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' -File | Select-Object -ExpandProperty FullName)
        }
        $parameters = @{ Path = "$PSScriptRoot/Fixtures/PrivacyTransportFactory.cs"; ReferencedAssemblies = $references; OutputAssembly = $dll }
        if ($PSVersionTable.PSEdition -eq 'Core') { $parameters.CompilerOptions = '/nowarn:1701' }
        Add-Type @parameters
        $transport = New-DnsPrivacyTransport -ProviderPath $dll -Resolver 'selected-target' -Relay 'selected-relay'
        try {
            $transport.Resolver | Should -Be 'selected-target'
            $transport.Relay | Should -Be 'selected-relay'
            $response = Resolve-Dns example.test -Configuration $transport.CreateConfiguration() -FullResponse -RetryCount 1
            $response.Status | Should -Be ([DnsClientX.DnsResponseCode]::NoError)
            $transport.Disposed | Should -BeFalse
        } finally { $transport.Dispose() }
        $transport.Disposed | Should -BeTrue
    }

    It 'inspects privacy stamp roles without loading an optional provider' {
        $target = ConvertFrom-DnsStamp 'sdns://BQUAAAAAAAAADnRhcmdldC5leGFtcGxlCi9kbnMtcXVlcnk'
        $target.Kind | Should -Be ([DnsClientX.DnsPrivacyStampKind]::OdohTarget)
        $target.Endpoint.AbsoluteUri | Should -Be 'https://target.example/dns-query'
    }
}
