# LafazFlow Polish Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining quality, resilience, UX, and release-readiness gaps so LafazFlow feels like a dependable native Windows dictation app while preserving local/offline-first transcription.

**Architecture:** Implement this as small shippable slices, not one large rewrite. Each slice must update versioning, add focused regression coverage, publish the pinned stable build, smoke launch it, scan for public-safety issues, commit, and push before moving to the next slice.

**Tech Stack:** .NET 9 WPF, xUnit, Windows Event Log checks, local `whisper.cpp` CLI, CUDA/VAD optional quality runtime, NAudio sound cues, Windows tray/clipboard/UI Automation APIs.

---

## Versioning Policy

Current version source: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`

Current badge source: `src/LafazFlow.Windows/UI/MiniRecorderViewModel.cs`

Rules:
- For each user-visible shipped polish slice, increment the pre-1.0 minor version: `0.1.0` -> `0.2.0` -> `0.3.0`.
- The mini recorder badge continues to show compact major/minor only: `v0.2`, `v0.3`, and so on.
- For emergency hotfixes inside the same shipped slice, increment patch internally: `0.2.0` -> `0.2.1`; badge may remain `v0.2` unless a dedicated tooltip/details view is added.
- Use `1.0.0` only when the app has a proper release package/installer, resilient crash handling, stable single-instance/tray behavior, and documented runtime setup.

Every task below must include:
- Update `<Version>` in `src/LafazFlow.Windows/LafazFlow.Windows.csproj`.
- Add or update tests for version display if behavior changes.
- Publish both `artifacts\stable-single\LafazFlow.Windows` and `artifacts\stable-cuda-quality\LafazFlow.Windows`.
- Relaunch the pinned `stable-single` executable unless the owner asks to keep the current process running.

## Files And Responsibilities

- `src/LafazFlow.Windows/App.xaml.cs`: application-level exception handling and graceful shutdown/recovery hooks.
- `src/LafazFlow.Windows/Services/AppCrashLogService.cs`: write privacy-safe crash records to the local log.
- `src/LafazFlow.Windows/Services/IAppCrashLogService.cs`: testable crash logging interface.
- `src/LafazFlow.Windows/Services/LatencyTrace.cs`: extend measured stages only when needed.
- `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml`: visual polish for compact shell, status, and animation states.
- `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml.cs`: animation safety, motion timing, and layout update behavior.
- `src/LafazFlow.Windows/UI/MiniRecorderVisualSpec.cs`: central motion, spacing, color, and timing constants.
- `src/LafazFlow.Windows/UI/SettingsWindow.xaml`: settings usability improvements.
- `src/LafazFlow.Windows/UI/SettingsViewModel.cs`: settings actions/status for diagnostics, model mode, and tests.
- `src/LafazFlow.Windows/Services/TranscriptionTextFormatter.cs`: punctuation, casing, marker removal, and final transcript cleanup.
- `src/LafazFlow.Windows/Services/VocabularyCorrectionService.cs`: deterministic local technical vocabulary corrections.
- `docs/reference-parity-checklist.md`: public-safe parity checklist using neutral wording such as "macOS reference workflow".
- `docs/windows-runtime-setup.md`: install/runtime setup for users.
- `tasks/todo.md`: plan/review status for each slice.
- `tasks/lessons.md`: correction patterns learned from owner feedback.
- `tests/LafazFlow.Windows.Tests/*`: focused regression coverage for each slice.

---

## Task 1: Crash Resilience And Animation Safety

**Target version:** `0.2.0`

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Modify: `src/LafazFlow.Windows/App.xaml.cs`
- Create: `src/LafazFlow.Windows/Services/IAppCrashLogService.cs`
- Create: `src/LafazFlow.Windows/Services/AppCrashLogService.cs`
- Modify: `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml.cs`
- Modify: `tests/LafazFlow.Windows.Tests/MiniRecorderVisualSpecTests.cs`
- Create: `tests/LafazFlow.Windows.Tests/AppCrashLogServiceTests.cs`
- Modify: `tasks/todo.md`
- Modify: `tasks/lessons.md`

Steps:
- [ ] Add a failing test proving crash logs are privacy-safe and include exception type, message, source, and timestamp without transcript/audio contents.
- [ ] Implement `IAppCrashLogService` and `AppCrashLogService` using the existing LafazFlow log directory.
- [ ] Wire `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, and `TaskScheduler.UnobservedTaskException` in `App.xaml.cs`.
- [ ] For WPF dispatcher exceptions, log the crash and show a short recorder/tray error if the UI is still alive; do not silently swallow fatal exceptions unless the exception is known recoverable.
- [ ] Add animation guards in `MiniRecorderWindow.xaml.cs` so width/height animations always have numeric current values before `DoubleAnimation` starts.
- [ ] Bump project version to `0.2.0`.
- [ ] Verify: focused crash/mini-recorder tests, full build, full tests, launch smoke, Windows Event Log no fresh LafazFlow crash after launch.
- [ ] Publish stable builds, relaunch pinned path, public scan, commit, and push.

## Task 2: Reference Parity Audit Checklist

**Target version:** `0.3.0`

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Create: `docs/reference-parity-checklist.md`
- Modify: `tasks/todo.md`
- Modify: `README.md` only if it needs public-safe setup wording.

Steps:
- [ ] Create a public-safe checklist with neutral labels: hotkey behavior, startup behavior, tray behavior, audio cues, visual motion, live preview, transcription latency, final paste, formatting, settings, crash behavior, installer/release.
- [ ] Add a "Current Status" column with `Done`, `Partial`, or `Missing`.
- [ ] Add a "Next Fix Slice" column so future work is traceable.
- [ ] Do not mention third-party product/trademark names in the document.
- [ ] Bump project version to `0.3.0`.
- [ ] Verify trademark scan, docs scan, full build/test, public scan, publish/relaunch, commit, and push.

## Task 3: Dictation Quality And Developer Vocabulary

**Target version:** `0.4.0`

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Modify: `src/LafazFlow.Windows/Services/TranscriptionTextFormatter.cs`
- Modify: `src/LafazFlow.Windows/Services/VocabularyCorrectionService.cs`
- Modify: `src/LafazFlow.Windows/Core/AppSettings.cs` if prompt/schema changes are needed.
- Modify: `src/LafazFlow.Windows/Services/SettingsStore.cs` if schema changes are needed.
- Modify: `tests/LafazFlow.Windows.Tests/TranscriptionTextFormatterTests.cs`
- Modify: `tests/LafazFlow.Windows.Tests/VocabularyCorrectionServiceTests.cs`
- Modify: `tests/LafazFlow.Windows.Tests/SettingsStoreTests.cs` if schema changes are needed.

Steps:
- [ ] Add failing tests for observed developer dictation terms: `Supabase`, `Vercel`, `Tailscale`, `Netlify`, `Mintlify`, `MediBrave`, `Luqman`, `shadcn`, `commit`, `Vite`.
- [ ] Add failing tests for common formatting complaints: question endings, "wait, why", continuation after comma, and removal of non-speech markers.
- [ ] Add deterministic local corrections only for high-confidence observed variants; avoid broad English rewrites.
- [ ] Expand the local prompt only if it helps local/offline recognition and preserve custom user prompts through migration.
- [ ] Bump project version to `0.4.0`.
- [ ] Verify focused formatter/vocabulary/settings tests, full build/test, publish/relaunch, public scan, commit, and push.

## Task 4: Latency And Fluidity Instrumentation

**Target version:** `0.5.0`

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Modify: `src/LafazFlow.Windows/Services/LatencyTrace.cs`
- Modify: `src/LafazFlow.Windows/Services/RecorderController.cs`
- Modify: `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml.cs`
- Modify: `src/LafazFlow.Windows/UI/SettingsViewModel.cs`
- Modify: `src/LafazFlow.Windows/UI/SettingsWindow.xaml`
- Modify: relevant latency/settings tests.

Steps:
- [ ] Add timing fields for hotkey-to-visible, recording-start, stop-to-queue, queue-wait, preview-start, final-whisper, paste, and UI-hide.
- [ ] Ensure latency logs never include transcript text, full audio paths, or clipboard contents.
- [ ] Show latest latency summary in Settings in a compact readable panel.
- [ ] Add tests for privacy-safe latency rows and parser compatibility with older rows.
- [ ] Bump project version to `0.5.0`.
- [ ] Verify focused latency/settings tests, full build/test, publish/relaunch, public scan, commit, and push.

## Task 5: Visual Motion Refinement

**Target version:** `0.6.0`

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Modify: `src/LafazFlow.Windows/UI/MiniRecorderVisualSpec.cs`
- Modify: `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml`
- Modify: `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml.cs`
- Modify: `tests/LafazFlow.Windows.Tests/MiniRecorderVisualSpecTests.cs`

Steps:
- [ ] Add tests around motion constants: entrance/exit duration, pulse duration, compact/expanded dimensions, and no-auto-width animation rule.
- [ ] Tune recorder shell timing so start, stop, processing, and completion feel smooth but fast.
- [ ] Ensure status text never pushes or shifts the main shell content.
- [ ] Ensure error/status text has readable fallback via tooltip/logs when clipped.
- [ ] Bump project version to `0.6.0`.
- [ ] Verify focused UI tests, full build/test, publish/relaunch, manual dictation smoke, public scan, commit, and push.

## Task 6: Audio Cue Refinement

**Target version:** `0.7.0`

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Modify: `src/LafazFlow.Windows/Services/SoundCueService.cs`
- Modify: `src/LafazFlow.Windows/Services/ISoundCueService.cs` only if needed.
- Modify: `src/LafazFlow.Windows/Core/AppSettings.cs` if cue volume/toggle is added.
- Modify: `src/LafazFlow.Windows/UI/SettingsWindow.xaml`
- Modify: `src/LafazFlow.Windows/UI/SettingsViewModel.cs`
- Modify: sound cue tests.

Steps:
- [ ] Add settings for sound cues enabled/disabled and cue volume if not already present.
- [ ] Add tests for cue mapping, missing asset handling, disabled mode, and non-crashing playback.
- [ ] Tune cue timing: start cue at recording start, stop cue immediately after stop, completion cue after paste success, error cue only on actual failure.
- [ ] Bump project version to `0.7.0`.
- [ ] Verify focused cue/settings tests, full build/test, publish/relaunch, manual cue smoke, public scan, commit, and push.

## Task 7: Settings UX And Runtime Diagnostics

**Target version:** `0.8.0`

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Modify: `src/LafazFlow.Windows/UI/SettingsWindow.xaml`
- Modify: `src/LafazFlow.Windows/UI/SettingsViewModel.cs`
- Modify: `src/LafazFlow.Windows/Core/AppSettings.cs`
- Modify: `src/LafazFlow.Windows/Services/SettingsStore.cs`
- Modify: settings tests.

Steps:
- [ ] Add clear Fast vs Quality profile status with current model filename and backend.
- [ ] Add local runtime checks for Whisper CLI path, model file, CUDA path, VAD model, microphone availability, and log folder access.
- [ ] Add buttons for "Test microphone", "Test transcription", "Open logs", and "Reset settings" if not already present.
- [ ] Add tests for settings status strings, validation failures, and schema migration.
- [ ] Bump project version to `0.8.0`.
- [ ] Verify focused settings tests, full build/test, publish/relaunch, public scan, commit, and push.

## Task 8: Installer And Release Packaging

**Target version:** `1.0.0` only if Tasks 1-7 are stable; otherwise `0.9.0`.

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Create or modify: `scripts/package-windows-release.ps1`
- Create or modify: `docs/windows-runtime-setup.md`
- Modify: `README.md`
- Modify: release/package tests or script checks.

Steps:
- [ ] Choose packaging route: single-folder portable zip first, installer second if dependencies are understood.
- [ ] Ensure no models, user audio, logs, secrets, or machine-local settings are packaged.
- [ ] Package the app icon, sound cue notices, license, README, runtime setup, and executable.
- [ ] Add script checks that fail if release output contains `.wav` user recordings, `.log`, model binaries, API-key-like strings, or local settings.
- [ ] Bump version to `0.9.0` for portable release, or `1.0.0` if installer/release criteria are fully met.
- [ ] Verify package on a clean output folder, public scan, launch smoke, commit, push, and tag only after owner approval.

---

## Execution Order

Recommended order:
1. Task 1: Crash Resilience And Animation Safety.
2. Task 2: Reference Parity Audit Checklist.
3. Task 3: Dictation Quality And Developer Vocabulary.
4. Task 4: Latency And Fluidity Instrumentation.
5. Task 5: Visual Motion Refinement.
6. Task 6: Audio Cue Refinement.
7. Task 7: Settings UX And Runtime Diagnostics.
8. Task 8: Installer And Release Packaging.

Reasoning:
- Stability comes first because a dictation app disappearing destroys trust.
- The parity checklist comes second so later polish is measured, not improvised.
- Quality, latency, motion, and audio then become targeted improvements.
- Settings and packaging come after internals are stable enough to expose clearly.

## Verification Checklist For Every Slice

Run:

```powershell
dotnet build
dotnet test
git diff --check
rg -n "(?i)(api[_-]?key|secret|password|token|bearer|sk-[A-Za-z0-9]|ghp_|github_pat|BEGIN (RSA|OPENSSH|PRIVATE) KEY)" --glob '!bin/**' --glob '!obj/**' --glob '!artifacts/**' --glob '!**/*.dll' --glob '!**/*.exe' --glob '!**/*.pdb' .
dotnet publish src\LafazFlow.Windows\LafazFlow.Windows.csproj -c Release -o artifacts\stable-single\LafazFlow.Windows
dotnet publish src\LafazFlow.Windows\LafazFlow.Windows.csproj -c Release -o artifacts\stable-cuda-quality\LafazFlow.Windows
```

Launch smoke:

```powershell
Get-Process LafazFlow.Windows -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
$start = Get-Date
Start-Process -FilePath "C:\Users\User\Documents\GitHub\lafazflow-windows\artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe" -WindowStyle Hidden
Start-Sleep -Seconds 5
Get-Process LafazFlow.Windows -ErrorAction SilentlyContinue | Select-Object Id,Path,StartTime
Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$start} -ErrorAction SilentlyContinue |
  Where-Object { $_.ProviderName -in @('Application Error','.NET Runtime') -and $_.Message -like '*LafazFlow*' } |
  Select-Object TimeCreated,ProviderName,Id,Message
```

Expected:
- Build passes with 0 errors.
- Tests pass.
- Public scan has no credentials; known false positives are docs/GPL/code identifiers only.
- The pinned `stable-single` process stays running.
- No fresh `.NET Runtime` or `Application Error` crash event appears after launch.
