$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
Invoke-Pester -Script "$here/Tests" -CI

