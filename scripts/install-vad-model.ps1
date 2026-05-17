param(
    [string]$ModelDirectory = "C:\Models\whisper",
    [string]$ModelUrl = "https://huggingface.co/ggml-org/whisper-vad/resolve/main/ggml-silero-v5.1.2.bin"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $ModelDirectory | Out-Null

$modelPath = Join-Path $ModelDirectory "ggml-silero-v5.1.2.bin"
if (Test-Path -LiteralPath $modelPath) {
    Write-Host "VAD model already exists: $modelPath"
    exit 0
}

Write-Host "Downloading ggml-silero-v5.1.2.bin to $modelPath"
Write-Host "This file is used locally for voice activity detection."
Invoke-WebRequest -Uri $ModelUrl -OutFile $modelPath
Write-Host "Done: $modelPath"
