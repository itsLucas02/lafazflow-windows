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
$ProgressLog = Join-Path $env:TEMP ("verify-progress-" + [guid]::NewGuid().ToString("N") + ".log")
function Log-Progress($message) {
    Add-Content -Path $ProgressLog -Value ("[{0:HH:mm:ss.fff}] {1}" -f (Get-Date), $message)
    Write-Host $message
}

Log-Progress "script-start"
if (-not (Test-Path $WorkerPath)) {
    throw "Worker not found at $WorkerPath. Build it first with scripts/build-whisper-worker.ps1."
}

$settings = Get-Content -Raw $SettingsPath | ConvertFrom-Json
$prompt = "English dictation only. Output English text only. Do not translate into Malay or Indonesian. Transcribe the spoken English words exactly. " + $settings.WhisperInitialPrompt
$fixtures = @(Get-ChildItem -Path $FixturesDirectory -Filter *.wav | Sort-Object Name)
if ($fixtures.Count -eq 0) {
    throw "No fixtures found in $FixturesDirectory"
}

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $WorkerPath
$psi.WorkingDirectory = Split-Path -Parent $WorkerPath
$psi.Arguments = "--model `"$ModelPath`" --vad-model `"$VadModelPath`" --threads $Threads --prompt `"$prompt`" --vad-params vt=0.50,vspd=250,vsd=100,vp=30,vo=0.10"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$abortFile = Join-Path $env:TEMP ("lafazflow-abort-" + [guid]::NewGuid().ToString("N") + ".flag")
$env:LAFAZFLOW_ABORT_FILE = $abortFile
$process = [System.Diagnostics.Process]::Start($psi)
$stderrTask = $process.StandardError.ReadToEndAsync()
trap {
    $message = if ($_ -is [System.Management.Automation.ErrorRecord]) {
        $_.Exception.Message
    } else {
        "$_"
    }
    $workerState = if ($null -eq $process) {
        "no-process"
    } elseif ($process.HasExited) {
        "exited=$($process.ExitCode)"
    } else {
        "alive"
    }
    Log-Progress "SCRIPT-ERROR: $message worker=$workerState"
    $errTail = ""
    try {
        if ($null -ne $stderrTask -and $stderrTask.IsCompleted) {
            $errTail = (($stderrTask.Result -split "`n") | Select-Object -Last 4) -join " | "
        }
    } catch {
    }
    Log-Progress "STDERR-TAIL: $errTail"
    if ($null -ne $process -and -not $process.HasExited) {
        $process.Kill()
    }
    break
}
Log-Progress "STARTED pid=$($process.Id)"

$pendingRead = $null
function Start-PendingRead {
    if ($process.HasExited) {
        throw "Worker exited early with code $($process.ExitCode)."
    }
    if ($null -eq $pendingRead) {
        $script:pendingRead = $process.StandardOutput.ReadLineAsync()
    }
}

function Complete-PendingRead {
    $line = $pendingRead.Result
    $script:pendingRead = $null
    return $line
}

function Read-Line([int]$TimeoutSeconds = 60) {
    Start-PendingRead
    if (-not $pendingRead.Wait([TimeSpan]::FromSeconds($TimeoutSeconds))) {
        throw "Timed out waiting for worker output after $TimeoutSeconds seconds."
    }
    return Complete-PendingRead
}

$ready = Read-Line
Log-Progress "READY=$ready"
if (-not ($ready -like "READY model=*")) {
    throw "Worker did not report READY. Got: $ready"
}
$loadLine = Read-Line
Log-Progress "LOAD=$loadLine"

function Get-VramMiB {
    $job = Start-Job -ScriptBlock {
        (nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits | Select-Object -First 1).Trim()
    }
    if (Wait-Job $job -Timeout 10) {
        $result = Receive-Job $job
        Remove-Job $job -Force
        return $result
    }
    Stop-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -Force
    return "unavailable"
}

$startWorkingSet = $process.WorkingSet64
$vramBefore = Get-VramMiB
Log-Progress "VRAM-BEFORE=$vramBefore"

$fixtures = @($fixtures | ForEach-Object { $_ })
$totalRequests = $fixtures.Count * $Repeats
$allLines = [System.Collections.Generic.List[string]]::new()
$writtenThisBatch = 0
for ($i = 0; $i -lt $totalRequests; $i++) {
    $fixture = $fixtures[$i % $fixtures.Count]
    $process.StandardInput.WriteLine("T req$i $($fixture.FullName)")
    $writtenThisBatch++
    if ($writtenThisBatch -eq 5) {
        $drained = 0
        while ($drained -lt 5) {
            $line = Read-Line 120
            $allLines.Add($line)
            if ($line -like "R *" -or $line -like "E *" -or $line -like "F *") {
                $drained++
            }
        }
        $writtenThisBatch = 0
    }
}
Log-Progress "WROTE $totalRequests REQUESTS"

