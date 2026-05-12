# LafazFlow Windows MVP Design

## Purpose
Build a Windows-native LafazFlow client that recreates the daily macOS VoiceInk/LafazFlow dictation loop as closely as practical while staying local, offline, and privacy-first by default.

The first release is not a full feature clone. It is the smallest usable Windows workflow that preserves the owner's muscle memory:

1. Trigger with a global hotkey.
2. See the same compact bottom recorder surface.
3. Speak while a live visualizer reacts to the microphone.
4. Stop recording.
5. Transcribe locally with a lightweight Whisper model.
6. Paste the final text into the previously focused app.

Cloud transcription and AI enhancement are explicitly out of scope for the MVP.

## Reference Behavior From macOS
The macOS reference app uses these concepts:

- `RecordingState`: `idle`, `starting`, `recording`, `transcribing`, `enhancing`, and `busy`.
- `MiniRecorderView`: a black floating recorder pill with compact and expanded layouts.
- `AudioVisualizer`: 15 thin rounded bars, white at about 85% opacity, animated at roughly 60 FPS from audio level data.
- `MiniRecorderPanel`: floating, bottom-center, transparent, non-activating, movable, and not hidden when focus changes.
- `HotkeyManager`: supports toggle, push-to-talk, and hybrid modes.
- `CursorPaster`: writes text to the clipboard, sends paste, and optionally restores previous clipboard content.
- `WhisperTranscriptionService`: reads local WAV samples and runs a local Whisper model.

The Windows MVP should preserve the user-visible behavior, not the Apple-specific implementation.

## Recommended Approach
Use a Windows-native WPF application on .NET 9.

WPF is recommended over WinUI for the first build because it is mature, available with the installed .NET desktop SDK, and straightforward for floating transparent windows, animation, system tray behavior, and Win32 interop. The UI can still feel modern because the MVP surface is intentionally small: one recorder panel, one settings window, and later a history view.

Use `whisper-cli.exe` as the first transcription bridge instead of a direct DLL binding. This is less elegant internally, but it proves the offline pipeline quickly and keeps the C#/native boundary simple. Once the UX loop works, a later milestone can replace the process bridge with direct `whisper.cpp` bindings.

## Architecture
The MVP is split into focused services:

- `HotkeyService`: registers and handles global hotkeys through Win32 APIs.
- `RecorderController`: coordinates recording state, panel visibility, and transcription.
- `AudioCaptureService`: records microphone input to 16 kHz mono WAV and reports audio meter levels.
- `WhisperCliTranscriptionService`: invokes local `whisper-cli.exe` with a local `.bin` model.
- `ClipboardPasteService`: sets clipboard text, sends `Ctrl+V`, and restores prior clipboard content when enabled.
- `SettingsStore`: stores local settings as JSON under `%AppData%\LafazFlow`.
- `MiniRecorderWindow`: floating recorder UI that mimics the macOS compact pill.

The controller owns the workflow. Individual services should be small and replaceable.

## MVP Workflow
Initial state:

- App starts hidden in the background.
- A tray icon is present for settings and exit.
- Hotkey defaults to a safe configurable shortcut, because Windows cannot reliably distinguish all left/right modifier-only keys without lower-level hooks.

Recording start:

- User presses the configured hotkey.
- App remembers the current foreground window handle.
- Recorder panel appears bottom-center without taking focus.
- State changes to `recording`.
- Audio capture starts and writes a temporary WAV file under `%LocalAppData%\LafazFlow\Recordings`.
- Visualizer bars animate from the live microphone meter.

Recording stop:

- User presses/releases according to selected hotkey mode.
- Audio capture stops.
- State changes to `transcribing`.
- Panel shows a `Transcribing` label and dot progress animation.
- `whisper-cli.exe` is invoked locally with the configured model.

Paste:

- On success, transcript text is trimmed and copied to the clipboard.
- App restores focus to the original foreground window when possible.
- App sends `Ctrl+V`.
- If clipboard restore is enabled, previous clipboard content is restored after a short delay.
- Panel hides and state returns to `idle`.

Failure:

- Panel remains visible long enough to show a concise error.
- No partial or failed transcript is pasted.
- The recording file is retained only when diagnostics mode is enabled.

## UI Requirements
The recorder should start with the macOS proportions:

- Compact width: 184 px.
- Expanded width target: 300 px, reserved for future live transcript.
- Control bar height: 40 px.
- Compact corner radius: 20 px.
- Expanded corner radius: 14 px.
- Background: black.
- Bottom-center position with 24 px bottom padding from the work area.
- Visualizer: 15 rounded bars, 3 px wide, 2 px gap, 4 px minimum height, 28 px maximum height.
- State transition fade around 200 ms.
- Compact-to-expanded animation around 300 ms when live transcript is added later.

The MVP can omit prompt and power-mode popovers, but it should reserve left and right icon slots so the control bar rhythm matches the macOS app.

## Hotkey Requirements
The MVP supports three modes:

- `Toggle`: key press starts; next press stops.
- `PushToTalk`: key down starts; key up stops.
- `Hybrid`: short press toggles hands-free recording; hold for at least 500 ms behaves as push-to-talk.

Windows implementation details:

- First version uses Win32 `RegisterHotKey` for normal shortcuts such as `Ctrl+Alt+Space`.
- A later milestone may add low-level keyboard hooks for modifier-only parity with right Command/right Option style behavior.
- The hotkey must not fire while the app is already `transcribing` or `busy`.

## Local Whisper Requirements
The MVP is offline-only:

- No network request is made during transcription.
- The app accepts a configured path to `whisper-cli.exe`.
- The app accepts a configured path to a local ggml `.bin` model.
- The default recommended model for first testing is a small/base quantized Whisper model from `whisper.cpp`.
- The app must validate that both paths exist before recording can start.

The first version should support a manually supplied `whisper-cli.exe` and model. Built-in model download can come later.

## Settings
Store settings in `%AppData%\LafazFlow\settings.json`:

- `HotkeyGesture`
- `HotkeyMode`
- `WhisperCliPath`
- `ModelPath`
- `RestoreClipboardAfterPaste`
- `ClipboardRestoreDelayMs`
- `AppendTrailingSpace`
- `KeepRecordingsForDiagnostics`

No API keys or cloud provider settings belong in the MVP settings file.

## Privacy And Security
The MVP must be safe for a public open-source repo:

- Do not commit models, binaries, user recordings, logs, API keys, tokens, or local machine paths.
- Add `.gitignore` entries for build output, recordings, models, logs, `.env`, and local settings.
- Keep transcription offline by default.
- Treat optional future cloud enhancement as a separate, opt-in milestone.

## Verification
The MVP is done only when these checks pass:

- `dotnet build` succeeds.
- Unit tests pass for settings, hotkey mode state transitions, whisper command construction, and clipboard restore timing logic.
- Manual test: start recording from hotkey, speak, stop, transcribe locally, paste into Notepad.
- Manual test: unplug or misconfigure model path and confirm no recording starts and a useful error appears.
- Manual test: verify no network requirement exists for the default transcription flow.

## Out Of Scope For MVP
- Cloud transcription providers.
- AI enhancement.
- History view.
- Dictionary and word replacements.
- Power Mode.
- Live partial transcript streaming.
- Model download UI.
- Direct `whisper.cpp` DLL binding.
- Installer/signing.

## Open Decisions For Owner Review
- Default hotkey: proposed `Ctrl+Alt+Space` for Windows safety.
- First UI tech: proposed WPF on .NET 9.
- First transcription bridge: proposed manually configured `whisper-cli.exe` path.
- First model management: proposed manual model path configuration before download UI.
