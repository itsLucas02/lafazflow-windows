# Task: Windows MVP Hotkey And Prerequisite Revision

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
