<#
.SYNOPSIS
Regenerates or checks the README provider table and JSON catalog from the compiled library.
#>
[CmdletBinding()]
param(
    [string] $AssemblyPath,
    [string] $ReadmePath,
    [string] $JsonPath,
    [switch] $Check
)
$ErrorActionPreference = 'Stop'
if (-not $AssemblyPath) { $AssemblyPath = Join-Path $PSScriptRoot '../DnsClientX/bin/Release/net8.0/DnsClientX.dll' }
if (-not $ReadmePath) { $ReadmePath = Join-Path $PSScriptRoot '../README.md' }
if (-not $JsonPath) { $JsonPath = Join-Path $PSScriptRoot '../Assets/Data/provider-profiles.json' }
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$rows = [Collections.Generic.List[string]]::new()
$rows.Add('<!-- provider-profiles:start -->')
$rows.Add('| Profile | Default | UDP | TCP | DoT | DoH wire | DoH JSON | DoH3 | DoQ |')
$rows.Add('| --- | --- | --- | --- | --- | --- | --- | --- | --- |')
$formats = @('DnsOverUDP', 'DnsOverTCP', 'DnsOverTLS', 'DnsOverHttps', 'DnsOverHttpsJSON', 'DnsOverHttp3', 'DnsOverQuic')
$labels = @{ DnsOverUDP = 'UDP'; DnsOverTCP = 'TCP'; DnsOverTLS = 'DoT'; DnsOverHttps = 'DoH wire'; DnsOverHttpsJSON = 'DoH JSON'; DnsOverHttpsWirePost = 'DoH POST'; DnsOverHttp3 = 'DoH3'; DnsOverQuic = 'DoQ' }
$profiles = [Collections.Generic.List[object]]::new()
foreach ($endpoint in [Enum]::GetValues([DnsClientX.DnsEndpoint])) {
    $supported = [DnsClientX.DnsResolverEndpointFactory]::GetSupportedRequestFormats($endpoint)
    if ($supported.Count -eq 0) { continue }
    # Metadata generation needs no reachable resolver. This fallback only supplies
    # addresses on resolver-less hosts; Configuration still selects the default format.
    $default = ([DnsClientX.Configuration]::new($endpoint, [DnsClientX.DnsSelectionStrategy]::First, [DnsClientX.SystemDnsFallback]::PublicResolvers)).RequestFormat.ToString()
    $profileFormats = @(foreach ($format in $formats) { if ($supported -contains [DnsClientX.DnsRequestFormat]$format) { $format } })
    $profiles.Add([ordered] @{ name = $endpoint.ToString(); default = $default; transports = $profileFormats })
    $cells = foreach ($format in $formats) { if ($profileFormats -contains $format) { 'yes' } else { '-' } }
    $rows.Add('| ' + $endpoint + ' | ' + $labels[$default] + ' | ' + ($cells -join ' | ') + ' |')
}
$rows.Add('<!-- provider-profiles:end -->')
$readme = [IO.File]::ReadAllText([IO.Path]::GetFullPath($ReadmePath))
$start = $readme.IndexOf('<!-- provider-profiles:start -->', [StringComparison]::Ordinal)
$endMarker = '<!-- provider-profiles:end -->'
$end = $readme.IndexOf($endMarker, [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -lt $start) { throw 'README provider generation markers are missing or out of order.' }
$result = $readme.Substring(0, $start) + ($rows -join "`n") + $readme.Substring($end + $endMarker.Length)
$data = [ordered] @{
    schema = 1
    transports = @(foreach ($format in $formats) { [ordered] @{ id = $format; label = $labels[$format] } })
    profiles = $profiles.ToArray()
}
# Compact JSON keeps serialization identical in Windows PowerShell 5.1 and PowerShell 7.
$json = (ConvertTo-Json -InputObject $data -Depth 6 -Compress) + "`n"
if ($Check) {
    if ($readme.Replace("`r`n", "`n") -cne $result.Replace("`r`n", "`n")) {
        throw 'README provider table differs from the library. Run Build/Update-ProviderDocumentation.ps1.'
    }
    if (-not [IO.File]::Exists([IO.Path]::GetFullPath($JsonPath)) -or
        [IO.File]::ReadAllText([IO.Path]::GetFullPath($JsonPath)).Replace("`r`n", "`n") -cne $json) {
        throw 'Provider JSON differs from the library/README. Run Build/Update-ProviderDocumentation.ps1.'
    }
    Write-Host "Provider documentation parity verified: $($profiles.Count) profiles, $($formats.Count) transports."
    return
}
[IO.File]::WriteAllText([IO.Path]::GetFullPath($ReadmePath), $result, [Text.UTF8Encoding]::new($false))
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($JsonPath)))
[IO.File]::WriteAllText([IO.Path]::GetFullPath($JsonPath), $json, [Text.UTF8Encoding]::new($false))
Write-Host "Updated $($profiles.Count) provider profiles in README and JSON from the compiled library."
