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
