param(
    [string]$CompilerPath
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot "docs\help"
$sourceProjectFile = Join-Path $sourceRoot "JiraCloneHelp.hhp"
$targetDirectory = Join-Path $repoRoot "src\JiraClone.WinForms\Help"
$targetFile = Join-Path $targetDirectory "JiraClone.chm"
$stagingRoot = Join-Path $env:TEMP "JiraCloneHelpBuild"
$stagingSourceRoot = Join-Path $stagingRoot "source"
$projectFile = Join-Path $stagingSourceRoot "JiraCloneHelp.hhp"
$intermediateOutput = Join-Path $stagingSourceRoot "JiraClone.chm"

$compilerCandidates = @(
    $CompilerPath,
    "C:\Program Files (x86)\HTML Help Workshop\hhc.exe",
    "C:\Program Files\HTML Help Workshop\hhc.exe"
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$resolvedCompiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $resolvedCompiler)
{
    throw "Cannot find hhc.exe. Install HTML Help Workshop or pass the compiler path by using -CompilerPath."
}

if (-not (Test-Path $sourceProjectFile))
{
    throw "Cannot find the CHM project file in source folder: ${sourceRoot}"
}

New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stagingSourceRoot | Out-Null
Copy-Item -Path (Join-Path $sourceRoot "*") -Destination $stagingSourceRoot -Recurse -Force
Remove-Item -LiteralPath $intermediateOutput -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $targetFile -ErrorAction SilentlyContinue

Push-Location $stagingSourceRoot
try
{
    $compilerOutput = & $resolvedCompiler $projectFile 2>&1
    $compilerOutput | Out-Host
}
finally
{
    Pop-Location
}

if (-not (Test-Path $intermediateOutput))
{
    throw "CHM compilation did not produce the expected output file."
}

$blockingCompilerErrors = @($compilerOutput | Where-Object { $_ -match "HHC\d{4}: Error:" -and $_ -notmatch "HHC6003" })
if ($blockingCompilerErrors.Count -gt 0)
{
    throw "CHM compilation reported blocking compiler errors."
}

$validationRoot = Join-Path $stagingRoot "validation"
Remove-Item -LiteralPath $validationRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
& "$env:WINDIR\hh.exe" -decompile $validationRoot $intermediateOutput | Out-Null
Start-Sleep -Seconds 2

if (-not (Test-Path (Join-Path $validationRoot "topics\index.html")))
{
    throw "CHM validation failed after compilation."
}

Copy-Item -LiteralPath $intermediateOutput -Destination $targetFile -Force
Remove-Item -LiteralPath $intermediateOutput -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "CHM created at: $targetFile"
