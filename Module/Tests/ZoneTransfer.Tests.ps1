Import-Module "$PSScriptRoot/../DnsClientX.psd1" -Force

Describe 'Get-DnsZoneTransfer cmdlet' {
    It 'Cmdlet is available' {
        Get-Command Get-DnsZone | Should -Not -BeNullOrEmpty
    }

    It 'Fails with a transport error when the explicit server is unreachable' {
        $socket = [System.Net.Sockets.Socket]::new([System.Net.Sockets.AddressFamily]::InterNetwork,
            [System.Net.Sockets.SocketType]::Stream, [System.Net.Sockets.ProtocolType]::Tcp)
        try {
            $socket.Bind([System.Net.IPEndPoint]::new([System.Net.IPAddress]::Loopback, 0))
            $failure = { Get-DnsZoneTransfer -Zone 'example.com' -Server '127.0.0.1' -Port $socket.LocalEndPoint.Port -ErrorAction Stop } | Should -Throw -PassThru
            $cause = $failure.Exception.GetBaseException()
            ($cause -is [System.Net.Sockets.SocketException] -or $cause -is [System.TimeoutException]) | Should -BeTrue
        } finally { $socket.Dispose() }
    }
}
