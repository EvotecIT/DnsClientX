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

    It 'binds shared query options for <Case>' -TestCases @(
        @{ Case = 'Name/DefaultResolver'; Target = @{ Name = 'example.com' }; Source = @{} }
        @{ Case = 'Pattern/DefaultResolver'; Target = @{ Pattern = 'host[1-2].example.com' }; Source = @{} }
        @{ Case = 'Name/DnsProvider'; Target = @{ Name = 'example.com' }; Source = @{ DnsProvider = 'Cloudflare' } }
        @{ Case = 'Pattern/DnsProvider'; Target = @{ Pattern = 'host[1-2].example.com' }; Source = @{ DnsProvider = 'Cloudflare' } }
        @{ Case = 'Name/Server'; Target = @{ Name = 'example.com' }; Source = @{ Server = '127.0.0.1' } }
        @{ Case = 'Pattern/Server'; Target = @{ Pattern = 'host[1-2].example.com' }; Source = @{ Server = '127.0.0.1' } }
        @{ Case = 'Name/ResolverEndpoint'; Target = @{ Name = 'example.com' }; Source = @{ ResolverEndpoint = 'udp@127.0.0.1:53' } }
        @{ Case = 'Pattern/ResolverEndpoint'; Target = @{ Pattern = 'host[1-2].example.com' }; Source = @{ ResolverEndpoint = 'udp@127.0.0.1:53' } }
        @{ Case = 'Name/ResolverDnsProvider'; Target = @{ Name = 'example.com' }; Source = @{ ResolverDnsProvider = 'Cloudflare' } }
        @{ Case = 'Pattern/ResolverDnsProvider'; Target = @{ Pattern = 'host[1-2].example.com' }; Source = @{ ResolverDnsProvider = 'Cloudflare' } }
        @{ Case = 'Name/ResolverSelection'; Target = @{ Name = 'example.com' }; Source = @{ ResolverSelectionPath = 'unused-selection.json' } }
        @{ Case = 'Pattern/ResolverSelection'; Target = @{ Pattern = 'host[1-2].example.com' }; Source = @{ ResolverSelectionPath = 'unused-selection.json' } }
    ) {
        param($Target, $Source)

        $options = @{
            Type = 'TXT'
            FullResponse = $true
            TypedRecords = $true
            ParseTypedTxtRecords = $true
            TimeOut = 0
            RetryCount = 1
            RetryDelayMs = 0
            RequestDnsSec = $false
            ValidateDnsSec = $false
        }

        # Invalid timeout reaches request validation after binding, before any network or file access.
        { Resolve-Dns @Target @Source @options -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.ArgumentOutOfRangeException]) -ExpectedMessage '*TimeOutMilliseconds*'
    }

    It 'requires either a name or a pattern when using the default resolver' {
        { Resolve-Dns -Name 'example.com' -Pattern 'host[1-2].example.com' -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
    }

    It 'preserves positional record types for name and pattern queries' {
        { Resolve-Dns example.com TXT -TimeOut 0 -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.ArgumentOutOfRangeException]) -ExpectedMessage '*TimeOutMilliseconds*'
        { Resolve-Dns -Pattern 'host[1-2].example.com' TXT -TimeOut 0 -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.ArgumentOutOfRangeException]) -ExpectedMessage '*TimeOutMilliseconds*'
        { Resolve-Dns example.com TXT -DnsProvider Cloudflare -TimeOut 0 -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.ArgumentOutOfRangeException]) -ExpectedMessage '*TimeOutMilliseconds*'
    }

    It 'keeps resolver sources and server-only transport settings separate' {
        { Resolve-Dns -Name 'example.com' -DnsProvider Cloudflare -Server '127.0.0.1' -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.InvalidOperationException]) -ExpectedMessage '*Specify only one resolver source*'
        { Resolve-Dns -Name 'example.com' -DnsProvider Cloudflare -RequestFormat DnsOverTCP -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.Management.Automation.PSArgumentException]) -ExpectedMessage '*Server transport*'
        { Resolve-Dns -Name 'example.com' -Server '127.0.0.1' -ResolverStrategy FastestWins -ErrorAction Stop } |
            Should -Throw -ExceptionType ([System.Management.Automation.PSArgumentException]) -ExpectedMessage '*Multi-resolver options*'
    }

    It 'exports benchmark cmdlet from the manifest' {
        (Get-Command Test-DnsBenchmark -ErrorAction Stop).Name | Should -Be 'Test-DnsBenchmark'
    }
}
