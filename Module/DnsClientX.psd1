﻿@{
    AliasesToExport      = @('Resolve-DnsQuery')
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @('Resolve-Dns','Get-DnsService')
    CompanyName          = 'Evotec'
    CompatiblePSEditions = @('Desktop', 'Core')
    Copyright            = '(c) 2011 - 2025 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description          = 'DnsClientX is PowerShell module that allows you to query DNS servers for information. It supports DNS over UDP, TCP and DNS over HTTPS (DoH) and DNS over TLS (DoT). It supports multiple types of DNS queries and can be used to query public DNS servers, private DNS servers and has built-in DNS Providers.'
    FunctionsToExport    = @()
    GUID                 = '77fa806c-70b7-48d9-8b88-942ed73f24ed'
    ModuleVersion        = '0.4.0'
    PowerShellVersion    = '5.1'
    PrivateData          = @{
        PSData = @{
            IconUri         = 'https://raw.githubusercontent.com/EvotecIT/DnsClientX/master/Assets/Icons/DnsClientX3_128x128.png'
            ProjectUri      = 'https://github.com/EvotecIT/DnsClientX'
            LicenseUri      = 'https://github.com/EvotecIT/DnsClientX/blob/master/LICENSE'
            RequireLicenseAcceptance = $false
            Tags            = @('Windows', 'MacOS', 'Linux', 'DNS', 'DoH', 'DoT', 'NetworkTools')
            ReleaseNotes    = 'See https://github.com/EvotecIT/DnsClientX/blob/master/README.md for complete documentation and release notes.'
        }
    }
    HelpInfoURI          = 'https://github.com/EvotecIT/DnsClientX/blob/master/README.md'
    RootModule           = 'DnsClientX.psm1'
}