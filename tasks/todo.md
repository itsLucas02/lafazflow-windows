# Task: Windows MVP Hotkey And Prerequisite Revision

# Task: Persistent Whisper Engine And Dictation Reliability

**Detailed implementation roadmap:** `docs/superpowers/plans/2026-08-13-persistent-whisper-engine-roadmap.md`

## M0 — Reference evidence pack and licensing gate

**Status:** Complete (exit gate passed)

**Deliverables**
- Reference manifest: `docs/references/2026-08-13-whisper-engine-reference-manifest.json`
- Provenance matrix and dependency decision: `docs/references/2026-08-13-whisper-engine-provenance-matrix.md`
- Updated `THIRD_PARTY_NOTICES.md` with whisper.cpp MIT notice and revision provenance

**Reference traceability (labels)**
- FluidVoice `4ce0584f` (GPL-3.0): startup preload, stop/audio-drain measurement — Reference adopted (behaviour only)
- Handy `37a26fd6` (MIT): retained engine, panic recovery, end-of-stream drain, RTF — Reference adopted / adapted for Windows
- VoiceInk `7023a6f7` (GPL-3.0): delayed prewarm, shared context — Reference adopted (behaviour only)
- whisper.cpp `592feef0` (MIT): context/state, abort callback, CUDA, VAD, timings — build source for the native worker
- Crash-isolated worker, current-user named pipe, sustained-degradation rule — Evidence-backed improvement

**Key decisions recorded**
- Native dependency: LafazFlow-owned worker calling whisper.cpp C API directly at pinned `592feef0` (crash boundary decisive vs in-process managed binding).
- Revision gap documented: current CUDA CLI is an unpinned `968eebe7`-era build (17/05/2026); worker pins `592feef0`; M1 measures the current CLI; M3 adds a same-revision reference CLI for controlled comparison.
- Licensing gate: no incompatible reuse; MIT worker linking is GPLv3-compatible; no source copied from GPL references.

**Files changed:** 3 (two reference docs, one notices update)
**Tests:** none required (documentation-only); existing suite untouched
**Limitations / evidence gaps:** recorded in the manifest (revision delta, CPU CLI release provenance)
**Rollback readiness:** documentation-only; revert files without code impact
**Next milestone entry conditions:** satisfied — M1 baseline can proceed.

## M1 — Reproducible baseline and privacy-safe telemetry

**Status:** Complete (exit gate passed)

**Deliverables**
- Baseline summary: `docs/references/2026-08-13-whisper-engine-m1-baseline.md`
- `WhisperTimingParser` (whisper.cpp timing block → structured fields) with tests
- `EngineSettingsFingerprint` (SHA-256 over engine-affecting settings; prompt/vocabulary excluded) with tests
- `TextCharMetrics` (character counts + final-character categories, no text storage) with tests
- Latency telemetry: new `LatencyTrace` fields (`audio_drain_ms`, `wave_finalize_ms`, `model_load_ms`, `inference_ms`, `response_transfer_ms`, raw/formatted/clipboard char counts and final categories); formatter/store parser extended, older rows parse with `na`; tests added
- Timing-aware transcription path: `ITranscriptionTimingProvider` + `WhisperCliTranscriptionService.TranscribeWithTimingAsync`; recorder populates the new trace fields; app wiring updated
- Benchmark tool: `--process` mode with structured timings, `--repeats`, `--label`, WAV duration reader, privacy-safe summary writer; tests added

