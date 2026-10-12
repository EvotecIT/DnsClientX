param(
    [Parameter(Mandatory)]
    [string] $ModulePath,
    [string] $ResultsPath
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Item -LiteralPath $ModulePath
$module = Import-Module $manifest.FullName -Force -PassThru
$required = @('Resolve-Dns', 'Find-DnsService', 'Get-DnsService', 'Invoke-DnsUpdate', 'Test-DnsHealth', 'Compare-DnsResolverSnapshot', 'Get-DnsQueryExplanation', 'Get-DnsCache', 'Clear-DnsCache')
foreach ($name in $required) {
    if (-not $module.ExportedCommands.ContainsKey($name)) { throw "Packaged module does not export $name." }
}
$library = [DnsClientX.ClientX].Assembly.Location
if (-not $library.StartsWith($manifest.Directory.FullName + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Module loaded its core outside the package: $library"
}
$data = Import-PowerShellDataFile -LiteralPath $manifest.FullName
foreach ($asset in @($data.FormatsToProcess) + @($data.TypesToProcess)) {
    if (-not (Test-Path -LiteralPath (Join-Path $manifest.Directory.FullName $asset))) { throw "Package asset is missing: $asset" }
}
if (@($data.FormatsToProcess).Count -eq 0 -or @($data.TypesToProcess).Count -eq 0) { throw 'Package needs its format and type assets.' }
$tests = Join-Path $manifest.Directory.FullName 'Tests'
New-Item -ItemType Directory -Path $tests -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot '../Module/Tests/*') -Destination $tests -Recurse -Force
Import-Module Pester -MinimumVersion 5.0 -ErrorAction Stop
$configuration = New-PesterConfiguration
$configuration.Run.Path = $tests
$configuration.Run.PassThru = $true
$configuration.Output.Verbosity = 'Detailed'
if ($ResultsPath) {
    $configuration.TestResult.Enabled = $true
    $configuration.TestResult.OutputPath = $ResultsPath
}
$result = Invoke-Pester -Configuration $configuration
if ($result.Result -ne 'Passed' -or $result.FailedCount -gt 0 -or $result.PassedCount -eq 0) { throw "Package tests $($result.Result): $($result.FailedCount) failures, $($result.PassedCount) passes." }
