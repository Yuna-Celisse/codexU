param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot "Sources\CodexUsageWidget.Windows\Program.cs"
$dist = Join-Path $repoRoot "dist"
$output = Join-Path $dist "codexU-win.exe"

$windowsRoot = if ($env:WINDIR) { $env:WINDIR } elseif ($env:SystemRoot) { $env:SystemRoot } else { "C:\Windows" }
$cscCandidates = @(
    (Join-Path $windowsRoot "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $windowsRoot "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "Cannot find .NET Framework csc.exe. Install .NET Framework Developer Pack or use a machine with Windows compiler tools."
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $csc `
    /nologo `
    /target:winexe `
    /optimize+ `
    /codepage:65001 `
    /utf8output `
    /out:$output `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Windows build failed with exit code $LASTEXITCODE."
}

Write-Host "Built $output"
