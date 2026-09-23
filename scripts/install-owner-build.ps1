[CmdletBinding()]
param(
    [string]$InstallDirectory = "$env:LOCALAPPDATA\Programs\LafazFlow",
    [string]$CudaDirectory = "C:\Tools\whisper.cpp-cuda\bin",
    [string]$WorkerDirectory = "C:\Tools\lafazflow-whisper-worker\bin"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$stage = Join-Path $repoRoot "artifacts\owner-install\LafazFlow"
$expectedCommit = (& git -C $repoRoot rev-parse HEAD).Trim()

Get-Process LafazFlow.Windows, lafazflow-whisper-worker, whisper-cli -ErrorAction SilentlyContinue |
    Stop-Process -Force

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

dotnet publish (Join-Path $repoRoot "src\LafazFlow.Windows\LafazFlow.Windows.csproj") `
    -c Release -r win-x64 --self-contained true -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

$cudaCli = Join-Path $CudaDirectory "whisper-cli.exe"
$worker = Join-Path $WorkerDirectory "lafazflow-whisper-worker.exe"
if (-not (Test-Path -LiteralPath $cudaCli)) { throw "CUDA whisper CLI not found: $cudaCli" }
if (-not (Test-Path -LiteralPath $worker)) { throw "CUDA worker not found: $worker" }

Copy-Item -LiteralPath $cudaCli -Destination $stage -Force
Get-ChildItem -LiteralPath $WorkerDirectory -File |
    Where-Object { $_.Extension -eq ".dll" -or $_.Name -eq "lafazflow-whisper-worker.exe" } |
    Copy-Item -Destination $stage -Force

New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null
Copy-Item -Path (Join-Path $stage "*") -Destination $InstallDirectory -Recurse -Force

$installedExe = Join-Path $InstallDirectory "LafazFlow.Windows.exe"
$installedVersion = (Get-Item -LiteralPath $installedExe).VersionInfo.ProductVersion
if ($installedVersion -notlike "*+$expectedCommit") {
    throw "Installed build is stale: expected commit $expectedCommit, found $installedVersion."
}

$taskbarShortcut = Join-Path $env:APPDATA "Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\LafazFlow.lnk"
if (Test-Path -LiteralPath $taskbarShortcut) {
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($taskbarShortcut)
    if (-not [string]::Equals($shortcut.TargetPath, $installedExe, [StringComparison]::OrdinalIgnoreCase)) {
        $shortcut.TargetPath = $installedExe
        $shortcut.WorkingDirectory = $InstallDirectory
        $shortcut.Save()
    }
}

Start-Process -FilePath $installedExe -WindowStyle Hidden
Start-Sleep -Milliseconds 750
$runningPath = (Get-Process LafazFlow.Windows -ErrorAction Stop | Select-Object -First 1).Path
if (-not [string]::Equals($runningPath, $installedExe, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Wrong LafazFlow build is running: $runningPath"
}

Write-Host "Installed and launched LafazFlow $installedVersion from $installedExe"
