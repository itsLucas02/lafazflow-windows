[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AppPath,

    [string]$WorkerPath = "",

    [Parameter(Mandatory = $true)]
    [string]$CliPath,

    [Parameter(Mandatory = $true)]
    [ValidateSet("LocalCuda", "OfficialCpu")]
    [string]$CliSource,

    [string]$CliRevision = "",

    [string]$CliReleaseIdentity = "",

    [string]$WorkerRevision = "968eebe77225d25e57a3f981da7c696310f0e881",

    [string]$Version = "",

    [string]$Runtime = "win-x64",

    [string]$Configuration = "Release",

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

function Get-Sha256([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "File not found for hashing: $path"
    }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
}

$workerReported = $null
if ($WorkerPath -and (Test-Path -LiteralPath $WorkerPath)) {
    try {
        $versionOut = & $WorkerPath --version 2>$null | Out-String
        if ($versionOut -match "whisper=([0-9a-f]{7,40})") {
            $workerReported = $Matches[1].ToLowerInvariant()
        }
    }
    catch {
        $workerReported = $null
    }
}

# Keep the documented full revision only when the binary's own report matches
# it as a prefix; otherwise record what the binary actually reports. A revision
# is never invented for a binary that cannot confirm it.
$effectiveWorkerRevision = $WorkerRevision
if ($workerReported) {
    if ($WorkerRevision -and -not $WorkerRevision.StartsWith($workerReported, [System.StringComparison]::OrdinalIgnoreCase)) {
        $effectiveWorkerRevision = $workerReported
    }
}
elseif (-not $effectiveWorkerRevision) {
    $effectiveWorkerRevision = $null
}

$effectiveCliRevision = $CliRevision
if ($CliSource -eq "OfficialCpu") {
    $effectiveCliRevision = $null
}

$manifest = [ordered]@{
    schema_version = 1
    package = [ordered]@{
        name = "LafazFlow"
        version = $Version
        runtime = $Runtime
        configuration = $Configuration
        generated_at_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
    app = [ordered]@{
        file = (Split-Path -Leaf $AppPath)
        sha256 = (Get-Sha256 $AppPath)
    }
    worker = if ($WorkerPath -and (Test-Path -LiteralPath $WorkerPath)) {
        [ordered]@{
            file = (Split-Path -Leaf $WorkerPath)
            revision = $effectiveWorkerRevision
            reported_version = $workerReported
            sha256 = (Get-Sha256 $WorkerPath)
        }
    }
    else {
        $null
    }
    cli = [ordered]@{
        file = (Split-Path -Leaf $CliPath)
        source = $CliSource
        revision = $effectiveCliRevision
        release_identity = $(if ($CliReleaseIdentity) { $CliReleaseIdentity } else { $null })
        sha256 = (Get-Sha256 $CliPath)
    }
}

$json = $manifest | ConvertTo-Json -Depth 6
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutputPath, $json, $utf8NoBom)

Write-Output "Artifact manifest written: $OutputPath"
