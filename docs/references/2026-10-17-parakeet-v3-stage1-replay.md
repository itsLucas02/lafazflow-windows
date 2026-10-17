# Stage 1 replay: Parakeet V3 vs the current Whisper engine (17 October 2026)

Status: Stage 1 of the [full replacement roadmap](../superpowers/plans/2026-10-10-lafazflow-full-replacement-roadmap.md) executed under the owner-approved autonomous protocol. Private per-fixture transcripts and audio stay local under `%LOCALAPPDATA%\LafazFlow\Benchmarks\v3-replay-2026-10-17\` and are deliberately not reproduced here.

## Setup

- Foundation: upstream Handy v0.9.8, pinned commit `14f6f0d31cb22a4268acfbddba3052820fd0a06e`, built locally (`bun` + `tauri build --no-bundle`, Vulkan SDK 1.4.363.0) in the isolated temp checkout.
- Engine A: Parakeet V3 ONNX int8 (`parakeet-tdt-0.6b-v3`); downloaded artifact SHA-256 verified against the pinned catalog hash `43d37191602727524a7d8c6da0eef11c4ba24320f5b4730f1a2497befc2efa77`; CPU execution (`bound_backend=onnx`).
- Engine B (current engine, same day): whisper.cpp CUDA `ggml-large-v3-turbo-q5_0.bin`, 16 threads, final-pass options exactly as the installed app's fallback (`-tp 0 -nf -sns`, VAD off for the final pass).
- Inputs: the four retained M1 fixtures (16 kHz mono), private.

## Results (aggregate, normalized; higher word similarity is better)

| Engine | Mean word-level similarity vs retained transcripts | Exact normalized matches | Endings preserved (last 3 words) |
| --- | --- | --- | --- |
| Parakeet V3 | 0.9705 | 1/4 | 3/4 |
| Whisper (current) | 0.9775 | 2/4 | 3/4 |
| Phonon-2 (optional third comparison) | 0.9575 | 1/4 | 3/4 |

- The engines produced **byte-identical normalized text on 2 of 4 fixtures** (V3 vs whisper). Remaining deltas: one extra stutter token from V3 on one fixture, and retained-transcript differences explained by LafazFlow post-processing applied at retention time (for example `road map` normalized to `roadmap`); no missing or invented content was observed for any engine on this corpus.
- One fixture's retained-transcript ending differs from **all three** engines identically (a shared snapshot artifact, not engine-specific).
- V3 timing (CPU, one-shot per process): model load 0.9-1.1 s; inference 240 ms (6.3 s audio) to 2017 ms (35.6 s audio); warm repeats 1183-1369 ms for 22.3 s audio (~16-19x realtime). The CUDA whisper CLI completed the same files end-to-end in ~0.9-1.3 s including load, so whisper remains the latency leader on this machine while V3 is viable with no GPU at all.
- No hallucinations, omission runs, or language drift occurred across 12 replay runs (4 fixtures × 3 engines).

## Phonon-2 (optional approved comparison)

- Setup: isolated Python 3.13 virtual environment, `fermion-research` CPU engine (Windows PyTorch CPU build); the Phonon-2 profile was fetched once to `~/.cache/fermion/speech` (~164 MB); entirely on-device.
- One-shot process overhead after the first fetch: ~3.0-4.0 s per file including interpreter and model load; these are not warm inference numbers and are not comparable to the in-process figures above.
- Accuracy on this corpus sits marginally below both primary engines (0.9575 vs 0.9705 and 0.9775), consistent with Phonon-2 being a distilled Parakeet V3; it remains an interesting lightweight candidate but has no native/GGML/ONNX runtime today, so it stays watch-only as recorded in the decision record.

## Owner trial artifact

An isolated portable test build is at `%LOCALAPPDATA%\Programs\HandyTestBuild` (upstream identity; portable `Data\` folder; Ctrl+Space hotkey; Parakeet V3 preinstalled and preselected; README.txt inside). It does not touch LafazFlow data or the double-Shift hotkey, and it is removable by deleting the folder. It must not become the daily driver.

## Limits and next steps

- Four fixtures on one machine cannot establish quality; live microphone trials (opening words, quiet endings, trailing silence, rapid dictations, long dictation) are required before any engine-default decision.
- The interim flight recorder in the installed app now retains every dictation; those recordings should drive the next evaluation round, including identical-audio replay through both engines.
- Stage 2 (LafazFlow-branded build) remains gated on the owner's trial feedback and the Phase C checkpoint (implementation home and branding scope).
