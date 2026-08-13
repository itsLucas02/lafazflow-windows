param(
    [ValidateSet("Cuda", "Cpu")]
    [string]$Backend = "Cuda",
    [string]$SourceDirectory = "C:\Tools\whisper.cpp-pinned-968eebe7",
    [string]$BuildDirectory = "",
    [string]$InstallDirectory = "C:\Tools\lafazflow-whisper-worker"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$workerSource = Join-Path $repoRoot "native\LafazFlow.WhisperWorker"

if (-not (Test-Path (Join-Path $SourceDirectory "CMakeLists.txt"))) {
    throw "Pinned whisper.cpp source not found at $SourceDirectory. Clone and checkout 968eebe7 first."
}

if (-not $BuildDirectory) {
    $BuildDirectory = Join-Path $SourceDirectory "build-worker-$($Backend.ToLowerInvariant())"
}

if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    $env:Path = "C:\Program Files\CMake\bin;$env:Path"
}

if (-not (Get-Command ninja -ErrorAction SilentlyContinue)) {
    $ninja = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter ninja.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($ninja) {
        $env:Path = "$($ninja.DirectoryName);$env:Path"
    }
}

if ($Backend -eq "Cuda") {
    if (-not (Get-Command nvcc -ErrorAction SilentlyContinue)) {
        $env:Path = "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v13.2\bin;$env:Path"
    }
    if (-not (Get-Command nvcc -ErrorAction SilentlyContinue)) {
        throw "CUDA Toolkit is required for the CUDA worker build."
    }
}

$vcvarsPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path -LiteralPath $vcvarsPath)) {
    $vcvarsPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
}
if (-not (Test-Path -LiteralPath $vcvarsPath)) {
    throw "MSVC vcvars64.bat was not found."
}

$cmake = (Get-Command cmake).Source
$ninjaDirectory = Split-Path -Parent (Get-Command ninja).Source
$escapedSource = $workerSource.Replace('"', '\"')
$escapedBuild = $BuildDirectory.Replace('"', '\"')
$escapedInstall = $InstallDirectory.Replace('"', '\"')
$escapedWhisper = $SourceDirectory.Replace('"', '\"')
$escapedCmake = $cmake.Replace('"', '\"')

$configureCommand = "`"$escapedCmake`" -S `"$escapedSource`" -B `"$escapedBuild`" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=`"$escapedInstall`" -DWHISPER_SOURCE_DIR=`"$escapedWhisper`""
if ($Backend -eq "Cuda") {
    $configureCommand += " -DGGML_CUDA=ON -DCMAKE_CUDA_COMPILER=`"$((Get-Command nvcc).Source)`""
}

$cmd = "call `"$vcvarsPath`" && set `"PATH=$ninjaDirectory;!PATH!`" && $configureCommand && `"$escapedCmake`" --build `"$escapedBuild`" --config Release --parallel && `"$escapedCmake`" --install `"$escapedBuild`" --config Release"

cmd.exe /v:on /s /c $cmd
if ($LASTEXITCODE -ne 0) {
    throw "Whisper worker build failed with exit code $LASTEXITCODE."
}

$installBin = Join-Path $InstallDirectory "bin"
if (-not (Test-Path -LiteralPath $installBin)) {
    New-Item -ItemType Directory -Force -Path $installBin | Out-Null
}
$builtWorker = Join-Path $BuildDirectory "lafazflow-whisper-worker.exe"
if (-not (Test-Path -LiteralPath $builtWorker)) {
    throw "Build completed but lafazflow-whisper-worker.exe was not found in the build directory."
}
$workerPath = Join-Path $installBin "lafazflow-whisper-worker.exe"
Copy-Item -LiteralPath $builtWorker -Destination $workerPath -Force
if (-not (Test-Path -LiteralPath $workerPath)) {
    throw "Build completed but lafazflow-whisper-worker.exe was not found."
}

if ($Backend -eq "Cuda") {
    $vcToolsRoot = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $vcvarsPath))) "Redist\MSVC"
    $vcRuntimeDirectory = Get-ChildItem $vcToolsRoot -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "x64\Microsoft.VC143.CRT" } |
        Where-Object { Test-Path -LiteralPath (Join-Path $_ "msvcp140.dll") }
    $vcRuntimeDirectory = $vcRuntimeDirectory | Select-Object -First 1
    if (-not $vcRuntimeDirectory) {
        throw "The matching x64 VC143 runtime was not found."
    }

    $workerDirectory = Split-Path -Parent $workerPath
    Get-ChildItem $vcRuntimeDirectory -Filter "*.dll" | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $workerDirectory $_.Name) -Force
    }
}

$smokeOut = Join-Path $env:TEMP "worker-smoke-out.txt"
$smokeErr = Join-Path $env:TEMP "worker-smoke-err.txt"
$smoke = Start-Process -FilePath $workerPath -ArgumentList "--version" -WindowStyle Hidden -Wait -PassThru `
    -RedirectStandardOutput $smokeOut -RedirectStandardError $smokeErr
if ($smoke.ExitCode -ne 0) {
    throw "Whisper worker smoke check failed with exit code $($smoke.ExitCode)."
}

Write-Host "Whisper worker is ready: $workerPath (backend $Backend)"
