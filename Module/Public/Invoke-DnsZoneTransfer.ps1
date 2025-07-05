function Invoke-DnsZoneTransfer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Zone,

        [Parameter(Mandatory = $true)]
        [string]$Server,

        [int]$Port = 53,

        [int]$TimeOut = [DnsClientX.Configuration]::DefaultTimeout
    )

    $client = [DnsClientX.ClientX]::new($Server, [DnsClientX.DnsRequestFormat]::DnsOverTCP, $TimeOut)
    try {
        $client.EndpointConfiguration.Port = $Port
        $task = $client.ZoneTransferAsync($Zone)
        $task.Wait()
        $task.Result
    } finally {
        $client.Dispose()
    }
}
