param(
    [string]$SourceDirectory = "C:\Tools\whisper.cpp-cuda-src",
    [string]$InstallDirectory = "C:\Tools\whisper.cpp-cuda",
    [string]$RepositoryUrl = "https://github.com/ggerganov/whisper.cpp.git"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git is required."
}

if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    $env:Path = "C:\Program Files\CMake\bin;$env:Path"
}

if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    throw "CMake is required. Install it first, then reopen PowerShell."
}

if (-not (Get-Command nvcc -ErrorAction SilentlyContinue)) {
    $env:Path = "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v13.2\bin;$env:Path"
}

if (-not (Get-Command nvcc -ErrorAction SilentlyContinue)) {
    throw "CUDA Toolkit is required. Install CUDA Toolkit, then reopen PowerShell."
}

$ninjaCommand = Get-Command ninja -ErrorAction SilentlyContinue
$ninjaPath = if ($ninjaCommand) { $ninjaCommand.Source } else { $null }
if (-not $ninjaPath) {
    $knownNinja = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter ninja.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($knownNinja) {
        $env:Path = "$($knownNinja.DirectoryName);$env:Path"
        $ninjaPath = $knownNinja.FullName
    }
}

if (-not (Get-Command ninja -ErrorAction SilentlyContinue)) {
    throw "Ninja is required for the CUDA build. Install Ninja, then rerun this script."
}

$vcvarsPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path -LiteralPath $vcvarsPath)) {
    $vcvarsPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
}

if (-not (Test-Path -LiteralPath $vcvarsPath)) {
    throw "MSVC vcvars64.bat was not found. Install Visual Studio 2022 C++ Build Tools."
}

if (-not (Test-Path -LiteralPath $SourceDirectory)) {
    git clone $RepositoryUrl $SourceDirectory
} else {
    git -C $SourceDirectory pull --ff-only
}

$buildDirectory = Join-Path $SourceDirectory "build-cuda-ninja"
$cudaCompiler = (Get-Command nvcc).Source
$cmakePath = (Get-Command cmake).Source
$ninjaDirectory = Split-Path -Parent (Get-Command ninja).Source
$cudaBin = Split-Path -Parent $cudaCompiler
$escapedSourceDirectory = $SourceDirectory.Replace('"', '\"')
$escapedBuildDirectory = $buildDirectory.Replace('"', '\"')
$escapedInstallDirectory = $InstallDirectory.Replace('"', '\"')
$escapedCudaCompiler = $cudaCompiler.Replace('"', '\"')
$escapedCmakePath = $cmakePath.Replace('"', '\"')
$escapedToolPath = "C:\Program Files\CMake\bin;$ninjaDirectory;$cudaBin;!PATH!".Replace('"', '\"')

$cmd = "call `"$vcvarsPath`" && set `"PATH=$escapedToolPath`" && `"$escapedCmakePath`" -S `"$escapedSourceDirectory`" -B `"$escapedBuildDirectory`" -G Ninja -DGGML_CUDA=ON -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=`"$escapedInstallDirectory`" -DCMAKE_CUDA_COMPILER=`"$escapedCudaCompiler`" && `"$escapedCmakePath`" --build `"$escapedBuildDirectory`" --config Release --parallel && `"$escapedCmakePath`" --install `"$escapedBuildDirectory`" --config Release"
cmd.exe /v:on /s /c $cmd
if ($LASTEXITCODE -ne 0) {
    throw "CUDA whisper.cpp build failed with exit code $LASTEXITCODE."
}

$cliPath = Join-Path $InstallDirectory "bin\whisper-cli.exe"
if (-not (Test-Path -LiteralPath $cliPath)) {
    $cliPath = Join-Path $InstallDirectory "whisper-cli.exe"
}

if (-not (Test-Path -LiteralPath $cliPath)) {
    throw "Build completed but whisper-cli.exe was not found under $InstallDirectory."
}

Write-Host "CUDA whisper-cli is ready: $cliPath"
