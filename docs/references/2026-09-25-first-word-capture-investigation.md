# First-word capture reliability: investigation and handoff

- **Investigation:** 24–25 September 2026 (Asia/Kuala_Lumpur)
- **Status:** Raw-mode owner build 25 installed and accepted in the observed session; recurrence remains possible
- **Source baseline:** `cc16a15` (`v1.1.2`, build 25)
- **Audience:** The engineer taking over a renewed opening-word failure

## Executive status

LafazFlow intermittently lost or damaged the first words of dictation, sometimes while later words were clear. The owner reproduced this in saved recordings, not just pasted text. A legacy 16 kHz `WaveInEvent` path and then ordinary shared-mode WASAPI at the microphone's native format both failed owner trials. Native bytes and the converted WAV from the **same** failing WASAPI stream closely matched, strongly locating that failure before LafazFlow's resampler and WAV writer. The exact device or Windows effect responsible was not isolated.

The shipping path now opens the selected WASAPI endpoint in **raw mode**, accepts its native stream, resamples to 16 kHz mono PCM16, and retains a 500 ms warm pre-roll. On this owner's hardware, raw mode delivered 20/20 immediate-opening trials and a longer dictation in build 24, followed by 3/3 immediate-opening trials in the cleaned build 25. All 23 short trials passed during this acceptance session. This is strong evidence of improvement on this machine, **not a guarantee that intermittent failure is eliminated on every device or after every restart**.

If the owner reports a recurrence, first confirm the running executable's embedded commit and obtain one timestamped, owner-labeled failure. Then compare the saved WAV opening with the pasted text. The decision procedure below prevents another broad capture rewrite based on transcription output alone.

## Observed failure and investigation chronology

| Stage | Change or observation | Result and inference |
| --- | --- | --- |
| Initial reports | Multiple owner-labeled outputs omitted an initial short word or changed the first word. The owner described crackling or absent initial speech in failed saved WAVs. | Some failures were present in the captured audio. ASR alone could not explain every report. |
| Earlier controls | The owner sometimes got flawless results, including a run of complete short phrases and natural dictation. A simultaneous independent 48 kHz microphone capture coincided with successful LafazFlow trials. Prompt, VAD, gain, and silence experiments did not reliably restore failed openings. | Intermittency made successful controls insufficient to locate the failed layer. Preserve a failed trial as the comparison anchor. |
| Build 21, `6412861` | Added callback sequence and timing, pre-roll count, endpoint/format, onset level, and pre-roll boundary diagnostics to `CAPTURE` logs. | Failed and successful WAVs did not show a convincing callback drop, misalignment, or pre-roll/live seam. These metrics are diagnostic signals, not proof that speech content is intact. |
| Build 22, `b9b63d4` | Replaced legacy `WaveInEvent` requesting 16 kHz PCM with WASAPI at the endpoint's native 48 kHz mono float format, converted via WDL to 16 kHz mono PCM16. | The owner labeled an opening-word trial as failed and heard that word **absent** in an isolated clip. Native-format WASAPI without raw mode was insufficient. |
| Build 23, `145984f` | Temporarily wrote native bytes beside converted WAVs from the same WASAPI stream. | Three native/converted comparisons aligned at roughly 500–510 ms pre-roll and correlated `0.99509`, `0.996426`, and `0.995945`. This strongly disfavors the conversion and writer as the source of the observed loss. It does not identify which upstream processing stage damaged the speech. |
| Build 24, `9502cd6` | Switched NAudio 3.1.0 WASAPI recorder to `WithRawMode()` with 50 ms buffer and `Audio` MMCSS priority. The owner's endpoint accepted raw mode. | Owner acceptance: 10/10 repetitions of one immediate-opening phrase, 10/10 trials across five previously problematic openings (two each), and a 20–30 second natural dictation with beginning, middle, and end intact and no stray audio. Completed paste and warm decode were observed; typical decode was about 230–320 ms. |
| Build 25, `cc16a15` | Removed the temporary native sidecar writer while retaining raw capture. Installed the canonical owner build. | Owner acceptance: 3/3 further repetitions of the immediate-opening phrase kept its first word. Full suite: 807/807; Release build: zero warnings/errors; real microphone capture/drain test passed. The installed app and one CUDA worker were running. Final observed decode timings were 451, 258, and 232 ms with 16 threads and no error. |

