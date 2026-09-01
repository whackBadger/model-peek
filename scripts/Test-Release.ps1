[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ExecutablePath,

    [string] $TestAssetDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'test-assets')
)

$ErrorActionPreference = 'Stop'
$ExecutablePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ExecutablePath)
$TestAssetDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($TestAssetDirectory)

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Release executable was not found: $ExecutablePath"
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath)
if ($versionInfo.ProductName -ne 'Model Peek') {
    throw "Unexpected product metadata: $($versionInfo.ProductName)"
}

if ($versionInfo.CompanyName -ne 'Model Peek contributors') {
    throw 'The executable contains unexpected company metadata.'
}

$binary = [System.IO.File]::ReadAllBytes($ExecutablePath)
$binaryViews = @(
    [System.Text.Encoding]::UTF8.GetString($binary),
    [System.Text.Encoding]::Unicode.GetString($binary)
)
if ($binaryViews | Where-Object { $_ -match '(?i)[a-z]:\\users\\[^\\/\r\n\x00]+' } | Select-Object -First 1) {
    throw 'The executable contains a Windows user-profile path.'
}

$cases = @(
    @{ File = 'cube-ascii.stl'; Expected = 0 },
    @{ File = 'triangle-binary.stl'; Expected = 0 },
    @{ File = 'cube sample with spaces.stl'; Expected = 0 },
    @{ File = 'malformed.stl'; Expected = 2 }
)

foreach ($case in $cases) {
    $assetPath = Join-Path $TestAssetDirectory $case.File
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "Test asset was not found: $assetPath"
    }

    $process = Start-Process `
        -FilePath $ExecutablePath `
        -ArgumentList @('--self-test', ('"{0}"' -f $assetPath)) `
        -Wait `
        -PassThru `
        -WindowStyle Hidden

    if ($process.ExitCode -ne $case.Expected) {
        throw "Self-test failed for $($case.File): expected $($case.Expected), received $($process.ExitCode)."
    }

    Write-Output "PASS $($case.File) (exit $($process.ExitCode))"
}

$hash = Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256
$signature = Get-AuthenticodeSignature -LiteralPath $ExecutablePath
Write-Output "SHA256 $($hash.Hash)"
Write-Output "Authenticode $($signature.Status)"
