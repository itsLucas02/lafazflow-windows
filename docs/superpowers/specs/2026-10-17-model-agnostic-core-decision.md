# Decision record: model-agnostic engine core for the LafazFlow replacement

Date: 17 October 2026, Asia/Kuala_Lumpur. Status: **Accepted by owner — all recommendations approved 17 October 2026** (resolutions recorded below). Supplements the [full replacement roadmap](../plans/2026-10-10-lafazflow-full-replacement-roadmap.md) and the [trailing invented speech incident](../../references/2026-10-08-trailing-invented-speech.md).

## Context

- The current LafazFlow Windows app is single-engine (whisper.cpp) end to end: worker protocol, settings fingerprint, decode options, VAD flags, prompt/vocabulary injection, formatter heuristics, and the model library all assume whisper.cpp. Swapping one whisper `.bin` file works; switching engine **families** (Parakeet, SenseVoice, remote engines) requires a rebuild. The owner explicitly rejects this trap: the product must not revolve around one dictation model.
- Trust failures persist on the installed 1.1.x build: intermittent missing words or whole sentences, and occasional invented endings (for example an unspoken closing courtesy phrase). Frequency is low and unpredictable, the owner cannot identify which recording was affected, and no matching retained audio exists, so no root cause is confirmed for any occurrence.
- The three modern reference apps were inspected at pinned revisions and all share the same architecture: models are catalog data, one adapter per engine family, and capture/UI/paste never know the model. See "Evidence from pinned references" below.

## Decision

1. **Do not rebuild from scratch, and do not keep patching the whisper-coupled core.** Port the valuable, engine-independent parts (behavior, specs, data, tests) onto a model-agnostic core. Keep the LafazFlow brand, local/offline privacy, and the daily dictate-into-any-app workflow.
2. **Four separated layers in the replacement:**
   - Audio front-end: capture → 16 kHz mono s16 → VAD decision → finalized WAV. Zero model knowledge.
   - Engine interface (small): `Load(model)`, `Transcribe(pcm, options) → text + meta`, `Cancel()`, `Unload()`, `Capabilities`.
   - Engine-family adapters: whisper-family GGML, ONNX families (Parakeet V3 and friends), and a remote OpenAI-compatible endpoint.
   - Model catalog as data: id, family, format, file/hash, size, languages, capabilities (streaming, hotwords, punctuation, translate), download source. **Adding or changing a model = catalog entry, never a rebuild.**
3. **Capability-scoped settings:** prompt/temperature/VAD controls appear only for engines that support them; the vocabulary and formatting pipeline applies uniformly to all engine output.
4. **Flight recorder:** every finalized dictation is kept in a local, auto-expiring ring buffer (private, local-only, with visible retention and purge controls) so any trust failure is replayable and root-causeable. The failure class "we cannot tell which recording it was" must never recur.
5. The currently installed build stays in use until the replacement passes owner-local acceptance (same rule as the roadmap).

## Evidence from pinned references

- **Handy** (Windows, v0.9.8 `14f6f0d3`): `LoadedEngine` enum with `TranscribeCpp(Session)` (whisper-family GGUF) plus Parakeet/Moonshine/SenseVoice/GigaAM/Canary/Cohere via `transcribe-rs` ONNX; catalog rows carry `EngineType`; capabilities are reconciled from the loaded model at runtime; live streaming is capability-gated with automatic batch fallback. Files: `src-tauri/src/managers/transcription.rs`, `src-tauri/src/managers/model.rs`.
- **VoiceInk** (`7023a6f7`): `TranscriptionModel` protocol — models are plain value structs (`WhisperModel`, `FluidAudioModel`, `TranscribeCppModel`, `CloudModel`, `CustomCloudModel`); `TranscriptionServiceRegistry.service(for: model.provider)` resolves one `TranscriptionService` per provider. Files: `VoiceInk/Models/TranscriptionModel.swift`, `VoiceInk/Transcription/Engine/TranscriptionServiceRegistry.swift`.
- **FluidVoice** (`4ce0584f`): `ASRService.transcriptionProvider` computed from the `SpeechModel` setting; cached provider classes (`FluidAudioProvider`, `WhisperProvider`, `AppleSpeechProvider`, `NemotronProvider`, external Core ML); `provider.prepare()` owns download/load; switching models resets provider state only — the capture pipeline is untouched. File: `Sources/Fluid/Services/ASRService.swift`.

