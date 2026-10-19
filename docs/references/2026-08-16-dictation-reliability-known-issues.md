# LafazFlow Dictation Reliability — Known Issues Reference

**Date:** 16/08/2026 (Asia/Kuala_Lumpur) — updated 17/10/2026 (section 4).
**Status:** Living reference for agents and maintainers. Update this file when symptoms, root causes, or fixes change. Do not paste transcripts, prompts, credentials, or personal data into this document.

> **25 September 2026 capture update:** Section 2 describes the August `WaveIn` implementation and is historical. The current owner build uses raw-mode WASAPI with native-format capture and 16 kHz conversion. For the investigation, evidence, current code path, and recurrence procedure, see [First-word capture reliability: investigation and handoff](2026-09-25-first-word-capture-investigation.md). Do not use the August `WaveIn` description to diagnose the current build.

## 1. Repetition-hallucination text leaks

> **8 October 2026 replacement request:** The owner reports real dictations ending in invented words and another dictation losing its spoken opening. The owner requests a complete replacement of the application internals and interface under the LafazFlow brand. See [Dictation replacement investigation](2026-10-08-trailing-invented-speech.md) and its linked roadmap. The reported recordings' causes remain unverified pending matching audio replay.

> **7 October 2026 no-speech incident:** Background noise passed the amplitude check and final decoding invented text. A separate worker speech-presence gate now rejects zero-speech recordings while preserving the full waveform for actual speech. See [No-speech hallucination](2026-10-07-no-speech-hallucination.md) for evidence, checks, configuration limits, and recurrence steps.

> **7 October 2026 update:** Long multi-word phrase loops were reproduced on retained audio and fixed by beam search for final worker decoding, shared phrase-loop detection, and one CLI recovery attempt. The August protection/gap descriptions below are historical. See [Long-dictation phrase-loop incident](2026-10-07-long-dictation-phrase-loop.md) for current behavior, evidence, limits, tests, delivery, and recurrence steps.

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

### Fix (implemented 16/08/2026)

1. **Live capture-readiness gate** (FluidVoice pattern): `AudioCaptureService` now tracks whether any PCM buffer has arrived (`HasReceivedAudio`) and exposes `WaitForFirstAudioAsync`. When recording starts, `RecorderController` waits up to 4 seconds for first audio; if none arrives, it tries each enumerated input device as a fallback (2-second probe each) and, if nothing delivers audio, aborts the session immediately with a clear "Microphone is not delivering audio…" message instead of wasting the user's speech.
2. **Device management + fallback** (VoiceInk/Handy pattern): `MicrophoneDeviceCatalog` enumerates NAudio input devices; `AudioCaptureService.TrySwitchInputDevice` swaps the active input to a working device mid-session (same 16 kHz/mono/16-bit format) and the mini recorder shows "Using microphone: <name>"; the preferred device name is persisted in settings (`MicrophoneDeviceName`, schema 18) so sessions bind to the last-known working microphone rather than a drifting Windows default. Diagnostics lists the available microphones.
3. The existing post-hoc silence check remains as the final safety net.

The microphone selector UI (choosing a device in Settings) remains a candidate follow-up; automatic fallback and fast failure are implemented.

#### False "no speech" rejection of audible takes (fixed 18/10/2026)

The owner reported that occasionally, especially after a long dictation, nothing pastes at all ("suddenly when about to paste, it doesn't paste"). Log triage on 18/10/2026: 57 of 464 LATENCY rows are `status=failed`. The recent failures split into two classes:

- **Dead microphone takes:** CAPTURE diagnostics show the whole take at near-zero level (`onset_100ms_rms=0.0000`; full-file peak 208/32768 and 245/32768) — the Razer Seiren Mini occasionally delivers a silent stream (USB/Windows device-level glitch). Nothing could have been transcribed; the app correctly refused to paste.
- **False rejection of audible audio (real bug):** one failed take (peak 2772/32768, ~8.5%) replayed offline through **both** engines transcribes perfectly ("Testing one two three testing one two three over") — the persistent worker's Silero speech-presence gate had returned an empty result for quiet-but-audible speech, and the app pasted nothing.

