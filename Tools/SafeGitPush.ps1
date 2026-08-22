param(
    [string]$Remote = 'origin',
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
if ($Remote.StartsWith('-') -or $Branch.StartsWith('-')) {
    throw 'Remote and branch names must not start with a hyphen.'
}

$cleanupScript = Join-Path $PSScriptRoot 'CleanupGitLfsTemp.ps1'
$pushExitCode = 1

& $cleanupScript
try {
    & git push $Remote $Branch
    $pushExitCode = $LASTEXITCODE
}
finally {
    & $cleanupScript
}

exit $pushExitCode
