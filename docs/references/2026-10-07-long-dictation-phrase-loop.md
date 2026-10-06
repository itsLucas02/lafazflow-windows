# Long dictation replaced by a repeated phrase

Date: 7 October 2026, Asia/Kuala_Lumpur.
Status: Reproduced, fixed, verified on retained audio, and installed for the owner.
Fix commit: `2cd280a99942bb169fe09d39b5d43e6a32198a97`, pushed to `main`.
Owner delivery: v1.1.2 build 27, canonical taskbar installation.

## Report and impact

The owner reported rare but recurring long-dictation failures: an otherwise meaningful transcript contained dozens of copies of one sentence fragment, replacing much of the spoken middle section. The corrupted text reached the target application. This wastes a long dictation and makes removing duplicates insufficient: removing a loop does not recover the omitted speech.

This is distinct from the earlier microphone-onset failure documented in [the capture investigation](2026-09-25-first-word-capture-investigation.md). Investigate the exact saved recording before changing capture or blaming a device.

## Confirmed evidence

The reported recording was about 78 seconds. At 02:24 on 7 October, capture finalized normally; the final worker returned success and the app pasted its output. Capture diagnostics reported no late or misaligned callbacks and a maximum callback gap of about 13.3 ms. These metrics alone do not prove speech integrity; replay provides the decisive evidence.

| Replay of the same retained WAV | Result |
| --- | --- |
| Previously installed greedy worker, two measured replays | Reproduced the owner's repeated-phrase output twice, including missing middle speech. |
| CUDA CLI with greedy search, beam size 1 and best-of 1 | Reproduced the same phrase loop. |
| CUDA CLI with its default beam search, same model and saved vocabulary prompt | Recovered meaningful middle speech and the ending. |
| CUDA CLI beam search with and without temperature fallback | Both recovered the middle speech. |
| Rebuilt worker using beam search for final decoding, three measured replays | All three recovered meaningful middle speech and ending without the phrase loop. Minor ASR wording differences remained. |

Active profile: Quality, CUDA, `ggml-large-v3-turbo-q5_0.bin`, 16 requested threads, preview VAD enabled. Final decoding reads the complete WAV without VAD trimming. The fix did not change microphone capture, model, vocabulary settings, thread count, or the final full-waveform policy.

## Cause and protection gap

`native/LafazFlow.WhisperWorker/main.cpp`, `BuildParams`, always selected `WHISPER_SAMPLING_GREEDY`, including authoritative final dictation. Greedy decoding reproducibly looped on this audio. The existing CLI path used beam search and recovered the missing content. Reproducing the loop in greedy CLI further isolates the failure to decoding rather than clipboard insertion or live-preview concatenation. This proves the decoder failure for this recording; it does not establish that every future omission or repetition has the same cause.

The existing `PromptLeakDetector` handled prompt echoes and repeated single-token runs. It did not recognize a repeated multi-word phrase surrounded by legitimate opening and ending speech. Its early return for an empty vocabulary prompt also disabled repetition checks unnecessarily. A worker response marked successful bypassed `RecoveringTranscriptionEngine` recovery, so the corrupted text reached the final paste guard undetected.

## Changes made

- Final worker requests now use Whisper's default beam-search parameters. Snapshot/preview requests remain greedy.
- The shared detector now recognizes normalized repeated phrases of 2–16 words: at least eight consecutive copies covering at least 40 words. Detection works without a vocabulary prompt and applies to both preview and final output. Existing single-token protections remain.
- When the primary worker returns a detected loop or prompt leak as successful text, the recovering engine retries the same audio once through the existing local CLI. Only a successful result that passes the detector is returned. A failed or still-corrupted recovery returns `repetition_recovery_failed` with no text.
- No deduplication, phrase substitution, cloud transcription, or new dependency was added. The recovery decodes the audio again rather than pretending duplicate deletion restores lost speech.

Affected source: `BuildParams` in the native worker; `PromptLeakDetector`; `RecoveringTranscriptionEngine`. The existing controller remains the final paste guard, and rolling preview uses the same detector.

## Verification and delivery

- Full Release suite: 813 passed, zero failed.
- Release build: zero warnings and zero errors.
- Regression coverage includes multi-word loops embedded in legitimate speech, an empty prompt, intentional short repetition, one successful CLI recovery, and one recovery that remains corrupted.
- An opt-in real-worker regression replays private long audio against an independently decoded local reference, checks substantive equivalence and the ending, and rejects detected loops.
- Canonical installation was updated and relaunched with `scripts/install-owner-build.ps1`. The executable's embedded commit matched source HEAD; installed worker SHA256 matched the rebuilt worker. One canonical app and one installed worker were running; the worker logged Ready.
- `origin/main` was checked against the fix commit after push. The working tree was clean.

Canonical executable: `%LOCALAPPDATA%\Programs\LafazFlow\LafazFlow.Windows.exe`.

This acceptance used the owner's already-recorded failure audio. It is not a new owner-spoken acceptance trial or a guarantee against every rare ASR failure. Beam search does more decoding work than greedy search. The phrase guard is deliberately bounded and may miss longer or irregular loops; it can also reject deliberately dictated extreme repetition. Short normal stutters remain allowed.

## Private evidence and runnable recurrence check

Evidence stays under `%LOCALAPPDATA%\LafazFlow`, outside Git:

- `Logs\lafazflow.log`: capture, latency, paste, and worker readiness evidence.
- `Benchmarks\repetition-20261007\long.wav` and `long.txt`: private failure audio and independent CLI reference.
- The same benchmark directory contains `worker-replay.log` and `worker-fixed.log` for before/after output. Verification summaries use timestamped filenames in the verifier's configured output directory.
- `Recordings\replay-loop-original.txt`: recovered dictation; related replay logs retain the CLI comparisons.

Run the retained-audio regression in PowerShell after building the corrected CUDA worker:

```powershell
./scripts/build-whisper-worker.ps1 -Backend Cuda
$env:LAFAZFLOW_TEST_LONG_AUDIO = "$env:LOCALAPPDATA\LafazFlow\Benchmarks\repetition-20261007\long.wav"
dotnet test -c Release --filter FullyQualifiedName~RetainedLongAudioPreservesSpeechWithoutPhraseLoop
Remove-Item Env:LAFAZFLOW_TEST_LONG_AUDIO
```

The test returns without replay when the environment variable, private recording, or required local worker/model/fixture prerequisites are absent. A passing generic CI run alone is therefore not proof of this audio replay. The reference `.txt` must be present alongside the WAV when replay is enabled.

For a recurrence:

1. Preserve the matching WAV, timestamp, running executable version, worker path, and privacy-safe diagnostics before changing settings or restarting. Do not commit audio or dictated content.
2. Replay that exact audio through the current worker and CLI with the active model/prompt and full final waveform. Compare greedy and beam decoding when appropriate.
3. If the speech exists in the WAV and one decode recovers it, investigate decoding and recovery. If the WAV itself loses or damages speech, follow the capture investigation instead.
4. Verify beginning, meaningful middle content, and ending. A shorter transcript or absent loop alone is not recovery proof.
5. If a loop escapes detection, record its normalized structure and extend a regression for that failure family without adding phrase-specific replacements. If both decoders fail, retain the evidence and document the limitation rather than claiming recovery.
6. Update this reference with new evidence, fix commit, checks, and canonical owner-install status. Install verified changes before reporting delivery.
