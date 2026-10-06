# Background noise transcribed as invented speech

Date: 7 October 2026, Asia/Kuala_Lumpur.
Status: Reproduced, fixed, verified, installed, and pushed to main.

## User report and impact

The owner started dictation, said nothing, and stopped. LafazFlow pasted an invented sentence containing short repeated clauses. The application must not treat a non-speech recording as dictated words. This is related to ASR hallucination but distinct from [the long-dictation phrase-loop failure](2026-10-07-long-dictation-phrase-loop.md): this output is too short and irregular for the bounded phrase-loop guard, and there is no user speech to recover.

## Evidence and confirmed cause

The matching session stopped at 03:06:44. The recording contains about 4.91 seconds including pre-roll. Preview recorded two empty results; final decoding returned 106 characters and pasted them. Capture had no late or misaligned callbacks. The recording and user-reported text remain private and outside Git.

Replaying identical audio with the active CUDA Quality model and prompt:

- Full-waveform beam CLI decoding without VAD produced invented speech again, with different wording.
- Existing Silero VAD at threshold 0.50 and minimum speech duration 250 ms detected zero speech segments. CLI decoding with VAD returned no transcript.

`AudioSignalAnalyzer` checks amplitude, not whether the sound is speech. Its silence thresholds are peak below 0.003 AND RMS below 0.0008. Background noise and transient sounds can pass this check. The final worker deliberately disabled VAD trimming to preserve quiet words in long dictation, but had no separate speech-presence gate. Therefore, it attempted to decode a no-speech recording and accepted invented output. Beam search fixes the reproduced long-dictation loop but does not guarantee no hallucination on non-speech audio.

## Fix

Before final decoding, when worker VAD is enabled and a VAD model is configured, run the existing Silero detector over the entire PCM buffer solely to determine whether any qualifying speech segment exists.

- Zero speech segments: return a successful empty response. The existing controller's no-speech gate prevents clipboard write/paste; the recovering engine does not retry successful empty output.
- Speech exists: decode the entire original PCM buffer with final beam search. Do not concatenate VAD segments or trim quiet words.
- VAD initialization/detection fails: return an explicit worker error rather than misrepresenting the failure as silence.
- Reuse one worker-owned CPU VAD context and release it at shutdown. The upstream `whisper_vad_segments_from_samples` API resets its recurrent state for each recording; preview and final detection do not share this context.

Changed code: `native/LafazFlow.WhisperWorker/main.cpp`, final `Transcribe` speech gate and shutdown cleanup. No microphone, model, prompt, amplitude threshold, dependency, or user-setting change was made.

## Verification

- Rebuilt the CUDA worker from the pinned whisper.cpp source.
- Opt-in regression replayed the reported private recording three times: successful empty text every time. Real speech decoded substantively correctly afterward using the same worker.
- The previous retained long recording still passed substantive equivalence and ending preservation without a phrase loop.
- Full Release suite passed 814/814 with both private-audio environment variables enabled. Existing `EmptyEngineResultDoesNotPaste` covers the controller delivery boundary.
- Release build: zero warnings and errors; diff whitespace check passed.

Private evidence: `%LOCALAPPDATA%\LafazFlow\Benchmarks\silent-20261007\silent.wav`, `without-vad.txt/.log`, and `with-vad.txt/.log`; matching capture/paste evidence is in `%LOCALAPPDATA%\LafazFlow\Logs\lafazflow.log`.

```powershell
./scripts/build-whisper-worker.ps1 -Backend Cuda
$env:LAFAZFLOW_TEST_NO_SPEECH_AUDIO = "$env:LOCALAPPDATA\LafazFlow\Benchmarks\silent-20261007\silent.wav"
dotnet test -c Release --filter FullyQualifiedName~RetainedNoSpeechAudioReturnsNoTextAndNextSpeechStillDecodes
Remove-Item Env:LAFAZFLOW_TEST_NO_SPEECH_AUDIO
```

This test returns without replay if the private audio variable/file or local worker/model/fixture prerequisites are unavailable. Generic CI success alone does not prove the private replay ran.

## Owner delivery

Fix commit: `158dd9e`. Installed and restarted v1.1.2 build 29 at the canonical taskbar executable. Embedded commit matched the fix; installed worker SHA256 matched the rebuilt worker. One canonical app and one installed worker were running, and the worker logged Ready. The fix was pushed to main. This subsequent delivery-note commit changes documentation only.

## Limits and recurrence

The gate follows the existing VAD-enabled worker configuration. VAD-disabled workers and direct CLI fallback do not gain this standalone gate. The owner uses VAD enabled with its model configured. This is not a universal promise that VAD can distinguish all music, background voices, or unusual speech; very brief or quiet speech may fail its existing threshold. No new owner-spoken acceptance trial was performed during this fix.

If invented text recurs, preserve the exact WAV and correlate version, worker path, settings, preview, and final output. Replay standalone VAD to determine whether speech was detected. If detection is zero but text was pasted, check the running build and whether direct CLI fallback bypassed the worker. If detection is positive on non-speech, investigate the detector's classification with retained audio instead of adding transcript-specific replacements or raising amplitude thresholds blindly. If genuine speech is rejected, compare its VAD results without reintroducing final waveform trimming. Update this reference with the evidence and delivery result.