**Fix (implemented 18/10/2026)**

- `RecorderController.ProcessJobAsync`: when the final engine succeeds but returns empty text and the finalized audio is audible (analyzer peak ≥ 0.02), retry once through the CLI path before declaring no speech. **Refined 19/10/2026 after a verified regression:** the retry now re-runs speech detection (VAD-enabled decode options when configured) and discards results matching a bounded silence-hallucination phrase family — the original energy-only retry let a noise-only take hallucinate "Thank you." See the [2026-10-08 incident reference](2026-10-08-trailing-invented-speech.md) for the full evidence chain.
- Empty results on near-silent captures (peak < 0.02) now raise the honest "Microphone input was silent…" message instead of "No speech was transcribed".
- Regression tests: audible+empty engine → CLI retry pastes; near-silent+empty → no retry + silent message; audible+empty engine+empty CLI → no-speech message.

**Follow-ups delivered:** the live "microphone is silent" warning (18/10/2026 — the shell swaps its level meter for "Mic silent - check device" within ~2.5s when the take stays below the level floor and clears it when audio returns), and automatic device recovery for the dead-stream class (20/10/2026 — after two consecutive takes captured below the speech floor, the *next* recording is routed to an alternate enumerated device, rotating per attempt and never rewriting the saved preference; one normal take clears the override, and a deliberate one-off silence never switches devices).

## 3. DeepSeek phonetic family and "their/there/they're" priming

### Symptom

The brand name `DeepSeek` is phonetically ambiguous: whisper produces `DeepSeq`, `DeepSec`, `Deep6`, `Deep Sea`, `Dipsick`, `Deep stick`, and similar shapes depending on the audio. Separately, the homophone trio `their/there/they're` is occasionally misheard as nonsense (`Dyer`, `Gear`, `Dyaa`, `DR`).

### Fix (implemented 16/08/2026)

- A bounded **phonetic family rule** replaces `deep` + (`seek|seq|sec|sick|stick|six|sea|sik|sique|6`) and `dip` + (`sick|seq|sec|seek|sea|six`) with the brand spelling `DeepSeek` in both the live preview and the final paste.
- **Approved trade-off:** the literal phrase "deep sea" (ocean) is also rewritten to `DeepSeek` because the owner's dictation is brand-focused; revisit with a guard if the ocean meaning is ever dictated.
- **Deliberately excluded real words:** `dipstick`, `gear`, and `DR`/`Dr.` are never rewritten, so legitimate dictation ("check the dipstick", "the gear is broken", "Dr. Smith", "DR strategy") is untouched.
- `their`, `there`, and `they're` were added to the vocabulary prompt to prime the correct homophone at the ASR level; no post-processing replacement is applied to these words (a global map would corrupt real words like `gear`/`DR`).

### Related ASR variance (16/08/2026)

Whisper's VAD can shift the first-word boundary on ambiguous audio onsets between builds and runs (observed: "please remember…" vs "at least remember…" on a retained fixture). This is inherent to the engine and is not a regression. Integration tests therefore assert substantive equivalence (normalized edit-distance ratio ≤ 0.15 plus preserved ending word) instead of exact raw equality, and cancel/preemption assertions accept either `Aborted` or `Ok` (cancellation is best-effort; a completed decode returning `Ok` is correct behavior). The invariants that matter — the worker stays healthy after a cancel, and a final request never waits behind preview — remain strictly asserted.

## 4. Coding homophones: `commit and push` heard as `comment and push`

### Symptom

During normal dictation the owner spoke "commit and push" but the pasted text read "comment and push" (reported 17/10/2026 while dictating a repository instruction into the running app). Whisper can prefer the more common word `comment` on fast or softly-spoken `commit` audio. The existing corrections covered `comit`, `git come in`, `git comes in`, `come in and push`, and `comes in and push`, but not `comment`.

