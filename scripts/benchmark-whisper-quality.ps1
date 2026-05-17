param(
    [Parameter(Mandatory = $true)]
    [string]$AudioPath,
    [string]$CpuCliPath = "C:\Tools\whisper.cpp\Release\whisper-cli.exe",
    [string]$CudaCliPath = "C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe",
    [string]$FastModelPath = "C:\Models\whisper\ggml-base.en.bin",
    [string]$QualityModelPath = "C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
    [string]$VadModelPath = "C:\Models\whisper\ggml-silero-v5.1.2.bin",
    [int]$Threads = 16
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AudioPath)) {
    throw "Audio file not found: $AudioPath"
}

$runs = @(
    @{ Name = "fast-cpu"; Cli = $CpuCliPath; Model = $FastModelPath; Extra = @("-tp", "0") },
    @{ Name = "quality-cpu"; Cli = $CpuCliPath; Model = $QualityModelPath; Extra = @("-tp", "0.2", "-sns") },
    @{ Name = "quality-cuda-vad"; Cli = $CudaCliPath; Model = $QualityModelPath; Extra = @("-tp", "0.2", "-sns", "--vad", "-vm", $VadModelPath, "-vt", "0.50", "-vspd", "250", "-vsd", "100", "-vp", "30", "-vo", "0.10") }
)

foreach ($run in $runs) {
    if (-not (Test-Path -LiteralPath $run.Cli) -or -not (Test-Path -LiteralPath $run.Model)) {
        Write-Host "SKIP $($run.Name): missing CLI or model"
        continue
    }

    if ($run.Name -like "*vad" -and -not (Test-Path -LiteralPath $VadModelPath)) {
        Write-Host "SKIP $($run.Name): missing VAD model"
        continue
    }

    $outputBase = Join-Path ([System.IO.Path]::GetDirectoryName($AudioPath)) ("bench-" + $run.Name + "-" + [Guid]::NewGuid().ToString("N"))
    $args = @(
        "-m", $run.Model,
        "-f", $AudioPath,
        "-t", "$Threads",
        "-otxt",
        "-nt",
        "-l", "en",
        "-of", $outputBase
    ) + $run.Extra

    $elapsed = Measure-Command {
        & $run.Cli @args | Out-Null
    }

    $textPath = "$outputBase.txt"
    $text = if (Test-Path -LiteralPath $textPath) { (Get-Content -LiteralPath $textPath -Raw).Trim() } else { "" }
    Write-Host "=== $($run.Name) ==="
    Write-Host ("elapsed_ms={0}" -f [int]$elapsed.TotalMilliseconds)
    Write-Host $text
}
