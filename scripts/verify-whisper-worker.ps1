param(
    [string]$WorkerPath = "C:\Tools\lafazflow-whisper-worker\bin\lafazflow-whisper-worker.exe",
    [string]$ModelPath = "C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
    [string]$VadModelPath = "C:\Models\whisper\ggml-silero-v5.1.2.bin",
    [int]$Threads = 16,
    [int]$Repeats = 25,
    [string]$FixturesDirectory = "$env:LOCALAPPDATA\LafazFlow\Benchmarks\fixtures-m1-2026-08-13",
    [string]$CliBaselineCsv = "$env:LOCALAPPDATA\LafazFlow\Benchmarks\lafazflow-transcription-bench-20260813-214637.csv",
    [string]$SettingsPath = "$env:APPDATA\LafazFlow\settings.json"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $WorkerPath)) {
    throw "Worker not found at $WorkerPath. Build it first with scripts/build-whisper-worker.ps1."
}
if (-not (Test-Path -LiteralPath $ModelPath)) {
    throw "Model not found at $ModelPath."
}
if (-not (Test-Path -LiteralPath $FixturesDirectory)) {
    throw "Fixtures directory not found: $FixturesDirectory"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$toolProject = Join-Path $repoRoot "tools\LafazFlow.WorkerVerification\LafazFlow.WorkerVerification.csproj"

Write-Host "Running worker verification (readiness, $Repeats repeats per fixture, text equivalence, VRAM/working-set)..."
dotnet run --project $toolProject -c Release -- `
    --worker $WorkerPath `
    --model $ModelPath `
    --vad-model $VadModelPath `
    --threads $Threads `
    --repeats $Repeats `
    --fixtures $FixturesDirectory `
    --settings $SettingsPath
if ($LASTEXITCODE -ne 0) {
    throw "Worker verification failed with exit code $LASTEXITCODE."
}
