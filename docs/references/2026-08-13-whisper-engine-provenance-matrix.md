# LafazFlow Persistent Whisper Engine — Reference Provenance Matrix (M0)

**Date:** 13/08/2026 (Asia/Kuala_Lumpur)
**Machine-readable manifest:** `docs/references/2026-08-13-whisper-engine-reference-manifest.json`

This evidence pack satisfies M0 of the persistent Whisper engine roadmap. Every major architectural decision in M1–M10 must trace to a pinned source or to an explicitly documented evidence gap. Labels are limited to:

- **Reference adopted** — behaviour reproduced without a material change.
- **Reference adapted for Windows** — proven behaviour preserved through Windows/.NET/native mechanisms.
- **Evidence-backed improvement** — LafazFlow deliberately differs, with a documented reference limitation and a measurable acceptance test.

## Pinned revisions and licenses

| Project | Pinned revision | License | Verified paths |
| --- | --- | --- | --- |
| FluidVoice | `4ce0584f93efbb5240d07b5039e23b09487b6ce0` | GPL-3.0 | `ContentView.swift`, `ASRService.swift`, `FluidAudioProvider.swift`, `WhisperProvider.swift`, `AudioEngineRetirementDrain.swift` |
| Handy | `37a26fd6ab905259d66affea57fff448288ca1aa` | MIT | `managers/transcription.rs`, `audio_toolkit/audio/recorder.rs`, `actions.rs`, `transcription_coordinator.rs` |
| VoiceInk | `7023a6f7e16ba09c3b131fe71f8cc9e55c065f19` | GPL-3.0 | `ModelPrewarmService.swift`, `WhisperModelManager.swift`, `WhisperTranscriptionService.swift`, `LibWhisper.swift`, `VoiceInkEngine.swift` |
| whisper.cpp | `592feef04a1802b18cbeffd0fd0eb5d02570c2ec` | MIT | `include/whisper.h`, `src/whisper.cpp`, `examples/cli/cli.cpp`, `ggml/src/ggml-cuda` |

All listed paths were verified to exist at their pinned revisions on 13/08/2026.

## Behaviour captured from each reference

### FluidVoice (GPL-3.0, behaviour adopted, not code copied)

| Behaviour | Verified evidence at pinned revision |
| --- | --- |
| Delayed startup model preload after UI appears | `ContentView.swift` calls `preloadASRModel()` after a ~1s `Task.sleep` |
| Stop/audio-drain phase measurement | `ASRService.swift` awaits provider reset drain and measures stop/final timings |
| Persistent ready provider | `FluidAudioProvider.swift` keeps a ready provider with separate streaming/final state |
| Retained model/session with locked readiness | `WhisperProvider.swift` |
| Bounded audio-resource retirement | `AudioEngineRetirementDrain.swift` |

### Handy (MIT)

| Behaviour | Verified evidence at pinned revision |
| --- | --- |
| Retained loaded engine | `LoadedEngine` behind `Arc<Mutex<Option<LoadedEngine>>>` in `transcription.rs` |
| Single-flight model loading | `transcription.rs` |
| Panic recovery by dropping/reloading engine | `catch_unwind` in `transcription.rs` |
| Real-time factor | `real_time_factor(audio_secs, compute_secs)` |
| End-of-stream audio drain | `recorder.rs` drains until the producer confirms end-of-stream (sentinel) |
| Exactly-once final pipeline completion | `transcription_coordinator.rs` repeated-input protection |

### VoiceInk (GPL-3.0, behaviour adopted, not code copied)

| Behaviour | Verified evidence at pinned revision |
| --- | --- |
| Optional delayed launch/wake prewarm | `ModelPrewarmService.swift` (~3s sleep, wake-from-sleep trigger, user toggle) |
| Shared loaded context with single-flight guard | `WhisperModelManager.swift` (`whisperContext == nil` guard) |
| Reuse of an already-loaded matching model | `WhisperTranscriptionService.swift` |
| Serialized context access with request settings | `LibWhisper.swift` |
| Recording-time loading + cleanup boundaries | `VoiceInkEngine.swift` |

### whisper.cpp (MIT)

Verified at `592feef0`: `whisper_init_from_file_with_params_no_state`, `whisper_init_state`, `whisper_full_params.abort_callback`, `whisper_get_timings`, `whisper_free_state`, `whisper_free`, CUDA backend (`GGML_CUDA`), Silero VAD, and `whisper_print_system_info`.

## Architecture decision traceability

