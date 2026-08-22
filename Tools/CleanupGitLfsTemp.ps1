param()

$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$lfsRoot = Join-Path $workspaceRoot '.git\lfs'
$lfsTemp = Join-Path $lfsRoot 'tmp'

if (-not (Test-Path -LiteralPath $lfsTemp)) {
    exit 0
}

$resolvedTemp = (Resolve-Path -LiteralPath $lfsTemp).Path
$expectedPrefix = (Resolve-Path -LiteralPath $lfsRoot).Path + '\'
if (-not $resolvedTemp.StartsWith(
        $expectedPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean unexpected path: $resolvedTemp"
}

if (Get-Process git-lfs -ErrorAction SilentlyContinue) {
    Write-Host 'Git LFS is active; temporary-file cleanup was skipped.'
    exit 0
}

Get-ChildItem -LiteralPath $resolvedTemp -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force
Write-Host "Git LFS temporary files cleaned: $resolvedTemp"
