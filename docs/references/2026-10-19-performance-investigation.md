# Dictation performance investigation (19 October 2026)

Living reference. Data source: the `LATENCY` rows in `%LOCALAPPDATA%\LafazFlow\Logs\lafazflow.log` (the app records every dictation's phases). Private recordings stay local; numbers here are aggregates only.

## Where the time goes (last 150 completed dictations)

| Phase | median | p90 | p99 | max |
| --- | ---: | ---: | ---: | ---: |
| Activation (hotkey → pill visible) | 11 ms | 23 ms | 283 ms | 283 ms |
| Audio drain (stop → audio finalized) | 214 ms | 324 ms | 993 ms | 1154 ms |
| Worker decode, warm (engine call wall time) | 238 ms | 627 ms | 819 ms | 873 ms |
| Paste (clipboard + key dispatch) | ~120 ms | ~150 ms | — | 155 ms |
| **Stop → pasted total** | **606 ms** | **1087 ms** | **2176 ms** | **2865 ms** |

Warm worker RTF by recording length: 5–15 s → 0.052 (~19x realtime); 15–40 s → 0.035 (~29x). The M10 benchmark measured 77x (RTF 0.013) with the verification tool; real-world requests carry fixed per-request overhead (fresh decode state for isolation, VAD state setup, prompt processing) that the benchmark's sustained loop amortizes differently, and preview contention was a likely contributor to the tail.

## Findings

1. **Audio drain was a ~214 ms tax on every dictation** with 1 s spikes. The sub-phase instrumentation added on 19/10/2026 proved the split: the device stop + finalize itself costs **13–31 ms** (`stop_total_ms`), while the remaining 200–400 ms was `ReplaceWarmInput` — the microphone re-open for the *next* recording, executed synchronously inside `AudioCaptureService.StopAsync()` and therefore blocking the current dictation's handoff. **Fixed (same day, build 48):** the warm replacement now runs on a background task guarded by the warm generation counter (skipped if a new session already started or the input was already replaced), so the stop handoff returns immediately; a new regression test gates the replacement and proves `StopAsync` completes anyway. Expected effect: drain drops from ~214 ms median to roughly the device-stop cost (~20–50 ms).
2. **Preview cancellation ran after the drain.** `StopLivePreviewAsync` was invoked ~220 ms after the stop request, so the single worker could still be mid-preview-abort when the final decode arrived. Fixed 19/10/2026: preview now cancels first and overlaps with the drain (with an ordering regression test).
3. **Paste path contains fixed sleeps** (~50 + 50 ms plus 3×40 ms verification delays, `ClipboardPasteService`). Some may be trimmable; each must be verified against its purpose (clipboard fidelity, paste retry).
4. **Activation is healthy** (median 11 ms) with rare ~283 ms spikes — likely first-show layout/creation cost; a launch-time shell prewarm would remove them.
5. **Cold worker restarts are rare** (2 of 150) and cost ~1 s on the next dictation; recovery restarts already warm the model in the background.

## Improvement plan (learning from Handy / FluidVoice / VoiceInk)

- **P0 — Drain reduction (done).** The sub-phase instrumentation attributed the drain: device stop + finalize 13–31 ms, the rest was the next-recording `ReplaceWarmInput`. The warm replacement now runs on a background task with a generation guard; live `LATENCY` rows confirmed the drop: **drain 207–447 ms → 19–60 ms** and stop→pasted 550–1200 ms → 370–777 ms (build 48).
- **P1 — Paste trims (done, build 49).** Clipboard settle 50 → 25 ms (writes are synchronous), and the foreground re-assert + its 50 ms delay are now skipped when the target window is already foreground (the normal case, since the recorder shell never takes focus). Saves ~75 ms typical, ~40 ms worst case.
- **P2 — Worker tail / flash attention (evaluated, kept off).** Flash attention was enabled at context creation for CUDA and verified against the retained corpus: **identical text** (same matches/recall/edit distance) and **identical wall times** (563–581 ms median) versus the same worker without it. It was reverted: no measurable benefit on this GPU, so the proven non-FA kernel path stays. Note: both binaries measured ~2x the old M10 wall times, which points at machine state rather than the flag.
  - **Power-state factor:** the GPU is an RTX 4070 **Laptop** GPU idling at ~210 MHz (max 3105 MHz) under the Windows **Balanced** plan (plugged in). Laptop dGPU clocks vary with power mode/thermal state, which can multiply dictation latency. If dictation feels slow, verify the Windows power mode (Best Performance) and that the machine is plugged in before treating it as an app regression.
- **P3 — Structural: incremental decoding.** Handy and FluidVoice both commit text progressively during speech, so the stop-time decode is small. LafazFlow currently cancels preview and re-decodes the whole take. Incremental chunked decoding with commit/stitch semantics is the structural fix that would cut stop-time worker work to the tail chunk only; it belongs to the replacement core's roadmap (design + tests, not a quick patch).
- **P4 — Shell prewarm (done, build 50).** The mini-recorder window is created, laid out and hidden once at startup (opacity-0 show → `UpdateLayout` → hide), removing the one-time first-show cost from the hotkey path. Best-effort and never blocks startup.
- **P5 — Monitor recalibration (done, build 50).** Slow-run thresholds moved from 750 ms / 1.75x to **500 ms / 1.6x** relative to the learned baseline (live warm median ≈ 238 ms made the old absolute bar unreachable for many degradations). The sustained 3-of-5 rule and the 10-minute restart cooldown are unchanged, and the boundary regression test was updated (700 ms with a 300 ms baseline stays normal).
- **P1 verification (live):** after build 49, `paste_ms` measured **41–63 ms** (was ~120 ms); drain stayed at 27–62 ms with one 258 ms outlier.

## Verification

- Per-dictation numbers stay in the `LATENCY` log; drain sub-phases now appear in the `CAPTURE` final line.
- Any change must be validated against the retained corpus for text quality (no silent quality trade) and the full test suite.