## Alternatives considered

- **A. Keep the WPF app and retrofit only an engine interface.** Rejected as the primary path: the trust failures live in the capture→final-decode boundary, and the surrounding UX and pipeline are the weakest layers; the retrofit would still carry whisper-shaped settings and behavior. In the meantime, the current app may receive a minimal interim evidence-capture change if the owner approves it.
- **B. Full from-scratch rewrite.** Rejected: discards verified behavior that is portable as specs/tests/data (capture state machine, exactly-once paste, vocabulary catalog, post-processing, fixtures, harness, install pipeline) and delays trust restoration the longest.
- **C. Adopt/port onto a model-agnostic foundation (chosen).** Fastest path to the modern standard, with engine-family freedom by construction and the proven capture pipelines where the current failures live.

## Salvage ledger

| Ports over (engine-independent) | Replaced (whisper/WPF-coupled) |
| --- | --- |
| Vocabulary catalog + correction rules (DeepSeek family, roadmap/route map, literal formatting) | Native whisper worker + its protocol + whisper-specific settings fingerprint |
| Post-processing pipeline stages (raw cleanup → vocabulary → literals → punctuation) | Whisper decode options, VAD flags, prompt-injection coupling |
| Capture-state design (stop/drain/session isolation, delivery commit, exactly-once paste) | WPF shell capture plumbing |
| 688-test suite as an executable behavior spec | One-shot CLI path as the primary engine |
| M1 fixtures + verification harness + benchmark methodology | Whisper-only model library |
| Owner-local install/delivery script; incident/reference discipline | |
| UX decisions (fixed shell, cues, forgiving hotkey) | |

## Added requirements to the roadmap (normative)

1. Engine catalog + adapters including a remote OpenAI-compatible endpoint, **proven by running a second engine family (Parakeet V3) and a remote endpoint without rebuilding the app**.
2. Flight recorder with documented defaults (proposal: last 50 finalized dictations or 48 hours, whichever comes first), visible retention/purge controls, and no cloud path.
3. Trust acceptance evidence: replay identical flight-recorder audio across candidate engines, plus owner-labeled live trials covering opening words, quiet endings, trailing silence, and invented-content cases before cutover.
4. Capability-scoped settings UI — no whisper-only knobs shown for engines that do not support them.

## Consequences

- Selecting a model becomes configuration; new models, mirrors, and renames are data changes.
- The replacement inherits a modern capture/VAD pipeline where the current trust failures live; existing specs and fixtures keep known regressions guarded.
- Until cutover, the current build remains the daily driver; no silent fallback to it is permitted inside the replacement.

## Approved resolutions (17 October 2026)

1. **Direction: option C approved** — adopt/port onto a model-agnostic core, including the salvage-ledger approach. No blind from-scratch rewrite; no continued patching of the whisper-coupled core.
2. **Flight recorder defaults approved as proposed:** always-on, keeps the last 50 finalized dictations or 48 hours (whichever comes first), stored locally under `%LOCALAPPDATA%\LafazFlow\FlightRecorder`, with a save-this-one control and a purge-all control; never leaves the machine.
3. **Interim flight recorder in the current app: approved.** Implement it as the next owner-local slice (plan tracked in `tasks/todo.md`) so the intermittent trust failures are captured and replayable while the replacement is built.
4. **Replacement v1 engine lineup approved:** whisper-family (keeps the current quality model working) + Parakeet V3 + remote OpenAI-compatible endpoint. SenseVoice/Moonshine deferred as optional speed tiers until the catalog proves itself.
5. **Phonon-2 role approved:** optional remote-endpoint comparison candidate in Stage 1 replays only (it runs as a local server); not a dependency. Revisit integration only if a native/GGML/ONNX runtime appears.

## Delivery log

- 17 Oct 2026: `comment and push` → `commit and push` homophone fix + recorder-test flake stabilization (commit `02d7210`).
- 17 Oct 2026: interim dictation flight recorder delivered and installed to the owner build (commit `181cff5`, installed build 36); ring at `%LOCALAPPDATA%\LafazFlow\FlightRecorder`, Settings → Diagnostics card live.
- 17 Oct 2026: owner granted a single approval for an autonomous continuation session; protocol and stop conditions recorded in the roadmap ("Autonomous execution protocol — 17 October 2026").
