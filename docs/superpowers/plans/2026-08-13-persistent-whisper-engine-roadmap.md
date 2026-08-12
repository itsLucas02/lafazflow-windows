# LafazFlow Persistent Whisper Engine Implementation Roadmap

**Status:** Plan-only; implementation requires explicit owner approval
**Prepared:** 13/08/2026 (Asia/Kuala_Lumpur)
**Primary outcome:** Make local Quality/CUDA dictation consistently fast without changing the owner's selected model or transcription settings, while preventing lost ending words and recovering automatically from native-engine failures.

## 1. Owner-approved product decisions

- Prepare Whisper, the selected model, CUDA, and VAD quietly when LafazFlow launches.
- Keep the engine ready until LafazFlow exits.
- Preserve the current `ggml-large-v3-turbo-q5_0.bin`, Quality profile, CUDA backend, VAD, English language mode, prompt, vocabulary, decode options, and thread count.
- Preserve live preview, but never allow preview to delay final transcription.
- If the engine crashes, restart it with the same settings and retry the unfinished dictation once.
- If performance is repeatedly abnormal, recover automatically and show one brief notice.
- Retain the existing one-shot CLI for diagnostics and a last-resort recovery attempt.
- Roll out to the owner's pinned local stable build first. A public GitHub release requires separate approval.
- Keep microphone selection/testing, broad punctuation changes, and unrelated UI redesign outside this roadmap.

## 2. Reference snapshot and traceability contract

Implementation must use these revisions as the initial evidence snapshot. Before coding a work package, verify whether its referenced file still exists at the pinned revision and capture any later revision only through an explicit roadmap note.