### Fix (implemented 17/10/2026)

- `VocabularyCorrectionService` now corrects the observed phrase `comment and push` to `commit and push`, the same bounded-phrase family as the existing `come in and push` rule.
- Real comment usage is preserved: only the exact phrase is rewritten — `comment on the pull request`, `left a comment`, and plain `comment` stay untouched (regression tests).
- Bounded trade-off (same as `come in and push`): a literal instruction to "comment and push" would also become `commit and push`; the owner's developer-dictation focus accepts this. Revisit only if a real comment-and-push dictation is ever observed.

## 5. Punctuation behavior: unintended question marks, missing question marks, premature sentence breaks

**Reported 18/10/2026** (owner, dictating with the installed build): question marks appear in unintended places; question marks are missing where expected; and periods sometimes split what the owner intends as one sentence ("I don't yet finish my sentence yet LafazFlow already put a period").

**Mechanism (confirmed behavior, not yet a parameterized root cause):** sentence-final punctuation is emitted by the Whisper model itself, per take. LafazFlow repairs only a small, high-confidence set of continuation patterns (for example `so that`, `and then`, `because`, `which means`) and converts statement-like trailing `?` to `.`. Everything outside those patterns is preserved as the model emitted it — so identical speech can be punctuated differently across takes, and continuation clauses outside the repair list keep the model's period.

**Evidence path:** `LATENCY` rows record raw/formatted/clipboard final-character categories and character counts per dictation, so a specific occurrence can be located; the finalized WAV is retained (diagnostics retention / flight recorder). Reproduction requires the owner's example text (what was said vs what was pasted).

**Direction:** do not broaden heuristic repair lists one phrase at a time (see the August lessons on whack-a-mole). Collect owner examples, repair only deterministic patterns with negative regressions, and treat the model-agnostic replacement's user-owned rule layer plus an optional local polish stage as the structural fix.

## 6. Diagnostics log corruption and Settings load failure (fixed 20/10/2026)

**Symptom:** opening Settings could throw after the app had been running a while. Discovered while running the test suite: 25 `SettingsViewModelTests` failed with `ArgumentException: An item with the same key has already been added. Key: state`.

