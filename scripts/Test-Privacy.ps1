[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git'))) {
    throw 'Privacy scanning requires an initialized git repository.'
}

$trackedFiles = @(git -C $RepositoryRoot ls-files)
if ($LASTEXITCODE -ne 0 -or $trackedFiles.Count -eq 0) {
    throw 'No tracked files were available for privacy scanning.'
}

$rules = [ordered]@{
    'Windows user-profile path' = '(?i)[a-z]:\\users\\[^\\/\r\n\x00]+'
    'Email address'             = '(?i)\b[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}\b'
    'Private key block'         = '-----BEGIN (?:[A-Z0-9 ]+ )?PRIVATE KEY-----'
    'GitHub access token'       = '(?i)\b(?:gh[pousr]_[a-z0-9_]{20,}|github_pat_[a-z0-9_]{20,})\b'
    'OpenAI API key'            = '(?i)\bsk-(?:proj-)?[a-z0-9_-]{20,}\b'
    'AWS access key'            = '\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'
    'Credential assignment'     = '(?i)\b(?:password|passwd|api[_-]?key|access[_-]?token)\s*[:=]\s*["'']?[^\s"'']{8,}'
}

$spacedUserName = if ($env:USERNAME) {
    $env:USERNAME -replace '([a-z])([A-Z])', '$1 $2'
}

$identityValues = @(
    $env:USERNAME,
    $spacedUserName,
    $env:USERPROFILE,
    (git -C $RepositoryRoot config user.name),
    (git -C $RepositoryRoot config user.email)
) | Where-Object {
    $_ -and
    $_.Length -ge 4 -and
    $_ -notmatch '(?i)^model peek contributors$' -and
    $_ -notmatch '@users\.noreply\.github\.com$'
} | Select-Object -Unique

$violations = [System.Collections.Generic.List[string]]::new()

foreach ($relativePath in $trackedFiles) {
    $fullPath = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    $textViews = @(
        [System.Text.Encoding]::UTF8.GetString($bytes),
        [System.Text.Encoding]::Unicode.GetString($bytes)
    )

    foreach ($rule in $rules.GetEnumerator()) {
        if ($textViews | Where-Object { $_ -match $rule.Value } | Select-Object -First 1) {
            $violations.Add("$relativePath [$($rule.Key)]")
        }
    }

    foreach ($identity in $identityValues) {
        if ($textViews | Where-Object { $_.IndexOf($identity, [StringComparison]::OrdinalIgnoreCase) -ge 0 } | Select-Object -First 1) {
            $violations.Add("$relativePath [local identity]")
        }
    }
}

if ($violations.Count -gt 0) {
    $summary = ($violations | Sort-Object -Unique) -join [Environment]::NewLine
    throw "Privacy scan failed. Review these tracked files without printing the matched values:`n$summary"
}

Write-Output "Privacy scan passed for $($trackedFiles.Count) tracked files."