**Measured baseline (owner's Quality/CUDA/large-turbo/VAD/16-thread settings, 32 runs)**
- Warm median 1202 ms; P90 1371 ms; P95 1400 ms; max 1466 ms; cold median 1383 ms
- Model load median 539 ms; inference median 147 ms; inference RTF median 0.007
- Failures 0; empty results 0; mean edit distance vs retained corpus 0.004

**Reference traceability**
- Handy RTF logging, FluidVoice phase logs — Reference adopted
- LatencyTrace/Diagnostics extension — Reference adapted for Windows

**Files changed:** app telemetry services + latency classes + recorder wiring + bench tool + 5 new test files
**Tests:** focused 64; full suite 579; Release build 0 warnings/0 errors; `git diff --check` clean
**Limitations:** `response_transfer_ms` is recorded as a field but measured only when the worker protocol lands (M4/M5); audio-drain/wave-finalize phases are approximate until M2's explicit async finalization; the existing service-mode benchmark overwrites fixture expected transcripts when run against a live corpus (process mode avoids this via temp copies)
**Rollback readiness:** additive telemetry; parsing is tolerant of older rows; recorder falls back to the plain transcription path when no timing provider is supplied
**Next milestone entry conditions:** satisfied — M2 (complete-audio stop and WAV finalization) can proceed.

## M2 — Complete-audio stop and WAV finalization

**Status:** Complete (exit gate passed)

**Deliverables**
- `IAudioCaptureService.Stop()` replaced with async `StopAsync()` returning `AudioCaptureFinalization` (path, sample/byte counts, duration, state, error kind)
- Explicit capture states: `Idle`, `Recording`, `Stopping`, `Finalized`, `Failed`
- Stop flow: request stop → keep session callbacks attached → await NAudio `RecordingStopped` → lock session, detach callbacks, finalize writer, publish counts
- Two-second bounded stop deadline (`audio_drain_timeout`); device errors finalize with `device_error`; writer failures mark `Failed` and block enqueue
- Session isolation preserved: a stopped session's late callbacks can never write into a later session
- `WavFileValidator` for header/byte/sample/duration parity
- Recorder enqueues final transcription only after successful finalization

**Reference traceability**
- Handy recorder end-of-stream drain — Reference adopted
- FluidVoice measured stop/audio-drain lifecycle — Reference adapted for Windows (NAudio `RecordingStopped`)

**Tests (focused 39, full 585, Release build 0 warnings/0 errors)**
- Final buffer arriving after stop request is included
- Old-session delayed callback cannot write into a new session
- Rapid sessions keep sample ownership; active-session replacement fails loudly
- Stop timeout finalizes received audio with `audio_drain_timeout`
- Writer failure marks `Failed`; device error finalizes with `device_error`
- Real WAV header/byte/sample/duration parity (100 ms fixture)
- Real spoken-ending preservation: last-3-word match 24/32 exact, **32/32** case/punctuation-insensitive on the retained real corpus

**Limitations / notes:** the ten live-microphone spoken-ending repetitions are part of M10 owner-local verification; the retained real corpus provides automated ending-word evidence here. CLI transcription remains functional.
**Rollback readiness:** restore the synchronous stop adapter only if async finalization fails; session isolation retained.
**Next milestone entry conditions:** satisfied — M3 (native persistent-engine proof of concept) can proceed.

## M3 — Native persistent-engine proof of concept

**Status:** Complete (exit gate passed)

**Deliverables**
- `native/LafazFlow.WhisperWorker` (C++17, no WPF dependency) linked against whisper.cpp pinned at `968eebe7` (matches the owner's CUDA CLI revision; explicit revision decision recorded in the M0 manifest)
- `scripts/build-whisper-worker.ps1` (CUDA/CPU builds, app-local MSVC runtime, `--version` smoke) and `scripts/verify-whisper-worker.ps1` (100-request proof driver with bounded pipe handling)
- Proof document: `docs/references/2026-08-13-whisper-engine-m3-proof.md`

**Verification (owner's exact Quality/CUDA/large-turbo/VAD/16-thread settings)**
- 100/100 repeated requests in one process; model reload 0 (all `load_ms=0`)
- Output equivalence vs current CLI: 100/100 normalized on identical files/settings
- Invalid audio rejected; cancellation (`F ... aborted`) followed by successful reuse
- Working set growth 0 bytes over 100 requests; VRAM 897→935 MiB (stable)
- Warm median 285 ms vs CLI 1202 ms (−76%); warm P95 424 ms vs 1400 ms (−70%); repeated model-load cost removed

**Key findings / evidence-backed notes**
- VAD runs only in `whisper_full` (requires `ctx->state`) at this revision; worker uses with-state init + `whisper_full` + `no_context=true` for per-request isolation
- CLI `--no-fallback` ⇒ `temperature_inc = 0`; replicated
- M3 abort uses a file signal; M4 replaces with the named-pipe Cancel op

**Traceability:** retained engine (Reference adapted for Windows); fresh per-request context (Reference adapted for Windows); VAD/decode parity (Reference adopted); crash-isolated worker (Evidence-backed improvement)
**Tests:** focused/full .NET suites untouched by M3 (native proof is script-verified); results above
**Rollback readiness:** POC only; production CLI path untouched
**Next milestone entry conditions:** satisfied — M4 (versioned protocol and worker supervisor) can proceed.

## M4 — Versioned local protocol and worker supervisor

**Status:** Complete (exit gate passed)

**Deliverables**
- `WhisperPipeProtocol` codec: versioned, length-prefixed binary frames (80-byte header: version, op/status, request/session IDs, deadline, 32-byte settings fingerprint, audio format, sample count, data); ops Initialize/Preview/Final/Cancel/Health/Shutdown; max frame 16 MB; 16 kHz mono s16 boundary format
- Worker is now a current-user-only named-pipe SERVER (SDDL `D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;<user>)`) using overlapped I/O with a 250 ms idle poll so a reader thread and writer never deadlock on the blocking-mode serialization
- `WhisperWorkerSupervisor`: single-flight startup, fingerprint-keyed session reuse, replace-only-after-ready on settings change, readiness/operation/shutdown timeouts, exact-child reap, state machine (Idle/Starting/Loading/Ready/Recovering/Unavailable), privacy-safe WORKER log lines
- `WhisperWorkerProcess`: hidden child launch with exact PID capture and optional diagnostics capture (worker stderr) for troubleshooting
- Integration: real worker + supervisor round-trip (Initialize loads the CUDA model once; Final returns text; Health reports completed/backend; Cancel aborts an in-flight decode; reuse after cancel; Shutdown leaves no process)

**Tests (focused 11, full 596, Release build 0 warnings/0 errors)**
- Codec: request/response round-trips; malformed length; oversized frame; invalid version; unknown op; response-kind rejection; wrong fingerprint length; hex round-trip
- Supervisor: two concurrent starts → one worker; fingerprint change → replacement worker; worker exit → Recovering; readiness timeout → Unavailable; shutdown reaps
- Integration (real worker, owner CUDA runtime): full op lifecycle above

**Key findings / evidence-backed notes**
- .NET 9 removed NamedPipeServerStream security constructors; the low-level handle path had async/serialization issues, so the worker owns the pipe (natural for a native server) with an SDDL descriptor
- Blocking-mode named pipes serialize I/O per instance; a reader thread blocks writers → overlapped I/O with idle polling is required for concurrent read (Cancel) + write (responses)
- No network listener exists; the pipe is restricted to the current user

**Traceability:** named pipe (Evidence-backed improvement); single-flight startup (Reference adapted for Windows - Handy single-flight loading); replace-only-after-ready (Reference adapted for Windows); state transitions (Reference adapted for Windows)
**Rollback readiness:** supervisor is unused by production transcription; CLI path stays authoritative
**Next milestone entry conditions:** satisfied — M5 (final dictation integration) can proceed.

## M5 — Final dictation integration

**Status:** Complete (exit gate passed)

**Deliverables**
- `ITranscriptionEngine` contract + `CliTranscriptionEngine` (existing one-shot CLI behind the same contract) + `WorkerTranscriptionEngine` (supervisor-backed final transcription)
- `WavPcmReader` converts finalized WAV → 16 kHz mono s16 PCM for the pipe boundary
- Recorder routes final dictation through the engine when available; empty/failed results never paste; exactly-once paste preserved
- `DictationJob` now carries an immutable `DictationId` (recording → paste) and `DeliveryCommitted`, set immediately before clipboard/paste
- MainWindow wires the worker engine when the worker executable is present, starts worker preparation fire-and-forget in `InitializeShell` (hotkey/tray never blocked), and reaps the worker on app exit

**Verification**
- Focused tests 38; full suite 603; Release build 0 warnings/0 errors
- Recorder: engine used with dictation id; exactly one paste on success; no paste on empty/failed result
- Worker engine integration: final transcription of retained audio matches expected normalized text
- Startup smoke: app launches, worker process starts and reaches `WORKER state=ready` with the owner's fingerprint, no crash events, no orphan processes after cleanup
- Text pipeline parity: worker engine applies `CleanTranscript` before post-processing so punctuation/casing behavior matches the CLI path (no regression)

**Traceability:** engine-neutral contract (Reference adapted for Windows - Handy/FluidVoice engine abstraction); startup preparation non-blocking (Reference adopted - FluidVoice preload); dictation id + delivery commit (Evidence-backed improvement for exactly-once)
**Rollback readiness:** CLI engine remains the fallback; switching the recorder back to the timing/plain path is a one-line change
**Next milestone entry conditions:** satisfied — M6 (live-preview integration and final priority) can proceed.

## M6 — Live-preview integration and final priority

**Status:** Complete (exit gate passed)

**Deliverables**
- Worker: a `Final` request now preempts an in-flight `Preview` decode (abort) and drops queued preview work; final transcription can never wait behind preview
- `RollingWhisperLiveTranscriptPreviewService` accepts an optional worker-backed snapshot transcriber; previews run through the persistent worker when available (CLI fallback otherwise); monotonic display stitching (v0.13.2) preserved; stale-session responses are ignored via the per-session callback capture + cancellation token
- Supervisor session: single background response dispatcher (one reader, responses matched by request id) + serialized writes, so concurrent preview/final requests cannot corrupt the stream; TCS registered before write to avoid a response race
- MainWindow wires the preview to the worker session (`WorkerPreviewTranscribeAsync`)

**Verification**
- Integration (real worker): a long Preview is aborted when a Final arrives; the Final returns Ok first; the Preview returns Aborted; worker stays healthy
- Preview service: worker snapshot path, monotonic stitching, stale-session suppression
- Focused 24 (M4/M6 protocol+supervisor+preview+integration); full suite 606; Release build 0 warnings/0 errors

**Key findings / evidence-backed notes**
- NamedPipeClientStream forbids concurrent reads and writes; a single dispatcher loop + write gate is required for correct concurrent ops
- Response registration must precede the request write to avoid losing fast responses

**Traceability:** final-preempts-preview (Reference adapted for Windows - Handy engine lease/coordinator); coalesce/drop superseded previews (Reference adapted for Windows); monotonic stitching preserved (LafazFlow v0.13.2)
**Rollback readiness:** preview falls back to the existing CLI path if the worker is absent
**Next milestone entry conditions:** satisfied — M7 (crash, timeout, retry, and CLI recovery) can proceed.

## M7 — Crash, timeout, retry, and CLI recovery

**Status:** Complete (exit gate passed)

**Deliverables**
- `TranscriptionRecoveryPolicy`: retryable (aborted, worker_unavailable/timeout/busy/internalerror/invalidrequest, pipe_broken) vs not retryable (invalid audio, missing model/VAD, invalid settings, user cancellation, delivery committed)
- `RecoveringTranscriptionEngine`: one replacement-worker retry (same fingerprint), then one identical-settings CLI recovery attempt; never retries after delivery commit; returns the failure on terminal failure
- `WorkerTranscriptionEngine` maps pipe/process failures to typed kinds (timeout, unavailable, pipe_broken) and rethrows user cancellation
- `WhisperWorkerSupervisor.RestartSessionAsync`: recovery-locked (single-flight gate) restart that reaps the old worker and starts a replacement with the same settings
- Recorder: delivery commit is set immediately before paste; empty/failed results never paste; audio retained per the diagnostics setting on failure
- MainWindow wires the recovering engine (worker primary + CLI fallback)

**Verification**
- Policy tests (retryable/non-retryable/committed/cancelled); recovering-engine tests (success no-restart, worker retry, CLI fallback, terminal failure, non-retryable skip)
- Real-worker integration: kill the worker mid-session → final fails → `RestartSessionAsync` → recovered final returns Ok; shutdown leaves no process
- Focused 55; full suite 624 green across three consecutive runs; Release build 0 warnings/0 errors

**Traceability:** panic/drop-and-reload (Reference adapted for Windows - Handy); bounded retry + CLI recovery (Evidence-backed improvement with policy tests); exactly-once via delivery commit (Evidence-backed improvement)
**Rollback readiness:** CLI engine remains the fallback; recovery is logged and testable
**Next milestone entry conditions:** satisfied — M8 (sustained performance-degradation monitor) can proceed.

## M8 — Sustained performance-degradation monitor

**Status:** Complete (exit gate passed)

**Deliverables**
- `PerformanceHealthMonitor`: per-fingerprint rolling windows (latest 30 eligible samples), eligibility rules, baseline, slow-run rule, and bounded degradation recovery
- Eligibility: excludes cold, retried, cancelled, failed, and sub-two-second dictations from baseline training; baseline established after 10 successful warm dictations
- Slow-run rule: a run is slow only when inference is at least 750 ms above the baseline median AND inference real-time factor is at least 1.75x the baseline median for comparable audio duration
- Sustained degradation: only 3 of the latest 5 eligible runs being slow declares degradation; a single outlier never restarts the engine
- Recovery: one restart per fingerprint, next success marked cold as recovery validation, and a 10-minute cooldown suppresses restart loops; crashes/timeouts remain immediate lifecycle failures outside the slow-run rule
- Recorder wiring: successful dictations record privacy-safe health samples (dictation id, fingerprint, inference ms, audio duration ms, cold/retried flags, timestamp); sustained degradation logs the fingerprint prefix and restarts the worker once with the last captured settings
- MainWindow wiring: health monitor shared with the recorder; degradation restart routes through `WhisperWorkerSupervisor.RestartSessionAsync` when the worker is present
- `TranscriptionEngineResult.WasRetried`: recovered (worker-retry or CLI-fallback) results are flagged so retried runs never train the baseline

**Verification**
- PerformanceHealthMonitor tests: baseline builds after 10 eligible samples and excludes cold/retried/sub-two-second; slow classification requires both inference and RTF rules; isolated outlier never triggers; 3-of-5 triggers once per cooldown; normal jitter never triggers; injected sustained delay triggers exactly one recovery
- Recorder regression: two successful dictations produce exactly one warm health sample with the engine's inference ms and no cold marking
- Focused 48; full suite 631 green; Release build 0 warnings/0 errors

**Reference traceability**
- Handy/FluidVoice duration and real-time-factor measurement — Reference adopted
- Rolling per-fingerprint health, 750 ms/1.75x slow rule, 3-of-5 sustained rule, and 10-minute restart cooldown — Evidence-backed improvement with measurable tests (no pinned reference implements LafazFlow's exact recovery rule)
**Rollback readiness:** monitoring is additive; the degradation restart is a single recorder hook that can be disabled without touching engine behavior
**Next milestone entry conditions:** satisfied — M9 (plain-language status and Diagnostics) can proceed.

## M9 — Plain-language status and Diagnostics

**Status:** Complete (exit gate passed)

**Deliverables**
- `VoiceEngineStatusSource`: converts worker state, health samples, and recovery events into plain-language snapshots — `Loading voice engine`, `Ready`, `Recovering voice engine`, `Using recovery engine`, `Voice engine needs attention`, and `Using compatibility engine` (CLI-only installs)
- Overview: new VOICE ENGINE card showing status, backend + model filename (no raw internal paths), and uptime
- Diagnostics: new Voice Engine card with uptime, cold/warm median and P95 from the latest samples, last recovery reason/outcome with timestamp, and a fingerprint-safe engine identity prefix
- `PerformanceHealthMonitor` now retains a bounded diagnostics history (latest 30 per fingerprint, including cold/retried/short samples) so reporting works without polluting the health baseline
- Supervisor: `RestartSessionAsync` accepts a recovery reason and raises `RecoveryRecorded(reason, succeeded)`; the recovering engine passes the typed failure kind (for example `worker_timeout`, `pipe_broken`) so Diagnostics explains why recovery happened
- Recorder degradation restart reports `Sustained slowdown` as its recovery reason
- Diagnostics latency grid now shows raw/formatted/clipboard character counts and final-character categories for punctuation investigations; advanced CLI paths and technical logs remain untouched
- Startup notification is truthful: the hotkey is registered first, and with a worker present the "ready" balloon is shown only after the worker reports Ready (or an attention message if startup fails)
- Settings ViewModel subscribes to status changes while the window is open and detaches on close

**Verification**
- VoiceEngineStatusSource tests: compatibility mode without worker; Ready + uptime progression; Unavailable → needs attention; recovery reason/outcome recorded (success and failure); retried delivery → "Using recovery engine"; disconnected worker → Recovering; cold/warm median/P95 summaries
- Health monitor test: diagnostics history retains cold/retried/short samples while the health window stays clean
- Recovering-engine test: restart delegate receives the typed failure reason
- Startup test: hotkey registration precedes any notification and worker readiness gates the balloon
- ViewModel/XAML tests: engine status properties reflect the source snapshot; Overview and Diagnostics render the new cards; latency grid exposes char/final-char columns
- Rapid back-to-back test hardened to model the real recorder state gate (second dictation starts while the first is still transcribing); full suite 643 green twice; Release build 0 warnings/0 errors

**Reference traceability**
- Readiness/recovery surfaced in plain language — Reference adapted for Windows (Handy/FluidVoice readiness reporting)
- Truthful startup notification and fingerprint-safe identity — Evidence-backed improvement (no pinned reference gates its startup balloon on real engine readiness)
**Rollback readiness:** status is additive; hiding the new cards or reverting the notification gating leaves engine behavior unchanged
**Next milestone entry conditions:** satisfied — M10 (full verification and owner-local rollout) can proceed.

## Agreed product direction
- Reproduce the proven keep-the-model-ready behaviour used by VoiceInk, Handy, and FluidVoice with an original Windows implementation.
- Use Handy as the primary Windows lifecycle reference, FluidVoice as the audio-finalization and measurement reference, and VoiceInk as the simple persistent-model reference.
- Preserve the owner's current Quality profile, NVIDIA CUDA acceleration, `ggml-large-v3-turbo-q5_0.bin`, VAD, language, prompt, vocabulary, thread count, local/offline privacy, and normal dictation workflow.
- Keep the existing one-shot `whisper-cli.exe` path available for diagnostics and safe recovery, but stop launching a fresh CLI for every successful everyday dictation once the persistent worker is proven.

## Implementation plan

### Stage 1: Establish an objective before/after baseline
- [ ] Add a repeatable benchmark using retained test audio and the exact current Quality/CUDA settings.
- [ ] Record first-dictation time, later-dictation median and P95, engine-load time, final-audio-drain time, paste time, empty-result rate, failure rate, and surviving-process count.
- [ ] Define the acceptance target: warm dictations remove the repeated model-load cost, sustained P95 materially improves over the current one-shot CLI baseline, and output text remains equivalent or better.

### Stage 2: Guarantee complete recording endings
- [ ] Replace best-effort recorder shutdown with an explicit end-of-audio handshake: stop accepting new microphone samples, drain every already-captured sample, finalize the WAV, then allow final transcription.
- [ ] Keep each recording session isolated so late callbacks cannot enter a later session.
- [ ] Add regression tests for a final word arriving in the last audio buffer, rapid back-to-back recordings, delayed callbacks, device removal, and drain timeout recovery.
- [ ] Log captured sample count and finalized WAV duration without recording transcript content.

### Stage 3: Build the persistent Windows Whisper worker
- [ ] Add a small crash-isolated background worker that loads whisper.cpp, the selected model, CUDA, and VAD once and accepts sequential transcription requests from LafazFlow.
- [ ] Send audio and the current request settings to the worker through a local-only Windows connection; never expose a network port.
- [ ] Validate request identity, size, deadlines, and response ownership so stale or crossed responses cannot be pasted.
- [ ] Keep exactly one final transcription active at a time and ensure final dictation remains higher priority than live preview and diagnostics.
- [ ] Reload the worker only when the model/backend settings change, the user requests it, the inactivity policy unloads it, or health recovery requires it.

### Stage 4: Make failure recovery automatic
- [ ] Detect worker startup failure, crash, timeout, invalid response, lost connection, and repeated abnormal latency.
- [ ] Terminate and fully reap an unhealthy worker, restart it once with bounded backoff, reload the same settings, and retry only when doing so cannot duplicate a paste.
- [ ] Use the existing one-shot CLI as a clearly logged recovery path when the persistent worker cannot become healthy; never silently switch model, GPU backend, or quality profile.
- [ ] Ensure a native Whisper/CUDA crash cannot terminate the WPF application.

### Stage 5: Add performance-health monitoring
- [ ] Maintain a local rolling window of cold/warm latency, median, P95, audio real-time factor, failures, empty results, restarts, and queue delay.
- [ ] Establish the healthy baseline from successful warm dictations on the current machine rather than one hard-coded millisecond threshold.
- [ ] Mark performance degraded only after a sustained pattern, not one slow dictation; attempt recovery and report the reason and outcome in Diagnostics.
- [ ] Add privacy-safe phase timings for audio drain, worker readiness, model load, inference, response transfer, formatting, and paste.

### Stage 6: Add understandable settings and status
- [ ] Add a plain-language engine setting: `Keep ready` (recommended), `Unload after 10 minutes`, and `Unload immediately`.
- [ ] Show `Loading voice engine`, `Ready`, `Recovering`, or `Using recovery engine` without exposing implementation jargon in normal UI.
- [ ] Preserve advanced CLI paths and existing diagnostics for technical troubleshooting.

### Stage 7: Verification and safe rollout
- [ ] Add focused lifecycle, crash, timeout, audio-tail, settings-change, rapid-dictation, and no-duplicate-paste tests.
- [ ] Run the full test suite and Release build, then compare the same audio corpus before and after for text, punctuation, ending-word retention, median, and P95 latency.
- [ ] Test the exact current CUDA/model/VAD profile through at least 30 warm dictations, forced worker crashes, app restart, sleep/wake, and device changes.
- [ ] Publish and relaunch the actual stable build only after all acceptance checks pass; do not change the public release until the owner separately approves a new release.

## Explicit non-goals for this slice
- Do not change the selected Whisper model or trade accuracy for a smaller model.
- Do not replace local transcription with a cloud service.
- Do not remove the existing CLI diagnostics/recovery capability.
- Do not combine the microphone-selection UI feature into this engine reliability change.
- Do not copy macOS-specific source code directly; implement the agreed behaviour natively for Windows and retain required license notices for any reused third-party code.

## Decision-complete design (owner-approved choices)

### Evidence boundary from reference applications
- FluidVoice is the closest match to LafazFlow's chosen startup behaviour: after its main content appears, it waits about one second and preloads the ASR model, then keeps the provider ready.
- Handy begins model loading when recording starts, overlaps loading with the user's speech, then reuses the loaded engine across later dictations until its configured idle-unload policy releases it.
- VoiceInk has an optional launch/wake prewarm scheduled after roughly three seconds, also loads the selected model during recording when needed, and currently runs cleanup paths after a completed pipeline; it is not evidence that every model remains loaded for the entire app session by default.
- LafazFlow's selected policy—prepare at launch and keep ready until exit—is a deliberate speed-first combination, not a claim that all three projects implement identical lifecycle timing.

### Reference-first engineering rule
- Primary reference repositories for this project are `altic-dev/FluidVoice`, `cjpais/Handy`, and `Beingpax/VoiceInk` at recorded commit SHAs captured when implementation begins.
- Before implementing each major subsystem, inspect and document the corresponding current reference paths: startup/model preparation, retained model lifecycle, recording stop/audio drain, preview/final priority, crash recovery, performance metrics, and model unloading.
- Each implementation-plan item and review entry must include a traceability note: `Reference adopted`, `Reference adapted for Windows`, or `Evidence-backed improvement`, with repository/file/commit links.
- Do not invent a materially different lifecycle or reliability mechanism merely from intuition. If none of the references implements the needed behaviour, first seek upstream whisper.cpp guidance and reproducible local evidence, then document the gap, alternatives, risks, and proof before implementation.
- An `Evidence-backed improvement` must state why the reference behaviour is insufficient for LafazFlow on Windows and must have a measurable acceptance test. “Cleaner architecture” or AI preference alone is not sufficient.
- Copy behaviour and proven patterns, not macOS-specific source blindly. Review licenses before reusing code; retain copyright and license notices for copied or adapted code as required by GPLv3/MIT and update `THIRD_PARTY_NOTICES.md` with exact provenance.
- Do not claim parity or superiority without before/after measurements using the same audio, settings, hardware, and workload.
- If implementation discoveries contradict the approved reference-backed plan, stop and return to planning rather than silently substituting an unproven design.

### Product behaviour
- Engine readiness: prepare immediately in the background at app launch and keep ready until LafazFlow exits.
- Startup sequence: `LafazFlow launches -> start worker -> load Whisper/model/CUDA/VAD -> report Ready`; this happens before any recording and is independent of the recording stop flow.
- Dictation sequence: `user finishes speaking -> drain/finalize audio -> send audio to the already-ready worker -> format -> paste`.
- GPU memory: retain the selected model and CUDA context for the whole app session; closing LafazFlow releases them.
- Live preview: preserve it, coalesce preview work so stale previews never accumulate, and cancel preview immediately when final transcription begins.
- Crash recovery: restart the worker with the exact same settings and retry the unfinished, not-yet-pasted dictation once.
- Degradation recovery: recover automatically after sustained abnormal latency and show one brief plain-language notice.
- Rollout: update and observe the owner's local stable build first; a new public GitHub release requires separate later approval.

### Native worker architecture
- Add `native/LafazFlow.WhisperWorker`, a small C++ executable linked against a pinned whisper.cpp revision.
- Build the worker from the same whisper.cpp source and native backend used for its paired CLI: CUDA for the owner's Quality runtime and CPU for the bundled public runtime.
- Load one `whisper_context` at worker startup and retain it. Create/reset fresh per-request decoding state so transcripts cannot leak context into later dictations.
- Keep the worker outside the WPF process. A native CUDA/Whisper crash can terminate only the worker; the main LafazFlow process detects exit and recovers.
- Communicate over a Windows named pipe restricted to the current user. Do not listen on TCP/HTTP and do not expose any network port.
- Use a versioned, length-prefixed protocol with: protocol version, request ID, workload, settings fingerprint, deadline, PCM format, sample count, request options, and bounded PCM payload.
- Require 16 kHz, mono, signed 16-bit PCM at the boundary. Reject wrong formats, oversized messages, unknown operations, mismatched request IDs, stale responses, and protocol-version mismatches.
- The WPF app owns worker startup and shutdown. On normal exit it requests graceful shutdown, waits briefly, then terminates only the exact child process if needed.

### Worker request lifecycle
- `Initialize`: load the configured model, CUDA backend, VAD model, and immutable engine options; return readiness and non-sensitive capability/timing metadata.
- `Preview`: use a fresh decode state, remain cancellable through whisper.cpp's abort callback, and return only if its recording/session ID is still current.
- `Final`: cancel/await preview, run with the exact current language, prompt, VAD, temperature, fallback, suppression, context, and thread settings, then return raw text plus phase timings.
- `Health`: report process uptime, model fingerprint, backend, readiness, completed requests, last failure category, and memory/timing metadata without transcript or audio content.
- `Shutdown`: release request state, VAD state, model context, CUDA resources, and exit cleanly.
- Only one inference request may use the retained model at once. Final work has priority; only the newest pending preview is retained.

### Configuration identity and reload rules
- Compute a worker fingerprint from worker/protocol version, whisper.cpp revision, model path plus safe file identity, backend, GPU choice, VAD path/options, language/decode settings, and thread count.
- Prompt and vocabulary may remain request-level when supported safely; settings requiring context recreation trigger a controlled worker replacement.
- Start a replacement worker, prove it is ready, then retire the previous idle worker. Never discard a healthy worker before its replacement is usable unless the old configuration is invalid or unsafe.
- Never silently substitute CPU, a smaller model, disabled VAD, another language, or different quality settings.

### Complete-audio stop handshake
- Change `IAudioCaptureService.Stop()` into an awaited stop/finalize operation.
- Recording stop enters `Stopping`, asks NAudio to stop, continues accepting callbacks already owned by that session, and waits for NAudio's `RecordingStopped` signal.
- After the stop signal, take the session lock, detach the callback, finalize/flush the WAV writer, record sample/byte counts, and only then make the file available for final transcription.
- Normal stop has a two-second safety deadline. On deadline, force-close only that input session, finalize all audio already received, record `audio_drain_timeout`, and continue if the WAV is valid; report an error if safe finalization failed.
- Preserve the existing session-ownership guard: callbacks from a stopped session can never write into another session.
- The processing visual/sound may appear immediately, but queueing and final transcription must wait for audio finalization.

### Automatic crash and retry policy
- A request is retryable only before clipboard/paste begins and only for worker exit, broken pipe, engine abort without user cancellation, timeout, or invalid worker response.
- First failure: terminate/reap the exact unhealthy worker tree, start a fresh worker with the same fingerprint, wait for readiness, then retry the same finalized audio once.
- If the restarted worker cannot complete, use the existing one-shot CLI once with the same model/backend/settings as the last recovery path.
- If both paths fail, preserve the retained audio, show a compact failure, and never paste empty, partial, or duplicate text.
- User cancellation, invalid audio, missing files, or invalid settings are not automatically retried.
- Apply a recovery lock so simultaneous requests cannot start multiple replacement workers.

### Performance-health algorithm
- Record privacy-safe phases: audio drain, queue wait, worker start/readiness, cold model load, warm inference, response transfer, formatting, clipboard/paste, and total stop-to-done.
- Record audio duration, inference real-time factor (`inference time / audio duration`), result character count, and terminal punctuation category; never log transcript text or PCM content.
- Key baselines by worker fingerprint. Cold/retried/cancelled/failed runs and recordings shorter than two seconds do not train the warm baseline.
- Establish a baseline after 10 successful warm dictations and retain up to the latest 30 healthy samples.
- Mark one run slow only when both conditions hold: inference is at least 750 ms above its baseline median and its real-time factor is at least 1.75x the baseline median for comparable audio duration.
- Declare sustained degradation when 3 of the latest 5 eligible warm runs are slow. One outlier never restarts the engine.
- On sustained degradation, restart once, mark the next successful run as recovery validation, and suppress another automatic degradation restart for 10 minutes to prevent restart loops.
- Timeouts, crashes, empty responses, surviving worker processes, and recovery outcomes remain separately visible in Diagnostics.

### Punctuation observability boundary
- This engine slice does not broaden question-mark grammar heuristics.
- Add privacy-safe stage metadata—raw final character category, formatted final character category, clipboard final character category, and character counts—to prove where punctuation changes without storing dictated text.
- Address a punctuation defect only when evidence shows which stage changed it and a focused regression can express the intended sentence structure.

### Build and packaging plan
- Pin the whisper.cpp revision used by both worker and CLI build inputs; do not build from an unpinned moving branch.
- Extend the CUDA build script to compile/deploy/smoke-test `lafazflow-whisper-worker.exe` beside the owner's CUDA CLI and matching native runtime DLLs.
- Extend release packaging to build/bundle the CPU worker beside the bundled CPU CLI, include required MIT/GPL notices, and fail packaging if worker readiness smoke checks fail.
- Keep CLI resolution intact for diagnostics and recovery. Add worker resolution beside the chosen CLI plus an advanced explicit worker path only if required for custom runtimes.
- Treat a worker/CLI/whisper.cpp revision mismatch as a visible configuration error, not a silent fallback.

### Test matrix
- Recorder: final buffer after stop request, delayed final callback, rapid sessions, callback from an old session, stop timeout, writer failure, device removal, zero audio, and valid WAV header/duration/sample parity.
- Protocol: partial reads/writes, oversized payload, invalid version, invalid request ID, stale response, disconnect, malformed metadata, cancellation, and current-user-only pipe access.
- Lifecycle: launch preparation, already-ready reuse, settings fingerprint change, app exit, worker crash during load/preview/final, hung worker, retry success, retry failure, CLI recovery, and no orphan process.
- Priority: many preview requests collapse to the latest; final cancels preview within the defined bound and cannot be delayed by diagnostics.
- Delivery: no paste before successful final result, exactly one paste after retry, no paste on terminal failure, retained audio remains recoverable.
- Quality: compare raw text, punctuation, ending-word retention, and empty-result rate against the existing CLI using the same retained corpus and exact settings.
- Performance: cold start plus at least 30 warm final dictations; compute median, P90, P95, maximum, real-time factor, and stop-to-paste time before and after.
- Environment: exact RTX 4070 CUDA Quality profile, app restart, Windows sleep/wake, display/GPU reset where safely reproducible, microphone device change, and rapid back-to-back dictation.

### Acceptance and rollout gates
- All focused and full tests pass; Release build and both stable publish profiles succeed; `git diff --check` passes.
- The selected model/backend/VAD/prompt/language/thread settings shown by Diagnostics exactly match the current configuration.
- Every audio-tail regression preserves the final buffer; real recordings maintain logged-duration/WAV-duration parity within normal device-buffer tolerance.
- No repeated model load occurs during 30 warm dictations with an unchanged fingerprint.
- Warm median must improve by at least the measured repeated-load cost; warm P95 must be at least 30% lower than the one-shot baseline and must not contain unexplained process-start/cleanup spikes.
- Raw transcript quality must not regress on the retained corpus; no increase in empty results or missing final phrases is accepted.
- Forced crash recovery completes one retry with exactly one paste; terminal double failure pastes nothing and preserves diagnostic audio.
- Update the owner's actual pinned stable build, run real hands-free dictation, and observe normal use before proposing a public version/tag/release.

### Rollback
- Preserve the current one-shot CLI implementation during local rollout as a recovery path and an explicit emergency compatibility mode.
- If the persistent worker fails an acceptance gate or causes real-use regressions, switch local execution back to the verified CLI path without changing the model, CUDA runtime, VAD, or user settings.
- Do not delete the worker diagnostics or failed-session evidence during rollback; use them to correct the worker before another rollout attempt.

## Plan: Optional Conservative Dictation Polish v0.12.2
- [x] Add an opt-in, local-only polish stage after developer literal formatting.
- [x] Rejoin only high-confidence continuation clauses while protecting developer literals.
- [x] Persist the setting and expose it clearly in Dictation settings.
- [x] Add pipeline, settings, and UI regression coverage.
- [x] Bump LafazFlow from `0.12.1` to `0.12.2`.
- [x] Run focused tests, full tests, build, diff check, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Optional Conservative Dictation Polish v0.12.2
- Added `conservative_dictation_polish` after developer literal formatting. It is disabled by default and runs entirely on-device; it has no provider, model, network, or transcript-export path.
- When enabled, it only rejoins lower-case continuation clauses after an erroneous full stop: `because`, `which means`, `so that`, `and then`, and `while`.
- Backticks, quotes, parentheses, brackets, and braces are protected before polishing, so developer literals and quoted text stay exact.
- Added the opt-in Dictation setting: `Polish broken continuation punctuation (on device)`.
- Bumped LafazFlow to `0.12.2` and settings schema to `16`; older settings safely retain the disabled default.
- Focused pipeline/settings/UI tests pass, 321 tests. Full `dotnet test` passes, 525 tests. Release build passes with 0 warnings and 0 errors. `git diff --check` passes.
- Published both stable artifacts and relaunched `stable-single`; the responding process reports product version `0.12.2+033530109babe64cdd993579bcb01aa894d85751` and file version `0.12.2.0`.

## Plan: Spoken Developer Literal Polish v0.12.1
- [x] Add regressions for the owner-dictated literal formatting failures.
- [x] Support `slash debug` and `slash env` as bounded slash commands.
- [x] Tolerate ASR commas, casing, `backtake`, `RunDev`, `open parent`, and `clues parent` in literal formatting.
- [x] Bump LafazFlow from `0.12.0` to `0.12.1`.
- [x] Run focused tests, full tests, build, diff check, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Spoken Developer Literal Polish v0.12.1
- Added regressions from the owner-dictated formatting sample covering `/debug`, `/env`, `.env`, `components.json`, `@luqman`, quoted text, backticked `npm run dev`, and paired parentheses.
- Expanded slash-command allow-list for `debug` and `env` without enabling broad slash rewrites.
- Made literal markers tolerant of ASR comma insertion, marker casing, `backtake`, `RunDev`, `open parent`, and `clues parent`.
- Guarded mismatched delimiter phrases such as `open bracket ... close parent` so they stay unchanged.
- Bumped LafazFlow to `0.12.1`.
- Focused post-processing/formatter/vocabulary tests pass, 227 tests.
- Full `dotnet test` passes, 519 tests.
- Full `dotnet build -c Release` passes with 0 warnings and 0 errors.
- `git diff --check` passes.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Developer Literal Formatting v0.12.0
- [x] Add a post-processing stage for high-confidence spoken developer literals.
- [x] Convert conservative developer phrases such as `slash help`, `dot env`, `components dot json`, quoted/backticked text, and simple paired delimiters.
- [x] Add negative regressions so normal English words like slash, dot, quote, and open parent are preserved.
- [x] Bump LafazFlow from `0.11.9` to `0.12.0`.
- [x] Run focused tests, full tests, build, diff check, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Developer Literal Formatting v0.12.0
- Added `developer_literal_formatting` as an explicit post-processing stage after vocabulary and before target-context casing.
- Added conservative developer literal conversions for slash commands, `.env`, file names such as `components.json`, mentions, backticks, quotes, parentheses, brackets, and braces.
- Guarded normal English phrases such as `Slash is...`, `The dot is...`, `Quote the...`, and `Open parent...`.
- Bumped LafazFlow to `0.12.0`.
- Focused post-processing/controller/formatter/vocabulary tests pass, 243 tests.
- Full `dotnet test` passes, 509 tests.
- Full `dotnet build -c Release` passes with 0 warnings and 0 errors.
- `git diff --check` passes.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Declarative Opinion Question-Mark Hotfix v0.11.9
- [x] Add regressions for the reported `I don't think I can rely on DeepSeek... It is quite dumb?` output.
- [x] Treat embedded declarative opinion clauses as statements even when the sentence starts with a polite `can you` request.
- [x] Repair `deep seek`/`deepseek` casing to `DeepSeek`.
- [x] Bump LafazFlow from `0.11.8` to `0.11.9`.
- [x] Run focused tests, full tests, build, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Declarative Opinion Question-Mark Hotfix v0.11.9
- Root cause: question preservation trusted a model-emitted `?` when the sentence started with polite `can you`, even if the sentence also contained declarative opinion clauses such as `I don't think...` or `It is quite...`.
- Added declarative-opinion detection so those complaint/statement clauses end with `.` instead of `?`.
- Added exact regressions for the reported DeepSeek example and a second `I agree, can you...` shape.
- Added `deep seek`/`deepseek` casing repair to `DeepSeek`.
- Bumped LafazFlow to `0.11.9`.
- Focused formatter/vocabulary/post-processing tests pass, 201 tests.
- Full `dotnet test` passes, 493 tests.
- Full `dotnet build -c Release` passes with 0 warnings and 0 errors.
- `git diff --check` passes.

## Plan: Layered Transcription Post-Processing Pipeline v0.11.8
- [x] Add a `TranscriptionPostProcessor` boundary after raw ASR and before UI/paste.
- [x] Move existing vocabulary correction, target-context continuation casing, and trailing separator handling behind named post-processing stages.
- [x] Keep behavior compatible with v0.11.7 while making stage order explicit and testable.
- [x] Add a raw cleanup stage for low-risk filler/stutter cleanup inspired by VoiceInk and Handy.
- [x] Add tests for stage order, owner examples, filler cleanup, stutter cleanup, and disabled vocabulary behavior.
- [x] Bump LafazFlow from `0.11.7` to `0.11.8`.
- [x] Run focused tests, full tests, build, diff check, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Layered Transcription Post-Processing Pipeline v0.11.8
- Added `TranscriptionPostProcessor` as the recorder-side boundary after raw ASR and before UI/paste.
- The pipeline now records explicit stages: `raw_cleanup`, `vocabulary`, `target_context`, and `trailing_separator`.
- Moved recorder-side vocabulary correction, target-context continuation casing, and trailing separator handling behind the new pipeline while preserving existing behavior.
- Added low-risk raw cleanup for leading filler words such as `um`, `uh`, and `hmm`, plus repeated short-word stutters such as `wh wh wh`.
- Kept AI polish out of this release; the boundary is ready for a later constrained/local or provider-backed polish stage.
- Bumped LafazFlow to `0.11.8`.
- Focused post-processing/controller/formatter/vocabulary tests pass, 223 tests.
- Full `dotnet test` passes, 489 tests.
- Full `dotnet build -c Release` passes with 0 warnings and 0 errors.
- `git diff --check` passes.

## Plan: Hotkey Diagnostics v0.11.5
- [x] Add a separate privacy-safe `HOTKEY` diagnostic event stream beside existing `LATENCY` rows.
- [x] Log double Shift detection/rejection, dispatcher delay, recorder toggle decisions, and live preview lifecycle.
- [x] Show recent hotkey events in Settings > Diagnostics with refresh and clear actions.
- [x] Keep logs free of transcript text, audio paths, clipboard contents, user paths, typed characters, and full window titles.
- [x] Bump LafazFlow to v0.11.5.
- [x] Verify focused tests, full tests, build, publish stable artifacts, safety scan, commit, and push.

## Review: Hotkey Diagnostics v0.11.5
- Planning complete at `docs/superpowers/plans/2026-06-19-hotkey-diagnostics.md`.
- Added a separate local `HOTKEY` diagnostics stream and Settings > Diagnostics viewer.
- Instrumented double Shift detection/rejection, UI dispatcher receipt, recorder toggle state decisions, and live preview lifecycle.
- Hotkey diagnostic fields are compact and sanitized: event, gesture, accepted, state, dispatch_ms, reason, and target only.
- Path-shaped diagnostic values are defensively redacted before writing.
- Focused diagnostics/settings/hotkey tests pass, 92 tests.
- Full `dotnet test` passes, 462 tests.
- Full `dotnet build` passes with 0 warnings and 0 errors.
- `git diff --check` passes.
- Republished `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe` and `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Relaunched the pinned stable-single app; it is responding and reports file version `0.11.5.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Hotkey Responsiveness Hotfix v0.11.4
- [x] Confirm the delayed black shell and ignored stop gesture from logs and code flow.
- [x] Add regression coverage proving recorder startup does not wait for live preview startup.
- [x] Show the recorder shell before live preview work and start preview asynchronously.
- [x] Make live preview session replacement non-blocking so stale preview cleanup cannot freeze the UI dispatcher.
- [x] Update lessons from the owner-reported hotkey regression.
- [x] Bump LafazFlow to v0.11.4.
- [x] Verify focused tests, full tests, build, publish stable artifacts, safety scan, commit, and push.

## Review: Hotkey Responsiveness Hotfix v0.11.4
- Root cause: `StartRecording` started live preview before showing the mini recorder shell, and live preview startup synchronously waited for previous preview cleanup. If a stale preview Whisper process was slow to cancel, the UI dispatcher could delay shell visibility and queued double Shift stop handling.
- The recorder now starts capture, plays the start cue, shows the shell, and marks the shell visible before starting live preview.
- Live preview startup now runs asynchronously with logged failures, so final dictation control stays responsive.
- Rolling live preview session replacement now cancels old sessions without blocking the caller.
- Focused hotkey/controller tests pass, 29 tests.
- Full `dotnet test` passes, 446 tests.
- Full `dotnet build` passes with 0 warnings and 0 errors.
- Republished `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe` and `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Relaunched the pinned stable-single app; it is responding and reports file version `0.11.4.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Model Library Clarity Hotfix v0.11.3
- [x] Hide idle download 0% labels.
- [x] Change model badges from pill ovals to rounded rectangles.
- [x] Exclude auxiliary Silero/VAD files from transcription model cards.
- [x] Add About credit: Made by Aizzul Luqman, 2026.
- [x] Add regression tests for the above behavior.
- [x] Bump app version to v0.11.3.
- [x] Run verification, publish, safety scan, commit, and push.

## Review: Model Library Clarity Hotfix v0.11.3
- Clarified model-card labels, removed misleading auxiliary VAD entries from the transcription picker, and added the requested About credit.
- Idle download progress labels are hidden; active downloads show action text such as Downloading 42%.
- Status and metadata badges now use rounded rectangles instead of pill ovals.
- Auxiliary Silero/VAD files are filtered out of imported transcription model cards.
- About now includes Made by Aizzul Luqman, 2026.
- Focused Settings/model tests passed: 48 tests.
- Full dotnet test passed: 445 tests.
- dotnet build passed with 0 warnings and 0 errors.
- git diff --check passed.
- Published stable-single and stable-cuda-quality artifacts.
- Relaunched stable-single; Settings opens as LafazFlow Settings - v0.11.3.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as token.

## Plan: Double-Shift Reliability And Log Retention
- [x] Inspect recent LafazFlow hotkey logs and current double-Shift detector behavior.
- [x] Improve double-Shift reliability for natural repeated tapping patterns.
- [x] Add log retention so `%LocalAppData%\LafazFlow\Logs\lafazflow.log` does not grow without bound.
- [x] Add focused regression tests for hotkey detection and log trimming.
- [x] Run focused tests and summarize the observed log evidence.

## Review: Double-Shift Reliability And Log Retention
- Root cause from the active log: `already_down` appeared 3,263 times versus 1,840 successful `second_shift` detections, meaning the detector was often stuck waiting for a key-up event instead of seeing a normal second Shift tap.
- Changed `DoubleShiftDetector` so a non-repeat Shift-down inside the double-tap window self-heals missed key-up state and triggers as `second_shift_after_missed_keyup`.
- Kept held-key repeat protection intact; repeat key-downs still return `repeat` and do not trigger dictation.
- Added `BoundedLogFileWriter` and routed hotkey, latency, paste, recorder, and crash log appends through it.
- Log retention now trims oversized `lafazflow.log` files to recent timestamped entries, with a tail fallback if recent logs are still too large.
- Focused hotkey/log/crash tests pass, 16 tests; full `dotnet test` passes, 465 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.

## Plan: Declarative Question Mark Cleanup And Rebuild v0.11.6
- [x] Inspect punctuation formatter and current Whisper argument/model path.
- [x] Add a general cleanup for model-emitted question marks on declarative dictation.
- [x] Keep real question starters and short tag questions intact.
- [x] Bump LafazFlow from `0.11.5` to `0.11.6`.
- [x] Run focused/full tests and build.
- [x] Rebuild stable artifacts and relaunch the updated app.
- [x] Commit and push the verified implementation.

## Review: Declarative Question Mark Cleanup And Rebuild v0.11.6
- Root cause: the formatter only added question marks for clear question starters; it preserved question marks that the local Whisper model already emitted on declarative statements.
- Added a general declarative-question cleanup that converts statement-like sentence-final `?` to `.`, including examples like `currently I'm still using 0.11.5?` and `of course?`.
- Kept real question starters and short tag questions such as `right?` and `you know?`.
- Bumped LafazFlow to `0.11.6`.
- Focused formatter tests pass, 53 tests; full `dotnet test` passes, 472 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Republished `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe` and `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`, then relaunched the pinned stable-single app.

## Plan: Continuation Punctuation And Edge Cases Repair v0.11.7
- [x] Join high-confidence `so that` continuation clauses after bad sentence breaks.
- [x] Treat declarative `not only...` statements as statements even when the model emits `?`.
- [x] Repair `age cases` to `edge cases` in developer/problem/scenario contexts.
- [x] Bump LafazFlow from `0.11.6` to `0.11.7`.
- [x] Run focused formatter/vocabulary tests, full tests, build, and diff check.
- [x] Rebuild stable artifacts, relaunch the pinned app, commit, and push.

## Review: Continuation Punctuation And Edge Cases Repair v0.11.7
- Root cause: formatter cleanup did not join `So that` continuation clauses after bad model punctuation, and declarative detection did not cover `not only...` statements.
- Added `so that` continuation repair so `...? So that...` and `... . So that...` become `..., so that...`.
- Added `not only` as a declarative lead-in so statement-like sentences no longer keep a final question mark.
- Added context-bound `age cases` -> `edge cases` repair near developer/problem/scenario/test wording.
- Bumped LafazFlow to `0.11.7`.
- Focused formatter/vocabulary tests pass, 186 tests; full `dotnet test` passes, 477 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Republished `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe` and `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`, then relaunched the pinned stable-single app.

## Plan: Open Source Dictation Formatting Review
- [x] Inspect VoiceInk's deterministic cleanup, paragraph formatting, word replacement, and AI enhancement flow.
- [x] Inspect FluidVoice's dictation post-processing, spoken punctuation, literal developer formatting, and app-aware routing.
- [x] Inspect Handy's custom-word fuzzy matching, raw output filtering, structured AI post-processing, and default cleanup prompt.
- [x] Document the LafazFlow output-quality direction as a layered pipeline instead of one-off formatter patches.
- [x] Capture a lesson from the owner's correction.

## Review: Open Source Dictation Formatting Review
- Wrote `docs/superpowers/research/2026-07-18-dictation-formatting-open-source-review.md`.
- Key finding: VoiceInk, FluidVoice, and Handy separate raw ASR from processed output and use multiple stages, while LafazFlow's current formatter is carrying too much responsibility.
- Recommended LafazFlow direction: introduce a `TranscriptionPostProcessor` boundary with raw cleanup, vocabulary/phonetic repair, developer literal formatting, intent punctuation repair, and optional constrained AI polish.
- Near-term slice should create the pipeline boundary first, then move existing formatter/vocabulary logic into named stages before adding model-based polishing.

## Plan: Model Library Card Polish v0.11.2
- [x] Audit the v0.11.1 model-card design complaint.
- [x] Fix model title contrast with explicit primary foreground.
- [x] Replace one-color status badge with state-coded badge colors.
- [x] Clarify model file size vs memory usage labels.
- [x] Replace generic speed/accuracy progress bars with compact score/dot meters.
- [x] Add regression coverage for card readability bindings.
- [x] Bump app version to v0.11.2.
- [x] Run verification, publish, safety scan, commit, and push.

## Review: Model Library Card Polish v0.11.2
- Card visual hierarchy, badge semantics, and metric presentation were upgraded while preserving the existing model-library actions and runtime behavior.
- Model names now explicitly use the primary text color for dark-background legibility.
- Status badges are state-coded for Active, Installed, Missing, and Downloading.
- Model file size and memory usage are labeled separately.
- Speed and accuracy now use compact score/dot meters instead of generic progress bars.
- Focused Settings tests passed: 38 tests.
- Full dotnet test passed: 442 tests.
- dotnet build passed with 0 warnings and 0 errors.
- git diff --check passed.
- Published stable-single and stable-cuda-quality artifacts.
- Relaunched stable-single; Settings opens as LafazFlow Settings - v0.11.2.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as token.

## Plan: Model Library Crash Hotfix v0.11.1
- [x] Investigate crash logs and Windows Application event logs.
- [x] Identify root cause: ProgressBar read-only bindings were loaded as TwoWay.
- [x] Patch model-card ProgressBar bindings to Mode=OneWay.
- [x] Add XAML regression coverage for read-only model progress bindings.
- [x] Bump app version to v0.11.1.
- [x] Run full verification, publish, safety scan, commit, and push.

## Review: Model Library Crash Hotfix v0.11.1
- Crash root cause: Settings > Models loaded ProgressBar bindings for SpeedPercent, AccuracyPercent, and DownloadProgressPercent without Mode=OneWay, causing WPF to throw a XamlParseException for read-only properties.
- Fixed the bindings and added a regression test.
- Focused SettingsWindowXamlTests passed: 15 tests.
- Full dotnet test passed: 442 tests.
- dotnet build passed with 0 warnings and 0 errors.
- git diff --check passed.
- Published stable-single and stable-cuda-quality artifacts.
- Relaunched stable-single; Settings opens as LafazFlow Settings - v0.11.1.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as token.

## Plan: Model Library UI v0.11.0
- [x] Audit the macOS reference workflow's model management structure.
- [x] Compare it with LafazFlow Windows Settings > Models.
- [x] Write implementation plan for model catalog, install state, cards, actions, and advanced paths.
- [x] Implement model catalog and install-state service.
- [x] Redesign Settings > Models around model cards.
- [x] Add in-app model download/import/use/delete actions.
- [x] Verify, publish, safety scan, commit, and push.

## Review: Model Library UI v0.11.0
- Planning doc written at docs/superpowers/plans/2026-06-12-model-library-ui.md.
- Current gap: LafazFlow Windows exposes raw runtime/model paths first, while the target UX needs a user-facing local model library with model cards, speed/accuracy/size metadata, install state, and direct actions.
- Added Whisper-only local model catalog for Base English, Small English, Medium English, and Large v3 Turbo Quantized.
- Added local model library service for install detection, import, safe delete, and download-with-temp-file behavior.
- Redesigned Settings > Models around model cards and moved raw path controls into Advanced Runtime Paths.
- Bumped app version to v0.11.0.
- Full dotnet test passed: 441 tests.
- dotnet build passed with 0 warnings and 0 errors.
- git diff --check passed.
- Published stable-single and stable-cuda-quality artifacts.
- Relaunched stable-single; Settings opens as LafazFlow Settings - v0.11.0.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as token.

## Plan: Settings Contrast Hotfix v0.10.23
- [x] Audit owner screenshots for unreadable Settings controls.
- [x] Keep the mini recorder shell out of scope for this fix.
- [x] Add explicit dark styles/templates for Settings ComboBox, Button, and DataGrid controls.
- [x] Add XAML regression checks for native dark-control styling.
- [x] Bump the patch version to v0.10.23.
- [x] Run full test/build/publish verification.
- [x] Relaunch the stable Windows app for owner review.
- [x] Run public repo safety scan.
- [x] Commit and push the contrast hotfix.

## Review: Settings Contrast Hotfix v0.10.23
- Native WPF control chrome now uses explicit dark Settings styles instead of leaking light defaults.
- ComboBox closed fields/dropdowns, Settings buttons, and Diagnostics DataGrid headers/cells/rows received dark contrast styles.
- Regression coverage added in SettingsWindowXamlTests.
- Focused Settings XAML tests passed: 13 tests.
- Full dotnet test passed: 426 tests.
- dotnet build passed with 0 warnings and 0 errors.
- git diff --check passed.
- Published artifacts/stable-single/LafazFlow.Windows and artifacts/stable-cuda-quality/LafazFlow.Windows.
- Relaunched stable-single; process is responding with LafazFlow Settings - v0.10.23.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as token.
- Committed and pushed as `7fb843d fix: improve settings contrast`.

## Plan
- [x] Review current Windows repo instructions and lessons.
- [x] Inspect macOS reference behavior for recorder UI, state, hotkeys, and paste flow.
- [x] Verify local Windows development toolchain.
- [x] Write Windows MVP design spec.
- [x] Write first implementation plan.
- [x] Review docs for ambiguity, accidental secrets, and public-readiness.
- [x] Commit and push planning docs for owner review.
- [x] Revise default hotkey to double Shift.
- [x] Document Windows development and runtime prerequisites.
- [x] Update implementation plan for low-level keyboard hook support.
- [x] Update lessons from owner correction.
- [x] Commit and push revision docs.
- [x] Plan Cursor paste reliability and offline accuracy upgrade.
- [x] Add regression tests for settings migration, Whisper prompts, and vocabulary corrections.
- [x] Implement robust clipboard restore defaults and SendInput paste.
- [x] Implement large turbo model preference and offline prompt/vocabulary correction.
- [x] Document large turbo install path.

## Review
- Design spec written at `docs/superpowers/specs/2026-05-12-windows-mvp-design.md`.
- Implementation plan written at `docs/superpowers/plans/2026-05-12-windows-mvp.md`.
- Local toolchain check: .NET SDK 9.0.313 is installed; CMake is not installed.
- Public-readiness scan found no credentials. Matches are documentation references to words such as "secret", "token", and `CancellationToken`.
- Planning docs pushed in commit `32e2999`.
- Default Windows hotkey revised to double Shift within 350 ms.
- Install prerequisites documented for development, runtime transcription, and optional future native `whisper.cpp` builds.
- Revision pushed in commit `cf2dd65`.
- Task 1 scaffold completed in commit `e274692`; `dotnet build` passed and placeholder tests were removed.
- Task 2 settings store completed in commit `f842ea5`; settings tests were written red-first, then passed.
- Full `dotnet test` after Task 2: pass, 2 tests.
- Task 3 Whisper CLI service completed in commit `2caf58c`; tests were written red-first, then passed.
- Full `dotnet test` after Task 3: pass, 6 tests.
- `dotnet build` after Task 3: pass.
- Task 4 floating recorder shell completed in commit `b81e4f4`; view-model tests were written red-first, then passed.
- `dotnet build` after Task 4: pass.
- Full `dotnet test` after Task 4: pass, 11 tests.
- App launch smoke check after Task 4: pass; WPF app stayed running and was stopped cleanly.
- Task 5 workflow wiring completed: double Shift detector, keyboard hook, microphone capture, local Whisper controller, and clipboard paste service.
- `dotnet build` after Task 5: pass.
- Full `dotnet test` after Task 5: pass, 15 tests.
- App launch smoke check after Task 5: pass; WPF app stayed running and was stopped cleanly.
- Output quality pass completed: added local transcript formatter and stronger Whisper CLI arguments (`-nt`, `-tp 0`).
- `dotnet build` after output quality pass: pass.
- Full `dotnet test` after output quality pass: pass, 18 tests.
- Error diagnostics pass completed: recorder now shows detailed error text and writes logs to `%LocalAppData%\LafazFlow\Logs\lafazflow.log`.
- Clipboard paste hardened with retry logic for transient Windows clipboard locks.
- `dotnet build` after diagnostics pass: pass.
- Full `dotnet test` after diagnostics pass: pass, 19 tests.
- Paste separator bug fixed: local dictation output now appends one trailing whitespace separator by default so consecutive dictations do not glue together after punctuation.
- Regression tests added for paste separator behavior and default settings.
- `dotnet build` after paste separator fix: pass.
- Full `dotnet test` after paste separator fix: pass, 22 tests.
- Cursor/quality upgrade implementation started from owner-approved plan: robust clipboard restore, `ggml-large-v3-turbo.bin` preference, Whisper prompt support, and local vocabulary corrections.
- Cursor/quality upgrade verification: `dotnet build` pass; full `dotnet test` pass, 27 tests.
- App launch smoke check after Cursor/quality upgrade: pass; app started and was stopped cleanly.
- Public-readiness scan after Cursor/quality upgrade found no credentials. Matches are documentation references and `CancellationToken`.
- Cursor paste regression investigation: Whisper transcription succeeded and generated `.txt` output, so the failure was isolated to clipboard restore/paste behavior.
- Cursor/VS Code paste fallback added: transcript remains on clipboard for Cursor-like targets after paste is attempted.
- Cursor paste fallback verification: `dotnet build` pass; full `dotnet test` pass, 29 tests.
- Public-readiness scan after Cursor paste fallback found no credentials. Matches are documentation references and `CancellationToken`.
- Cross-app paste failure investigation: clipboard contained the transcript, so transcription and clipboard set worked; `SendInput` native `INPUT` struct was 32 bytes instead of the expected 40 bytes on 64-bit Windows.
- SendInput interop fixed and now throws a visible error if key dispatch fails.
- SendInput interop verification: targeted native structure test pass; `dotnet build` pass; full `dotnet test` pass, 30 tests.
- Public-readiness scan after SendInput interop fix found no credentials. Matches are documentation references and `CancellationToken`.
- Model latency correction: full `ggml-large-v3-turbo.bin` was too slow for rapid dictation, so default priority now prefers `ggml-large-v3-turbo-q5_0.bin`.
- Added `scripts/install-fast-dictation-model.ps1` for the preferred 547 MiB quantized model.
- Installed `C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin` locally for the owner.
- Q5 model priority verification: targeted settings tests pass; `dotnet build` pass; full `dotnet test` pass, 30 tests.
- App launch smoke after Q5 priority change: pass; app started and was stopped cleanly.
- Public-readiness scan after Q5 priority change found no credentials. Matches are documentation references and `CancellationToken`.
- Latency benchmark on `Hey, my name is Lucas. Can you tell me your name?`: `ggml-base.en.bin` with 16 threads was about 0.59s; `ggml-large-v3-turbo-q5_0.bin` with 16 threads was about 7.14s.
- Default model priority changed back to `ggml-base.en.bin` for real-time dictation speed, with Q5 retained as optional quality mode.
- Whisper CLI arguments now include `-t 16` by default on this machine.
- Added offline vocabulary corrections for `MediBrave` variants: `Maddy Breath`, `medibrief`, `Mad brave`, `medi brave`, and `maddy brave`.
- MediBrave vocabulary regression tests pass, 7 targeted tests.

## Plan: the macOS reference app Parity UX Slice 1
- [x] Add view-model support for a small recent transcript queue.
- [x] Show the most recent completed transcript in the mini recorder shell.
- [x] Add a processing pulse so transcribing feels alive instead of static.
- [x] Keep transcription/model/paste behavior unchanged.
- [x] Verify with focused view-model tests, full build/test, launch smoke, and public-readiness scan.

## Review: the macOS reference app Parity UX Slice 1
- Added an in-memory recent transcript queue capped at 5 items.
- Mini recorder now shows the latest completed transcript as a compact preview below the shell.
- Transcribing/enhancing status now pulses with animated dots, and processing bars continue moving instead of freezing.
- Focused view-model tests pass; full `dotnet build` and `dotnet test` pass, 38 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Fix Main Bar Stability And Sound Cues
- [x] Replace preview `StackPanel` layout with a non-shifting overlay above the fixed recorder shell.
- [x] Add local system sound cues for recording start, stop/transcribing, completion, and error.
- [x] Keep the transcription model, vocabulary, and paste behavior unchanged.
- [x] Verify with build/test, launch smoke, and public-readiness scan.

## Review: Fix Main Bar Stability And Sound Cues
- Main recorder shell is fixed at bottom-center again; transcript preview overlays above it and no longer shifts the bar.
- Added local Windows system sound cues for recording start, transcribing start, completion, and error.
- Full `dotnet build` and `dotnet test` pass, 38 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Remove Bad Sound Cues And Add the macOS reference app Vocabulary
- [x] Remove Windows notification/error-style sound cues.
- [x] Add offline vocabulary corrections for `the macOS reference app` variants: `app namek`, `app name`, and `app name`.
- [x] Verify with targeted vocabulary tests, full build/test, launch smoke, and public-readiness scan.

## Review: Remove Bad Sound Cues And Add the macOS reference app Vocabulary
- Muted the current system sound cue implementation because Windows notification sounds felt like OS errors.
- Added deterministic offline `the macOS reference app` corrections for `app namek`, `app name`, and `app name`.
- Targeted vocabulary tests pass, 10 tests; full `dotnet build` and `dotnet test` pass, 41 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Reference Recorder Mechanics Slice
- [x] Update lessons from the fixed-recorder-shell correction.
- [x] Add view-model tests for processing dots instead of processing text.
- [x] Match the compact the macOS reference app recorder shell dimensions more closely: 184px wide, 40px tall, fixed bottom anchor.
- [x] Replace transcribing/enhancing center text with a five-dot processing indicator.
- [x] Keep transcript preview layered above the shell without shifting the main bar.
- [x] Verify with focused tests, full build/test, launch smoke, and public-readiness scan.

## Review: Reference Recorder Mechanics Slice
- Added a layout-stability lesson for the mini recorder shell.
- Processing states now expose a five-step processing indicator instead of mutating center text.
- Mini recorder shell now uses a fixed 184px by 40px compact bar, closer to the reference compact dimensions.
- Transcribing/enhancing now show five pulsing dots in the center, while error details still use text.
- Transcript preview remains layered above the shell and does not participate in the shell layout.
- Focused `MiniRecorderViewModelTests` pass, 9 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 41 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Cursor Terminal Paste And Test Correction
- [x] Capture lessons from the `test`/`that's` and Cursor terminal paste corrections.
- [x] Add failing tests for targeted testing-dictation correction.
- [x] Add failing tests for Cursor/VS Code paste key selection.
- [x] Implement targeted offline correction for testing phrases without globally rewriting normal `that's`.
- [x] Use `Ctrl+Shift+V` for Cursor/VS Code targets while keeping `Ctrl+V` for normal apps.
- [x] Verify with focused tests, full build/test, launch smoke, and public-readiness scan.

## Review: Cursor Terminal Paste And Test Correction
- Added contextual offline correction for testing phrases where local Whisper hears `test` as `that's`.
- Preserved ordinary `that's` sentences such as `That's correct`.
- Added paste key gesture policy: Cursor/VS Code targets use `Ctrl+Shift+V`; generic apps keep `Ctrl+V`.
- Focused vocabulary/paste policy tests pass, 19 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 50 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Rapid Dictation Queue
- [x] Add red tests for sequential transcription queue behavior.
- [x] Implement in-memory sequential dictation queue.
- [x] Add red tests for double Shift triggering on second key-down without repeat spam.
- [x] Implement key-down double Shift detection.
- [x] Add red tests for queue-aware mini recorder state.
- [x] Implement pending queue UI state.
- [x] Add red tests for `rapidness` vocabulary correction.
- [x] Add the offline `rapidness` correction.
- [x] Refactor recorder controller to enqueue completed recordings and allow immediate next recording while previous jobs process.
- [x] Verify with build, full tests, launch smoke, public-readiness scan, then commit and push.

## Review: Rapid Dictation Queue
- Added an in-memory sequential dictation queue that processes/pastes completed recordings in order.
- Stopping a recording now enqueues the audio and returns the recorder to idle immediately, so another double Shift can start the next dictation while previous audio is still processing.
- Each queued job keeps its original target window for paste.
- Mini recorder processing dots now stay active for pending background transcriptions and hide while actively recording.
- Double Shift now triggers on the second key-down instead of waiting for key-up, with repeat suppression.
- Added offline `repeteness` -> `rapidness` vocabulary correction.
- Focused queue/controller/hotkey/view-model/vocabulary tests pass; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 59 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Fix Queued Clipboard STA Regression
- [x] Inspect runtime log for the clipped `Clipboard data coul...` recorder error.
- [x] Add a regression test proving queued paste runs through the recorder window dispatcher.
- [x] Marshal queued paste operations back to the WPF STA dispatcher.
- [x] Verify with focused tests, full build/test, launch smoke, public-readiness scan, then commit and push.

## Review: Fix Queued Clipboard STA Regression
- Root cause: queued transcription jobs run on background MTA threads, while WPF clipboard/OLE APIs require STA.
- Queued paste now runs through the mini recorder window dispatcher; Whisper transcription still runs in the background.
- Added a controller regression test proving queued paste is invoked through the window dispatcher.
- Focused `RecorderControllerTests` pass, 3 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 60 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Add Commit And Shadcn Vocabulary
- [x] Add failing tests for `commit` and `shadcn` dictation variants.
- [x] Add offline vocabulary corrections without globally rewriting normal `come in`.
- [x] Verify with focused vocabulary tests, full build/test, launch smoke, public-readiness scan, then commit and push.

## Review: Add Commit And Shadcn Vocabulary
- Added offline corrections for `comit`, `git come in`, `git comes in`, `come in and push`, and `comes in and push`.
- Preserved normal `come in` sentences outside coding/push contexts.
- Added offline corrections for `Chat CN`, `ChatCN`, `shad cn`, and `shad c n` to `shadcn`.
- Focused `VocabularyCorrectionServiceTests` pass, 23 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 68 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.

## Plan: Scrub Third-Party Trademark References
- [x] Scan tracked repository content for the trademarked reference app name and close variants.
- [x] Replace public docs/task wording with neutral LafazFlow/macOS reference workflow wording.
- [x] Rename the optional model install script to a neutral filename.
- [x] Remove vocabulary correction code/tests that emitted the trademarked name.
- [x] Verify with focused tests, full build/test, launch smoke, public-readiness scan, and a clean trademark scan.

## Review: Scrub Third-Party Trademark References
- Current tracked files no longer mention the trademarked reference app name or close variants.
- Renamed `scripts/install-fast-dictation-model.ps1` and updated README/docs references.
- Removed the app-name vocabulary correction that produced the trademarked output.
- Focused `VocabularyCorrectionServiceTests` pass, 20 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 65 tests.
- App launch smoke passed; public-readiness scan found no credentials. Trademark scan found no current tracked-file matches.

## Plan: Improve Shadcn Dictation And Stop Hotkey Reliability
- [x] Add failing tests for newly observed `shadcn` misrecognitions.
- [x] Add failing tests for a more forgiving double Shift stop gesture and stale key-down recovery.
- [x] Add offline `shadcn` corrections for the newly observed phrases.
- [x] Widen double Shift timing and recover if a Shift key-up is missed.
- [x] Verify with focused tests, full build/test, launch smoke, public-readiness scan, trademark scan, then commit and push.

## Review: Improve Shadcn Dictation And Stop Hotkey Reliability
- Added offline corrections for the newly observed `shadcn` variants: `Chet's the end`, `Shut CN`, and `Sh*t's the end`.
- Increased double Shift detection from 350 ms to 500 ms and added stale key-down recovery so a missed Shift key-up does not block the next double-tap.
- Focused `DoubleShiftDetectorTests` and `VocabularyCorrectionServiceTests` pass, 29 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 70 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.
- Trademark scan found no current tracked-file matches.

## Plan: Expand Shadcn Phonetic Vocabulary
- [x] Add failing tests for the latest observed `shadcn` phonetic outputs.
- [x] Add deterministic offline corrections for those variants.
- [x] Verify with focused tests, full build/test, public-readiness scan, trademark scan, then commit and push.

## Review: Expand Shadcn Phonetic Vocabulary
- Added offline corrections for the latest observed `shadcn` variants: `Shit, CN`, `Shut the end`, `Sh*t-C-N`, `Shut-see-in`, `Shat-C-N`, and `Shetxian`.
- Focused `VocabularyCorrectionServiceTests` pass, 29 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 76 tests.
- App launch smoke passed after stopping the previous running LafazFlow process that locked the Windows executable.
- Public-readiness scan found no credentials. Matches are documentation references and `CancellationToken`.
- Trademark scan found no current tracked-file matches.

## Plan: Bundle GPL Sound Cues
- [x] Copy the exact cue assets from the local macOS reference repo into the Windows app resources.
- [x] Add GPLv3 license text and third-party notice attribution for the bundled sound assets.
- [x] Implement asset-backed sound playback without Windows system notification sounds.
- [x] Add tests for sound cue asset mapping and bundled file presence.
- [x] Verify with focused tests, full build/test, launch smoke, and public-readiness scan, then commit and push.

## Review: Bundle GPL Sound Cues
- Bundled `recstart.mp3`, `recstop.mp3`, `pastess.mp3`, and `esc.wav` under `src/LafazFlow.Windows/Resources/Sounds`.
- Added GPLv3 license text and third-party notice attribution for the bundled sound cue assets.
- Replaced the no-op sound service with NAudio-backed playback from bundled assets: start, stop/transcribing, completion, and error.
- Added `SoundCueServiceTests` for cue-to-file mapping, missing-asset behavior, playback dispatch, and copy-to-output.
- Focused `SoundCueServiceTests` pass, 7 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 83 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are GPL/docs words such as `password` and `secret`.
- Attribution scan now intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Bottom Mini Recorder UI Parity
- [x] Add tests for smoothed audio levels and live transcript readiness.
- [x] Add tests for recorder layout constants, visualizer height math, and processing rhythm.
- [x] Implement EMA audio smoothing and partial transcript state.
- [x] Add testable visual constants and visualizer calculations matching the macOS reference workflow.
- [x] Polish WPF shell material, side-label opacity, fade transitions, and processing dot timing.
- [x] Verify with focused tests, full build/test, launch smoke, public-readiness scan, then commit and push.

## Review: Bottom Mini Recorder UI Parity
- Added reference-style audio meter smoothing, reset-on-stop behavior, and live transcript readiness state without faking live text.
- Added testable mini recorder visual constants and visualizer height calculation matching the bottom mini recorder reference dimensions and 15-bar behavior.
- Polished the WPF shell with black-glass styling, subtle side labels, softer border/shadow, 200 ms state fades, 180 ms processing dot rhythm, and prepared 184-to-300 px expansion for future live text.
- Focused mini recorder UI tests pass, 21 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 92 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are GPL/docs words such as `password` and `secret`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Add Luqman Owner Name Vocabulary
- [x] Add failing tests for observed `Luqman` name variants and spelled-out form.
- [x] Add `Luqman` to the default Whisper prompt.
- [x] Add deterministic offline corrections for `Lukamine`, `Lukman`, `Luqmen`, `L-U-Q-M-A-N`, and `S-N-L-U-Q-M-E-N`.
- [x] Verify with focused tests, full build/test, launch smoke, public-readiness scan, then commit and push.

## Review: Add Luqman Owner Name Vocabulary
- Added `Luqman` to the default Whisper prompt so new settings include the owner name as local context.
- Added offline corrections for observed name variants and spelled forms: `Lukamine`, `Lukman`, `Luqmen`, `L-U-Q-M-A-N`, and `S-N-L-U-Q-M-E-N`.
- Focused vocabulary/settings tests pass, 39 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 97 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are GPL/docs words such as `password` and `secret`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Settings And Model UX
- [x] Add a right-click settings entry point to the mini recorder shell.
- [x] Add a settings window for Whisper CLI path, model path, threads, paste behavior, preview, vocabulary, and diagnostics toggles.
- [x] Validate settings before save and keep invalid changes out of persisted config.
- [x] Show local settings, logs, and recordings folders with open-folder actions.
- [x] Verify with focused tests, full build/test, public-readiness scan, launch smoke, then commit and push.

## Review: Settings And Model UX
- Added a right-click settings entry point on the floating mini recorder.
- Added a settings window for local Whisper paths, model path, threads, preview/vocabulary/paste behavior, clipboard restore delay, and diagnostics retention.
- Invalid Whisper CLI/model paths are rejected before saving, and numeric settings are clamped to safe local values.
- Settings, logs, and recordings folders are shown with open-folder actions.
- Focused settings tests pass, 5 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 123 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.

## Review: Fix Settings Window Crash
- Root cause: read-only folder display fields used default TwoWay `TextBox.Text` bindings against getter-only properties.
- Fixed the folder fields to bind one-way and added a XAML regression test for the binding mode.
- Focused settings tests pass, 6 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 124 tests.
- Manual right-click smoke passed: right-clicking the actual mini recorder shell opened `LafazFlow Settings` and the app stayed running.
- Public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.

## Plan: Latency Instrumentation
- [x] Add a privacy-safe latency trace model and log formatter.
- [x] Carry latency traces through recording, queueing, transcription, formatting, UI update, paste, cleanup, and failure paths.
- [x] Append one local `LATENCY` summary line per completed or failed dictation job.
- [x] Add regression tests for stage timing, privacy-safe formatting, success reporting, and failure reporting.
- [x] Verify with focused tests, full build/test, public-readiness scan, launch smoke, then commit and push.

## Review: Latency Instrumentation
- Added local-only latency traces for recording setup, recording duration, stop-to-queue, queue wait, Whisper, post-processing, UI update, paste, cleanup, and totals.
- Added one safe `LATENCY` line per completed or failed dictation job in `%LocalAppData%\LafazFlow\Logs\lafazflow.log`.
- Latency log lines include model filename, thread count, target process name, stage timings, status, and exception type only; transcript text and full local paths are not logged.
- Focused latency/controller tests pass, 5 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 129 tests.
- App launch smoke passed; public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.

## Plan: Stop Responsiveness UX Tuning
- [x] Add regression tests for immediate stop feedback before audio stop completes.
- [x] Add regression tests that prevent a second toggle from starting a new recording during stop handoff.
- [x] Add regression tests that allow a new recording after the stopped job is queued.
- [x] Move audio stop/final queue handoff off the UI path while keeping final transcription authoritative.
- [x] Verify with focused tests, full build/test, public-readiness scan, launch smoke, then commit and push.

## Review: Stop Responsiveness UX Tuning
- Stopping now switches the recorder into the transcribing handoff state and plays the stop cue before audio stop/finalization completes.
- Audio stop and queue handoff now run in the background, so the mini bar can repaint processing dots during the stop-to-queue gap.
- Double Shift is ignored during the short stop handoff, then rapid next recording is allowed again once the stopped job is queued.
- Clipboard restore defaults, local model behavior, final transcript authority, and paste policy are unchanged.
- Focused stop-handoff tests pass, 3 tests; focused controller/view-model tests pass, 30 tests.
- Full `dotnet test` passes, 132 tests; full `dotnet build` passes with 0 warnings.
- App launch smoke passed; public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.

## Plan: Live Preview Calmness Cleanup
- [x] Keep live preview enabled while slowing preview cadence and rolling-window churn.
- [x] Skip preview transcription when too little new audio has arrived.
- [x] Replace per-suppression preview logs with one aggregate session summary.
- [x] Preserve final transcription/paste authority and all local/offline behavior.
- [x] Verify with focused preview tests, full build/test, public-readiness scan, launch smoke, then commit and push.

## Review: Live Preview Calmness Cleanup
- Live preview stays enabled by default, but preview cadence is calmer: 2200 ms interval, 1800 ms minimum audio, 6000 ms rolling window, and 1000 ms minimum new audio.
- Preview Whisper snapshots are skipped when too little new audio arrived since the previous attempt.
- Per-suppression log spam is replaced by one session summary with attempted, accepted, duplicate, regressive, and empty counts.
- Final transcription, queueing, paste, clipboard restore, and model defaults are unchanged.
- Focused preview/stabilizer tests pass, 10 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 139 tests.
- Stable publish/launch smoke passed from `artifacts\stable-preview-calm\LafazFlow.Windows\LafazFlow.Windows.exe`; public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Clipboard Bad Data Paste Recovery
- [x] Reproduce the failure from logs and identify the failing clipboard boundary.
- [x] Make clipboard restore snapshot best-effort so invalid existing clipboard data does not block transcript paste.
- [x] Add regression coverage for unreadable clipboard formats and failed snapshot fallback.
- [x] Update lessons with the owner correction pattern.
- [x] Verify with focused clipboard tests, full build/test, public-readiness scan, stable launch smoke, then commit and push.

## Review: Clipboard Bad Data Paste Recovery
- Root cause: clipboard restore snapshotting read every existing clipboard format before paste, and Antigravity exposed a bad format that threw `CLIPBRD_E_BAD_DATA`.
- Clipboard restore is now best-effort: unreadable formats are skipped, and a failed previous-clipboard snapshot no longer blocks writing and pasting the transcript.
- Added `ClipboardDataObjectSnapshot` regression coverage for mixed readable/unreadable formats, all-unreadable data, and unreadable format lists.
- Updated `tasks\lessons.md` so future clipboard restore work preserves paste as the primary behavior.
- Focused clipboard tests pass, 12 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 142 tests.
- Stable publish/launch smoke passed from `artifacts\stable-clipboard-fix\LafazFlow.Windows\LafazFlow.Windows.exe`; public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Later Today Backlog: Native-Feel Improvement Tracks
- True local streaming preview / faster preview backend while keeping final transcript authoritative.
- Latency viewer / diagnostics panel using existing local `LATENCY` logs.
- Installer and release packaging with clean Windows setup guidance.
- Advanced formatting and vocabulary, including developer dictation terminology.

## Plan: Developer Dictation Cleanup
- [x] Capture the owner-provided bad/good transcript pair as regression coverage.
- [x] Add deterministic offline cleanup for high-confidence technical dictation errors.
- [x] Expand the default local Whisper prompt with developer/shadcn vocabulary.
- [x] Preserve local/offline behavior and final transcript authority.
- [x] Verify with focused formatter/vocabulary/settings tests, full build/test, public-readiness scan, stable launch smoke, then commit and push.

## Review: Developer Dictation Cleanup
- Added regression coverage for the owner-provided bad/good developer dictation example.
- Added offline cleanup for `reuse whatever we use have`, `Install one's reuse forever`, protected skill-token spacing, and the observed command sentence punctuation.
- Expanded the default local Whisper prompt with developer terms including `shadcn/ui`, `components.json`, `Radix UI`, `Tailwind CSS`, `FieldGroup`, `InputGroup`, `npx shadcn@latest`, and `build-web-apps:shadcn`.
- Bumped settings schema and migrated only the previous default prompt to the expanded developer prompt; custom prompts are preserved.
- Focused vocabulary/settings tests pass, 42 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 145 tests.
- Stable publish/launch smoke passed from `artifacts\stable-dev-dictation-cleanup\LafazFlow.Windows\LafazFlow.Windows.exe`; public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Latency Viewer Diagnostics Panel
- [x] Add a parser for existing local `LATENCY` log lines.
- [x] Add clear-history behavior that removes only latency lines and preserves other logs.
- [x] Show the latest 20 latency rows inside Settings diagnostics.
- [x] Add Refresh, Open Logs, and Clear Latency actions.
- [x] Preserve privacy-safe diagnostics: no transcript text, no full paths, no audio data.
- [x] Verify with focused diagnostics tests, full build/test, public-readiness scan, stable launch smoke, then commit and push.

## Review: Latency Viewer Diagnostics Panel
- Added a local latency diagnostics reader that parses existing `LATENCY key=value` lines into recent rows and ignores malformed/non-latency log lines.
- Added Clear Latency behavior that rewrites `lafazflow.log` while preserving non-latency logs.
- Extended Settings diagnostics with a recent latency table and Refresh, Open Logs, and Clear Latency actions.
- The viewer uses the existing privacy-safe fields only: status, target, model filename, stage timings, totals, and exception type.
- Focused latency/settings diagnostics tests pass, 18 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 155 tests.
- Stable publish/launch smoke passed from `artifacts\stable-latency-viewer\LafazFlow.Windows\LafazFlow.Windows.exe`; public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Windows Shell UX Polish
- [x] Stop showing the mini recorder bar on app startup.
- [x] Add a tray icon with Settings, Open Logs, and Exit actions.
- [x] Update tray tooltip status for idle, recording, transcribing, pending jobs, and errors.
- [x] Keep the single-instance mutex and signal the existing instance to open Settings on second launch.
- [x] Verify with focused shell UX tests, full build/test, public-readiness scan, stable launch smoke, then commit and push.

## Review: Windows Shell UX Polish
- Startup now initializes the app in silent idle mode: hotkeys start, but the mini recorder bar stays hidden until dictation, processing, or errors need it.
- Added a Windows tray icon using the app icon, with Settings, Open Logs, and Exit LafazFlow actions.
- Added tray status text for idle, recording, transcribing, pending transcription, and error states.
- Second launches still fail the single-instance mutex, but now signal the already-running instance to open/focus Settings before exiting.
- Focused shell UX tests pass, 12 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 164 tests.
- Stable publish/launch smoke passed from `artifacts\stable-shell-polish\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Second-launch process smoke passed: the second process exited and the running process count stayed at one.
- Public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Strip Blank Audio Markers
- [x] Add regression tests for Whisper `[BLANK_AUDIO]` marker leaks at the start, middle, and end of transcripts.
- [x] Remove bracketed audio-status metadata markers in the transcript formatter before final paste.
- [x] Update lessons so ASR metadata markers are treated as non-user content.
- [x] Verify with focused formatter tests, full build/test, public-readiness scan, stable launch smoke, then commit and push.

## Review: Strip Blank Audio Markers
- Root cause: Whisper can emit `[BLANK_AUDIO]` as non-speech metadata, and `TranscriptionTextFormatter` only removed timestamps, whitespace noise, and spaces before punctuation.
- Added formatter cleanup for bracketed audio markers such as `[BLANK_AUDIO]`, including casing/spacing variants.
- Focused formatter tests pass, 8 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 169 tests.
- Stable publish/launch smoke passed from `artifacts\stable-strip-blank-audio\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Public-readiness scan found no credentials. Matches are GPL/docs words and `CancellationToken`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Strip Non-Speech Markers And Continue Casing
- [x] Add regression tests for bracketed non-speech markers such as `[MUSIC PLAYING]`.
- [x] Add regression tests for continuation casing after existing comma/colon/semicolon context.
- [x] Extend transcript cleanup to remove known Whisper metadata markers without deleting normal bracketed user text.
- [x] Add a best-effort focused text context reader and apply lowercase continuation only when target context is available.
- [x] Update lessons with the owner correction pattern.
- [x] Verify with focused tests, full build/test, public-readiness scan, stable launch smoke, then commit and push.

## Review: Strip Non-Speech Markers And Continue Casing
- Root cause: the formatter only stripped blank/silence/no-audio markers, so Whisper metadata such as `[MUSIC PLAYING]` leaked into final paste.
- Added known non-speech marker cleanup for music, laughter, applause, noise, background noise, and inaudible captions while preserving normal bracketed user text.
- Added best-effort target text context through Windows UI Automation and continuation casing for comma/colon/semicolon-style context.
- Continuation casing preserves acronyms and the pronoun `I`; if an app does not expose focused text context, LafazFlow falls back to the existing sentence-start behavior.
- Focused formatter/controller tests pass, 22 tests; full `dotnet test` passes, 183 tests.
- Full `dotnet build` passes with 0 warnings.
- Published and launch-smoked the stable build from `artifacts\stable-context-casing-markers\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.

## Plan: Offline Quality Profile And CUDA Readiness
- [x] Add red tests for Fast/Quality profile settings and public-default model behavior.
- [x] Add red tests for CUDA backend path selection, VAD validation, and Whisper argument construction.
- [x] Add red tests for spelled-letter and isolated `T` dictation cleanup.
- [x] Implement settings, CLI argument, validation, and UI support for local quality mode.
- [x] Add safe setup/benchmark scripts for CUDA whisper.cpp and VAD assets without committing binaries/models.
- [x] Update lessons from the owner correction about matching the reference model before blaming the model.
- [x] Verify with focused tests, full build/test, public-readiness scan, stable launch smoke, then commit and push.

## Review: Offline Quality Profile And CUDA Readiness
- Added Fast and Quality transcription profiles while keeping Fast/base.en as the public default.
- Added CPU/CUDA backend settings, quality model path, CUDA CLI path, VAD toggle, and VAD model path to Settings.
- Quality runtime now targets `ggml-large-v3-turbo-q5_0.bin`; when VAD is enabled, Whisper CLI args include local Silero VAD and reference-style decode settings.
- Added deterministic cleanup for spelled `staff` and isolated `T` phrases.
- Added scripts for prerequisite checks, VAD model install, CUDA whisper.cpp build, and model/backend benchmarking.
- Installed `C:\Models\whisper\ggml-silero-v5.1.2.bin` locally for VAD.
- Current CUDA readiness check: RTX 4070 present; Git and Visual Studio present; CMake, CUDA Toolkit `nvcc`, and CUDA-built `whisper-cli.exe` are still missing.
- Focused quality/settings/vocabulary tests pass, 61 tests; full `dotnet test` passes, 190 tests.
- Full `dotnet build` passes with 0 warnings.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.
- Attribution scan intentionally contains source-name matches only in `THIRD_PARTY_NOTICES.md`.
- Published and launch-smoked the stable build from `artifacts\stable-quality-profile\LafazFlow.Windows\LafazFlow.Windows.exe`.

## Plan: Activate CUDA Quality Runtime
- [x] Prove whether the installed CUDA whisper-cli actually loads on this machine.
- [x] Find the missing runtime path instead of assuming CUDA is unavailable.
- [x] Patch the app launch environment so CUDA runtime DLLs are visible to whisper-cli.
- [x] Verify focused tests, full build/test, prerequisite script, CUDA CLI, and local settings.
- [x] Commit and push the CUDA activation fixes.

## Review: Activate CUDA Quality Runtime
- Root cause: CUDA whisper-cli existed, but Windows could not load `cublas64_13.dll` unless CUDA 13's `bin\x64` runtime directory was on `PATH`.
- Added process-level PATH injection for Whisper launches so the app can find the CUDA runtime DLLs without requiring a reboot or global PATH edit.
- Updated setup scripts to handle CMake/CUDA/Ninja/MSVC discovery and CUDA 13 runtime DLL checks.
- Local settings now use Quality profile, CUDA backend, VAD enabled, `ggml-large-v3-turbo-q5_0.bin`, and the CUDA whisper-cli path.
- Focused transcription-service tests pass, 8 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 191 tests.
- CUDA CLI smoke passes and reports `NVIDIA GeForce RTX 4070 Laptop GPU`; prerequisite check reports all required local assets present.
- Published and launch-smoked the stable build from `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Fix CUDA Live Preview Runtime Path
- [x] Trace all Whisper launch paths after the CUDA runtime DLL error.
- [x] Patch live preview Whisper launches to use the same CUDA runtime PATH injection as final transcription.
- [x] Verify build/test, publish a fresh stable build, relaunch, then commit and push.

## Review: Fix CUDA Live Preview Runtime Path
- Root cause: live preview had a separate Whisper `ProcessStartInfo` path and was still launching CUDA whisper-cli without the CUDA 13 `bin\x64` runtime DLL directory.
- Live preview now uses the same `WhisperCliTranscriptionService.BuildProcessPath` environment as final transcription.
- Focused Whisper/live-preview tests pass, 13 tests; full `dotnet test` passes, 191 tests; `dotnet build` passes with 0 warnings.
- Republished and relaunched `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Harden English-Only Dictation
- [x] Reproduce the Malay/Indonesian output from the saved WAV that triggered the complaint.
- [x] Compare old decode flags against stricter English-only decode flags on the same audio.
- [x] Add deterministic English-only prompt prefix, temperature 0, and no-fallback decode settings.
- [x] Verify focused tests, full build/test, publish/relaunch, public scan, then commit and push.

## Review: Harden English-Only Dictation
- Root cause: the multilingual quality model reproduced the observed Malay/Indonesian output on the saved English WAV with the old quality flags.
- The same WAV returned English when decoded with deterministic temperature, no fallback, and an explicit English-only prompt prefix.
- Quality and Fast decode now use `-tp 0` and `-nf`; prompts are prefixed with an English-only instruction before vocabulary terms.
- Focused Whisper tests pass, 8 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 191 tests.
- Republished and relaunched `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Conservative Dictation Formatting Polish
- [x] Add regression tests for clear question starters, `Wait, why/what/how` punctuation, and non-question wait sentences.
- [x] Add regression tests for conversational `weight` as `wait` while preserving measurement uses.
- [x] Implement conservative formatter and vocabulary corrections.
- [x] Verify focused tests, full build/test, publish/relaunch, public scan, then commit and push.

## Review: Conservative Dictation Formatting Polish
- Added conservative question inference for clear question starters such as `why`, `what`, `how`, and `can`.
- Added `Wait, why/what/how` normalization so short lead-ins do not become `Wait. Why`.
- Added contextual `weight` to `wait` correction for conversational lead-ins while preserving measurement/body/scale uses.
- Accounted for formatter-before-vocabulary pipeline order by repairing `Wait, why...` punctuation inside vocabulary correction when needed.
- Focused formatter/vocabulary tests pass, 68 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 208 tests.
- Republished and relaunched `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Add Supabase Superbiz Correction
- [x] Add a focused regression test for `superbiz` -> `Supabase`.
- [x] Add the observed `superbiz` vocabulary correction.
- [x] Verify focused vocabulary tests, full build/test, publish/relaunch, public scan, then commit and push.

## Review: Add Supabase Superbiz Correction
- Added the observed `superbiz` phonetic variant to the local offline Supabase vocabulary correction.
- Focused vocabulary tests pass, 49 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 208 tests.
- Republished and relaunched `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Visible Version And Pinned Build Refresh
- [x] Confirm the running taskbar-pinned app path and compare it with latest stable output.
- [x] Add compact `v0.1` assembly version display beside the mini recorder board.
- [x] Shorten clipboard failures to `Clipboard error` on the board while preserving full detail in the tooltip/logs.
- [x] Verify focused tests, full build/test, publish to the pinned `stable-single` path, public scan, then commit and push.

## Review: Visible Version And Pinned Build Refresh
- Root cause: the taskbar-pinned running process was `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, an older build from May 16, while recent fixes were being launched from `stable-cuda-quality`.
- Added assembly version `0.1.0` and a compact `v0.1` badge beside the mini recorder board.
- Clipboard failures now show `Clipboard error` on the small board while preserving the full message in `StatusDetail` and logs.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Focused mini recorder/clipboard tests pass, 24 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 210 tests.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Move Version Into Mini Recorder Shell
- [x] Confirm the version badge is currently a floating label outside the mini recorder shell.
- [x] Move the compact `v0.1` label into the shell's right-side slot.
- [x] Verify focused UI tests, full build/test, publish/relaunch pinned path, public scan, then commit and push.

## Review: Move Version Into Mini Recorder Shell
- Removed the loose floating version label outside the mini recorder shell.
- Bound the shell's right-side slot to the compact app version, so `v0.1` now appears inside the black pill.
- Focused mini recorder tests pass, 21 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 210 tests.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Dynamic Mini Recorder Shell Layout
- [x] Replace fixed side columns with auto-sized side labels and a stable center area.
- [x] Keep `v0.1` inside the mini recorder shell while restoring balanced right padding.
- [x] Use bounded compact shell growth instead of hard-coding around the current version string.
- [x] Verify focused UI tests, full build/test, publish/relaunch pinned path, public scan, then commit and push.

## Review: Dynamic Mini Recorder Shell Layout
- Replaced fixed 36px side columns with auto-sized `OK` and `v0.1` labels around a stable center area.
- The compact shell now keeps a 184px minimum but can grow modestly up to 232px for future compact metadata without crowding the right edge.
- Added a lesson to keep compact shell metadata content-aware instead of tuned to one version string.
- Focused mini recorder tests pass, 33 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 210 tests.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Public-readiness scan found no credentials. Matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Fix Compact Width Animation Crash
- [x] Inspect application and Windows event logs for the real crash stack trace.
- [x] Identify the width animation `NaN` root cause from the latest shell layout change.
- [x] Restore a concrete compact shell width while keeping balanced side-label spacing.
- [x] Verify focused UI tests, full build/test, publish/relaunch pinned path, public scan, then commit and push.

## Review: Fix Compact Width Animation Crash
- Root cause: the previous dynamic shell change removed the concrete `Width`, leaving WPF's `Width` value as `NaN`; the existing live transcript expansion animation then crashed when animating `RecorderShell.Width`.
- Restored a concrete balanced compact width (`208`) while preserving the 184px reference minimum and the auto-sized side-label layout.
- Kept expanded transcript width at `300` by allowing the shell max width to reach the expanded width.
- Focused mini recorder tests pass, 33 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 210 tests.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: LafazFlow Polish Roadmap
- [x] Review current versioning source and compact badge behavior.
- [x] Write a full polish roadmap covering crash resilience, parity audit, dictation quality, latency, motion, audio cues, settings UX, and packaging.
- [x] Define version bump rules for pre-1.0 visible releases and emergency patch releases.
- [ ] Seek owner approval before implementing Task 1.

## Review: LafazFlow Polish Roadmap
- Implementation plan saved at `docs/superpowers/plans/2026-05-19-lafazflow-polish-roadmap.md`.
- Proposed version policy: each user-visible polish slice bumps minor version while pre-1.0 (`v0.2`, `v0.3`, etc.); emergency hotfixes may bump patch internally (`0.2.1`) while keeping the compact badge as `v0.2`.
- Recommended next implementation slice is Task 1: Crash Resilience And Animation Safety, targeting `0.2.0`.

## Plan: Crash Resilience And Animation Safety
- [x] Add privacy-safe crash logging for app-level unhandled exception surfaces.
- [x] Wire dispatcher, app-domain, and unobserved-task exception handlers during startup.
- [x] Treat WPF animation dispatcher exceptions as recoverable after logging.
- [x] Guard mini recorder numeric width/height animation origins against `NaN` and infinity.
- [x] Guard custom corner-radius and grid-length animations against unexpected origin values.
- [x] Bump app version to `0.2.0`.
- [x] Verify focused tests, full build/test, publish/relaunch pinned path, public scan, then commit and push.

## Review: Crash Resilience And Animation Safety
- Added privacy-safe `CRASH` logging for dispatcher, app-domain, and unobserved-task exception surfaces.
- WPF animation dispatcher exceptions are now logged and treated as recoverable instead of immediately terminating LafazFlow.
- Hardened numeric width/height animation origins against `NaN` and infinity.
- Hardened custom corner-radius and grid-length animations against unexpected or invalid origin values.
- Bumped LafazFlow to `0.2.0`, so the compact recorder badge now shows `v0.2`.
- Focused crash/animation tests pass, 19 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 219 tests.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: Reference Parity Audit Checklist
- [x] Create a public-safe checklist comparing LafazFlow Windows with the neutral macOS reference workflow.
- [x] Cover hotkeys, startup, tray, recorder shell, visual motion, audio cues, live preview, local transcription, paste, formatting, settings, diagnostics, crash resilience, and packaging.
- [x] Add `Done`, `Partial`, and `Missing` status values with evidence and next fix slice.
- [x] Bump app version to `0.3.0`.
- [x] Strengthen compact version display test against the assembly major/minor version.
- [x] Verify focused tests, full build/test, publish/relaunch pinned path, public scan, then commit and push.

## Review: Reference Parity Audit Checklist
- Added `docs/reference-parity-checklist.md` with public-safe parity status across hotkeys, startup, tray, recorder shell, motion, audio cues, live preview, local transcription, paste, formatting, vocabulary, settings, diagnostics, crash resilience, repository hygiene, runtime docs, and packaging.
- Bumped LafazFlow to `0.3.0`, so the compact recorder badge now shows `v0.3`.
- Strengthened the compact version test so `AppVersion` must match the assembly major/minor version.
- Focused mini recorder view-model tests pass, 21 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 219 tests.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: Add Context7 Vocabulary Hotfix
- [x] Add offline corrections for observed `Context7` variants: `contact 7`, `contacts 7`, `contact seven`, and `contacts seven`.
- [x] Add `Context7` to the default Whisper prompt.
- [x] Migrate existing default prompts to include `Context7` while preserving custom prompts.
- [x] Bump patch version to `0.3.1` while keeping compact badge behavior as `v0.3`.
- [x] Verify focused vocabulary/settings tests, full build/test, publish/relaunch pinned path, public scan, then commit and push.

## Review: Add Context7 Vocabulary Hotfix
- Added offline `Context7` corrections for `contact 7`, `contacts 7`, `contact seven`, and `contacts seven`.
- Added `Context7` to the default local Whisper prompt so new settings include it as recognition context.
- Bumped settings schema to migrate existing default prompts to include `Context7`; custom prompts remain preserved.
- Bumped LafazFlow to `0.3.1`; compact badge remains `v0.3`.
- Focused vocabulary/settings/version tests pass, 82 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 224 tests.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: Dictation Quality And Developer Vocabulary
- [x] Add focused offline corrections for newly observed developer/tooling phrases and the `consent form` compound issue.
- [x] Add `MCP`, `Vite`, and `MediBrave` to the default local Whisper prompt.
- [x] Migrate existing default prompts to the updated prompt while preserving custom prompts.
- [x] Bump LafazFlow to `0.4.0` so the compact badge shows `v0.4`.
- [x] Verify focused tests, full build/test, publish/relaunch pinned path, public scan, then commit and push.

## Review: Dictation Quality And Developer Vocabulary
- Added focused offline corrections for `MCP`, `Vite`, and the observed `consenForm` / `consentForm` compound issue.
- Added `MCP`, `Vite`, and `MediBrave` to the default local Whisper prompt.
- Bumped settings schema to migrate previous default prompts, including the Context7 prompt, while preserving custom prompts.
- Bumped LafazFlow to `0.4.0`, so the compact recorder badge now shows `v0.4`.
- Added a lesson for repairing accidental ASR compounds narrowly instead of applying broad camel-case splitting.
- Focused vocabulary/settings/version tests pass, 91 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 233 tests.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: Latency And Fluidity Instrumentation
- [x] Extend latency checkpoints for hotkey dispatch, recorder visibility, preview start/stop, stop hotkey-to-queue, and UI hide.
- [x] Carry the double Shift detection timestamp from the low-level hook into the recorder latency trace.
- [x] Keep preview-stop measurement non-blocking and make latency trace checkpoint storage thread-safe.
- [x] Extend latency logs and Settings diagnostics with additive fields while preserving older latency rows.
- [x] Bump LafazFlow to `0.5.0` so the compact badge shows `v0.5`.
- [x] Verify focused tests, full build/test, diff check, public scans, publish/relaunch pinned path, then commit and push.

## Review: Latency And Fluidity Instrumentation
- Added additive latency checkpoints for hotkey dispatch, recorder visibility, stop hotkey-to-queue, preview start/stop, and UI hide.
- Carried the double Shift detection timestamp from the keyboard hook into the recorder trace.
- Made latency checkpoint storage thread-safe so non-blocking preview-stop timing cannot race latency reporting.
- Extended `LATENCY` logs and Settings diagnostics with hotkey, queue, preview, paste, hide, and summary fields while preserving older rows with `na`.
- Bumped LafazFlow to `0.5.0`, so the compact recorder badge now shows `v0.5`.
- Focused latency/settings/controller tests pass, 55 tests; full `dotnet test` passes, 235 tests.
- Full `dotnet build` passes with 0 warnings after rerunning separately from tests to avoid a WPF markup-cache file lock.
- `git diff --check` passes. Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: Visual Motion Refinement
- [x] Refine recorder entrance, exit, state fade, expansion, and processing pulse timing constants.
- [x] Align processing dot count with pulse step count so every pulse step has an active visible dot.
- [x] Replace hard-coded motion values in the recorder window with visual spec constants.
- [x] Use transform/opacity easing for entrance/exit and keep layout animation limited to the small live-preview expansion surface.
- [x] Soften audio smoothing while preserving responsive speech movement and dynamic aqua/cyan bar colors.
- [x] Bump LafazFlow to `0.6.0` so the compact badge shows `v0.6`.
- [x] Verify focused tests, full build/test, diff check, public scans, publish/relaunch pinned path, then commit and push.

## Review: Visual Motion Refinement
- Refined compact recorder motion timing: faster entrance/exit, state fades, live transcript expansion, and processing pulse rhythm.
- Aligned processing dot count with processing pulse steps so every pulse frame has a visible active dot.
- Replaced hard-coded recorder motion values with `MiniRecorderVisualSpec` constants for dot count, bar count, frame throttle, scale, and translate offsets.
- Switched entrance/exit/state animations to cubic easing while keeping transform/opacity as the primary motion path and limiting layout animation to the small live transcript expansion surface.
- Softened audio smoothing to reduce twitchy drops while preserving responsive speech movement and the existing aqua/cyan dynamic bar colors.
- Bumped LafazFlow to `0.6.0`, so the compact recorder badge now shows `v0.6`.
- Focused visual/UI tests pass, 40 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 237 tests.
- `git diff --check` passes. Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: Audio Cue Refinement
- [x] Add settings for enabling sound cues and configuring sound cue volume.
- [x] Raise the default sound cue volume to `50%`.
- [x] Clamp saved sound cue volume to the `0.0` through `1.0` range.
- [x] Make sound cue playback respect the current settings and remain non-fatal for missing assets or audio device errors.
- [x] Preserve cue timing: start after recording begins, stop/transcribing immediately after stop begins, completion after paste succeeds, and error on real failure.
- [x] Add Settings UI controls for sound cue enablement and volume.
- [x] Bump LafazFlow to `0.7.0` so the compact badge shows `v0.7`.
- [x] Verify focused tests, full build/test, diff check, public scans, publish/relaunch pinned path, then commit and push.

## Review: Audio Cue Refinement
- Added `EnableSoundCues` and `SoundCueVolume` settings with a default enabled `50%` volume.
- Added Settings UI controls for cue enablement and cue volume.
- Sound cue playback now respects current settings, clamps volume, skips disabled/zero-volume cues, and stays non-fatal for missing assets or audio output failures.
- Recorder cue timing remains pinned: start after recording begins, stop/transcribing immediately after stop starts, completion after paste succeeds, and error on real failure.
- Bumped LafazFlow to `0.7.0`, so the compact recorder badge now shows `v0.7`.
- Focused sound/settings/controller tests pass, 74 tests; full `dotnet build` passes with 0 warnings; full `dotnet test` passes, 250 tests.
- `git diff --check` passes. Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
- Republished both `artifacts\stable-single` and `artifacts\stable-cuda-quality`, then relaunched the pinned `stable-single` path.
- Launch smoke stayed running and produced no fresh LafazFlow crash event after relaunch.

## Plan: Settings UX And Runtime Diagnostics
- [x] Bump LafazFlow to `0.8.0`.
- [x] Add runtime diagnostics tests for Fast/Quality profile summaries, missing local paths, microphone availability, logs folder writability, and CLI smoke failures.
- [x] Add settings reset tests to ensure detected defaults are persisted safely.
- [x] Add Settings window tests for runtime status rows and new action buttons.
- [x] Implement runtime diagnostics service and testable environment probe.
- [x] Wire runtime status, test microphone, test transcription, open logs, and reset settings into Settings.
- [x] Verify with focused tests, full tests, publish, relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Settings UX And Runtime Diagnostics
- Added a runtime diagnostics service with testable probes for local Whisper files, CUDA/VAD readiness, microphone availability, log-folder writability, and CLI smoke checks.
- Added a Settings runtime status section with profile summary, diagnostic rows, refresh, test microphone, test transcription, open logs, and reset settings actions.
- Added `ResetToDefaults()` persistence in `SettingsStore` and wired Settings reset through a confirmation dialog.
- Bumped LafazFlow to `0.8.0`, so the compact recorder badge now shows `v0.8`.
- Focused runtime/settings tests pass, 36 tests; full `dotnet test` passes, 261 tests.
- Republished `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, relaunched it, and verified the stable build reports file version `0.8.0.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
## Plan: Dictation Post-Processing Quality
- [x] Bump LafazFlow to `0.8.1`.
- [x] Add formatter regression tests for bad `. And...` continuation breaks and conversational question punctuation.
- [x] Add vocabulary regression tests for narrow `Dokumen` English dictation drift.
- [x] Implement conservative continuation-boundary repair in `TranscriptionTextFormatter`.
- [x] Implement lead-in question punctuation repair in `TranscriptionTextFormatter`.
- [x] Implement narrow `Dokumen` to `document` correction in `VocabularyCorrectionService`.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Dictation Post-Processing Quality
- Added regression tests for the reported `. And...` continuation breaks, conversational question endings, and `Dokumen` English dictation drift.
- Added conservative formatter repair for high-confidence continuation phrases such as `. And then`, `. And there`, `. And we`, `. And it`, and related variants.
- Added question lead-in handling for `So what...` and `But how...` while preserving existing direct question behavior.
- Added narrow vocabulary correction for English-context `dokumen everything/this/that/it` without broad non-English rewriting.
- Bumped LafazFlow to `0.8.1`.
- Focused formatter/vocabulary/Whisper tests pass, 99 tests; full `dotnet test` passes, 272 tests; full `dotnet build` passes with 0 warnings.
- Republished `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, relaunched it, and verified the stable build reports file version `0.8.1.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.
## Plan: Custom Vocabulary Settings
- [x] Bump LafazFlow to `0.9.0`.
- [x] Add `CustomVocabularyTerms` settings persistence and schema `7` migration tests.
- [x] Add prompt builder tests for built-in prompt plus custom terms, blank-line trimming, case-insensitive dedupe, and casing preservation.
- [x] Add Settings ViewModel and XAML tests for multiline custom vocabulary editing.
- [x] Add transcription wiring tests proving final transcription and live preview receive the combined prompt.
- [x] Implement schema v7 setting, prompt builder, Settings UI, and transcription wiring.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Custom Vocabulary Settings
- Added `CustomVocabularyTerms` as a persisted schema `7` setting.
- Added a prompt builder that appends trimmed custom terms to the built-in local Whisper prompt, dedupes terms case-insensitively, and preserves the user's preferred casing.
- Added a multiline Custom Vocabulary box in Settings for names, product terms, acronyms, and project-specific words.
- Wired final transcription and live preview to use the combined built-in plus custom vocabulary prompt.
- Bumped LafazFlow to `0.9.0`.
- Focused vocabulary/settings/controller/live-preview tests pass, 66 tests; full `dotnet test` passes, 279 tests; full `dotnet build` passes with 0 warnings.
- Republished and relaunched `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, and verified the stable build reports file version `0.9.0.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.

## Plan: Testing Bias And Version Visibility
- [x] Bump LafazFlow to `0.9.1`.
- [x] Add default prompt bias for `testing` and common test-count phrases.
- [x] Add narrow vocabulary correction for observed `let's think` test-count misrecognitions.
- [x] Add shared compact app-version helper and use it from mini recorder, Settings, and tray status/menu.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Testing Bias And Version Visibility
- Added default prompt bias for `testing`, `Testing, testing, one, two, three`, and `Testing one two three over`.
- Added narrow offline correction for `Let's think` followed by `one/two/three` or `1/2/3`, including optional `over`, while preserving normal `Let's think about...` sentences.
- Added shared compact version text and reused it in the mini recorder, Settings title/header, tray tooltip, and tray menu header.
- Bumped LafazFlow to `0.9.1`; compact visible version remains `v0.9`.
- Focused correction/settings/tray/version tests pass, 135 tests; full `dotnet test` passes, 290 tests; full `dotnet build` passes with 0 warnings.
- Republished and relaunched `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, and verified the stable build reports file version `0.9.1.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.

## Plan: Formatting Engine Polish
- [x] Bump LafazFlow to `0.9.2`.
- [x] Add formatter regression tests for clearer question endings and conservative `. And ...` continuation repair.
- [x] Add target-context tests for mid-sentence casing while preserving known product/name casing.
- [x] Add narrow `rappers` to `wrappers` vocabulary tests for coding/UI contexts while preserving real rapper contexts.
- [x] Add prompt bias for `wrapper`, `wrappers`, `component wrapper`, and `without wrappers`, with default-prompt migration tests.
- [x] Implement formatter, vocabulary, prompt, migration, and version changes.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Formatting Engine Polish
- Added question punctuation regressions for clear question phrases such as `what do we have next`, `can you tell me`, and `is there`.
- Expanded conservative `. And ...` continuation repair to include the observed `And you...` pattern while preserving normal separate sentences.
- Added target-context preservation for known product/name casing after mid-sentence punctuation: `Supabase`, `Context7`, `Luqman`, and `MediBrave`.
- Added context-bound `rappers` to `wrappers` correction for coding/UI phrases such as `without any rappers`, `component rappers`, and `with no rappers`, while preserving real music contexts.
- Added default prompt bias for `wrapper`, `wrappers`, `component wrapper`, and `without wrappers`, plus migration from the previous default prompt.
- Bumped LafazFlow to `0.9.2`; compact visible version remains `v0.9`.
- Focused formatter/vocabulary/settings tests pass, 147 tests; full `dotnet test` passes, 316 tests; full `dotnet build` passes with 0 warnings.
- Republished and relaunched `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, and verified the stable build reports file version `0.9.2.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.

## Plan: Theirs DRS Dictation Repair
- [x] Bump LafazFlow to `0.9.3`.
- [x] Add narrow vocabulary tests for `DRs` to `theirs` in observed UI/code comparison contexts.
- [x] Add negative vocabulary tests preserving legitimate `DRS` acronym contexts.
- [x] Add default prompt bias for `theirs`, `theirs originally`, and `compare theirs`, with migration from the previous default prompt.
- [x] Implement vocabulary correction, prompt, migration, and version changes.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Theirs DRS Dictation Repair
- Added context-bound `DRs` to `theirs` correction for observed comparison phrases such as `see DRs originally`, `compare DRs`, `use DRs originally`, and `took DRs`.
- Preserved legitimate acronym contexts such as `DRS system`, `DRS score`, and `DRS file`.
- Added default prompt bias for `theirs`, `theirs originally`, and `compare theirs`, plus migration from the previous default prompt.
- Bumped LafazFlow to `0.9.3`; compact visible version remains `v0.9`.
- Focused vocabulary/settings tests pass, 100 tests; full `dotnet test` passes, 324 tests; full `dotnet build` passes with 0 warnings.
- Republished and relaunched `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, and verified the stable build reports file version `0.9.3.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.

## Plan: Full Patch Version And Stale Document Repair
- [x] Bump LafazFlow to `0.9.4`.
- [x] Show full semantic patch version in shell, Settings, tray tooltip, and tray menu.
- [x] Add default prompt bias for `stale`, `stale document`, `stale docs`, and `stale file`.
- [x] Add narrow vocabulary correction for `still/steel document`, `still/steel docs`, and `still/steel file`.
- [x] Preserve normal `still` and `steel` sentences.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Full Patch Version And Stale Document Repair
- Changed shared app version text from major/minor to full semantic patch format, so shell, Settings, tray tooltip, and tray menu now show `v0.9.4`.
- Added default prompt bias for `stale`, `stale document`, `stale docs`, and `stale file`, plus migration from the previous default prompt.
- Added narrow correction for `still document`, `steel document`, `still docs`, `steel docs`, `still file`, and `steel file`.
- Preserved normal `still` and `steel` sentences such as `I am still working` and `The steel frame is strong`.
- Bumped LafazFlow to `0.9.4`.
- Focused version/vocabulary/settings tests pass, 154 tests; full `dotnet test` passes, 334 tests; full `dotnet build` passes with 0 warnings.
- Republished and relaunched `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, and verified the stable build reports file version `0.9.4.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and code identifiers.

## Plan: Custom Correction Rules
- [x] Bump LafazFlow to `0.10.0` and settings schema to `12`.
- [x] Add failing tests for persisted `CustomCorrectionRules`, Settings validation, Settings UI binding, built-in-plus-custom correction order, live preview correction, and final transcription correction.
- [x] Add a multiline `Custom Correction Rules` Settings field using `heard phrase => corrected phrase`, with validation for malformed lines.
- [x] Apply built-in vocabulary corrections first and user correction rules second, gated by `EnableVocabularyCorrections`.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Custom Correction Rules
- Added Settings support for multiline custom correction rules in the format `heard phrase => corrected phrase`.
- Added validation so malformed nonblank rule lines are rejected before settings are saved.
- Added schema `12` persistence for `CustomCorrectionRules`, defaulting and migrating to an empty value.
- Applied corrections in this order: built-in vocabulary corrections first, custom rules second, only when vocabulary corrections are enabled.
- Wired custom rules into both final transcription and live preview.
- Bumped LafazFlow to `0.10.0`.
- Focused correction/settings/controller/live-preview tests pass, 166 tests; full `dotnet test` passes, 346 tests; full `dotnet build` passes with 0 warnings.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Supabase Near-Miss Hotfix
- [x] Bump LafazFlow to `0.10.1`.
- [x] Add a focused regression for `Supabaes` to `Supabase`.
- [x] Add the observed near-miss product spelling to offline vocabulary correction.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Supabase Near-Miss Hotfix
- Added `Supabaes` as an offline correction to `Supabase`.
- Bumped LafazFlow to `0.10.1`.
- Focused vocabulary regression passes; full `dotnet test` passes, 346 tests; full `dotnet build` passes with 0 warnings.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Transcription Parity Harness
- [x] Build a local-only console benchmark harness that runs the same WAV fixtures through multiple LafazFlow transcription configurations.
- [x] Use private ignored recordings with matching `.txt` expected transcripts so no voice recordings are committed.
- [x] Run current settings, Fast CPU, Quality CPU, Quality CUDA when available, and macOS-like decode options against the same audio.
- [x] Record local metrics: total latency, model/backend/profile, normalized edit distance, key-term hits, raw transcript, post-processed transcript, expected transcript, and errors.
- [x] Add a report command that writes local Markdown/CSV under ignored diagnostics output.
- [x] Use the report to decide whether the next implementation should tune decode flags, switch defaults, add a persistent Whisper worker, or investigate a Parakeet/FluidAudio-style local backend.
- [x] Verify harness tests, full build/test, public safety scans, and confirm no WAV/model/output artifacts are tracked.

## Review: Transcription Parity Harness
- Added `tools/LafazFlow.TranscriptionBench`, a local-only console tool for benchmarking existing private LafazFlow recordings.
- The harness discovers `.wav` files with matching `.txt` expected transcripts from `%LOCALAPPDATA%\LafazFlow\Recordings`.
- Added benchmark configs for current settings, Fast CPU, Quality CPU, Quality CUDA with VAD when available, and macOS-like q5 decode settings.
- Reports are written locally to `%LOCALAPPDATA%\LafazFlow\Benchmarks` as Markdown and CSV with full transcript text for debugging.
- Added ignored benchmark folders to keep fixtures/reports out of public git.
- Focused benchmark/Whisper tests pass, 13 tests; full `dotnet test` passes, 351 tests; full `dotnet build` passes with 0 warnings.
- Real smoke run passed with one existing recording and `fast-cpu-base-en`, producing local Markdown/CSV reports.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Supabase Supabease Hotfix
- [x] Bump LafazFlow to `0.10.2`.
- [x] Add a focused regression for `Supabease` to `Supabase`.
- [x] Add the observed near-miss product spelling to offline vocabulary correction.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.
- [x] Commit and push the Windows repo changes.

## Review: Supabase Supabease Hotfix
- Added `Supabease` as an offline correction to `Supabase`.
- Bumped LafazFlow to `0.10.2`.
- Focused vocabulary regression passes; full `dotnet test` passes, 351 tests; full `dotnet build` passes with 0 warnings.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Stripe Vocabulary Hotfix
- [x] Add focused regression tests for `strike`/lowercase `stripe` in payment/developer contexts.
- [x] Preserve normal English uses such as worker strikes and visual stripes.
- [x] Add `Stripe` to the built-in local prompt and migrate previous default prompts.
- [x] Bump LafazFlow to `0.10.3`.
- [x] Verify focused tests, full tests, build, publish/relaunch, and public safety scans.

## Review: Stripe Vocabulary Hotfix
- Added context-bound offline repairs for `strike`/lowercase `stripe` in payment/developer phrases such as `Stripe checkout`, `Stripe webhooks`, and `Stripe dashboard`.
- Preserved normal English uses such as worker strikes, lightning strikes, and visual stripes.
- Added `Stripe` to the default local Whisper prompt and bumped settings schema so previous default prompts migrate while custom prompts remain preserved.
- Bumped LafazFlow to `0.10.3`.
- Focused vocabulary/settings tests pass, 125 tests; full `dotnet test` passes, 359 tests; full `dotnet build` passes with 0 warnings.
- Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Regression Pack Command
- [x] Write implementation plan at `docs/superpowers/plans/2026-05-24-regression-pack-command.md`.
- [x] Add a private local `--pack daily` resolver for benchmark fixtures.
- [x] Add tests for pack path parsing, custom pack roots, and invalid pack names.
- [x] Add `Stripe` to benchmark key-term checks.
- [x] Improve empty-pack CLI guidance.
- [x] Verify focused tests, full tests, build, safety scans, and a local pack smoke run.

## Review: Regression Pack Command
- Added `--pack <name>` and `--packs-root <path>` support to `tools/LafazFlow.TranscriptionBench`.
- `--pack daily` resolves to `%LOCALAPPDATA%\LafazFlow\RegressionPacks\daily` by default and expects private `.wav` plus matching `.txt` fixture pairs.
- Added pack-name validation to prevent unsafe path traversal names such as `..\secret`.
- Added `Stripe` to benchmark key-term tracking.
- Improved empty-pack CLI output with the exact folder and fixture-pair example.
- Seeded the private local `daily` pack from the owner-recorded target clips; these files remain outside the repo.
- Smoke run passed: `--pack daily --take 4 --configs current-settings` produced `4/4` successful runs, `928 ms` average latency, `0.000` edit distance, and `7/7` key terms.
- Focused `TranscriptionBenchTests` pass, 7 tests; full `dotnet test` passes, 362 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Recorder UX Polish v0.10.4
- [x] Keep the mini recorder shell fixed at compact height during recording and processing.
- [x] Replace shell-expanding live transcript preview with a subtle overlay above the shell.
- [x] Tune recorder motion for faster daily dictation: 120 ms entrance, 95 ms exit, 90 ms state fade, and 140 ms preview overlay fade.
- [x] Add aqua processing dots with active-dot scale progression.
- [x] Tune audio level smoothing for faster attack and controlled release.
- [x] Bump LafazFlow to `0.10.4`.
- [x] Verify focused tests, full tests, build, safety scans, stable publish/relaunch, and launch smoke.

## Review: Recorder UX Polish v0.10.4
- Mini recorder shell now stays compact and fixed at 40 px height while recording and processing.
- Live transcript preview is now a separate subtle overlay above the shell instead of resizing the shell.
- Recorder motion is faster for daily dictation: 120 ms entrance, 95 ms exit, 90 ms state fade, and 140 ms preview overlay fade.
- Processing dots now use the aqua palette and active-dot scale progression.
- Audio smoothing now uses a faster attack with controlled release for a more responsive meter.
- Bumped LafazFlow to `0.10.4`.
- Focused recorder/version tests pass, 48 tests; full `dotnet test` passes, 364 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.4.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Sound Cue Polish v0.10.5
- [x] Keep existing bundled sound files unchanged.
- [x] Add per-cue gain multipliers in `SoundCueService`.
- [x] Keep Settings volume as the master volume and clamp final playback volume.
- [x] Add four Settings test buttons: Test Start, Test Stop, Test Done, and Test Error.
- [x] Make test buttons use current edited sound settings, including unsaved enablement and volume.
- [x] Bump LafazFlow to `0.10.5`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.
- [ ] Owner listening review of the four Settings cue test buttons.

## Review: Sound Cue Polish v0.10.5
- Kept the existing bundled sound files unchanged.
- Added per-cue gain multipliers: start `0.8`, stop/transcribing `1.0`, done `0.8`, error `0.55`.
- Final playback volume now uses `master volume * cue gain`, clamped from `0.0` to `1.0`.
- Added four Settings cue test buttons: Test Start, Test Stop, Test Done, and Test Error.
- Test buttons use the currently edited Settings values, including unsaved sound enablement and volume.
- Bumped LafazFlow to `0.10.5`.
- Focused sound/settings/controller tests pass, 48 tests; full `dotnet test` passes, 373 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.5.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.
- Manual listening review is left to the owner because automated verification can prove wiring and volume policy, but not whether the cue feels pleasant through the actual speaker/headphone setup.

## Plan: Sound Cue Audibility Hotfix v0.10.6
- [x] Reproduce the volume regression with focused sound cue tests.
- [x] Restore cue playback so Settings volume maps directly to actual playback volume.
- [x] Keep the Settings cue test buttons from v0.10.5.
- [x] Bump LafazFlow to `0.10.6`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.
- [ ] Owner listening review of restored cue loudness.

## Review: Sound Cue Audibility Hotfix v0.10.6
- Root cause: v0.10.5 added per-cue gain multipliers on top of the user's calibrated Settings volume, dropping default start/done cues to `0.4` and error cues to `0.275`.
- Restored playback gain to `1.0` for all cue kinds so Settings volume maps directly to actual playback volume again.
- Kept the four Settings test buttons from v0.10.5.
- Bumped LafazFlow to `0.10.6`.
- Focused sound cue tests first failed against the v0.10.5 regression, then passed after the fix: 23 tests.
- Full `dotnet test` passes, 373 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Responsive Sound Cue Asset Hotfix v0.10.7
- [x] Measure bundled cue durations and loudness to identify why feedback feels slow.
- [x] Add a regression test requiring start/stop cue assets to stay brief.
- [x] Trim start/stop trailing silence while leaving completion/error assets unchanged.
- [x] Modestly normalize the stop cue so it is audible without changing Settings volume math.
- [x] Bump LafazFlow to `0.10.7`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.
- [ ] Owner listening review of faster start/stop cue feel.

## Review: Responsive Sound Cue Asset Hotfix v0.10.7
- Root cause: start/stop cue assets decoded at about `1.296s`; most of that was trailing silence, so hotkey feedback felt slow even when playback started.
- Trimmed `recstart.mp3` to about `0.474s`.
- Trimmed `recstop.mp3` to about `0.480s` and modestly normalized it from roughly `-9.9 dB` peak to roughly `-5.7 dB` peak so the stop cue is audible again.
- Left completion/error cue files unchanged.
- Added a regression test requiring start/stop cues to stay under `0.55s`.
- Bumped LafazFlow to `0.10.7`.
- Focused sound cue tests pass, 23 tests; full `dotnet test` passes, 375 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Smooth Sound Cue Playback Hotfix v0.10.8
- [x] Audit start/stop cue files for clipping, duration, silence, format, and loudness.
- [x] Rebuild start/stop cues from the original source assets instead of reusing tightly cut MP3s.
- [x] Convert start/stop cues to short PCM WAV files with small fades to avoid MP3 edge artifacts.
- [x] Update app cue mapping and copied content files from `recstart.mp3`/`recstop.mp3` to `recstart.wav`/`recstop.wav`.
- [x] Add regression tests proving start/stop cues are short PCM WAV files.
- [x] Bump LafazFlow to `0.10.8`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.
- [ ] Owner listening review of crackle-free start/stop cues.

## Review: Smooth Sound Cue Playback Hotfix v0.10.8
- Root cause: the v0.10.7 start/stop cues were tightly trimmed MP3 files. They did not clip, but short MP3 cue boundaries can crackle or break up during NAudio playback.
- Rebuilt start/stop cues from the original longer source assets, not from the v0.10.7 cut files.
- Converted start/stop cues to PCM WAV with small fade-in/fade-out edges: `recstart.wav` is about `0.474s`; `recstop.wav` is about `0.511s`.
- Updated app cue mapping and content copy rules to use `recstart.wav` and `recstop.wav`.
- Removed the short MP3 start/stop cue files from the repo to avoid accidental packaging.
- Added regression tests proving start/stop cues stay brief and use 16-bit PCM WAV.
- Bumped LafazFlow to `0.10.8`.
- Focused sound/recorder tests pass, 28 tests; full `dotnet test` passes, 377 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Persistent Sound Cue Mixer Hotfix v0.10.9
- [x] Audit why crackling remains after switching start/stop assets to WAV.
- [x] Replace per-cue `WaveOutEvent` creation with one persistent output device.
- [x] Cache decoded cue samples instead of opening/decoding the file on every play.
- [x] Mix overlapping cues through one `MixingSampleProvider`.
- [x] Add tests proving all bundled cues decode to the persistent mixer format.
- [x] Bump LafazFlow to `0.10.9`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.
- [ ] Owner listening review of crackle-free processing cue playback.

## Review: Persistent Sound Cue Mixer Hotfix v0.10.9
- Root cause: crackling persisted after the WAV asset fix, especially during stop/processing, because playback still created and initialized a new `WaveOutEvent` output device for each cue.
- Replaced per-cue output-device creation with one persistent `WaveOutEvent` and `MixingSampleProvider`.
- Cached decoded cue samples on first play so hotkey feedback does not open/decode files during the stop/transcription handoff.
- Mixed overlapping cues through the persistent mixer instead of opening competing output devices.
- Added tests proving all bundled cues decode to the persistent mixer format.
- Also hardened `RecorderController` error logging so a locked log file cannot break dictation flow.
- Bumped LafazFlow to `0.10.9`.
- Focused sound/logging tests pass, 30 tests; full `dotnet test` passes, 381 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Completion Cue Loudness Tuning v0.10.10
- [x] Keep start, stop/processing, and error cue gains unchanged.
- [x] Boost only the completed cue so fade-out success feedback is easier to hear.
- [x] Clamp final playback volume at `1.0` to avoid clipping through the mixer.
- [x] Bump LafazFlow to `0.10.10`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.
- [ ] Owner listening review of completed cue loudness.

## Review: Completion Cue Loudness Tuning v0.10.10
- Boosted only the completed cue gain to `1.45`.
- Kept start, stop/processing, and error cue gains at `1.0`.
- Final playback volume remains clamped at `1.0`, so high Settings volume cannot exceed mixer max.
- Bumped LafazFlow to `0.10.10`.
- Hardened latency diagnostics log reading to tolerate shared log access while the app is running.
- Focused sound/diagnostics tests pass, 32 tests; full `dotnet test` passes, 382 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Per-Cue Sound Settings v0.10.11
- [x] Keep the existing master sound cue volume as the global cap.
- [x] Add persisted per-cue volume controls for Start, Stop, Done, and Error.
- [x] Clamp per-cue levels to `0%` through `200%` and clamp final playback to mixer max.
- [x] Update Settings so each cue has a slider and a matching test button.
- [x] Migrate older settings to the new cue defaults.
- [x] Bump LafazFlow to `0.10.11`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.
- [ ] Owner listening review of the per-cue sliders on real speakers/headphones.

## Review: Per-Cue Sound Settings v0.10.11
- Added per-cue persisted volume settings: Start `100%`, Stop `100%`, Done `145%`, and Error `100%`.
- Kept the master volume as the overall level, then multiply it by the edited cue level and clamp final playback to `1.0`.
- Settings now shows a master slider plus separate Start, Stop, Done, and Error sliders with test buttons.
- Settings schema migrated to `14`, including clamp protection for older or manually edited settings files.
- Bumped LafazFlow to `0.10.11`.
- Focused sound/settings/controller tests pass, 89 tests.
- Full `dotnet test` passes, 381 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.11.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Command Sentence Question-Mark Repair v0.10.12
- [x] Add failing formatter regressions for command/reminder sentences that incorrectly end with `?`.
- [x] Preserve real questions in mixed paragraphs.
- [x] Add a conservative sentence-level punctuation repair for command/reminder lead-ins.
- [x] Guard the question-starter heuristic so `Do not forget to...` stays declarative.
- [x] Bump LafazFlow to `0.10.12`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Command Sentence Question-Mark Repair v0.10.12
- Fixed formatter handling for command/reminder sentences such as `Also don't forget to commit and push... ?`, converting them to declarative periods.
- Preserved actual questions such as `How do you plan to verify it?` and `Can you make sure to verify it?`.
- Added a guard so the existing `do` question-starter heuristic does not turn `Do not forget to...` back into a question.
- Added a lesson for keeping command/reminder punctuation separate from question heuristics.
- Bumped LafazFlow to `0.10.12`.
- Focused formatter tests pass, 46 tests; full `dotnet test` passes, 393 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.12.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Best Bang For Buck Vocabulary Hotfix v0.10.13
- [x] Add failing vocabulary regressions for `best bank for bug` and `best bank for buck`.
- [x] Preserve normal `bank` and `bug` sentences.
- [x] Add a narrow idiom-level correction for `best bang for buck` in option/comparison contexts.
- [x] Bump LafazFlow to `0.10.13`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Best Bang For Buck Vocabulary Hotfix v0.10.13
- Added offline correction for observed `best bank for bug` / `best bank for buck` homophones to `best bang for buck`.
- Kept the repair phrase-level and context-bound so ordinary `bank` and `bug` sentences are not rewritten.
- Added a lesson to repair idioms as complete phrases instead of broad single-word homophone swaps.
- Bumped LafazFlow to `0.10.13`.
- Focused vocabulary tests pass, 114 tests; full `dotnet test` passes, 399 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.13.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Better Stack Comparison Vocabulary Hotfix v0.10.14
- [x] Add failing regressions from the owner's repeated `best bang for buck` comparison tests.
- [x] Extend the idiom repair to cover `best bank for bulk`.
- [x] Add context-bound repairs for `batter stack errors`, `battle stack errors`, and `Better Stack Eros`.
- [x] Add product casing for `Sentry`.
- [x] Preserve normal `bank`, `bug`, `bulk`, and unrelated hardware stack sentences.
- [x] Bump LafazFlow to `0.10.14`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Better Stack Comparison Vocabulary Hotfix v0.10.14
- Repaired the latest observed comparison outputs to `best bang for buck option between Better Stack Errors and Sentry`.
- Added `bulk` as an observed `buck` homophone only inside the `best bang for buck` idiom.
- Added phrase-level Better Stack Errors repairs without touching unrelated `battery stack errors` style text.
- Bumped LafazFlow to `0.10.14`.
- Focused vocabulary tests pass, 119 tests; full `dotnet test` passes, 404 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.14.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Supabase Contabo Vocabulary Hotfix v0.10.15
- [x] Verify the running app, model, and recent latency logs before changing code.
- [x] Add focused vocabulary regressions for observed `Supabeas` and `Inventabo` drift.
- [x] Add narrow offline corrections to `Supabase` and `Contabo`.
- [x] Bump LafazFlow to `0.10.15`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Supabase Contabo Vocabulary Hotfix v0.10.15
- Runtime audit showed the current app is `v0.10.14` using `ggml-large-v3-turbo-q5_0.bin` through the CUDA quality profile.
- The regression was vocabulary coverage, not a model/profile switch.
- Added exact observed correction coverage for `Supabeas` to `Supabase` and `Inventabo` to `Contabo`.
- Bumped LafazFlow to `0.10.15`.
- Focused vocabulary/settings tests pass, 20 tests; full `dotnet test` passes, 404 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.15.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Storage Question Vocabulary Hotfix v0.10.16
- [x] Add failing regressions for `How much storage/space would it be?` drifting from `take`.
- [x] Preserve normal `How much would it be?` pricing/estimation questions.
- [x] Add a narrow storage/space-only correction for `would it be` to `would it take`.
- [x] Bump LafazFlow to `0.10.16`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Storage Question Vocabulary Hotfix v0.10.16
- Added storage/space-only correction coverage for `How much storage would it be?` to `How much storage would it take?`.
- Also covered `disk space` and `space` variants.
- Preserved normal pricing/estimation wording such as `How much would it be?` and `How much money would it be?`.
- Added a lesson for keeping semantic verb repairs domain-bound.
- Bumped LafazFlow to `0.10.16`.
- Focused storage question tests pass, 6 tests; full `dotnet test` passes, 410 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Stable publish/launch smoke passed from `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe`, reporting file version `0.10.16.0`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Revert Unsafe Storage Question Rewrite v0.10.17
- [x] Add a failing guard proving `How much storage would it be?` is preserved.
- [x] Preserve `How much storage would it take?` without needing post-processing.
- [x] Remove the unsafe `would it be` to `would it take` storage rewrite.
- [x] Update lessons to ban deterministic intent rewrites when both phrases are valid.
- [x] Bump LafazFlow to `0.10.17`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Revert Unsafe Storage Question Rewrite v0.10.17
- Root cause: v0.10.16 made a text-only post-processing guess between two valid phrases, so it could not distinguish an intended `would it be` from an intended `would it take`.
- Removed the unsafe storage `would it be` to `would it take` default correction.
- Added regression coverage proving `How much storage/disk space/space would it be?` and `How much storage/disk space/space would it take?` are all preserved.
- Updated the lesson to avoid deterministic intent rewrites when both phrases are valid and there is no audio confidence signal.
- Bumped LafazFlow to `0.10.17`.
- Focused storage verb-choice tests pass, 9 tests; full `dotnet test` passes, 413 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Silent Microphone Capture Hotfix v0.10.18
- [x] Verify today's recordings and logs to separate paste failure from empty transcription.
- [x] Add tests for detecting effectively silent WAV recordings.
- [x] Add a controller guard so silent recordings show an error and never paste an empty transcript.
- [x] Use the Windows wave mapper/default recording device instead of hard-coding input device `0`.
- [x] Bump LafazFlow to `0.10.18`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Silent Microphone Capture Hotfix v0.10.18
- Root cause: today's recordings were valid WAV files but had near-zero signal (`-80 dB` to `-90 dB` peak), so VAD produced no speech segments and LafazFlow attempted to paste an empty transcript.
- Confirmed this was not primarily a Cursor paste failure: logs showed successful paste attempts, while paired transcript files were `0` bytes.
- Added `AudioSignalAnalyzer` to detect effectively silent 16-bit PCM WAV recordings.
- Added a controller guard that fails with a microphone-input error before Whisper or paste when the captured WAV is effectively silent.
- Added a second guard so whitespace-only transcription output cannot be pasted.
- Switched `WaveInEvent` to `DeviceNumber = -1` so Windows selects the wave mapper/default recording input instead of assuming device `0`.
- Bumped LafazFlow to `0.10.18`.
- Focused silent-audio tests pass, 3 tests; full `dotnet test` passes, 416 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Transient No-Speech Error Auto-Dismiss v0.10.19
- [x] Reproduce the UX issue from the error path: failed queued jobs stay in `RecordingState.Error` with no hide/reset path.
- [x] Summarize no-speech and silent-mic errors for the compact recorder shell.
- [x] Keep the full error message in `StatusDetail` for tooltip/log surfaces.
- [x] Auto-dismiss transient queued-dictation errors only if the same error is still active.
- [x] Bump LafazFlow to `0.10.19`.
- [x] Verify focused tests, full tests, build, safety scans, and stable publish/relaunch.

## Review: Transient No-Speech Error Auto-Dismiss v0.10.19
- Root cause: queued dictation failures called `SetError` but had no timed hide/reset path, so the mini recorder shell stayed pinned in `RecordingState.Error`.
- Added compact shell labels for no-speech and silent-microphone errors: `No speech` and `Mic silent`.
- Preserved the full error message in `StatusDetail` for tooltip/detail surfaces.
- Added guarded auto-dismiss for transient queued-dictation errors; it hides only if the same error is still active, so a newer recording is not hidden by an old timer.
- Bumped LafazFlow to `0.10.19`.
- Focused no-speech/silent-mic tests pass, 3 tests; full `dotnet test` passes, 419 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Fast Paste Dismiss And Exit Animation Polish v0.10.20
- [x] Separate visible paste completion from clipboard restore wait.
- [x] Restore clipboard asynchronously while logging restore success or failure.
- [x] Hide the mini recorder immediately after paste gesture success.
- [x] Guard mini recorder show/hide animation overlap for rapid dictation cycles.
- [x] Add regression coverage for hiding before clipboard restore completion.
- [x] Bump LafazFlow to `0.10.20`.
- [x] Verify tests, build, stable publish, launch smoke, and safety scans.

## Review: Fast Paste Dismiss And Exit Animation Polish v0.10.20
- Root cause: `ClipboardPasteService.PasteAsync` waited at least `1500ms` to restore the previous clipboard before returning, so the UI kept showing processing dots even though the text had already pasted.
- Changed paste completion to mean the paste gesture has been dispatched; clipboard restore now continues in the background and logs independently.
- Kept the delayed clipboard restore on the WPF UI context so the non-blocking restore path still uses the clipboard safely.
- Added a `ClipboardPasteResult` so paste logging can report target, gesture, restore scheduling, and restore delay without blocking UI dismissal.
- Hardened the mini recorder entrance/exit path so a new show cancels stale hide animations and resets opacity/scale/translation cleanly.
- Bumped LafazFlow to `0.10.20`.
- Full `dotnet test` passes, 420 tests; full `dotnet build` passes with 0 warnings; `git diff --check` passes.
- Republished `artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe` and `artifacts\stable-cuda-quality\LafazFlow.Windows\LafazFlow.Windows.exe`, then relaunched the pinned stable-single app.
- Stable launch smoke reports product version `0.10.20+10487b9d4f2018d17db7fc1482db252adb2211fd`.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as `token`.

## Plan: Invisible Shell After No-Speech Hotfix v0.10.21
- [x] Audit recent logs and confirm hotkeys/transcription continue while the shell can become visually absent.
- [x] Make the mini recorder entrance path self-heal when the window is technically visible but opacity is near zero.
- [x] Reset stale hide state when `Hide()` is called while already hidden.
- [x] Add source regression coverage for the self-healing show guard.
- [x] Update lessons with the WPF animation-state failure pattern.
- [x] Bump LafazFlow to `0.10.21`.

## Review: Invisible Shell After No-Speech Hotfix v0.10.21
- Root cause: the recorder window could be technically visible while opacity was near zero after overlapping hide/show or transient error dismissal, causing later recordings to run without a visible shell.
- Changed ShowBottomCenter to replay entrance animation when IsVisible is false, the hide flag is active, or opacity is below 0.05.
- Changed Hide to clear stale hide state when it is called while already hidden.
- Added source regression coverage for the invisible-visible self-healing guard.
- Bumped LafazFlow to 0.10.21.
- Full dotnet test passes, 421 tests; full dotnet build passes with 0 warnings; git diff --check passes.
- Republished artifacts\\stable-single\\LafazFlow.Windows\\LafazFlow.Windows.exe and artifacts\\stable-cuda-quality\\LafazFlow.Windows\\LafazFlow.Windows.exe, then relaunched the pinned stable-single app.
- Stable launch smoke reports product version 0.10.21+d9ed0ff53252da0bb6e4857b150099bdb478e193.

## Plan: Settings UI Redesign v0.10.22
- [x] Keep the black recorder shell out of scope.
- [x] Add Settings section navigation state to the view model.
- [x] Replace the raw single-scroll Settings window with a two-pane sidebar shell.
- [x] Regroup existing controls into Overview, Dictation, Models, Vocabulary, Sound, Clipboard, Diagnostics, and About.
- [x] Preserve existing bindings, browse handlers, diagnostics handlers, sound test handlers, Save, Cancel, and validation.
- [x] Add tests for sidebar navigation, selected section state, and recorder-shell non-involvement.
- [x] Bump LafazFlow to 0.10.22.

## Review: Settings UI Redesign v0.10.22
- Reworked only SettingsWindow.xaml; no mini recorder shell files were changed for this redesign.
- Added SettingsSection and SelectedSection so Settings opens on Overview and switches sections through the left sidebar.
- Moved technical runtime/model/VAD controls under Models, vocabulary textareas under Vocabulary, sound controls under Sound, clipboard behavior under Clipboard, diagnostics tables under Diagnostics, and folders/reset under About.
- Kept all existing settings persistence, validation, dialogs, browse actions, diagnostics actions, sound cue test buttons, and Save/Cancel behavior intact.
- Bumped LafazFlow to 0.10.22.
- Full dotnet test passes, 425 tests; full dotnet build passes with 0 warnings; git diff --check passes.
- Republished artifacts\\stable-single\\LafazFlow.Windows\\LafazFlow.Windows.exe and artifacts\\stable-cuda-quality\\LafazFlow.Windows\\LafazFlow.Windows.exe, then relaunched the pinned stable-single app.
- Stable launch smoke reports product version 0.10.22+c2f311a32b6331317bad37312040cddef4a18f8e and the second-launch Settings signal keeps the app responsive.
- Trademark scan found no forbidden public mentions. Public-readiness scan found no credentials; matches are GPL/docs words and local code identifiers such as token.

## Plan: Permanent CUDA Runtime Repair
- [x] Reproduce the configured CUDA CLI failure independently of LafazFlow.
- [x] Confirm the Windows native crash signature and compiler/runtime version mismatch.
- [x] Keep the current Quality profile, CUDA backend, CLI path, VAD settings, and `ggml-large-v3-turbo-q5_0.bin` model unchanged.
- [x] Deploy the matching app-local MSVC runtime beside the CUDA CLI.
- [x] Make the CUDA build and prerequisite scripts perform a real CLI smoke check.
- [x] Verify real CUDA transcription with the current model and VAD configuration.
- [x] Run the full application test suite and document the result.

## Review: Permanent CUDA Runtime Repair
- Root cause: `whisper-cli.exe` was built with MSVC 14.44 but loaded the machine-wide MSVC 14.28 runtime, crashing before argument parsing with Windows exception `0xC0000005` in `MSVCP140.dll`.
- Installed the matching redistributable VC143 runtime app-locally in `C:\Tools\whisper.cpp-cuda\bin`; the configured CUDA CLI path, Quality profile, VAD settings, and `ggml-large-v3-turbo-q5_0.bin` model remain unchanged.
- Updated the CUDA build script to deploy its matching app-local runtime and refuse success unless `whisper-cli --help` exits successfully.
- Updated the prerequisite check to report the app-local runtime version and fail on a broken CLI smoke check.
- Updated process failure reporting to retain stdout, signed and hexadecimal exit codes, and actionable native access-violation guidance.
- Verified the exact current CUDA + quality model + VAD pipeline on the RTX 4070: exit code 0 and approximately 0.87 seconds total processing for an 11.7-second retained recording.
- Focused Whisper service tests pass, 11 tests; full `dotnet test` passes, 527 tests; Release build succeeds with 0 warnings and 0 errors; `git diff --check` passes.
- Republished both `artifacts\stable-single\LafazFlow.Windows` and `artifacts\stable-cuda-quality\LafazFlow.Windows`; the app was left stopped for owner-controlled end-to-end dictation testing.

## Plan: Public Project Positioning And Release Readiness
- [x] Audit the current desktop technology stack, Overview page, GitHub description, README, and release channel.
- [x] Replace macOS-reference/MVP positioning with professional privacy-first Windows dictation messaging.
- [x] Document the current feature set, native technology stack, source setup, privacy model, and contribution workflow.
- [x] Approve and implement the Overview page redesign as a separate verified UI slice.
- [ ] Add a reproducible Windows release pipeline, package manifest, checksums, and clean-machine installation verification before publishing the first GitHub Release.

## Review: Overview Page Redesign
- Replaced the generic Overview title and stacked diagnostics cards with an everyday dictation-first hierarchy.
- Added a clear ready state, double-Shift instruction, local/private status, active runtime and model summaries, and a smaller setup-check action group.
- Preserved the existing dark identity, navigation, settings bindings, microphone/transcription checks, and Diagnostics page.
- Added XAML regression coverage for the new hierarchy and removal of the generic `Quick Actions` block.
- Focused Settings XAML tests pass, 19 tests; full `dotnet test` passes, 528 tests; Release build and isolated Release publish succeed; `git diff --check` passes.
- Pixel-level capture was unavailable because the Computer Use native bridge could not connect. After owner approval to terminate LafazFlow for verified updates, the normal stable artifacts were republished and the updated stable app was relaunched successfully.

## Review: Clean Second-Launch Shutdown
- Root cause: a second LafazFlow instance stored an already-disposed mutex in the application field, then `OnExit` attempted to release it and crashed with `ObjectDisposedException` after signaling the running instance.
- Keep failed-acquisition mutexes local and assign the application-owned mutex only after acquisition succeeds.
- Added lifecycle regression coverage so the disposed failed-acquisition handle cannot be stored again.
- Focused lifecycle tests pass, 4 tests; full `dotnet test` passes, 529 tests; Release build succeeds with 0 warnings and 0 errors.
- Republished both stable artifacts, relaunched the stable app, confirmed the primary process is responsive, and confirmed the second-launch Settings signal exits cleanly with code 0.

## Plan: Overview Badge And Scrollbar Polish
- [x] Replace the oversized oval local/private badge with compact badge geometry.
- [x] Replace default bright WPF scrollbar chrome with a dark orientation-aware template.
- [x] Add regression coverage for badge radius and scrollbar styling.
- [x] Verify tests, Release build, stable publish, relaunch, and direct-to-main push.

## Review: Overview Badge And Scrollbar Polish
- Replaced the `999`-radius oval status treatment with a compact 6px rounded badge and tighter padding.
- Added a dark WPF scrollbar template with restrained tracks, compact thumbs, horizontal and vertical paging behavior, hover feedback, and teal drag feedback.
- Focused Settings XAML tests pass, 20 tests; full `dotnet test` passes, 530 tests; Release build succeeds with 0 warnings and 0 errors; `git diff --check` passes.
- Republished both stable artifacts, relaunched LafazFlow successfully, confirmed the primary process remains responsive, and confirmed second-launch Settings signaling exits with code 0.

## Plan: WPF UI Reform Foundation
- [x] Adopt WPF UI 4.2 as the official Fluent component layer on .NET 9/WPF.
- [x] Register dark WPF UI themes and control resources application-wide.
- [x] Migrate Settings to `FluentWindow`, WPF UI `TitleBar`, rounded Windows 11 chrome, and Mica backdrop support.
- [x] Migrate Overview surfaces and actions to WPF UI cards and buttons.
- [x] Add regression coverage for the package, theme resources, Fluent shell, and migrated Overview controls.
- [x] Verify focused/full tests, stable publish, relaunch, and direct-to-main push.

## Review: WPF UI Reform Foundation
- Added WPF UI 4.2.0 to the existing .NET 9 WPF application; recording, transcription, hotkeys, clipboard, CUDA, and tray services remain unchanged.
- Registered WPF UI dark themes and control resources at application scope.
- Migrated Settings from a plain WPF `Window` to WPF UI `FluentWindow` with a Fluent title bar, rounded Windows 11 chrome, and Mica backdrop support.
- Migrated Overview hero/setup surfaces to WPF UI cards and its setup actions to WPF UI buttons with a primary action appearance.
- Added regression coverage for package version, theme dictionaries, Fluent shell, Mica, cards, and buttons.
- Focused Settings XAML tests pass, 21 tests; full `dotnet test` passes, 531 tests; Release build succeeds with 0 warnings and 0 errors; `git diff --check` passes.
- Republished both stable artifacts, relaunched LafazFlow, confirmed the primary process remains responsive, confirmed second-launch Settings signaling exits with code 0, and observed no new Windows application errors.

## Plan: Quality CUDA Transcription Latency Regression
- [x] Separate queue, formatting, paste, and Whisper timing from recent real dictations.
- [x] Identify the long-running `whisper-cli` process and its parent command.
- [x] Compare current Whisper latency with pre-WPF-UI Quality/CUDA dictations.
- [x] Audit final and live-preview process lifecycle, cancellation, and cleanup.
- [x] Reproduce with the current Quality profile, CUDA backend, CLI, VAD, and `ggml-large-v3-turbo-q5_0.bin` unchanged.
- [x] Implement a permanent root-cause fix with regression coverage.
- [x] Verify focused/full tests, Release build, stable publish, relaunch, and direct-to-main push.

### Investigation evidence
- The reported slow period averaged `1678ms` in Whisper and `1877ms` stop-to-done, compared with the preceding 30-run baseline of `940ms` and `1131ms`.
- Found an orphaned `whisper-cli --help` process and its `check-quality-prereqs.ps1` parent left alive since 19:30; terminated only those exact stale processes.
- Direct current-setting benchmarks returned to `878-1130ms` with the same Quality profile, CUDA CLI, VAD, prompt, threads, and large-v3-turbo Q5 model.
- Nine fresh real dictations averaged `897ms` Whisper and `1091ms` stop-to-done, slightly faster than the original baseline; recorder setup, queue handoff, post-processing, and paste also match the original baseline.
- WPF UI with the Fluent Settings window open did not reproduce the slowdown, so the UI migration is not the transcription bottleneck.

## Review: Quality CUDA Transcription Lifecycle Hardening v0.12.3
- Added one shared `WhisperProcessCoordinator` for final transcription, live preview, and in-app diagnostics.
- Serialized all Whisper CLI work so multiple model-loading processes cannot compete for CUDA resources.
- Final transcription now cancels active live-preview or diagnostic work before taking exclusive CLI ownership.
- Added workload deadlines, whole-process-tree termination, bounded output draining, and final best-effort cleanup so cancelled or timed-out processes cannot remain orphaned.
- Hardened `check-quality-prereqs.ps1` with a five-second smoke-check timeout and process-tree cleanup.
- Preserved the current Quality profile, CUDA CLI, large-v3-turbo Q5 model, VAD, 16 threads, prompt, and all user settings.
- Added real child-process regressions proving final-work preemption, timeout cleanup, and coordinator recovery.
- Focused lifecycle/transcription tests pass, 28 tests; full suite passes, 534 tests; Release build succeeds with 0 warnings and 0 errors; prerequisite smoke check passes; `git diff --check` passes.
- Republished both stable artifacts and relaunched `stable-single`; the responding process reports file version `0.12.3.0`, second-launch Settings signaling exits with code 0, and no `whisper-cli` process remains after startup.

## Plan: Recording Session Isolation And Trailing Speech Repair
- [x] Confirm the missing ending is absent from raw Whisper output, not removed by post-processing.
- [x] Replay the retained WAV with current, padded, and disabled VAD configurations.
- [x] Compare WAV duration with LafazFlow's logged recording duration and identify cross-session audio corruption.
- [x] Make microphone devices, callbacks, and WAV writers session-owned instead of shared mutable fields.
- [x] Add a regression proving a stopped session cannot write into or stop the next recording.
- [x] Bump the patch version and verify focused/full tests, Release build, real duration parity, stable publish, relaunch, and direct-to-main push.

## Review: Recording Session Isolation And Trailing Speech Repair v0.12.4
- Root cause: `AudioCaptureService` stored the active `WaveInEvent` and `WaveFileWriter` in shared mutable fields. A late callback from a stopped recording could therefore write into the next session's writer during rapid dictation cycles.
- Corrupted retained WAVs were approximately 1.5-2x longer than their logged sessions: `11.549s` became `23.05s`, `37.187s` became `73.5s`, and `25.177s` became `38.45s`.
- Replaying the affected WAV with current VAD, larger VAD padding, and VAD disabled could not recover `scenario-based`, proving VAD was not the root cause.
- Replaced service-level capture resources with session-owned input, callback, writer, active-state guard, and deterministic callback detachment.
- Late callbacks from stopped sessions are rejected and cannot write into or stop a later recording; overlapping active starts now fail loudly instead of replacing resources.
- Added regressions for late cross-session callbacks and active-session replacement.
- Focused capture/controller tests pass, 28 tests; full suite passes, 536 tests; Release build succeeds with 0 warnings and 0 errors; `git diff --check` passes.
- Real v0.12.4 verification passed twice: `14.135s` logged vs `13.900s` WAV and `16.783s` logged vs `16.550s` WAV. Both raw transcripts preserved the complete final phrase `such as this is the final ending.`
- Republished both stable artifacts and relaunched the pinned `stable-single` v0.12.4 build.
# Task: Launch Polish v0.13.0

## Plan: Silent Hidden Startup And First-Run Setup v0.13.0
- [x] Start the tray app without ever showing the blank 800x450 MainWindow that caused the launch flash.
- [x] Move shell initialization out of the Loaded event into an idempotent `InitializeShell` called from startup.
- [x] Add first-run detection to `SettingsStore` and open the Settings setup window on the very first launch.
- [x] Keep the second-launch single-instance path routed to Settings.
- [x] Add regression tests for hidden startup, first-run detection, and onboarding completion.
- [x] Bump LafazFlow from `0.12.4` to `0.13.0`.
- [x] Run focused tests, full tests, build, diff check, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Silent Hidden Startup And First-Run Setup v0.13.0
- Root cause: `App.OnStartup` called `MainWindow.Show()` and `MainWindow.OnLoaded` then called `Hide()`, so every launch painted a blank 800x450 dark window (and a brief taskbar entry) before hiding.
- `MainWindow` now starts as a hidden, non-taskbar, non-activating host window and is never shown; shell initialization moved to an idempotent `InitializeShell` called directly from app startup.
- Added `SettingsStore.IsFirstRun` and `MarkOnboardingComplete`; the very first launch now opens the Settings setup window automatically, then marks onboarding complete so later launches stay silent.
- Second-launch single-instance routing to Settings is preserved.
- Bumped LafazFlow to `0.13.0`.
- Focused startup/settings tests pass (31); full `dotnet test` passes (541); Release build passes with 0 warnings and 0 errors; `git diff --check` passes; public-readiness scan found no credentials.
- Published `artifacts\stable-single\LafazFlow.Windows` and `artifacts\stable-cuda-quality\LafazFlow.Windows`; relaunched the pinned stable-single app, which reports file version `0.13.0.0`. Launch smoke found no visible windows (hidden start verified) and no fresh crash events.

## Plan: Startup Acknowledgement Hotfix v0.13.1
- [x] Show a tray notification on every successful launch so the user knows the app started.
- [x] Keep first-run setup opening while notifying on later launches.
- [x] Add regression tests for the startup notification path.
- [x] Bump LafazFlow from `0.13.0` to `0.13.1`.
- [x] Run focused tests, full tests, build, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Startup Acknowledgement Hotfix v0.13.1
- Owner feedback: launching the app gave no signal that it started successfully; the first-run setup window did not appear on this machine because an existing profile already marked onboarding complete.
- Added `TrayIconService.ShowStartupNotification`, which shows a tray balloon on every successful launch: `LafazFlow is ready. Double-press Shift to dictate.`
- `InitializeShell` now calls the notification after the hotkey is active, so the tray hint appears only after startup actually completed.
- First-run behavior is unchanged: a truly fresh profile still opens the Settings setup window, and later launches get the tray acknowledgement instead.
- Bumped LafazFlow to `0.13.1`.
- Focused startup/tray tests pass (13); full `dotnet test` passes (543); Release build passes with 0 warnings and 0 errors; `git diff --check` passes. One pre-existing timing-sensitive RecorderController test flaked once under load and passed 5/5 in isolation and in the final full run.
- Published `artifacts\stable-single\LafazFlow.Windows` and `artifacts\stable-cuda-quality\LafazFlow.Windows`; relaunched the pinned stable-single app, which reports file version `0.13.1.0`; launch smoke shows no visible windows and no fresh crash events.

## Plan: Live Preview Trust v0.13.2
- [x] Make the live preview monotonic so words already shown are never removed by rolling-window regressions.
- [x] Stitch overlapping rolling-window transcripts by longest shared word overlap.
- [x] Show the latest words in the overlay with a leading ellipsis instead of freezing on the sentence start.
- [x] Let the overlay grow up to two compact lines so more of the live text is visible.
- [x] Add regression tests for monotonic growth, stitching, non-overlap appending, and latest-word display.
- [x] Bump LafazFlow from `0.13.1` to `0.13.2`.
- [x] Run focused tests, full tests, build, publish stable artifacts, relaunch pinned app, commit, and push.

## Review: Live Preview Trust v0.13.2
- Diagnosis evidence: the controlled five-dictation test captured all 18 words in every recording at the raw engine level; VAD on/off, `-sns` on/off, and beam search produced identical output; audio levels were healthy. The engine and microphone are capable on clear speech, so the perceived word loss was traced to the live preview: it showed the start of the transcript, trimmed the end, and suppressed regressive rolling-window updates, making words appear to vanish while recording.
- `RollingWhisperLiveTranscriptPreviewService` now maintains a monotonic displayed preview: words once shown are never removed. Rolling-window transcripts are stitched by the longest shared word overlap, and non-overlapping windows append instead of replacing.
- The mini recorder overlay now binds to `PreviewDisplay` (the latest words with a leading ellipsis when truncated), can grow up to two compact lines, and shows the full transcript on hover via tooltip.
- Bumped LafazFlow to `0.13.2`.
- Focused preview/view-model tests pass (72); full `dotnet test` passes (549); Release build passes with 0 warnings and 0 errors; `git diff --check` passes.
- Published `artifacts\stable-single\LafazFlow.Windows` and `artifacts\stable-cuda-quality\LafazFlow.Windows`; relaunched the pinned stable-single app, which reports file version `0.13.2.0`.

## Plan: Release Pipeline v1.0.0
- [x] Add bundled `whisper-cli.exe` fallback so a fresh user never needs to build or install whisper.cpp.
- [x] Move the default model folder to a per-user writable location (`%LocalAppData%\LafazFlow\Models`) so model downloads never require admin rights.
- [x] Add `scripts/package-windows-release.ps1` producing a self-contained portable ZIP with bundled whisper CLI, docs, licenses, and release safety checks.
- [x] Add an Inno Setup template and installer build support in the packaging script.
- [x] Add a GitHub Actions release workflow that tests, packages, and publishes artifacts to a GitHub Release on `v*` tags.
- [x] Add `docs/windows-runtime-setup.md` and a README Download section for end users.
- [x] Add regression tests for packaging artifacts, bundled CLI fallback, and user-writable model directory.
- [x] Bump LafazFlow from `0.13.2` to `1.0.0`.
- [x] Run focused tests, full tests, build, verify a real packaged ZIP end-to-end, commit, and push.

## Review: Release Pipeline v1.0.0
- Added `scripts/package-windows-release.ps1`: self-contained win-x64 publish, bundles the latest official CPU `whisper-cli.exe` (with `--help` smoke check), copies README/LICENSE/third-party notices/runtime docs, runs release safety checks (user audio, logs, settings, model binaries, credential patterns), and produces a portable ZIP plus an optional Inno Setup installer.
- Added `scripts/lafazflow-setup.iss` (installer template with Start Menu/desktop shortcuts) and `.github/workflows/release.yml` (tests on Windows, installs Inno Setup, packages, publishes ZIP + installer to a GitHub Release on `v*` tags or manual dispatch).
- Bundled CLI fallback: fresh installs and migrated profiles resolve a packaged `whisper-cli.exe` next to the app (schema 17), and the default model folder moved to `%LocalAppData%\LafazFlow\Models` so downloads never require admin rights.
- Added `docs/windows-runtime-setup.md` and a README Download section for end users.
- Bumped LafazFlow to `1.0.0`.
- Full `dotnet test` passes (557); Release build passes with 0 warnings and 0 errors; `git diff --check` passes.
- Verified end-to-end: `LafazFlow-1.0.0-win-x64-portable.zip` (80 MB) extracted and launched cleanly with the bundled whisper CLI; stable artifacts republished and pinned app relaunched as v1.0.0 with hidden start and no crash events.
- Findings: during packaged-app smoke, the mini recorder appeared only because the double-Shift hotkey fired on real Shift key presses (foreground app `Code`) - the hotkey is sensitive while typing; recorded as a follow-up, not changed in this slice. The first public GitHub release is intentionally not cut; the repo policy requires owner approval before tagging.

## Plan: Hotkey Hold Hardening v1.0.0
- [x] Fix auto-repeat detection in the low-level keyboard hook by tracking the Shift key's down/up state instead of reading a non-existent repeat flag from `KBDLLHOOKSTRUCT`.
- [x] Make the double-Shift detector reject any key-down that arrives while Shift is still held, so holding Shift never triggers dictation.
- [x] Recover missed key-up states through the existing stale timeout instead of self-healing into a trigger.
- [x] Add regression tests: held Shift with and without repeat flags never triggers; proper double-taps still trigger; missed key-up no longer triggers.
- [x] Verify end-to-end with simulated keyboard input: hold Shift does not start dictation, double-tap still does.
- [x] Full tests, build, republish stable artifacts, commit, push, then cut the first public GitHub release.

## Review: Hotkey Hold Hardening v1.0.0
- Root cause: the low-level keyboard hook read a WM_KEYDOWN-style repeat flag (`0x40000000`) from `KBDLLHOOKSTRUCT.flags`, which never exists there, so OS auto-repeat key-downs while holding Shift looked like fresh presses. The detector's missed-keyup self-heal then treated the first auto-repeat as a second tap and started dictation.
- The hook service now tracks the Shift key's down/up state and flags auto-repeats correctly; the detector rejects any key-down that arrives while Shift is still held (`repeat`/`already_down`) and recovers stuck states via the stale timeout instead of self-healing into a trigger.
- Live verification with simulated keyboard input: holding Shift with auto-repeats produced only `first_shift`/`repeat` rejections and no trigger; a genuine double-tap still fired `second_shift` and started recording as designed. The near-silent test recording was correctly rejected (no paste).
- Focused hotkey tests pass (14); full `dotnet test` passes (560); Release build clean; stable artifacts republished and pinned app relaunched.

## Plan: Release Pipeline CI Robustness v1.0.0
- [x] First GitHub Actions run failed because the runner has no audio output device: `NAudioSoundCuePlayer` threw `BadDeviceId` in its constructor, failing tests that build the real `SoundCueService`.
- [x] Make sound cues degrade gracefully: if the audio output device cannot be initialized, cues are silently skipped instead of crashing startup or tests.
- [x] Widen the timing margins of the process-coordinator timeout test so it is stable under CI load.
- [x] Add regression tests for the no-audio fallback.
- [x] Full tests, build, republish stable artifacts, commit, push, force-move the `v1.0.0` tag, and re-run the release workflow.

## Review: Release Pipeline CI Robustness v1.0.0
- First GitHub Actions run failed on the runner's missing audio device (`BadDeviceId`); `NAudioSoundCuePlayer` now falls back to a silent no-op player when the output device cannot be initialized, so the app also starts safely on machines without audio output.
- Widened the process-coordinator timeout test margins for CI load and made the whisper thread assertion machine-independent (`Math.Min(16, Environment.ProcessorCount)`).
- Full `dotnet test` passes (562); Release build clean; all three CI retries addressed; the `v1.0.0` tag now points at the verified code.

## Review: First Public Release v1.0.0
- Pushed tag `v1.0.0`; GitHub Actions ran the full suite, built the Inno Setup installer, packaged the portable ZIP, and published the release automatically.
- Live release: `LafazFlow-1.0.0-setup.exe` (54.4 MB) and `LafazFlow-1.0.0-win-x64-portable.zip` (79.0 MB) at https://github.com/itsLucas02/lafazflow-windows/releases/tag/v1.0.0.

# Task: Windows MVP Hotkey And Prerequisite Revision
