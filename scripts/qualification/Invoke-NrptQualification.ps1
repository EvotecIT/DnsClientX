[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string] $AssemblyPath,
    [Parameter(Mandatory)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'NRPT qualification may run only on a disposable GitHub-hosted Windows runner.'
}
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$responder = $null
$rule = $null
$ruleRemoved = $false
try {
    $responder = Start-Process -FilePath 'python' -ArgumentList @('-u', (Join-Path $PSScriptRoot 'nrpt-responder.py')) -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'responder.log') -RedirectStandardError (Join-Path $OutputDirectory 'responder.stderr')
    $ready = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if ($responder.HasExited) {
            throw 'NRPT responder exited before becoming ready.'
        }
        if ((Get-Content -LiteralPath (Join-Path $OutputDirectory 'responder.log') -ErrorAction SilentlyContinue) -contains 'ready') {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 100
    }
    if (-not $ready) {
        throw 'NRPT responder did not bind within five seconds.'
    }
    $rule = Add-DnsClientNrptRule -Namespace '.qualification.invalid' -NameServers '127.0.0.1' -DisplayName 'Disposable DNS qualification' -PassThru
    Get-DnsClientNrptPolicy -Effective | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'effective-policy.json') -Encoding UTF8
    dotnet $AssemblyPath --nrpt | Set-Content -LiteralPath (Join-Path $OutputDirectory 'nrpt-result.json') -Encoding UTF8
    if ($LASTEXITCODE -ne 0) {
        throw 'Native NRPT routing contract was not observed; inspect retained evidence.'
    }
} finally {
    try {
        if ($null -ne $rule) {
            Remove-DnsClientNrptRule -Name $rule.Name -Force
            if ($null -ne (Get-DnsClientNrptRule -Name $rule.Name -ErrorAction SilentlyContinue)) {
                throw 'Task NRPT rule remains after cleanup.'
            }
        }
        $ruleRemoved = $true
    } finally {
        if ($null -ne $responder -and -not $responder.HasExited) {
            Stop-Process -Id $responder.Id
            $responder.WaitForExit()
        }
        [pscustomobject]@{
            TaskRuleRemoved = $ruleRemoved
            TaskResponderExited = ($null -eq $responder -or $responder.HasExited)
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'cleanup.json') -Encoding UTF8
    }
}