| Project | Pinned revision | Primary evidence used |
| --- | --- | --- |
| [FluidVoice](https://github.com/altic-dev/FluidVoice/tree/4ce0584f93efbb5240d07b5039e23b09487b6ce0) | `4ce0584f93efbb5240d07b5039e23b09487b6ce0` | Startup preload, provider readiness, audio-stop benchmarks, final/streaming managers, audio retirement/drain discipline |
| [Handy](https://github.com/cjpais/Handy/tree/37a26fd6ab905259d66affea57fff448288ca1aa) | `37a26fd6ab905259d66affea57fff448288ca1aa` | Retained loaded engine, model idle policy, panic recovery, real-time factor, stop/end-of-stream drain, final-work coordination |
| [VoiceInk](https://github.com/Beingpax/VoiceInk/tree/7023a6f7e16ba09c3b131fe71f8cc9e55c065f19) | `7023a6f7e16ba09c3b131fe71f8cc9e55c065f19` | Optional launch/wake prewarm, shared Whisper context, serialized context access, request settings, model lifecycle |
| [whisper.cpp](https://github.com/ggml-org/whisper.cpp/tree/592feef04a1802b18cbeffd0fd0eb5d02570c2ec) | `592feef04a1802b18cbeffd0fd0eb5d02570c2ec` | Native context/state APIs, abort callback, CUDA/VAD parameters, timings, cleanup, worker build source |

### Required traceability labels

Every implementation task and final review must use one of these labels:

- **Reference adopted:** behaviour is reproduced without a material change.
- **Reference adapted for Windows:** the proven behaviour is preserved but implemented through Windows/.NET/native mechanisms.
- **Evidence-backed improvement:** LafazFlow deliberately differs, with a documented reference limitation and a measurable acceptance test.

No architectural decision may be justified only by preference, elegance, convention, or AI intuition.

### Key reference paths

**FluidVoice**

- `Sources/Fluid/ContentView.swift`: delayed startup model preload.
- `Sources/Fluid/Services/ASRService.swift`: readiness single-flight, stop/audio-drain phase measurement, final transcription metrics, short-audio handling.
- `Sources/Fluid/Services/FluidAudioProvider.swift`: persistent ready provider, separate streaming/final state, real-time-factor logs, fallback final manager.
- `Sources/Fluid/Services/WhisperProvider.swift`: retained model/session, locked readiness state, model validation, backend selection.
- `Sources/Fluid/Services/AudioEngineRetirementDrain.swift`: bounded asynchronous audio-resource retirement.

**Handy**

- `src-tauri/src/managers/transcription.rs`: retained engine/session, single-flight model loading, idle watcher, engine lease, panic containment, real-time-factor measurement.
- `src-tauri/src/audio_toolkit/audio/recorder.rs`: command-driven recorder, final in-flight chunk handling, end-of-stream sentinel, bounded drain.
- `src-tauri/src/actions.rs`: loading at recording start, VAD preload, final processing coordination, exactly-once pipeline completion.
- `src-tauri/src/transcription_coordinator.rs`: recording/processing state coordination and repeated-input protection.

**VoiceInk**

- `VoiceInk/Services/ModelPrewarmService.swift`: optional delayed launch/wake prewarm.
- `VoiceInk/Transcription/Whisper/WhisperModelManager.swift`: shared loaded Whisper context.
- `VoiceInk/Transcription/Whisper/WhisperTranscriptionService.swift`: reuse of an already-loaded matching model context.
- `VoiceInk/Transcription/Whisper/LibWhisper.swift`: serialized native context access and request-level decode settings.
- `VoiceInk/Transcription/Engine/VoiceInkEngine.swift`: recording-time loading and pipeline cleanup boundaries.

## 3. Target user experience

### Launch flow

1. LafazFlow starts hidden in the tray.
2. The background worker starts.
3. It loads the exact selected model, CUDA backend, and VAD model.
4. Tray/Diagnostics moves from `Loading voice engine` to `Ready`.
5. The engine remains ready until app exit unless recovery or a settings change requires replacement.

### Dictation flow

1. Double Shift starts recording immediately.
2. Live preview remains best-effort and cannot build a backlog.
3. Double Shift stops recording.
4. LafazFlow collects every microphone buffer already captured for that session.
5. The WAV is finalized and verified.
6. Final audio is sent to the already-ready engine.
7. Raw output is formatted and pasted exactly once.

### Early dictation during startup

- Recording starts even if model preparation is incomplete.
- Preparation continues while the user speaks.
- After recording is finalized, final transcription waits for that one in-flight preparation task.
- LafazFlow must never start a second competing model load.

### Failure flow

- A native Whisper/CUDA crash does not close the WPF application.
- The same finalized audio remains available.
- LafazFlow starts one replacement worker with the same configuration and retries once.
- If replacement fails, the existing CLI receives one recovery attempt with identical settings.
- Text is pasted only after one complete successful result.

## 4. Architecture and evidence classification

| LafazFlow decision | Classification | Evidence and reason |
| --- | --- | --- |
| Preload shortly after app launch | Reference adopted | FluidVoice `ContentView.swift`; VoiceInk `ModelPrewarmService.swift` |
| Keep the model ready | Reference adapted for Windows | Handy retained `LoadedEngine`; FluidVoice ready providers; VoiceInk shared context |
| Fresh decode state per request | Reference adopted/adapted | VoiceInk actor/context discipline and upstream whisper.cpp state APIs |
| Drain final microphone samples before transcription | Reference adopted/adapted | Handy recorder end-of-stream drain; FluidVoice stop/audio drain measurement |
| Final transcription preempts preview | Reference adapted for Windows | Handy engine lease/coordinator; FluidVoice distinct streaming/final paths |
| Automatic engine reload after invalid native state | Reference adapted for Windows | Handy drops a panicked engine and reloads on next use |
| Crash-isolated background worker | Evidence-backed improvement | Handy documents Windows Whisper crashes; separate worker limits a native failure to the worker. Must pass forced-crash tests proving WPF survival and recovery. |
| Named pipe restricted to current user | Evidence-backed Windows adaptation | Needed for local process isolation without a network listener. Must pass unauthorized-client and malformed-message tests. |
| Sustained-degradation restart | Evidence-backed improvement | Reference tools measure latency/RTF but do not establish LafazFlow's exact recovery rule. Must prove fewer slow runs without restart loops. |

## 5. Delivery strategy

This is not one large rewrite. It is a sequence of independently verified work packages. A package cannot begin until the prior package's exit gate passes. Any contradiction with the pinned references or local measurements returns the project to planning.

### Milestone overview

| ID | Milestone | User-visible result | Depends on |
| --- | --- | --- | --- |
| M0 | Reference evidence pack | No behaviour change | None |
| M1 | Reproducible baseline and phase telemetry | Better Diagnostics only | M0 |
| M2 | Complete-audio finalization | Ending words reliably reach the WAV | M1 |
| M3 | Native persistent-engine proof | Repeated test audio avoids model reload | M1 |
| M4 | Worker protocol and supervisor | Engine can start, report ready, stop, and recover safely | M3 |
| M5 | Final dictation integration | Everyday dictation uses ready engine | M2, M4 |
| M6 | Live-preview integration | Preview remains, final stays higher priority | M5 |
| M7 | Crash, timeout, and CLI recovery | Failures recover without duplicate paste | M5 |
| M8 | Performance-health monitor | Sustained slowdowns trigger bounded recovery | M1, M7 |
| M9 | Plain-language status and Diagnostics | User can see Ready/Recovering and evidence | M8 |
| M10 | Full verification and local rollout | Owner uses updated stable build | M2-M9 |
| M11 | Public-release decision | Optional later GitHub release | Owner approval after observation |

## 6. Detailed work packages

### M0 — Reference evidence pack and licensing gate

**Purpose:** Turn inspiration into auditable engineering inputs.

**Tasks**

- [ ] Record the four pinned SHAs above in a machine-readable reference manifest under `docs/references/` or `eng/`.
- [ ] For each relevant path, capture the behaviour being adopted—not copied code—and its traceability label.
- [ ] Review FluidVoice, Handy, VoiceInk, whisper.cpp, and any binding/library licenses.
- [ ] Decide whether implementation will call whisper.cpp directly through a LafazFlow-owned native worker or adapt a maintained binding; compare version control, CUDA support, VAD parity, abort support, packaging, and crash boundaries.
- [ ] If any source is directly copied or substantially adapted, record file-level origin, revision, license, and modifications in `THIRD_PARTY_NOTICES.md` before merging that code.
- [ ] Confirm the selected whisper.cpp revision matches or intentionally replaces the current CUDA CLI revision.

**Deliverables**

- Reference manifest.
- Provenance matrix.
- Native dependency decision record.
- Updated third-party notices if necessary.

**Exit gate**

- Every M1-M10 design choice has at least one pinned source reference or an explicitly documented evidence gap.
- Licensing review identifies no incompatible reuse.
- No implementation starts from an unpinned upstream branch.

**Rollback:** Documentation-only; revise evidence pack.

### M1 — Reproducible baseline and privacy-safe telemetry

**Purpose:** Establish what “better” means before changing the engine.

**Reference basis**

- **Reference adopted:** Handy real-time-factor logging.
- **Reference adopted:** FluidVoice `ASR_BENCH` phase logs.
- **Reference adapted for Windows:** LafazFlow's existing `LatencyTrace` and diagnostic viewer.

**Tasks**

- [ ] Extend `tools/LafazFlow.TranscriptionBench` to execute a fixed retained-audio corpus with the exact current CLI/model/CUDA/VAD/prompt/thread configuration.
- [ ] Add a settings fingerprint that excludes secrets and transcript content.
- [ ] Measure: audio duration, queue wait, process start, model load, inference, process exit/drain, output read, formatting, paste, and total stop-to-done.
- [ ] Parse whisper.cpp timing output into structured fields where available.
- [ ] Record cold versus warm/replay runs distinctly.
- [ ] Capture raw/formatter/clipboard character counts and final-character categories without storing text.
- [ ] Run at least 30 current-engine repetitions across short, medium, and long retained recordings.
- [ ] Save the baseline summary as a dated, privacy-safe document or generated test artifact excluded from public user content.

**Expected files**

- Modify `tools/LafazFlow.TranscriptionBench/*`.
- Modify `src/LafazFlow.Windows/Services/LatencyTrace.cs`.
- Modify latency reporter/formatter/store classes.
- Add focused benchmark/parser tests.

**Exit gate**

- Same-audio repeated runs reproduce both normal and slow behaviour or establish a defensible current distribution.
- Baseline includes median, P90, P95, maximum, failure rate, empty-result rate, and inference real-time factor.
- Logs contain no transcript, PCM, clipboard contents, or sensitive full paths.
- Full test suite remains green.

**Rollback:** Telemetry fields are additive and parser-compatible with older rows.

### M2 — Complete-audio stop and WAV finalization

**Purpose:** Guarantee that final spoken words reach transcription input.

**Reference basis**

- **Reference adopted:** Handy handles the in-flight chunk, drains until an end-of-stream sentinel, and only then returns samples.
- **Reference adapted for Windows:** FluidVoice's measured stop/audio-drain lifecycle.
- **Reference adapted for NAudio:** Use `RecordingStopped` as the explicit completion signal.

**Tasks**

- [ ] Change `IAudioCaptureService.Stop()` to an asynchronous result-bearing finalization operation.
- [ ] Introduce explicit capture states: `Idle`, `Recording`, `Stopping`, `Finalized`, `Failed`.
- [ ] On stop request, keep the session callback attached while NAudio completes shutdown.
- [ ] Accept only buffers belonging to that exact capture session.
- [ ] Await `RecordingStopped`, then lock the session, detach callbacks, flush/finalize the writer, and publish finalized sample/byte counts.
- [ ] Use a two-second bounded stop deadline.
- [ ] On deadline, close only that input session, finalize audio already received, validate the WAV, and report `audio_drain_timeout`.
- [ ] Do not enqueue final transcription until finalization succeeds.
- [ ] Keep the processing visual responsive while finalization completes.

**Tests**

- [ ] A final buffer arriving after stop request is included.
- [ ] An old session's delayed callback cannot write into a new session.
- [ ] Rapid back-to-back sessions retain correct sample ownership.
- [ ] Device removal and `RecordingStopped` errors finalize safely or fail loudly.
- [ ] Stop timeout cannot deadlock the UI or queue.
- [ ] WAV header, byte count, sample count, and duration agree.
- [ ] Real spoken ending phrases survive at least ten repeated stop tests.

**Exit gate**

- All recorder/controller tests pass.
- Real WAV duration matches captured-session duration within normal buffer tolerance.
- No lost final buffer in deterministic tests.
- Current CLI transcription remains functional; persistent worker not yet required.

**Rollback:** Restore the synchronous stop adapter only if async finalization fails verification; retain session isolation from v0.12.4.

### M3 — Native persistent-engine proof of concept

**Purpose:** Prove model reuse and CUDA/VAD parity before integrating with the app.

**Reference basis**

- **Reference adopted:** Handy retains one `LoadedEngine`/session.
- **Reference adopted:** FluidVoice/VoiceInk retain ready model contexts.
- **Reference adapted for Windows:** Build a command-line test worker from pinned whisper.cpp.

**Tasks**

- [ ] Create `native/LafazFlow.WhisperWorker` with no WPF dependency.
- [ ] Load the configured model once at process startup.
- [ ] Create/reset fresh decode state for every request.
- [ ] Match all current CLI decode settings: language, prompt/carry prompt, threads, temperature, no fallback, non-speech suppression, max context, VAD model and thresholds.
- [ ] Provide request cancellation through whisper.cpp's abort callback.
- [ ] Return raw text plus load/inference/timing metadata.
- [ ] Release per-request state after every request and model/CUDA resources on shutdown.
- [ ] Run the existing retained corpus repeatedly within one process.

**Proof comparisons**

- [ ] Worker raw output versus CLI raw output on identical files/settings.
- [ ] First worker request versus later requests.
- [ ] Memory and VRAM stability across 100 requests.
- [ ] Cancellation followed by a successful later request.
- [ ] Invalid/corrupt audio rejection.

**Exit gate**

- No model reload occurs after initialization with an unchanged fingerprint.
- Output is equivalent or explainably better; unexplained text regression blocks progress.
- Warm median improves by at least the measured repeated-load cost.
- No unbounded RAM/VRAM growth across 100 requests.
- Cancellation leaves the worker reusable.

**Rollback:** Discard the proof worker without touching LafazFlow's production transcription path.

### M4 — Versioned local protocol and worker supervisor

**Purpose:** Make the native worker safe and manageable from the WPF app.

**Reference basis**

- **Reference adapted for Windows:** Handy's engine manager and load single-flight.
- **Evidence-backed improvement:** Process isolation plus a current-user-only named pipe.

**Protocol requirements**

- Versioned, length-prefixed binary messages.
- Operations: `Initialize`, `Preview`, `Final`, `Cancel`, `Health`, `Shutdown`.
- Required fields: protocol version, request ID, recording/session ID, workload, settings fingerprint, deadline, audio format, sample count, bounded payload size.
- Boundary format: 16 kHz mono signed 16-bit PCM.
- Maximum request size derived from LafazFlow's maximum recording duration plus a small header allowance.
- Unknown operations, malformed lengths, wrong formats, stale IDs, and fingerprint mismatches are rejected.

**Supervisor tasks**

- [ ] Add a single-flight startup task.
- [ ] Start worker hidden as a child of LafazFlow and capture exact PID/identity.
- [ ] Restrict pipe access to the current Windows user.
- [ ] Implement readiness timeout, health query, graceful shutdown, bounded reap, and exact-child termination.
- [ ] Calculate configuration fingerprint and replace the worker only when necessary.
- [ ] Start-and-prove a replacement before retiring a healthy idle worker.
- [ ] Record worker state transitions without transcript/audio content.

**Tests**

- Partial reads/writes, malformed length, oversized request, invalid version, stale response, wrong request ID, disconnect, timeout, unauthorized client, worker exit, and app shutdown.
- Two concurrent startup calls produce one worker.
- Settings change cannot cross responses between old and new workers.
- App exit leaves no worker process.

**Exit gate**

- Protocol fuzz/negative tests pass.
- Forced worker termination never terminates WPF.
- Supervisor reliably reaches `Ready`, `Recovering`, or terminal `Unavailable` without hanging.
- No network listener exists.

**Rollback:** Supervisor remains unused by production transcription; CLI path stays authoritative.

### M5 — Final dictation integration

**Purpose:** Route everyday final transcription through the ready worker.

**Reference basis**

- **Reference adapted for Windows:** Handy retained engine use.
- **Reference adapted for Windows:** FluidVoice skips readiness work when the provider is already ready.

**Tasks**

- [ ] Introduce an engine-neutral `ITranscriptionEngine` contract.
- [ ] Adapt current CLI service behind the same contract.
- [ ] Add worker-backed implementation.
- [ ] Initialize the worker during hidden shell startup without blocking hotkey registration or tray responsiveness.
- [ ] If recording starts during preparation, reuse the same readiness task.
- [ ] Send only finalized audio to final transcription.
- [ ] Preserve existing formatter, vocabulary, continuation, clipboard, and paste ordering.
- [ ] Keep one immutable dictation ID from recording through paste.
- [ ] Mark delivery committed immediately before clipboard/paste so retry logic cannot duplicate a paste.

**Tests**

- Startup preparation does not block app startup.
- First dictation waits for existing initialization rather than starting another worker.
- Later dictations reuse the same worker and model.
- Settings shown in Diagnostics match the request and worker fingerprint.
- Exactly one paste on success.
- No paste for empty/failed result.

**Exit gate**

- Final dictation uses the worker under the owner's exact Quality/CUDA profile.
- CLI remains callable for diagnostics and recovery.
- Retained-corpus text does not regress.
- Full suite and Release build pass.

**Rollback:** Switch engine selection back to CLI without changing user model/backend settings.

### M6 — Live preview integration and final priority

**Purpose:** Keep helpful live text without harming final latency or stability.

**Reference basis**

- **Reference adapted for Windows:** FluidVoice separate streaming/final managers.
- **Reference adapted for Windows:** Handy engine lease and final pipeline coordination.

**Tasks**

- [ ] Keep at most one running preview and one newest pending preview.
- [ ] Drop superseded pending previews rather than queueing them.
- [ ] On final stop, cancel preview through the worker abort mechanism and await a short bounded handoff.
- [ ] Final transcription obtains exclusive engine use before diagnostics or new preview work.
- [ ] Preserve monotonic display stitching from LafazFlow v0.13.2.
- [ ] Ignore preview responses whose recording/session ID is stale.

**Exit gate**

- Preview load cannot increase final queue delay beyond the defined handoff bound.
- Rapid recordings cannot display stale preview from another session.
- Final raw transcript remains authoritative.
- Preview-disabled and preview-enabled final latency remain within the accepted difference.

**Rollback:** Disable worker preview and retain final worker path; live preview may temporarily use the existing interruptible CLI only if it cannot compete with final work.

### M7 — Crash, timeout, retry, and CLI recovery

**Purpose:** Recover predictably without duplicate output.

**Reference basis**

- **Reference adapted for Windows:** Handy drops a panicked engine and reloads.
- **Evidence-backed improvement:** Automatic same-audio retry in an isolated replacement worker.

**Retry policy**

- Retry only before delivery is committed.
- Retryable: worker exit, broken pipe, request timeout, invalid response, native abort not caused by user cancellation.
- Not retryable: user cancellation, invalid audio, missing model/VAD, invalid settings, or delivery already committed.
- One replacement-worker retry, then one identical-settings CLI recovery attempt.
- A recovery lock prevents multiple workers from being started by concurrent failures.

**Tests**

- Crash during load, preview, final inference, response transfer, and shutdown.
- Hung worker and broken pipe.
- Replacement readiness failure.
- Retry succeeds and pastes once.
- Worker retry plus CLI recovery succeeds and pastes once.
- Both fail, paste nothing, retain audio, show compact error.
- No orphan or zombie worker/CLI process.

**Exit gate**

- Forced native crash never closes LafazFlow.
- Exactly-once delivery property is proven in automated tests.
- Recovery never changes model/backend/VAD/settings silently.

**Rollback:** Disable automatic retry and use the proven CLI path while retaining crash evidence.

### M8 — Sustained performance-degradation monitor

**Purpose:** Detect real slowdowns without reacting to a single outlier.

**Reference basis**

- **Reference adopted:** Handy and FluidVoice duration/real-time-factor measurements.
- **Evidence-backed improvement:** LafazFlow's rolling health and bounded automatic restart policy.

**Eligibility and baseline**

- Key health samples by worker/configuration fingerprint.
- Exclude cold, retried, cancelled, failed, and sub-two-second dictations from baseline training.
- Establish baseline after ten successful warm dictations.
- Retain the latest 30 eligible healthy samples.

**Initial slow-run rule**

A run is slow only when both apply:

- Inference is at least 750 ms above the baseline median.
- Inference real-time factor is at least 1.75 times the baseline median for comparable audio duration.

Declare sustained degradation only when three of the latest five eligible runs are slow.

**Recovery rule**

- Restart once with the same fingerprint.
- Mark the next success as recovery validation.
- Suppress another degradation restart for ten minutes.
- Crashes/timeouts remain immediate lifecycle failures and do not wait for the slow-run rule.

**Calibration task**

- [ ] Replay recorded normal/slow distributions through the detector.
- [ ] Confirm no restart for isolated current outliers.
- [ ] Confirm detection for intentionally injected sustained delay.
- [ ] Adjust thresholds only from captured evidence and record the reason.

**Exit gate**

- No restart loop under injected sustained slowdown.
- One outlier never restarts the engine.
- Sustained injected slowdown triggers one recovery and records the outcome.
- Diagnostic summaries remain understandable and privacy-safe.

**Rollback:** Disable automatic degradation restart while retaining monitoring and warnings.

### M9 — Plain-language status and Diagnostics

**Purpose:** Make readiness and recovery understandable without exposing implementation jargon.

**Normal statuses**

- `Loading voice engine`
- `Ready`
- `Recovering voice engine`
- `Using recovery engine`
- `Voice engine needs attention`

**Tasks**

- [ ] Add worker readiness/backend/model summaries to Overview and Diagnostics without raw internal paths on the everyday surface.
- [ ] Add cold/warm median and P95, last recovery reason/outcome, engine uptime, and current fingerprint-safe identity.
- [ ] Preserve advanced CLI paths and existing technical logs.
- [ ] Add final-character category and character-count stage diagnostics for punctuation investigations.
- [ ] Ensure startup notification is emitted only when the hotkey is registered and engine status is truthful.

**UI boundary**

- This is a status integration, not a Settings redesign.
- No new engine-unload selector is added because the owner chose keep-ready until exit.

**Exit gate**

- A non-programmer can distinguish loading, ready, recovering, and failed states.
- UI reports the active Quality/CUDA model accurately.
- No transcript/audio contents appear in status or logs.

**Rollback:** Hide new status fields; engine functionality remains testable through logs.

### M10 — Full verification and owner-local rollout

**Purpose:** Prove the complete system before considering it finished.

**Automated verification**

- [ ] Focused unit and integration tests for M1-M9.
- [ ] Full `dotnet test LafazFlow.Windows.sln`.
- [ ] Release build with zero errors and warnings.
- [ ] Native worker build and readiness smoke tests for CPU and the owner's CUDA runtime.
- [ ] `git diff --check` and public safety/credential scans.
- [ ] Packaging dry run confirms worker, CLI, native runtimes, licenses, and notices are complete.

**Performance verification**

- [ ] Same retained corpus, hardware, settings, and workload before versus after.
- [ ] At least 30 warm final dictations.
- [ ] At least 100 worker requests for memory/VRAM stability.
- [ ] Report cold load, warm median, P90, P95, maximum, RTF, stop-to-paste, failures, empties, and restarts.
- [ ] Warm P95 must be at least 30% below the one-shot baseline.
- [ ] Warm median must improve by at least the measured repeated model-load cost.
- [ ] No unexplained process-start/cleanup spikes.

**Quality and reliability verification**

- [ ] Raw transcript comparison on retained corpus.
- [ ] No increase in missing final phrases, empty results, or punctuation-stage changes.
- [ ] Ten repeated spoken ending tests.
- [ ] Rapid back-to-back dictation.
- [ ] Forced crash, hang, disconnect, and retry.
- [ ] App restart, Windows sleep/wake, and microphone device change.
- [ ] Exactly one paste after recovery; zero paste after terminal failure.
- [ ] No surviving worker or CLI processes after shutdown.

**Local rollout**

- [ ] Stop the currently running pinned LafazFlow process if necessary.
- [ ] Publish both stable profiles.
- [ ] Launch the actual pinned stable executable.
- [ ] Confirm version, process path, hidden startup, hotkey, readiness status, and no fresh Windows crash event.
- [ ] Owner uses the build for normal hands-free dictation before any public-release proposal.

**Exit gate**

- Every acceptance requirement passes.
- Any failed acceptance item returns to its owning milestone; the roadmap is not declared complete.

**Rollback:** Republish the last verified CLI-based stable build or select CLI compatibility execution while preserving the exact current settings and captured diagnostics.

### M11 — Optional public release

This milestone is explicitly outside current implementation approval.

After successful local observation:

- [ ] Summarize real-use performance and recovery evidence.
- [ ] Select semantic version based on user-visible scope.
- [ ] Update README/runtime documentation and release notes.
- [ ] Verify clean-machine CPU package and document optional CUDA configuration.
- [ ] Obtain explicit owner approval to tag and publish.
- [ ] Run the existing GitHub release workflow and verify installer/portable artifacts.

## 7. Cross-cutting engineering rules

### Privacy

- No transcript text, PCM/audio content, clipboard contents, window titles, or sensitive full paths in performance logs.
- Retained diagnostic recordings remain controlled by existing user settings.
- Named-pipe messages never leave the local machine.

### Exactly-once paste

- A dictation owns one immutable ID.
- Recovery may rerun transcription only before delivery commit.
- Clipboard/paste begins only after one final result is selected.
- Delivery commit is irreversible for retry decisions even if clipboard restoration later fails.

### Settings fidelity

- No silent CPU fallback.
- No silent model substitution.
- No silent VAD disablement.
- No silent language, prompt, thread, or quality change.
- Any mismatch blocks readiness and explains the exact category in Diagnostics.

### Process ownership

- LafazFlow terminates only the exact worker/CLI process it started and its verified child tree.
- Never kill unrelated `whisper-cli` processes by name during normal runtime.
- Startup and recovery are single-flight.

### Scope control

- Microphone device selection/testing follows after this roadmap unless explicitly reprioritized.
- Question-mark rules require stage evidence and focused examples; they are not bundled into engine migration.
- No unrelated UI reform or public release occurs inside this roadmap.

## 8. Roadmap progress reporting

After each milestone, update `tasks/todo.md` with:

- Milestone status.
- Reference traceability labels and links.
- Files changed.
- Tests and measured results.
- Known limitations.
- Rollback readiness.
- Whether the next milestone's entry conditions are satisfied.

An implementation milestone is not “done” because code compiles. It is done only after its exit gate passes.

## 9. Final definition of success

The roadmap is complete only when the owner's local stable LafazFlow build:

- Prepares the exact selected Whisper/CUDA engine quietly at launch.
- Reuses the loaded model across warm dictations.
- Preserves final audio buffers and ending words.
- Keeps preview from delaying final transcription.
- Survives and recovers from a forced native-engine crash.
- Detects sustained degradation without restart loops.
- Pastes exactly once after success and never after terminal failure.
- Improves warm P95 latency by at least 30% against the same-audio one-shot baseline.
- Shows no transcript-quality regression on the retained corpus.
- Leaves no orphan worker/CLI processes.
- Has passed automated verification and real owner hands-free observation.
