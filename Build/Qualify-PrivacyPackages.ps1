<#
.SYNOPSIS
Qualifies DNSCrypt native delivery through a non-SDK packages.config consumer.
.DESCRIPTION
Reuses the existing package consumer's exact dependency graph. Windows restores,
builds and runs the staged legacy consumer. StageFromCache provides only the real
MSBuild native-copy and byte-identity checks on another operating system.
#>
param(
    [Parameter(Mandatory)][string] $AssetsFile,
    [Parameter(Mandatory)][string] $PackageCache,
    [Parameter(Mandatory)][string] $OutputRoot,
    [string] $NuGetConfig,
    [switch] $StageFromCache
)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a fresh task-owned output directory for the legacy consumer.' }
if (!$StageFromCache -and !$IsWindows) { throw 'Native Framework execution requires Windows. Use StageFromCache for copy proof only.' }
$assets = Get-Content -LiteralPath $AssetsFile -Raw | ConvertFrom-Json
$target = @($assets.targets.PSObject.Properties | Where-Object { $_.Name -match '^(net472|\.NETFramework,Version=v4\.7\.2)$' })[0]
if (!$target -and $StageFromCache) { $target = @($assets.targets.PSObject.Properties | Where-Object { $_.Name -match '^(net8\.0|\.NETCoreApp,Version=v8\.0)$' })[0] }
if (!$target) { throw 'The resolved package graph does not contain the required consumer target.' }
$packages = @($target.Value.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' })
$provider = @($packages | Where-Object { $_.Name -like 'DnsClientX.DnsCrypt/*' })[0]
$sodium = @($packages | Where-Object { $_.Name -like 'libsodium/*' })[0]
if (!$provider -or !$sodium) { throw 'The resolved graph must contain the candidate DNSCrypt provider and libsodium.' }
$candidateVersion = $provider.Name.Split('/')[1]
$fixture = Join-Path $PSScriptRoot '../scripts/qualification/privacy-packages-legacy'
$consumerRoot = New-Item -ItemType Directory -Path (Join-Path $OutputRoot 'consumer')
$packageRoot = Join-Path $OutputRoot 'packages'
$binaryRoot = Join-Path $OutputRoot 'bin'
Copy-Item -LiteralPath (Join-Path $fixture 'LegacyPrivacyPackages.csproj'), (Join-Path $fixture 'Program.cs') -Destination $consumerRoot

$configuration = [xml] '<packages />'
$references = [xml] '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><ItemGroup /></Project>'
foreach ($package in $packages) {
    $id, $version = $package.Name.Split('/')
    $entry = $configuration.CreateElement('package')
    $entry.SetAttribute('id', $id); $entry.SetAttribute('version', $version); $entry.SetAttribute('targetFramework', 'net472')
    $null = $configuration.DocumentElement.AppendChild($entry)
    $installedPath = Join-Path $packageRoot "$id.$version"
    if ($StageFromCache) {
        $archive = Join-Path $PackageCache "$($id.ToLowerInvariant())/$version/$($id.ToLowerInvariant()).$version.nupkg"
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $installedPath)
    }
    foreach ($asset in $package.Value.runtime.PSObject.Properties) {
        if (!$asset.Name.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase)) { continue }
        $reference = $references.CreateElement('Reference', $references.DocumentElement.NamespaceURI)
        $reference.SetAttribute('Include', [IO.Path]::GetFileNameWithoutExtension($asset.Name))
        $hint = $references.CreateElement('HintPath', $references.DocumentElement.NamespaceURI)
        $hint.InnerText = Join-Path $installedPath $asset.Name
        $null = $reference.AppendChild($hint)
        $null = $references.DocumentElement.FirstChild.AppendChild($reference)
    }
}
$configuration.Save((Join-Path $consumerRoot 'packages.config'))
$references.Save((Join-Path $consumerRoot 'PackageReferences.props'))
if (!$StageFromCache) {
    if (!$NuGetConfig) { throw 'Native qualification requires the existing isolated NuGet.Config.' }
    & nuget restore (Join-Path $consumerRoot 'packages.config') -PackagesDirectory $packageRoot -ConfigFile $NuGetConfig -NonInteractive
    if ($LASTEXITCODE) { throw 'Legacy packages.config restore failed.' }
}
$project = Join-Path $consumerRoot 'LegacyPrivacyPackages.csproj'
$arguments = @($project, '-nologo', "-p:LegacyPackagesDirectory=$packageRoot", "-p:CandidateVersion=$candidateVersion",
    "-p:OutputPath=$binaryRoot/", "-p:IntermediateOutputPath=$OutputRoot/obj/")
& dotnet msbuild @arguments -t:QualifyNativeCopies
if ($LASTEXITCODE) { throw 'Legacy packages.config native-copy qualification failed.' }
foreach ($rid in 'win-x86', 'win-x64', 'win-arm64') {
    $relative = "runtimes/$rid/native/libsodium.dll"
    $source = Join-Path $packageRoot "libsodium.$($sodium.Name.Split('/')[1])/$relative"
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath (Join-Path $binaryRoot $relative)).Hash) {
        throw "The legacy consumer's $rid DLL differs from the resolved libsodium package."
    }
}
if ($StageFromCache) {
    Write-Output 'PASS non-SDK packages.config native-copy targets and all three DLL hashes; native Windows execution remains required.'
} else {
    & dotnet msbuild @arguments -t:Build
    if ($LASTEXITCODE) { throw 'Legacy packages.config consumer build failed.' }
    & (Join-Path $binaryRoot 'LegacyPrivacyPackages.exe')
    if ($LASTEXITCODE) { throw 'Legacy packages.config native consumer failed.' }
}
