param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Documentation', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [bool] $SignModule = $true,

    [string] $ProjectBuildConfigPath = '..\Build\project.build.json',

    [string] $PowerShellGalleryApiKeyPath = 'C:\Support\Important\PowerShellGalleryAPI.txt',

    [string] $GitHubApiKeyPath = 'C:\Support\Important\GitHubAPI.txt'
)

Import-Module PSPublishModule -Force -ErrorAction Stop

Build-Module -ModuleName 'DnsClientX' -RunMode $RunMode {
    # Usual defaults as per standard module
    $Manifest = [ordered] @{
        ModuleVersion        = '2.1.X'
        CompatiblePSEditions = @('Desktop', 'Core')
        GUID                 = '77fa806c-70b7-48d9-8b88-942ed73f24ed'
        Author               = 'Przemyslaw Klys'
        CompanyName          = 'Evotec'
        Copyright            = "(c) 2011 - $((Get-Date).Year) Przemyslaw Klys @ Evotec. All rights reserved."
        Description          = 'DnsClientX is PowerShell module that allows you to query DNS servers for information. It supports DNS over UDP, TCP and DNS over HTTPS (DoH) and DNS over TLS (DoT). It supports multiple types of DNS queries and can be used to query public DNS servers, private DNS servers and has built-in DNS Providers.'
        Tags                 = @('Windows', 'MacOS', 'Linux')
        IconUri              = 'https://raw.githubusercontent.com/EvotecIT/DnsClientX/master/Assets/Icons/DnsClientX3_128x128.png'
        ProjectUri           = 'https://github.com/EvotecIT/DnsClientX'
        PowerShellVersion    = '5.1'
        #FormatsToProcess     = @('DnsClientX.Format.ps1xml')
    }
    New-ConfigurationManifest @Manifest


    $ConfigurationFormat = [ordered] @{
        RemoveComments                              = $false

        PlaceOpenBraceEnable                        = $true
        PlaceOpenBraceOnSameLine                    = $true
        PlaceOpenBraceNewLineAfter                  = $true
        PlaceOpenBraceIgnoreOneLineBlock            = $false

        PlaceCloseBraceEnable                       = $true
        PlaceCloseBraceNewLineAfter                 = $false
        PlaceCloseBraceIgnoreOneLineBlock           = $false
        PlaceCloseBraceNoEmptyLineBefore            = $true

        UseConsistentIndentationEnable              = $true
        UseConsistentIndentationKind                = 'space'
        UseConsistentIndentationPipelineIndentation = 'IncreaseIndentationAfterEveryPipeline'
        UseConsistentIndentationIndentationSize     = 4

        UseConsistentWhitespaceEnable               = $true
        UseConsistentWhitespaceCheckInnerBrace      = $true
        UseConsistentWhitespaceCheckOpenBrace       = $true
        UseConsistentWhitespaceCheckOpenParen       = $true
        UseConsistentWhitespaceCheckOperator        = $true
        UseConsistentWhitespaceCheckPipe            = $true
        UseConsistentWhitespaceCheckSeparator       = $true

        AlignAssignmentStatementEnable              = $true
        AlignAssignmentStatementCheckHashtable      = $true

        UseCorrectCasingEnable                      = $true
    }
    # format PSD1 and PSM1 files when merging into a single file
    # enable formatting is not required as Configuration is provided
    New-ConfigurationFormat -ApplyTo 'OnMergePSM1', 'OnMergePSD1' -Sort None @ConfigurationFormat
    # format PSD1 and PSM1 files within the module
    # enable formatting is required to make sure that formatting is applied (with default settings)
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'DefaultPSM1' -EnableFormatting -Sort None
    # when creating PSD1 use special style without comments and with only required parameters
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'OnMergePSD1' -PSD1Style 'Minimal'

    # configuration for documentation, at the same time it enables documentation processing
    New-ConfigurationDocumentation -Enable -PathReadme 'Docs\Readme.md' -Path 'Docs' -SyncExternalHelpToProjectRoot

    New-ConfigurationImportModule -ImportSelf -ImportRequiredModules

    $newConfigurationBuildSplat = @{
        Enable                            = $true
        SignModule                        = $SignModule
        MergeModuleOnBuild                = $true
        MergeFunctionsFromApprovedModules = $true
        CertificateThumbprint             = '92E95FB58EFFA6A4A75E77A33CDD6BFE6DD30F1A'
        NETProjectPath                    = '..\DnsClientX.PowerShell\DnsClientX.PowerShell.csproj'
        ResolveBinaryConflicts            = $true
        ResolveBinaryConflictsName        = 'DnsClientX.PowerShell'
        NETProjectName                    = 'DnsClientX.PowerShell'
        NETBinaryModule                   = 'DnsClientX.PowerShell.dll'
        NETConfiguration                  = 'Release'
        NETFramework                      = 'net472', 'net8.0'
        DotSourceLibraries                = $true
        NETSearchClass                    = 'DnsClientX.PowerShell.CmdletResolveDnsQuery'
        NETBinaryModuleDocumentation      = $true
        SyncNETProjectVersion             = $true
    }

    New-ConfigurationBuild @newConfigurationBuildSplat

    $GitHubTokenPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GitHubApiKeyPath)
    New-ConfigurationProjectBuild -Name 'DnsClientX' -ConfigPath $ProjectBuildConfigPath -Enabled -BuildBeforeModule -UseAsReleaseVersionSource -ProvideLocalNuGetFeed -PublishNuget -PublishGitHub -Options @{ GitHubAccessTokenFilePath = $GitHubTokenPath }
    New-ConfigurationRelease -StageRoot '..\Artefacts\UploadReady' -VersionSource ProjectBuild -PrimaryProject 'DnsClientX' -SynchronizeModuleVersion -BuildOrder 'Packages', 'Module' -PublishOrder 'NuGet', 'PowerShellGallery', 'GitHub'

    New-ConfigurationArtefact -Type Unpacked -Enable -Path '..\Artefacts\Unpacked' -ModulesPath 'Modules'
    New-ConfigurationArtefact -Type Packed -Enable -Path '..\Artefacts\Packed' -IncludeTagName -ArtefactName 'DnsClientX-PowerShellModule.<TagModuleVersionWithPreRelease>.zip' -ID 'ToGitHub'

    New-ConfigurationPublish -Type PowerShellGallery -FilePath $PowerShellGalleryApiKeyPath -Enabled:$false
    New-ConfigurationPublish -Type GitHub -FilePath $GitHubTokenPath -UserName 'EvotecIT' -RepositoryName 'DnsClientX' -Enabled:$false -ID 'ToGitHub' -GenerateReleaseNotes -OverwriteTagName 'DnsClientX-v{ModuleVersionWithPreRelease}'
} -ExitCode
