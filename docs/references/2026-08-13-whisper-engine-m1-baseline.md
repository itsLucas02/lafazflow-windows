# LafazFlow Persistent Whisper Engine — M1 One-Shot CLI Baseline

**Date:** 13/08/2026 (Asia/Kuala_Lumpur)
**Label:** `one-shot-cli-m1-baseline`
**Status:** Baseline recorded before any engine replacement. M2–M10 will be compared against these numbers using the same audio corpus, settings, hardware, and workload.

## Method

- Engine under test: the current one-shot `whisper-cli.exe` exactly as LafazFlow launches it (fresh process per dictation).
- Tool: `tools/LafazFlow.TranscriptionBench` in `--process` mode, which reproduces the app's exact CLI invocation (arguments, working directory, PATH) and parses whisper.cpp timing output.
- Corpus: four retained recordings from the owner's local recordings (approx. 6.3 s, 17.6 s, 22.3 s, 35.6 s), copied to a private fixture folder under `%LocalAppData%\LafazFlow\Benchmarks` (never committed).
- Repeats: 8 per fixture = 32 runs; first run per fixture is labelled cold, remaining 28 are warm.
- Full local report (with transcripts, for local analysis only): `%LocalAppData%\LafazFlow\Benchmarks\lafazflow-transcription-bench-20260813-214637.*`. This committed document contains summary statistics only and no transcript/audio/clipboard/prompt contents.

## Settings under test (unchanged owner configuration)

- Profile: Quality
- Backend: CUDA (NVIDIA GeForce RTX 4070 Laptop GPU, 8 GB VRAM)
- Model: `ggml-large-v3-turbo-q5_0.bin`
- VAD: enabled (Silero, thresholds `-vt 0.50 -vspd 250 -vsd 100 -vp 30 -vo 0.10`)
- Language: English (`-l en`), deterministic decoding (temperature 0, no fallback, non-speech suppression), prompt and custom vocabulary unchanged
- Threads: 16
- Engine fingerprint: `EngineSettingsFingerprint.Compute(settings)` (SHA-256 over profile, backend, CLI/model/VAD paths, VAD flag, threads, and resolved decode options; prompt and vocabulary excluded as request-level)

## Results (32 runs, 0 failures, 0 empty results)

| Metric | Value |
| --- | ---: |
| Cold median (total, ms) | 1383 |
| Warm median (total, ms) | 1202 |
| Warm P90 (total, ms) | 1371 |
| Warm P95 (total, ms) | 1400 |
| Warm max (total, ms) | 1466 |
| Model load median (ms) | 539 |
| Inference median (ms) | 147 |
| Inference RTF median | 0.007 |
| Mean edit distance vs retained expected | 0.004 |

## Reading

- Every warm dictation pays the full one-shot cost: process start + model load (~539 ms median) + VAD + inference (~147 ms) + output read and cleanup. Total warm median is 1202 ms; P95 is 1400 ms.
- Inference itself is fast (RTF 0.007 on the RTX 4070); the repeated model-load and process-start cost is the dominant removable overhead targeted by the persistent worker (M3–M5).
- The acceptance target is: warm P95 at least 30% lower than this 1400 ms baseline (i.e. <= ~980 ms) with no repeated model load, no ending-phrase loss, and no text regression on the same corpus.

## Reproducibility

The baseline can be re-run with:

```powershell
dotnet run --project tools\LafazFlow.TranscriptionBench -c Release -- `
  --settings "$env:APPDATA\LafazFlow\settings.json" `
  --recordings "$env:LOCALAPPDATA\LafazFlow\Benchmarks\fixtures-m1-2026-08-13" `
  --configs current-settings --process --repeats 8 --label one-shot-cli-m1-baseline
```

## Traceability

- Reference adopted: Handy real-time-factor logging; FluidVoice `ASR_BENCH` phase logs.
- Reference adapted for Windows: LafazFlow's existing `LatencyTrace`/diagnostics extended with engine phase fields.
- Privacy: no transcript, PCM, clipboard, prompt, or sensitive full paths are logged; the committed summary is statistic-only.