The five-phrase build 24 set was spoken twice, starting immediately after double Shift. User-labeled outcomes, not automated word recognition scores, form the acceptance counts above. The exact spoken content remains in the private session record, not this repository.

## Current capture path and ownership

1. `RecorderController` responds to double Shift activation and handles microphone readiness, stop/finalization, full-WAV transcription through the persistent worker, and paste. Final transcription is given the complete WAV; this fix did not introduce a VAD trim at the beginning.
2. `MicrophoneDeviceCatalog` enumerates active WASAPI capture endpoints and resolves the persisted preferred microphone by name. A blank preference follows the Windows default between recordings. Check the current code before assuming older fallback behavior still applies.
3. `AudioCaptureService` keeps a warm stream, holds the latest **16,000 bytes = 500 ms** of 16 kHz mono PCM16, and prepends it on start. It owns the active session and waits for recording stop/drain before finalizing the WAV. Generation/session ownership protects subsequent recordings from late callbacks.
4. `WasapiAudioInputDevice` uses `WasapiRecorderBuilder().WithDevice(endpoint).WithRawMode().WithBufferLength(50).WithMmcssThreadPriority("Audio").Build()`. `NativePcm16Resampler` converts the endpoint-native format to 16 kHz mono PCM16 and flushes before the stop event is published. Raw capture support is required on the chosen endpoint; a failed open should be surfaced rather than silently treated as a successful recording.
5. `CaptureOnsetMetrics` reads up to the first 10 seconds of a finalized 16 kHz WAV and logs threshold crossings, onset RMS/peak, and the sample jump at the pre-roll boundary. Thresholds and numeric onset metrics cannot determine whether a particular spoken word is present.

Primary code: `src/LafazFlow.Windows/Services/AudioCaptureService.cs`, `CaptureOnsetMetrics.cs`, `MicrophoneDeviceCatalog.cs`, and `RecorderController.cs`. Focused tests: `AudioCaptureServiceTests.cs`, `CaptureOnsetMetricsTests.cs`, `NativePcm16ResamplerTests.cs`, `MicrophoneDeviceCatalogTests.cs`, and `WasapiAudioInputDeviceHardwareTests.cs` in `tests/LafazFlow.Windows.Tests/`.

## Reproduce and triage a recurrence

1. **Establish version and a labeled failure.** Check the running process path and file product version against `C:\Users\User\AppData\Local\Programs\LafazFlow\LafazFlow.Windows.exe` and its embedded Git commit. Ask for the approximate local time, intended opening, and actual pasted opening of one failure. Do not infer a failure from an unlabeled WAV or a single successful run.
2. **Locate the exact session.** In `%LOCALAPPDATA%\LafazFlow\Logs\lafazflow.log`, correlate nearby `HOTKEY`, `CAPTURE`, and `LATENCY` entries with the WAV modification time in `%LOCALAPPDATA%\LafazFlow\Recordings`. Note endpoint/format, callback count/sequence, pre-roll bytes, callback gap, alignment, and onset metrics. Keep transcript text and audio out of issues, commits, and shared logs.
3. **Separate capture from decoding.** Have the owner listen locally to a *short copy* of that WAV's opening and label the missing word as clearly audible, crackling/faint, or absent. If it is clearly audible, replay the **same WAV** with the actual active prompt and VAD settings before touching capture. Prior prompt over-conditioning made intact audio decode incorrectly; the August known-issues reference records that separate failure family. If it is damaged or absent, investigate capture and upstream processing first.
4. **Check for obvious session faults.** A wrong microphone, no first audio, failed raw-mode open, stop/drain error, short/invalid WAV, or callback loss is actionable on its own. If the metrics look normal, do not conclude that the opening is present; build 21 showed why those counters cannot prove speech content.
5. **If raw-mode WAVs again lose the opening, localize once.** Recreate a tightly scoped native sidecar experiment from build-23 commit `145984f` on a diagnostic branch or temporary local build, capturing native and converted bytes from the *same* stream for one labeled failing trial. Compare aligned waveforms; do not leave private sidecars enabled in a release. If native is also damaged, inspect device/driver, endpoint effects or raw-mode availability, and competing applications. If native is clean but converted is damaged, investigate the resampler, pre-roll handoff, and WAV writer. A separate microphone recording is useful only when it overlaps a LafazFlow **failure**.
6. **Accept on the owner's real workflow.** Repeat immediate-opening phrases after double Shift, include one longer paragraph, verify no stray earlier audio, completed paste, worker/backend and latency, then run the relevant automated and hardware checks. Record both failures and successes with the build commit. An intermittent defect needs repeated owner-labeled trials; unit tests alone cannot close it.

