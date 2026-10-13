<#
.SYNOPSIS
Tests the shipped module ZIP in a fresh PowerShell process.
.DESCRIPTION
Copies module tests beside the extracted manifest and rejects assemblies loaded from another location.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ZipPath,
    [Parameter(Mandatory)][string] $TestsPath,
    [Parameter(Mandatory)][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) {
    throw 'OutputDirectory must be a new directory for this package consumer.'
}
Expand-Archive -LiteralPath $ZipPath -DestinationPath $OutputDirectory
$moduleRoot = Join-Path $OutputDirectory 'DnsClientX'
Copy-Item -LiteralPath $TestsPath -Destination $moduleRoot -Recurse
Import-Module (Join-Path $moduleRoot 'DnsClientX.psd1') -Force -ErrorAction Stop

function Assert-PackagedAssemblies {
    $prefix = [IO.Path]::GetFullPath($moduleRoot) + [IO.Path]::DirectorySeparatorChar
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
Write-Host "Package SHA256: $((Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash)"
Write-Host "PowerShell $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition))"
Import-Module Pester -MinimumVersion 5.0.0 -ErrorAction Stop
$configuration = New-PesterConfiguration
$configuration.Run.Path = Join-Path $moduleRoot 'Tests'
$configuration.Run.PassThru = $true
$configuration.Output.Verbosity = 'Detailed'
$configuration.TestResult.Enabled = $true
$configuration.TestResult.OutputPath = Join-Path $OutputDirectory 'TestResults.xml'
$configuration.TestResult.OutputFormat = 'NUnitXml'
$result = Invoke-Pester -Configuration $configuration
Assert-PackagedAssemblies
if ($null -eq $result) { throw 'Packaged module discovery did not execute any tests.' }
$executed = $result.TotalCount - $result.SkippedCount - $result.NotRunCount
if ($result.Result -ne 'Passed' -or $executed -eq 0 -or $result.FailedCount -gt 0 -or $result.NotRunCount -gt 0) {
    throw "Packaged module tests failed: result=$($result.Result), total=$($result.TotalCount), failed=$($result.FailedCount), failed blocks=$($result.FailedBlocksCount), failed containers=$($result.FailedContainersCount), not run=$($result.NotRunCount)."
}
