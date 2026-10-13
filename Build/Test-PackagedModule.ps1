param(
    [Parameter(Mandatory)]
    [string] $ModulePath,
    [string] $ResultsPath,
    [string] $TestsPath = (Join-Path $PSScriptRoot '../Module/Tests')
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Item -LiteralPath $ModulePath
$module = Import-Module $manifest.FullName -Force -PassThru
$required = @('Resolve-Dns', 'Find-DnsService', 'Get-DnsService', 'Receive-DnsService', 'Get-DnsDesignatedResolver', 'Get-DnsTransportCapability', 'Invoke-DnsUpdate', 'Test-DnsHealth', 'Compare-DnsResolverSnapshot', 'Get-DnsQueryExplanation', 'Get-DnsCache', 'Clear-DnsCache')
foreach ($name in $required) {
    if (-not $module.ExportedCommands.ContainsKey($name)) { throw "Packaged module does not export $name." }
}
function Assert-PackagedAssemblies {
    $prefix = [IO.Path]::GetFullPath($manifest.Directory.FullName) + [IO.Path]::DirectorySeparatorChar
    $comparison = [StringComparison]::Ordinal
    if ([IO.Path]::DirectorySeparatorChar -eq '\') { $comparison = [StringComparison]::OrdinalIgnoreCase }
    foreach ($assembly in @([DnsClientX.ClientX].Assembly, [DnsClientX.PowerShell.CmdletResolveDnsQuery].Assembly)) {
        $location = [IO.Path]::GetFullPath($assembly.Location)
        if (-not $location.StartsWith($prefix, $comparison)) {
            throw "Assembly '$($assembly.GetName().Name)' was loaded outside the extracted package: $location"
        }
        Write-Host "$($assembly.GetName().Name) $($assembly.GetName().Version): $location"
    }
}
Assert-PackagedAssemblies
Write-Host "PowerShell $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition))"
$data = Import-PowerShellDataFile -LiteralPath $manifest.FullName
foreach ($asset in @($data.FormatsToProcess) + @($data.TypesToProcess)) {
    if (-not (Test-Path -LiteralPath (Join-Path $manifest.Directory.FullName $asset))) { throw "Package asset is missing: $asset" }
}
if (@($data.FormatsToProcess).Count -eq 0 -or @($data.TypesToProcess).Count -eq 0) { throw 'Package needs its format and type assets.' }
$tests = Join-Path $manifest.Directory.FullName 'Tests'
New-Item -ItemType Directory -Path $tests -Force | Out-Null
Get-ChildItem -LiteralPath $TestsPath | Copy-Item -Destination $tests -Recurse -Force
Import-Module Pester -MinimumVersion 5.0 -ErrorAction Stop
$configuration = New-PesterConfiguration
$configuration.Run.Path = $tests
$configuration.Run.PassThru = $true
$configuration.Output.Verbosity = 'Detailed'
if ($ResultsPath) {
    $configuration.TestResult.Enabled = $true
    $configuration.TestResult.OutputPath = $ResultsPath
    $configuration.TestResult.OutputFormat = 'NUnitXml'
}
$result = Invoke-Pester -Configuration $configuration
Assert-PackagedAssemblies
if ($null -eq $result) { throw 'Packaged module discovery did not execute any tests.' }
$executed = $result.TotalCount - $result.SkippedCount - $result.NotRunCount
if ($result.Result -ne 'Passed' -or $executed -eq 0 -or $result.FailedCount -gt 0 -or $result.NotRunCount -gt 0) {
    throw "Packaged module tests failed: result=$($result.Result), total=$($result.TotalCount), failed=$($result.FailedCount), failed blocks=$($result.FailedBlocksCount), failed containers=$($result.FailedContainersCount), not run=$($result.NotRunCount)."
}
