BeforeAll {
    if (-not ('DnsWorkflowServer' -as [type])) {
        Add-Type -Path "$PSScriptRoot/Fixtures/DnsWorkflowServer.cs"
    }

    function New-FixtureQuestion {
        param([string] $Name)
        $packet = [Collections.Generic.List[byte]]::new()
        $packet.AddRange([byte[]](0x12, 0x34, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0))
        foreach ($label in $Name.Split('.')) {
            $bytes = [Text.Encoding]::ASCII.GetBytes($label)
            $packet.Add([byte]$bytes.Length)
            $packet.AddRange($bytes)
        }
        $packet.AddRange([byte[]](0, 0, 1, 0, 1))
        return ,$packet.ToArray()
    }
}

Describe 'Loopback DNS fixture packet and shutdown safety' {
    It 'continues serving valid queries after <Label>' -TestCases @(
        @{ Label = 'an empty datagram'; Datagram = [byte[]]@() }
        @{ Label = 'a truncated name label'; Datagram = [byte[]](0x12, 0x34, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 5, 97, 98, 99, 100) }
        @{ Label = 'a truncated question'; Datagram = [byte[]](0x12, 0x34, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 3, 97, 98, 99, 0, 0) }
    ) {
        param($Label, $Datagram)
        $peer = [DnsWorkflowServer]::new()
        $client = [Net.Sockets.UdpClient]::new()
        try {
            $client.Connect([Net.IPAddress]::Loopback, $peer.Port)
            $client.Client.ReceiveTimeout = 2000
            $null = $client.Send($Datagram, $Datagram.Length)
            $query = New-FixtureQuestion 'fixture.test'
            $null = $client.Send($query, $query.Length)
            $sender = [Net.IPEndPoint]::new([Net.IPAddress]::Any, 0)
            $response = $client.Receive([ref]$sender)
            $response[0] | Should -Be 0x12
            $response[1] | Should -Be 0x34
            $response[7] | Should -Be 1
            ([Net.IPAddress]::new([byte[]]$response[-4..-1])).IPAddressToString | Should -Be '192.0.2.10'
            $peer.Queries.Count | Should -Be 1
            $peer.Queries[0] | Should -Be 'fixture.test'
        } finally {
            $client.Close()
            $peer.Dispose()
        }
    }

    It 'disposes the receiver and a pending delayed reply without an exception' {
        $peer = [DnsWorkflowServer]::new()
        $client = [Net.Sockets.UdpClient]::new()
        try {
            $query = New-FixtureQuestion 'slow.test'
            $null = $client.Send($query, $query.Length, [Net.IPEndPoint]::new([Net.IPAddress]::Loopback, $peer.Port))
            $watch = [Diagnostics.Stopwatch]::StartNew()
            while ($peer.Queries.Count -eq 0 -and $watch.ElapsedMilliseconds -lt 2000) {
                Start-Sleep -Milliseconds 10
            }
            $peer.Queries.Count | Should -Be 1
        } finally {
            $client.Close()
            $peer.Dispose()
        }
    }
}