| LafazFlow decision | Label | Evidence |
| --- | --- | --- |
| Preload shortly after app launch | Reference adopted | FluidVoice `ContentView.swift`; VoiceInk `ModelPrewarmService.swift` |
| Keep the model ready for the session | Reference adapted for Windows | Handy retained `LoadedEngine`; FluidVoice ready providers; VoiceInk shared context |
| Fresh decode state per request | Reference adapted for Windows | VoiceInk context discipline; whisper.cpp `whisper_init_state` |
| Drain final microphone samples | Reference adopted | Handy recorder end-of-stream drain; FluidVoice stop/drain measurement |
| Final transcription preempts preview | Reference adapted for Windows | Handy engine lease/coordinator; FluidVoice distinct streaming/final paths |
| Automatic engine reload after invalid native state | Reference adapted for Windows | Handy drops a panicked engine and reloads |
| Crash-isolated background worker | Evidence-backed improvement | Handy documents Windows Whisper crashes; separate process isolates native failure. Acceptance: forced-crash tests prove WPF survival and recovery |
| Current-user-only named pipe | Evidence-backed improvement | Local process isolation without a network listener. Acceptance: unauthorized-client and malformed-message tests |
| Sustained-degradation restart | Evidence-backed improvement | References measure latency/RTF but not this recovery rule. Acceptance: no restart loop under injected slowdown |

## Native dependency decision record

**Decision:** LafazFlow builds and owns a small native worker (`native/LafazFlow.WhisperWorker`) that calls the whisper.cpp C API directly at the pinned revision.

**Compared alternative:** a maintained managed binding (whisper.net-style wrapper).

| Criterion | Direct LafazFlow-owned worker | Maintained binding |
| --- | --- | --- |
| Version control | Exact pinned SHA `592feef0` | Tracks the binding's own cadence |
| CUDA | `GGML_CUDA` build identical to the owner's CUDA CLI | Depends on binding-provided native assets |
| VAD parity | whisper.cpp Silero VAD with identical thresholds | Varies by wrapper |
| Abort support | `whisper_full_params.abort_callback` | Usually exposed, but through wrapper layers |
| Packaging | Worker + runtime DLLs produced by LafazFlow build scripts | NuGet/native assets, extra supply chain |
| Crash boundary | Separate process; native failure cannot kill WPF | In-process by default; a CUDA crash can kill the app |

The crash boundary is the decisive criterion and is consistent with the roadmap's `Evidence-backed improvement` classification.

## whisper.cpp revision decision

- **Worker build input:** pinned `592feef04a1802b18cbeffd0fd0eb5d02570c2ec`.
- **Current owner CUDA CLI:** built 17/05/2026 from source checkout `968eebe77225d25e57a3f981da7c696310f0e881` (unpinned `main`, May 2026).
- **Baseline policy:** M1 measures the current CLI exactly as the owner runs it. M3 builds a same-revision reference CLI beside the worker for controlled before/after comparison. No silent replacement of the owner's runtime.

## Licensing gate

- **FluidVoice (GPL-3.0)** and **VoiceInk (GPL-3.0)**: behaviour and patterns only, no source copied. LafazFlow is itself GPL-3.0 (`LICENSE`), so even future direct reuse would be license-compatible; none is planned.
- **Handy (MIT)**: behaviour only; MIT permits reuse with attribution if any code is ever copied.
- **whisper.cpp (MIT)**: the native worker links against whisper.cpp; MIT is compatible with LafazFlow GPL-3.0. The MIT license text and provenance are recorded in `THIRD_PARTY_NOTICES.md` (updated in M0).
- **Sound cue assets** (GPL-3.0, Beingpax) already documented in `THIRD_PARTY_NOTICES.md`.
- **Conclusion:** no incompatible reuse identified. No implementation starts from an unpinned upstream branch for the worker.

## Evidence gaps (documented, not silently ignored)

1. Current CUDA CLI revision (`968eebe7`-era) differs from the pinned worker revision (`592feef0`). Handled by a controlled same-revision comparison in M3.
2. Bundled CPU CLI comes from the official `whisper-bin-x64.zip` release at packaging time; the worker pins `592feef0` for reproducible builds.

## Exit gate status

- Every M1–M10 design choice has a pinned source reference or a documented evidence gap: **pass**.
- Licensing review identifies no incompatible reuse: **pass**.
- No implementation starts from an unpinned upstream branch: **pass** for the worker; the owner's existing CLI is preserved as-is for baseline and recovery.
