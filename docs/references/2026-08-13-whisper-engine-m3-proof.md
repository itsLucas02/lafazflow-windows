# LafazFlow Persistent Whisper Engine — M3 Native Worker Proof

**Date:** 14/08/2026 (Asia/Kuala_Lumpur)
**Status:** Exit gate passed

## What was built

- `native/LafazFlow.WhisperWorker`: a small C++17 executable with no WPF dependency, linked against whisper.cpp pinned at `968eebe77225d25e57a3f981da7c696310f0e881` (the same revision as the owner's CUDA CLI source, chosen deliberately for controlled equivalence).
- Loads the configured model, CUDA backend, and Silero VAD once at startup; serves sequential transcription requests with per-request `no_context` isolation (no past-transcript reuse), matching the CLI's single-file semantics.
- Replicates every CLI decode setting used by LafazFlow: English, prompt with `carry_initial_prompt`, threads 16, temperature 0, no fallback (`temperature_inc = 0`), non-speech suppression, and VAD thresholds `vt=0.50, vspd=250, vsd=100, vp=30, vo=0.10`.
- Cooperative abort via whisper.cpp's `abort_callback` (M3 proof uses a file signal; M4 replaces it with the named-pipe Cancel operation).
- Build: `scripts/build-whisper-worker.ps1` (CUDA/CPU); verification: `scripts/verify-whisper-worker.ps1`.

## Verification results

| Check | Result |
| --- | --- |
| Repeated requests in one process | 100/100 succeeded |
| Model reload after initialization | 0 (all 100 `M` rows report `load_ms=0`; `LOAD` printed once) |
| Output equivalence vs current CLI (identical files/settings, normalized) | 100/100 |
| Invalid/corrupt audio rejection | 1/1 (`E ... invalid_audio`) |
| Cancellation | `F cancel1 aborted`, followed by successful reuse (`R after1`) |
| Clean shutdown | exit code 0 after `Q` |
| Working set growth over 100 requests | 0 bytes (316,510,208 → 316,510,208) |
| VRAM over 100 requests | 897 → 935 MiB (+38 MiB, stable, no unbounded growth) |

## Warm timing vs M1 one-shot baseline (same corpus, same settings)

| Metric | M1 CLI one-shot | Persistent worker | Delta |
| --- | ---: | ---: | ---: |
| Warm median (total) | 1202 ms | 285 ms | −76% |
| Warm P90 | 1371 ms | 411 ms | −70% |
| Warm P95 | 1400 ms | 424 ms | −70% |
| Model load (repeated) | 539 ms per run | 0 after init | removed |

## Key implementation findings

- VAD in whisper.cpp at this revision runs only inside `whisper_full` (which requires `ctx->state`), not `whisper_full_with_state`. The worker therefore uses `whisper_init_from_file_with_params` (with state) and `whisper_full`, with `no_context = true` to guarantee per-request isolation. This was the root cause of both a missing-VAD output difference and an access violation when the no-state init was used.
- The CLI's `--no-fallback` maps to `temperature_inc = 0` (single deterministic pass); the worker replicates this exactly.

## Traceability

- Retained loaded engine / reused context — Reference adapted for Windows (Handy `LoadedEngine`, VoiceInk shared context).
- Fresh per-request decode context — Reference adapted for Windows (VoiceInk context discipline; whisper.cpp `no_context` isolation with a persistent context).
- VAD and decode-settings parity — Reference adopted (whisper.cpp CLI parameters at the pinned revision).
- Crash-isolated worker process — Evidence-backed improvement (proof of isolation and stability above; forced-crash tests in M7).

## Limitations and rollback

- M3 is a proof-of-concept binary outside the repo's production build; it does not touch LafazFlow's transcription path. Rollback = discard the POC.
- Abort signalling is a file watch in M3; M4 replaces it with the versioned named-pipe protocol.
- Per-request `no_context` isolation differs mechanically from a fresh `whisper_state` but achieves the same observable guarantee (no cross-request transcript leakage); documented as an intentional adaptation.
