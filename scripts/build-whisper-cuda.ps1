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
    throw "CMake is required. Install it first, then reopen PowerShell."
}

if (-not (Get-Command nvcc -ErrorAction SilentlyContinue)) {
    throw "CUDA Toolkit is required. Install CUDA Toolkit, then reopen PowerShell."
}

if (-not (Test-Path -LiteralPath $SourceDirectory)) {
    git clone $RepositoryUrl $SourceDirectory
} else {
    git -C $SourceDirectory pull --ff-only
}

$buildDirectory = Join-Path $SourceDirectory "build-cuda"
cmake -S $SourceDirectory -B $buildDirectory -DGGML_CUDA=ON -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=$InstallDirectory
cmake --build $buildDirectory --config Release --parallel
cmake --install $buildDirectory --config Release

$cliPath = Join-Path $InstallDirectory "bin\whisper-cli.exe"
if (-not (Test-Path -LiteralPath $cliPath)) {
    $cliPath = Join-Path $InstallDirectory "whisper-cli.exe"
}

if (-not (Test-Path -LiteralPath $cliPath)) {
    throw "Build completed but whisper-cli.exe was not found under $InstallDirectory."
}

Write-Host "CUDA whisper-cli is ready: $cliPath"
