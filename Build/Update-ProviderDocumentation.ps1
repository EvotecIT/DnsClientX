<#
.SYNOPSIS
Regenerates the README provider table from the compiled library's provider definitions.
#>
[CmdletBinding()]
param(
    [string] $AssemblyPath = (Join-Path $PSScriptRoot '../DnsClientX/bin/Release/net8.0/DnsClientX.dll'),
    [string] $ReadmePath = (Join-Path $PSScriptRoot '../README.md')
)
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$rows = [Collections.Generic.List[string]]::new()
$rows.Add('<!-- provider-profiles:start -->')
$rows.Add('| Profile | Default | UDP | TCP | DoT | DoH wire | DoH JSON | DoH3 | DoQ |')
$rows.Add('| --- | --- | --- | --- | --- | --- | --- | --- | --- |')
$formats = @('DnsOverUDP', 'DnsOverTCP', 'DnsOverTLS', 'DnsOverHttps', 'DnsOverHttpsJSON', 'DnsOverHttp3', 'DnsOverQuic')
$labels = @{ DnsOverUDP = 'UDP'; DnsOverTCP = 'TCP'; DnsOverTLS = 'DoT'; DnsOverHttps = 'DoH wire'; DnsOverHttpsJSON = 'DoH JSON'; DnsOverHttpsWirePost = 'DoH POST'; DnsOverHttp3 = 'DoH3'; DnsOverQuic = 'DoQ' }
foreach ($endpoint in [Enum]::GetValues([DnsClientX.DnsEndpoint])) {
    $supported = [DnsClientX.DnsResolverEndpointFactory]::GetSupportedRequestFormats($endpoint)
    if ($supported.Count -eq 0) { continue }
    $default = if ($endpoint -eq 'System') { 'DnsOverUDP' } elseif ($endpoint -eq 'SystemTcp') { 'DnsOverTCP' } else { ([DnsClientX.Configuration]::new($endpoint)).RequestFormat.ToString() }
    $cells = foreach ($format in $formats) { if ($supported -contains [DnsClientX.DnsRequestFormat]$format) { 'yes' } else { '-' } }
    $rows.Add('| ' + $endpoint + ' | ' + $labels[$default] + ' | ' + ($cells -join ' | ') + ' |')
}
$rows.Add('<!-- provider-profiles:end -->')
$readme = [IO.File]::ReadAllText([IO.Path]::GetFullPath($ReadmePath))
$start = $readme.IndexOf('<!-- provider-profiles:start -->', [StringComparison]::Ordinal)
$endMarker = '<!-- provider-profiles:end -->'
$end = $readme.IndexOf($endMarker, [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -lt $start) { throw 'README provider generation markers are missing or out of order.' }
$result = $readme.Substring(0, $start) + ($rows -join "`n") + $readme.Substring($end + $endMarker.Length)
[IO.File]::WriteAllText([IO.Path]::GetFullPath($ReadmePath), $result, [Text.UTF8Encoding]::new($false))
Write-Host "Updated $($rows.Count - 4) provider profiles from the compiled library."