**Cause chain:** two independent writers (the hotkey diagnostics writer and the supervisor's WORKER logger) could interleave into `lafazflow.log`. One HOTKEY line lost its newline mid-write (`...targe`) and a WORKER line was concatenated onto it, producing a single line with two `state=` keys. Both diagnostic stores parsed lines with `ToDictionary`, which throws on duplicate keys, and the exception propagated through Settings loading. The preview logger also bypassed `BoundedLogFileWriter` with a raw `File.AppendAllText`.

**Fix (20/10/2026):**
- `BoundedLogFileWriter.AppendLine` now serializes trim + append under a process-wide lock, so in-process writers cannot interleave.
- The preview logger routes through `BoundedLogFileWriter` instead of `File.AppendAllText`.
- `HotkeyDiagnosticLogStore` and `LatencyDiagnosticLogStore` parse tolerantly: the first occurrence of a key wins and corrupted/interleaved lines never throw. Regression tests cover both parsers with duplicate-key lines.

**Verification:** the 25 previously failing Settings tests pass, the full suite is green, and the corrupted historical line in the live log no longer breaks Settings.

## 7. Cross-session handoff deadlock: "A microphone recording is already active" (fixed 20/10/2026)

**Symptom (owner):** after one deliberately silent take, every later dictation showed the same failure and could not recover without restarting the app; the app remained running in this state.

**Evidence chain (live log):** rapid back-to-back takes. Take 1's background job was still retrying through the CLI when take 2 started recording. Take 1 then failed ("No speech was transcribed") and its error handler called `SetError` while take 2 was actively recording; take 1's delayed live-silence monitor also fired into take 2 (warning ~60 ms after take 2 started). The recorder UI state became `Error` while the capture session stayed open. Every later toggle treated `Error` as idle and tried to start, and `AudioCaptureService.Start` threw `InvalidOperationException: A microphone recording is already active.` forever.

**Fixes:**
- A failed background job never paints its error/sound over a newer recording (`ProcessJobAsync` catch checks the current state under the dispatcher; failures are still logged).
- Readiness and silence monitors are session-generation-guarded: a stale monitor can neither abort nor warn into a newer recording.
- `StartRecording` self-heals a stale capture session: on the specific "already active" failure it clears the stale session in the background so the next toggle starts cleanly.

**Regression tests:** background-job failure during a newer recording keeps `Recording`; a stale silence monitor does not warn in the newer session; the stale-session failure triggers capture stop and the next start succeeds.

**Note:** the CLI retry (18 Oct) lengthened the background-job window, which made this pre-existing cross-session race reachable within seconds of rapid dictation.

**Second occurrence (same pattern, different error):** the microphone auto-recovery could select an endpoint that cannot actually open — a 4-channel capture endpoint failed with `NotSupportedException: Unsupported microphone channel count: 4`. The escape hatch added above only matched the "already active" message, so the bad override persisted and every later dictation failed identically until restart. Fixed in the same slice: (a) recovery candidates are **warm-opened for validation before being committed**, and any candidate that cannot open is rejected (recovery disabled for the session); (b) **any** start failure while a recovery override is active now clears the override, disables auto-recovery for the session, and retries the start once on the user's preferred microphone. Two regression tests cover a candidate that fails validation and a candidate whose start fails (fallback to the preferred device).

## 8. What has already been done (context for future agents)

- **M0–M10 persistent Whisper engine roadmap** (commits `1b2a3b9` … `73fa4a8`): persistent crash-isolated CUDA worker, complete-audio drain, versioned named-pipe protocol, final-preempts-preview, crash/timeout/CLI recovery, performance-health monitoring, plain-language Diagnostics, post-warmup memory verification, package provenance manifest.
- **v1.1.0 public release** (`5d1a014` tag `v1.1.0`): CPU worker + Official CPU CLI package, tag/version validation workflow, backend compatibility checks (CUDA settings never silently run a CPU worker).
- **v1.1.1 patch candidate** (`dd31111`): roadmap/roadmaps terminology, canonical per-user install path, taskbar/Desktop/Start Menu shortcuts repointed to the installed build.
- **Stale-build trap eliminated**: verified on 16/08/2026 that the taskbar pin pointed at `artifacts\stable-single` (`1.0.0+cae70dd`); it was repointed to the installed `1.1.1+dd31111` build and the running instance replaced.
- **Repetition-leak guard generalized** (16/08/2026): standalone repetition loops (same token 15+ times, dominating ≥80% of the transcript) now fail both the live preview and the final paste.
- **Mic readiness + fallback** (16/08/2026): first-audio gate with automatic device fallback and persisted preferred microphone (schema 18).

## 9. Open decisions awaiting owner approval

- Generalize the repetition-leak guard (section 1).
- DeepSeek phonetic family + prompt anchoring (separate discussion; includes the "deep sea" ambiguity rule).
- Live capture-readiness gate and microphone device management (section 2).
- Full microphone selector UI in Settings (auto-fallback and fast failure are done).

## 10. Cross-references

- `src/LafazFlow.Windows/Services/PromptLeakDetector.cs`
- `src/LafazFlow.Windows/Services/RollingWhisperLiveTranscriptPreviewService.cs`
- `src/LafazFlow.Windows/Services/TranscriptionPostProcessor.cs`
- `src/LafazFlow.Windows/Services/RecorderController.cs`
- `src/LafazFlow.Windows/Services/AudioCaptureService.cs`
- `src/LafazFlow.Windows/Services/AudioSignalAnalyzer.cs`
- `docs/references/2026-08-13-whisper-engine-reference-manifest.json` (pinned reference revisions)
- `tasks/lessons.md` (reusable lessons)
