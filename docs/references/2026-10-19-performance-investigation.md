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

- **P0 — Drain reduction (in progress).** Use the new instrumentation to attribute the 214 ms; then reduce the dominant sub-phase (device stop latency and/or finalize path). Reference behavior: Handy's cpal stop returns in tens of ms; FluidVoice bounds its drain explicitly and measures it. Target: median ≤ 100 ms. Expected saving ~100–150 ms per dictation.
- **P1 — Paste trims.** Audit each fixed delay in `ClipboardPasteService`; trim the ones whose purpose is already covered by verification, keep the retry path. Target ≤ 80 ms.
- **P2 — Worker tail.** Re-measure warm p90 after the preview-ordering fix; then try flash-attention in the worker build (`-fa`) as a one-flag experiment with the retained corpus as the quality gate.
- **P3 — Structural: incremental decoding.** Handy and FluidVoice both commit text progressively during speech, so the stop-time decode is small. LafazFlow currently cancels preview and re-decodes the whole take. Incremental chunked decoding with commit/stitch semantics is the structural fix that would cut stop-time worker work to the tail chunk only; it belongs to the replacement core's roadmap (design + tests, not a quick patch).
- **P4 — Shell prewarm.** Create and measure the mini-recorder shell once at launch to remove first-show spikes.
- **P5 — Monitor recalibration.** After P0–P2, recalibrate the performance-health slow-run rule against the new baseline so genuine regressions are still caught.

## Verification

- Per-dictation numbers stay in the `LATENCY` log; drain sub-phases now appear in the `CAPTURE` final line.
- Any change must be validated against the retained corpus for text quality (no silent quality trade) and the full test suite.