$corruptWav = Join-Path $env:TEMP ("lafazflow-corrupt-" + [guid]::NewGuid().ToString("N") + ".wav")
[System.IO.File]::WriteAllBytes($corruptWav, [byte[]](1, 2, 3, 4))
$process.StandardInput.WriteLine("T corrupt1 $corruptWav")
$drained = 0
while ($drained -lt 1) {
    $line = Read-Line 120
    $allLines.Add($line)
    if ($line -like "R *" -or $line -like "E *" -or $line -like "F *") {
        $drained++
    }
}
Log-Progress "GOT $($allLines.Count) LINES"

$endWorkingSet = $process.WorkingSet64
$vramAfter = Get-VramMiB
Log-Progress "VRAM-AFTER=$vramAfter"

$longFixture = $fixtures | Sort-Object Length -Descending | Select-Object -First 1
$shortFixture = $fixtures | Sort-Object Length | Select-Object -First 1
$process.StandardInput.WriteLine("T cancel1 $($longFixture.FullName)")
Start-Sleep -Milliseconds 120
[System.IO.File]::WriteAllText($abortFile, "abort")
Log-Progress "ABORT FILE CREATED"
$cancelLine = $null
while ($null -eq $cancelLine -or -not ($cancelLine -like "F cancel1*")) {
    $cancelLine = Read-Line 60
}
Log-Progress "CANCEL=$cancelLine"
for ($retry = 0; $retry -lt 10; $retry++) {
    try {
        [System.IO.File]::Delete($abortFile)
        break
    } catch {
        Start-Sleep -Milliseconds 50
    }
}
Log-Progress "ABORT FILE REMOVED"
$process.StandardInput.WriteLine("T after1 $($shortFixture.FullName)")
$afterLine = $null
while ($null -eq $afterLine -or -not ($afterLine -like "R after1*")) {
    $afterLine = Read-Line 60
}
Log-Progress "AFTER=$afterLine"

$process.StandardInput.WriteLine("Q")
if (-not $process.WaitForExit(15000)) {
    $process.Kill()
    throw "Worker did not exit cleanly after Q."
}

$baseline = @(Import-Csv $CliBaselineCsv | Where-Object { [string]::IsNullOrWhiteSpace($_.error) })
function Normalize($text) {
    return (($text -replace '[^\p{L}\p{N}\s]', '') -split '\s+' | Where-Object { $_ } | ForEach-Object { $_.ToLowerInvariant() }) -join ' '
}

$baselineByFixture = @{}
foreach ($row in $baseline) {
    if (-not $baselineByFixture.ContainsKey($row.fixture_id)) {
        $baselineByFixture[$row.fixture_id] = Normalize $row.raw
    }
}

$fixtureMatches = @{}
$totalComparisons = 0
$totalMatches = 0
foreach ($line in $allLines) {
    if ($line -match '^R (req\d+) (.*)$') {
        $reqIndex = [int](($m = $Matches[1]) -replace 'req', '')
        $fixtureId = $fixtures[$reqIndex % $fixtures.Count].BaseName
        if (-not $fixtureMatches.ContainsKey($fixtureId)) {
            $fixtureMatches[$fixtureId] = "0/0"
        }
        $current = $fixtureMatches[$fixtureId] -split '/'
        $count = [int]$current[1] + 1
        $matched = [int]$current[0]
        if ($baselineByFixture.ContainsKey($fixtureId) -and (Normalize $Matches[2]) -eq $baselineByFixture[$fixtureId]) {
            $matched++
        }
        $fixtureMatches[$fixtureId] = "$matched/$count"
        $totalComparisons++
        $totalMatches += $matched - [int]$current[0]
    }
}

$summary = [pscustomobject]@{
    Ready = $ready
    LoadLine = $loadLine
    TotalRequests = $totalRequests
    SuccessResults = @($allLines | Where-Object { $_ -like "R req*" }).Count
    InvalidAudioRejected = @($allLines | Where-Object { $_ -like "E corrupt1 invalid_audio" }).Count
    WorkerExitCode = $process.ExitCode
    WorkingSetBeforeBytes = $startWorkingSet
    WorkingSetAfterBytes = $endWorkingSet
    WorkingSetGrowthBytes = $endWorkingSet - $startWorkingSet
    VramBeforeMiB = $vramBefore
    VramAfterMiB = $vramAfter
    CancelRecovery = "$cancelLine / $afterLine"
    FixtureMatches = $fixtureMatches
    TotalEquivalenceMatches = "$totalMatches/$totalComparisons"
}

$summary | ConvertTo-Json -Depth 5

if (-not $process.HasExited) {
    $process.Kill()
}