Useful local commands (PowerShell, from the repository root):

```powershell
git rev-parse HEAD
Get-Item "$env:LOCALAPPDATA\Programs\LafazFlow\LafazFlow.Windows.exe" | Select-Object FullName, @{Name='ProductVersion';Expression={$_.VersionInfo.ProductVersion}}
Get-Content "$env:LOCALAPPDATA\LafazFlow\Logs\lafazflow.log" -Tail 200
dotnet test
$env:LAFAZFLOW_TEST_REAL_MIC = '1'; dotnet test --filter FullyQualifiedName~WasapiAudioInputDeviceHardwareTests; Remove-Item Env:LAFAZFLOW_TEST_REAL_MIC
```

The hardware test opens the default real microphone, waits for audio, stops, and asserts 16 kHz output and a successful drain. Run it only when using the microphone is appropriate. The full suite's hardware test otherwise returns early.

For a subsequent **source implementation**, follow `AGENTS.md`: run relevant tests, commit the final source, run `scripts/install-owner-build.ps1` so the canonical taskbar executable embeds that commit and launches, verify the process path/version and CUDA worker plus real capture, and push `main`. A build left only under `artifacts` is not an owner rollout. This handoff itself is documentation only and does not require reinstalling an unchanged executable.

## Limits, privacy, and retained evidence

- The Windows raw stream option bypasses system capture processing when supported, except endpoint-specific always-on effects. It is a plausible explanation for the improvement, **not proof of a named effect or driver defect**. Raw-mode behavior can vary by device and driver.
- The owner reported a pattern where failure could return after reboot. The successful build-24/25 session does not establish post-reboot durability; verify that specifically if failure is reported again.
- The former diagnostic sidecars and recordings were **not deleted**. Thirty native sidecar WAVs remained in `%LOCALAPPDATA%\LafazFlow\Recordings\NativeDebug` at handoff; build 25 no longer writes new ones. Treat all recordings as private, and seek owner direction before cleanup or sharing.
- Settings for model, CUDA, CLI, VAD, and 16-thread operation were not intentionally changed by this capture fix. Verify active settings from the running build when investigating a future report.
- This document contains no private audio, verbatim transcripts, prompts, endpoint IDs, or machine-specific log dumps. Use local evidence; publish only aggregate metrics or sanitized excerpts.

## Reference implementations and platform basis

These were **behavioral references**, not source imports:

- Handy, `8f9cf53cd1410cda26beea39ff802ac306e39585`: `src-tauri/src/audio_toolkit/audio/recorder.rs`, first-real-sample acknowledgment (lines 376–390) and native sample rate/resampling (lines 555–563).
- FluidVoice, `a0f4f4bf1d0562b31ae0e67fbefc86a8d7b72f3c`: `Sources/Fluid/Services/AudioCaptureReadinessGate.swift`, first-PCM readiness signal (lines 107–110).
- VoiceInk, `d7b528aaf184db2ee946dffe920e557a1a34617d`: `VoiceInk/Infrastructure/Audio/Devices/AudioDeviceManager+RecordingRouting.swift`, usable device fallback (lines 55–75).
- Microsoft WASAPI `AUDCLNT_STREAMOPTIONS_RAW`: system audio processing is bypassed where the endpoint supports raw capture; endpoint-specific always-on processing can remain. NAudio 3.1.0 supplies the recorder builder used here.

Historical context: `docs/references/2026-08-16-dictation-reliability-known-issues.md` describes earlier prompt and silent-recording fixes. Its August `WaveIn` behavior is historical; this document is authoritative for the September capture path.
