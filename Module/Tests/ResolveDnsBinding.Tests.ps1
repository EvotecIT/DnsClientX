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
}
