[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "",
    [string]$WhisperCliLocalPath = "",
    [string]$InnoSetupCompilerPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if (-not $Version)
{
    $csproj = Get-Content -Raw (Join-Path $repoRoot "src\LafazFlow.Windows\LafazFlow.Windows.csproj")
    if ($csproj -match "<Version>([^<]+)</Version>")
    {
        $Version = $matches[1].Trim()
    }
    else
    {
        throw "Could not read <Version> from LafazFlow.Windows.csproj."
    }
}

if (-not $OutputRoot)
{
    $OutputRoot = Join-Path $repoRoot "artifacts\release"
}

$staging = Join-Path $OutputRoot "_staging"
$appDir = Join-Path $staging "LafazFlow"
$portableZip = Join-Path $OutputRoot "LafazFlow-$Version-$Runtime-portable.zip"

if (Test-Path $staging)
{
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $appDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

Write-Host "Publishing LafazFlow $Version ($Configuration, $Runtime, self-contained)..."
dotnet publish (Join-Path $repoRoot "src\LafazFlow.Windows\LafazFlow.Windows.csproj") `
    -c $Configuration -r $Runtime --self-contained true -o $appDir | Out-Null
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if ($WhisperCliLocalPath -and (Test-Path $WhisperCliLocalPath))
{
    Write-Host "Using local whisper-cli: $WhisperCliLocalPath"
    Copy-Item -LiteralPath $WhisperCliLocalPath -Destination $appDir
}
else
{
    Write-Host "Downloading latest whisper.cpp Windows binary release..."
    $latest = Invoke-RestMethod `
        -Uri "https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest" `
        -Headers @{ "User-Agent" = "LafazFlow-release-packager" }
    $asset = $latest.assets | Where-Object { $_.name -eq "whisper-bin-x64.zip" } | Select-Object -First 1
    if (-not $asset)
    {
        throw "whisper-bin-x64.zip was not found in the latest whisper.cpp release."
    }

    $zipPath = Join-Path $env:TEMP "whisper-bin-x64.zip"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -UseBasicParsing
    $expandDir = Join-Path $env:TEMP ("whisper-bin-" + [guid]::NewGuid().ToString("N"))
    Expand-Archive -LiteralPath $zipPath -DestinationPath $expandDir
    Get-ChildItem -Path $expandDir -Recurse -File -Include "whisper-cli.exe", "*.dll" |
        Copy-Item -Destination $appDir
    Remove-Item -LiteralPath $expandDir -Recurse -Force
}

$bundledCli = Join-Path $appDir "whisper-cli.exe"
if (-not (Test-Path $bundledCli))
{
    throw "whisper-cli.exe is missing from the package."
}

Write-Host "Smoke-checking bundled whisper-cli.exe..."
$smokeToken = [guid]::NewGuid().ToString("N")
$smokeOut = Join-Path $env:TEMP "whisper-smoke-$smokeToken-out.txt"
$smokeErr = Join-Path $env:TEMP "whisper-smoke-$smokeToken-err.txt"
$smokeProcess = Start-Process `
    -FilePath $bundledCli `
    -ArgumentList "--help" `
    -WindowStyle Hidden `
    -Wait `
    -PassThru `
    -RedirectStandardOutput $smokeOut `
    -RedirectStandardError $smokeErr
Remove-Item -LiteralPath $smokeOut, $smokeErr -Force -ErrorAction SilentlyContinue
if ($smokeProcess.ExitCode -ne 0)
{
    throw "Bundled whisper-cli.exe failed its --help smoke check (exit code $($smokeProcess.ExitCode))."
}

Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $appDir
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $appDir
Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md") -Destination $appDir
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\windows-runtime-setup.md") -Destination $appDir

Write-Host "Running release safety checks..."
$violations = @()
Get-ChildItem -Path $appDir -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($appDir.Length)
    if (($_.Extension -in @(".wav", ".mp3")) -and $relative -notlike "*\Resources\Sounds\*")
    {
        $violations += "user audio file: $relative"
    }
    if ($_.Extension -eq ".log")
    {
        $violations += "log file: $relative"
    }
    if ($_.Name -in @("settings.json", "local-settings.json"))
    {
        $violations += "settings file: $relative"
    }
    if ($_.Extension -eq ".bin")
    {
        $violations += "model binary: $relative"
    }
}

$textExtensions = @(".md", ".txt", ".json", ".xml", ".config", ".cs", ".ps1", ".yml", ".yaml", ".iss")
Get-ChildItem -Path $appDir -Recurse -File |
    Where-Object { $textExtensions -contains $_.Extension.ToLowerInvariant() } |
    ForEach-Object {
        $content = Get-Content -Raw $_.FullName
        if ($content -match "(?i)(sk-[A-Za-z0-9]{20,}|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|BEGIN (RSA|OPENSSH|PRIVATE) KEY|api[_-]?key\s*[:=]\s*['\""][^'\""]{8,}['\""])")
        {
            $violations += "credential pattern: $($_.FullName.Substring($appDir.Length))"
        }
    }

if ($violations.Count -gt 0)
{
    throw "Release safety check failed:`n" + ($violations -join "`n")
}

Write-Host "Creating portable ZIP..."
Compress-Archive -Path $appDir -DestinationPath $portableZip -CompressionLevel Optimal

$installer = $null
if ($InnoSetupCompilerPath)
{
    if (-not (Test-Path $InnoSetupCompilerPath))
    {
        throw "Inno Setup compiler not found: $InnoSetupCompilerPath"
    }

    $installer = Join-Path $OutputRoot "LafazFlow-$Version-setup.exe"
    Write-Host "Building installer with Inno Setup..."
    & $InnoSetupCompilerPath `
        "/dMyAppVersion=$Version" `
        "/dMyAppSource=$appDir" `
        "/dMyOutputDir=$OutputRoot" `
        "/dMyOutputFile=LafazFlow-$Version-setup" `
        (Join-Path $PSScriptRoot "lafazflow-setup.iss")
    if ($LASTEXITCODE -ne 0)
    {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
    }
}
else
{
    Write-Host "Inno Setup compiler was not provided; portable ZIP only. Pass -InnoSetupCompilerPath to build the installer."
}

Write-Host ""
Write-Host "Package ready: $portableZip"
if ($installer)
{
    Write-Host "Installer ready: $installer"
}
