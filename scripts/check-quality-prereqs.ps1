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

function Test-CommandOrPath($Name, [string[]]$Paths) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        "OK      $Name -> $($command.Source)"
        return
    }

    foreach ($path in $Paths) {
        if (Test-Path -LiteralPath $path) {
            "OK      $Name -> $path"
            return
        }
    }

    "MISSING $Name"
}

"=== LafazFlow Quality Mode Prerequisites ==="
Test-Command "git"
Test-CommandOrPath "cmake" @(
    "C:\Program Files\CMake\bin\cmake.exe"
)
Test-CommandOrPath "nvcc" @(
    "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v13.2\bin\nvcc.exe",
    "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v13.1\bin\nvcc.exe",
    "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9\bin\nvcc.exe"
)
Test-Command "nvidia-smi"

$vsRoots = @(
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools",
    "C:\Program Files\Microsoft Visual Studio\2022\Community",
    "C:\Program Files\Microsoft Visual Studio\2022"
)
$vsRoot = $vsRoots | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($vsRoot) {
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

$cudaDll = Get-ChildItem "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA" -Recurse -Filter "cublas64_*.dll" -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($cudaDll) {
    "OK      CUDA runtime DLLs -> $($cudaDll.DirectoryName)"
} else {
    "MISSING CUDA runtime DLLs"
}
