Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Resolve-Dns binding' {
    It 'exposes Server parameter with ServerName alias and list type' {
        $cmd = Get-Command Resolve-Dns
        $parameter = $cmd.Parameters['Server']

        $parameter | Should -Not -BeNullOrEmpty
        $parameter.Aliases | Should -Contain 'ServerName'
        $parameter.ParameterType | Should -Be ([System.Collections.Generic.List[string]])
    }

    It 'rejects null Server values during binding' {
        { Resolve-Dns -Name 'example.com' -Server $null -ErrorAction Stop } | Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
    }

    It 'exposes parity-focused advanced query parameters' {
        $cmd = Get-Command Resolve-Dns

        $cmd.Parameters.Keys | Should -Contain 'EnableEdns'
        $cmd.Parameters.Keys | Should -Contain 'EdnsBufferSize'
        $cmd.Parameters.Keys | Should -Contain 'ClientSubnet'
        $cmd.Parameters.Keys | Should -Contain 'CheckingDisabled'
        $cmd.Parameters.Keys | Should -Contain 'RequestNsid'
        $cmd.Parameters.Keys | Should -Contain 'DnsSelectionStrategy'
        $cmd.Parameters.Keys | Should -Contain 'RequestFormat'
        $cmd.Parameters.Keys | Should -Contain 'Port'
        $cmd.Parameters.Keys | Should -Contain 'UserAgent'
        $cmd.Parameters.Keys | Should -Contain 'HttpVersion'
        $cmd.Parameters.Keys | Should -Contain 'IgnoreCertificateErrors'
        $cmd.Parameters.Keys | Should -Contain 'UseTcpFallback'
        $cmd.Parameters.Keys | Should -Contain 'ProxyUri'
        $cmd.Parameters.Keys | Should -Contain 'MaxConnectionsPerServer'
        $cmd.Parameters.Keys | Should -Contain 'MaxConcurrency'
    }

    It 'allows DNSSEC switches on ResolverEndpoint syntax' {
        $syntax = (Get-Command Resolve-Dns -Syntax | Out-String)
        $syntax | Should -Match 'ResolverEndpoint .*ValidateDnsSec'
        $syntax | Should -Match 'ResolverEndpoint .*RequestDnsSec'
        $syntax | Should -Match 'ResolverEndpoint .*FullResponse'
        $syntax | Should -Match 'ResolverEndpoint .*RequestNsid'
        $syntax | Should -Match 'Server .*RequestFormat'
        $syntax | Should -Match 'Server .*Port'
    }

    It 'exports benchmark cmdlet from the manifest' {
        (Get-Command Test-DnsBenchmark -ErrorAction Stop).Name | Should -Be 'Test-DnsBenchmark'
    }
}
