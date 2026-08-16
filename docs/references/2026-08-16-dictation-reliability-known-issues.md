# LafazFlow Dictation Reliability — Known Issues Reference

**Date:** 16/08/2026 (Asia/Kuala_Lumpur)
**Status:** Living reference for agents and maintainers. Update this file when symptoms, root causes, or fixes change. Do not paste transcripts, prompts, credentials, or personal data into this document.

## 1. Repetition-hallucination text leaks

### Symptom

Whisper occasionally locks onto a single token and repeats it many times instead of transcribing the user's speech. The repeated text appears in the **live preview** while recording and can also reach the **final pasted output**.

### Observed shapes (chronological)

1. `Custom vocabulary, Individu, Individu, Individu, ...` — the vocabulary prompt marker followed by a repeated invented word.
2. `1.1.1.1.1.1.1.1.1...` — a pure repetition loop with no prompt marker (version-like tokens).

Both shapes are whisper repetition loops on weak or ambiguous audio, not user speech. They are the same failure family with different token shapes.

### Current protection

- `src/LafazFlow.Windows/Services/PromptLeakDetector.cs` runs in both the live preview (`RollingWhisperLiveTranscriptPreviewService`) and the final paste (`RecorderController.ProcessJobAsync` via the recorder's no-speech gate).
- The detector currently flags a transcript only when:
  - it starts with the vocabulary prompt marker (`custom vocabulary`) **and** has a long repeated-word run or is a prompt echo, **or**
  - it is a ≥40-character verbatim echo of the prompt.

### Known gap

A **pure repetition loop without the prompt marker** (for example `1.1.1.1.1...`, or a repeated word without the marker) is NOT currently flagged, because the repetition signal is gated behind the marker condition. The repetition detector already exists (`HasLongRepeatedWordRun`); it is simply not applied standalone.

### Verified log evidence

Failure to capture this shape is confirmed by user reports on 15–16/08/2026; the guard's code path was inspected and reproduced conceptually (a normalized `1 1 1 1 ...` transcript has a repetition run ≥ threshold but fails the marker check).

### Fix (implemented 16/08/2026)

`PromptLeakDetector` now also flags a **standalone runaway repetition** — the same normalized token repeated 15+ times consecutively AND dominating ≥80% of the transcript — independent of the prompt marker. Both the live preview and the final paste fail such dictations as no-speech instead of leaking the loop. Regression tests cover the pure `1.1.1.1…` shape, the marker+repetition shape, and legitimate repeated words embedded in real speech (which stay untouched).

## 2. Voice not captured (silent recordings)

### Symptom

The user starts recording and speaks (often at length), but the app either records silence and rejects it with "Microphone input was silent", or records audio that passes the silence check but whisper produces nothing ("No speech was transcribed"). The user's speech is effectively wasted.

### Verified log evidence (as of 16/08/2026)

- `Microphone input was silent. Check the Windows input device, mic mute, and input volume.` — **18 occurrences** in `%LOCALAPPDATA%\LafazFlow\Logs\lafazflow.log`, including multiple times on 16/08/2026.
- `No speech was transcribed. Check the microphone input and try again.` — **10 occurrences**, also including 16/08/2026.

### Current behavior

- `AudioCaptureService` opens NAudio `WaveIn` on the **Windows default input device**; there is no device selection, no remembered device, and no fallback.
- Silence is only detected **after recording stops** by `AudioSignalAnalyzer` (peak/RMS thresholds); there is no live "audio is actually flowing" gate.
- The live audio-level meter exists in the mini recorder UI (`AudioLevelChanged`), but the app still presents "Recording" even when zero audio arrives.

### Reference-project findings (pinned revisions)

#### FluidVoice — `AudioCaptureReadinessGate` (4ce0584f)

Recording is only considered ready when the **first real PCM sample arrives** (`signalFirstPCM`). If no audio arrives within the timeout, the session resolves `timedOut`; format invalidation and stale sessions are also handled. The user is never left talking into a dead capture path without a fast, explicit failure. `AudioStartupGate` additionally delays CoreAudio initialization until the UI has settled, avoiding init races at launch.

#### VoiceInk — `Recorder+RecordingDeviceSetup` (7023a6f7)

VoiceInk maintains an audio device manager (`availableDevices`, `lastUsedMicrophoneDeviceID`), observes device-change requests, and **switches to a fallback microphone** when the active device fails (including closed-lid built-in microphone blockage). It notifies the user "Using: <device>" / "Switched to: <device>" and shows an explicit error when no usable microphone remains.

#### Handy — `audio_toolkit/audio/device.rs` (37a26fd6)

Handy enumerates input devices via cpal, exposes each device with its name and whether it is the default, and supports explicit per-device selection rather than trusting an implicit default.

### Fix direction (not yet approved/implemented)

1. **Live capture-readiness gate** (FluidVoice pattern): when recording starts, require the first PCM buffer within a short deadline; if none arrives, fail fast with a clear "microphone is not delivering audio" message instead of wasting the user's speech.
2. **Device manager + fallback** (VoiceInk/Handy pattern): enumerate input devices, remember the last-used device, detect device failure/change (including exclusive-mode contention), switch to a fallback with a visible notification, and surface the active device in Diagnostics.
3. Keep the existing post-hoc silence check as a final safety net.

The microphone selector/device UI was previously listed as an explicit non-goal; the repeated silent-recording failures make it a candidate to reprioritize.

## 3. What has already been done (context for future agents)

- **M0–M10 persistent Whisper engine roadmap** (commits `1b2a3b9` … `73fa4a8`): persistent crash-isolated CUDA worker, complete-audio drain, versioned named-pipe protocol, final-preempts-preview, crash/timeout/CLI recovery, performance-health monitoring, plain-language Diagnostics, post-warmup memory verification, package provenance manifest.
- **v1.1.0 public release** (`5d1a014` tag `v1.1.0`): CPU worker + Official CPU CLI package, tag/version validation workflow, backend compatibility checks (CUDA settings never silently run a CPU worker).
- **v1.1.1 patch candidate** (`dd31111`): roadmap/roadmaps terminology, canonical per-user install path, taskbar/Desktop/Start Menu shortcuts repointed to the installed build.
- **Stale-build trap eliminated**: verified on 16/08/2026 that the taskbar pin pointed at `artifacts\stable-single` (`1.0.0+cae70dd`); it was repointed to the installed `1.1.1+dd31111` build and the running instance replaced.

## 4. Open decisions awaiting owner approval

- Generalize the repetition-leak guard (section 1).
- DeepSeek phonetic family + prompt anchoring (separate discussion; includes the "deep sea" ambiguity rule).
- Live capture-readiness gate and microphone device management (section 2).
- Commit/push of this document and any approved fixes.

## 5. Cross-references

- `src/LafazFlow.Windows/Services/PromptLeakDetector.cs`
- `src/LafazFlow.Windows/Services/RollingWhisperLiveTranscriptPreviewService.cs`
- `src/LafazFlow.Windows/Services/TranscriptionPostProcessor.cs`
- `src/LafazFlow.Windows/Services/RecorderController.cs`
- `src/LafazFlow.Windows/Services/AudioCaptureService.cs`
- `src/LafazFlow.Windows/Services/AudioSignalAnalyzer.cs`
- `docs/references/2026-08-13-whisper-engine-reference-manifest.json` (pinned reference revisions)
- `tasks/lessons.md` (reusable lessons)
