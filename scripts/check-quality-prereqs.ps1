param(
    [string]$CudaCliPath = "C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe",
    [string]$QualityModelPath = "C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
    [string]$VadModelPath = "C:\Models\whisper\ggml-silero-v5.1.2.bin"
)

$ErrorActionPreference = "Stop"

function Test-Command($Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        "OK      $Name -> $($command.Source)"
    } else {
        "MISSING $Name"
    }
}

"=== LafazFlow Quality Mode Prerequisites ==="
Test-Command "git"
Test-Command "cmake"
Test-Command "nvcc"
Test-Command "nvidia-smi"

$vsRoot = "C:\Program Files\Microsoft Visual Studio\2022"
if (Test-Path -LiteralPath $vsRoot) {
    "OK      Visual Studio 2022 -> $vsRoot"
} else {
    "MISSING Visual Studio 2022 Build Tools or Community"
}

if (Test-Path -LiteralPath $CudaCliPath) {
    "OK      CUDA whisper-cli -> $CudaCliPath"
} else {
    "MISSING CUDA whisper-cli -> $CudaCliPath"
}

if (Test-Path -LiteralPath $QualityModelPath) {
    "OK      Quality model -> $QualityModelPath"
} else {
    "MISSING Quality model -> $QualityModelPath"
}

if (Test-Path -LiteralPath $VadModelPath) {
    "OK      VAD model -> $VadModelPath"
} else {
    "MISSING VAD model -> $VadModelPath"
}

if (Get-Command nvidia-smi -ErrorAction SilentlyContinue) {
    "=== NVIDIA GPU ==="
    nvidia-smi --query-gpu=name,driver_version,memory.total --format=csv,noheader
}
