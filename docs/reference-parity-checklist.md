# LafazFlow Windows Reference Parity Checklist

This checklist tracks how closely LafazFlow Windows matches the owner's macOS reference workflow while staying Windows-native, local-first, and public-safe.

Status values:
- `Done`: implemented and verified well enough for daily use.
- `Partial`: usable, but polish or reliability work remains.
- `Missing`: not implemented yet.

| Area | Target Behavior | Current Status | Evidence | Next Fix Slice |
| --- | --- | --- | --- | --- |
| Global hotkey | Double Shift starts recording; Double Shift stops and queues transcription without blocking the next recording. | Done | `DoubleShiftDetectorTests`, rapid dictation queue tests, daily owner testing. | None unless timing complaints return. |
| Startup behavior | App starts silently, registers hotkey, and keeps the mini recorder hidden until dictation or an error needs attention. | Done | Shell polish notes and launch smoke checks in `tasks/todo.md`. | Settings/release polish only. |
| Single instance | Launching LafazFlow again should not create duplicate hotkey listeners and should route to the existing app. | Done | `SingleInstanceTests`; second-launch smoke recorded in `tasks/todo.md`. | Installer shortcut polish. |
| Tray behavior | Tray icon exposes Settings, Open Logs, and Exit, with useful idle/recording/transcribing/error tooltip state. | Partial | Tray service exists and shell UX tests pass, but no broad manual checklist yet. | Task 7: Settings UX And Runtime Diagnostics. |
| Recorder shell placement | Compact floating shell stays bottom-centered, avoids startup clutter, and does not shift during preview/status changes. | Done | Mini recorder layout tests and owner feedback after shell stability fixes. | Task 5 only for visual fine-tuning. |
| Version visibility | Compact shell shows a small visible version badge from the app assembly version. | Done | `MiniRecorderViewModelTests.AppVersionUsesCompactMajorMinorFormat`; current target is `v0.7`. | Continue version bumps per roadmap. |
| Visual motion | Recorder entrance, exit, audio meter, processing dots, and live transcript expansion feel smooth and do not crash. | Partial | Motion constants, processing dots, and animation easing have been refined; manual owner feel-check remains useful. | Continue tuning only after daily-use feedback. |
| Audio meter | Voice-reactive bars respond to input level with stable dimensions and aqua/cyan height-based color. | Done | `MiniRecorderVisualSpecTests` cover bar height, smoothing, and color behavior. | Task 5 for fine tuning only. |
| Audio cues | Start, stop/transcribing, completion, and error cues play from bundled local assets without Windows notification sounds. | Done | Cue settings, volume control, non-crashing playback, and recorder timing tests pass. | Revisit only if owner wants different cue assets. |
| Local transcription | Default flow uses local `whisper.cpp` and local model files without cloud transcription. | Done | README, settings tests, Whisper CLI tests, and successful local latency logs. | Task 7 for clearer runtime checks. |
| Quality profile | Quality mode can use the local quantized large turbo model, CUDA backend, and VAD when configured. | Partial | CUDA activation and VAD checks are implemented; settings UX still needs clearer health/status. | Task 7: Settings UX And Runtime Diagnostics. |
| English-only decoding | English dictation should stay English even with multilingual models. | Done | Hardened decode flags and tests from the English-only dictation slice. | Task 3 if new language drift appears. |
| Live preview | While recording, preview should feel helpful and never block final paste. | Partial | Preview is async and final transcript remains authoritative; logs still show regressive preview attempts. | Task 4: Latency And Fluidity Instrumentation. |
| Rapid dictation | After stopping one dictation, the user can start the next while previous transcription/paste finishes in order. | Done | Queue tests and daily use; pending jobs use processing indicator. | Task 4 if queue latency becomes visible. |
| Paste behavior | Final transcript is pasted into the original target app; Cursor-like targets use compatible paste behavior; clipboard restore is best-effort. | Partial | Clipboard tests pass and recent logs show successful paste; paste itself still costs about 1.6s. | Task 4, then Task 7 for diagnostics. |
| Formatting cleanup | Remove non-speech markers and handle obvious sentence/question/continuation formatting locally. | Partial | Formatter tests cover blank/audio/music markers and some question/continuation cases; owner still reports quality gaps. | Task 3: Dictation Quality And Developer Vocabulary. |
| Developer vocabulary | Common coding/product terms and owner-specific words are corrected offline without broad English rewrites. | Partial | Vocabulary tests cover many observed variants; more daily terms remain likely. | Task 3: Dictation Quality And Developer Vocabulary. |
| Settings window | User can configure local paths, profiles, backend, paste behavior, preview, vocabulary, and diagnostics. | Partial | Settings tests pass; UX needs clearer Fast/Quality status and runtime checks. | Task 7: Settings UX And Runtime Diagnostics. |
| Diagnostics | Logs and latency rows are local, privacy-safe, and accessible from Settings/tray. | Partial | Latency viewer, compact latest summary, and crash logging are implemented; runtime health checks still need Task 7. | Task 7: Settings UX And Runtime Diagnostics. |
| Crash resilience | UI animation exceptions are logged and narrowly recoverable; app launch smoke shows no fresh crash events. | Done | `AppCrashLogServiceTests`, custom animation tests, `v0.2` launch smoke. | Revisit only after new crash evidence. |
| Public repository hygiene | Public docs avoid third-party trademark references and do not commit models, logs, recordings, secrets, or local settings. | Done | Trademark and public-readiness scans are part of every release slice. | Maintain every release. |
| Runtime setup docs | A user can understand required local Whisper CLI/model/CUDA/VAD setup. | Partial | README covers model basics; full Windows runtime guide is not complete. | Task 8: Installer And Release Packaging. |
| Release packaging | User can install or run a clean packaged build without relying on development artifact folders. | Missing | Current stable use depends on `artifacts\stable-single`. | Task 8: Installer And Release Packaging. |

## Next Recommended Order

1. Task 7: Settings UX And Runtime Diagnostics.
2. Task 8: Installer And Release Packaging.

Task 7 is next because cue polish is implemented and the remaining daily-use gaps are clearer runtime health, model/backend status, and diagnostic actions.
