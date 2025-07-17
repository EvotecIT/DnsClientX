Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Resolve-Dns cmdlet' {
    It 'Fails when TimeOut is less than or equal to zero' {
        { Resolve-Dns -Name 'example.com' -TimeOut 0 -ErrorAction Stop } | Should -Throw -ExceptionType System.ArgumentOutOfRangeException
    }

    It 'Throws when AllServers is used without Server' {
        { Resolve-Dns -Name 'example.com' -AllServers -ErrorAction Stop } | Should -Throw -ExceptionType System.InvalidOperationException
    }

    It 'Removes duplicate servers before processing' {
        $result = Resolve-Dns -Name 'example.com' -Server @('127.0.0.1', '127.0.0.1') -AllServers -FullResponse -TimeOut 10 -ErrorAction SilentlyContinue
        $result.Count | Should -Be 1
    }

    It 'Returns typed records when requested' {
        $answer = [DnsClientX.DnsAnswer]@{
            Type    = [DnsClientX.DnsRecordType]::A
            DataRaw = '127.0.0.1'
        }
        $typed = [DnsClientX.DnsRecordFactory]::Create($answer)
        $typed | Should -Not -BeNullOrEmpty
    }

    It 'Enumerates typed answers as individual objects' {
        $ans1 = [DnsClientX.DnsAnswer]@{
            Type    = [DnsClientX.DnsRecordType]::TXT
            DataRaw = 'foo=bar'
        }
        $ans2 = [DnsClientX.DnsAnswer]@{
            Type    = [DnsClientX.DnsRecordType]::TXT
            DataRaw = 'v=spf1 -all'
        }
        $response = [DnsClientX.DnsResponse]@{
            Answers = @($ans1, $ans2)
        }
        $Values = $response.Answers | ForEach-Object {
            [DnsClientX.DnsRecordFactory]::Create($_, $true)
        }
        $types = $Values | ForEach-Object { $_.GetType().Name }
        $types | Should -Contain 'KeyValueTxtRecord'
        $types | Should -Contain 'SpfRecord'
    }
}
