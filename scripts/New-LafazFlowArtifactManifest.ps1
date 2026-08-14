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

    [string]$WorkerRevision = "",

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
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($path)
        try {
            $hash = $sha256.ComputeHash($stream)
            return [System.BitConverter]::ToString($hash).Replace("-", "").ToLowerInvariant()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $sha256.Dispose()
    }
}

function Test-Hex40([string]$value) {
    return $value -match "^[0-9a-fA-F]{40}$"
}

# CLI provenance is fail-closed: a Local CUDA binary is never assigned an
# unverified revision. The revision must be supplied explicitly by the packager
# as documented package/build provenance and is bound to the binary's SHA-256.
$effectiveCliRevision = $null
$cliRevisionEvidence = $null
if ($CliSource -eq "LocalCuda") {
    if (-not (Test-Hex40 $CliRevision)) {
        throw "LocalCuda packaging requires a full 40-character hexadecimal -CliRevision. Refusing to guess provenance for the selected CLI."
    }
    $effectiveCliRevision = $CliRevision.ToLowerInvariant()
    $cliRevisionEvidence = "explicit package/build provenance"
}
else {
    # Official CPU binaries come from the GitHub release; no source revision is
    # invented unless the release itself provides trustworthy evidence.
    $effectiveCliRevision = $null
    $cliRevisionEvidence = $null
}

$workerReported = $null
$effectiveWorkerRevision = $null
if ($WorkerPath -and (Test-Path -LiteralPath $WorkerPath)) {
    if (-not (Test-Hex40 $WorkerRevision)) {
        throw "Packaging a worker requires a full 40-character hexadecimal -WorkerRevision. Refusing to guess provenance for the selected worker."
    }
    try {
        $versionOut = & $WorkerPath --version 2>$null | Out-String
        if ($versionOut -match "whisper=([0-9a-f]{7,40})") {
            $workerReported = $Matches[1].ToLowerInvariant()
        }
    }
    catch {
        $workerReported = $null
    }
    if (-not $workerReported) {
        throw "The selected worker did not report a whisper revision via --version; cannot corroborate the supplied -WorkerRevision."
    }
    if (-not $WorkerRevision.StartsWith($workerReported, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The supplied worker revision $WorkerRevision does not begin with the revision the binary reported ($workerReported)."
    }
    $effectiveWorkerRevision = $WorkerRevision.ToLowerInvariant()
}
elseif ($WorkerPath) {
    throw "The selected worker path was not found: $WorkerPath"
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
        revision_evidence_type = $cliRevisionEvidence
        release_identity = $(if ($CliReleaseIdentity) { $CliReleaseIdentity } else { $null })
        sha256 = (Get-Sha256 $CliPath)
    }
}

$json = $manifest | ConvertTo-Json -Depth 6
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutputPath, $json, $utf8NoBom)

Write-Output "Artifact manifest written: $OutputPath"
