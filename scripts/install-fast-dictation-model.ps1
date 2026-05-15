param(
    [string]$ModelDirectory = "C:\Models\whisper",
    [string]$ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $ModelDirectory | Out-Null

$modelPath = Join-Path $ModelDirectory "ggml-large-v3-turbo-q5_0.bin"
if (Test-Path -LiteralPath $modelPath) {
    Write-Host "Model already exists: $modelPath"
    exit 0
}

Write-Host "Downloading ggml-large-v3-turbo-q5_0.bin to $modelPath"
Write-Host "This file is about 547 MiB and stays local on this machine."
Invoke-WebRequest -Uri $ModelUrl -OutFile $modelPath
Write-Host "Done: $modelPath"
