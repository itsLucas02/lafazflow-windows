# Multichannel microphone capture failure after Razer disconnect

**Date:** 29/10/2026 (Asia/Kuala_Lumpur)
**Status:** Fixed in source; owner-local delivery recorded in `tasks/todo.md`.
**Fix commit:** `fix: downmix multichannel microphones to mono` (this change set).
**Related:** [Dictation reliability - known issues](2026-08-16-dictation-reliability-known-issues.md), especially sections 2 and 7.

## Symptom and user impact

The owner unplugged the Razer Seiren Mini, which had been the Windows default capture device. Every subsequent double-Shift dictation failed before any audio was recorded. The recorder showed the raw error `System.NotSupportedException: Unsupported microphone channel count: 4` and nothing reached transcription or paste. The application process stayed alive, but dictation was unusable until the build was replaced.

## Affected build and settings

- Installed build: `1.1.3+b3d030f3a20387c4a2c8d5c5b83c583d185b29bd` (build 59), the current `main` before this fix.
- Settings: `%APPDATA%\LafazFlow\settings.json` selected `Microphone DeviceName = Microphone Array (Intel(R) Smart Sound Technology for Digital Microphones)`.
- Capture path: `WasapiAudioInputDevice` uses `WasapiRecorderBuilder.WithRawMode()`, so it opens the endpoint's native mix format. The Intel array exposes `WAVE_FORMAT_EXTENSIBLE IEEE_FLOAT 48000Hz 4 channels 32 bit`.

## Evidence and reproduction

1. Before the Razer was disconnected, retained `CAPTURE` diagnostics showed the Razer Seiren Mini as the Windows default with a 1-channel 48 kHz float mix format, and dictation worked.
2. On 29/10/2026 between 21:22 and 21:23, eight consecutive start attempts logged `System.NotSupportedException: Unsupported microphone channel count: 4` in `%LOCALAPPDATA%\LafazFlow\Logs\lafazflow.log`. The same log shows the settings save at 21:23:25 selecting the Intel array, followed by the same failure on the next attempts.
3. Replacing the default endpoint with the Intel array reproduced the condition deterministically: `MicrophoneDeviceCatalog.ResolveIndex` returns the selected endpoint, `WasapiAudioInputDevice` obtains its native 4-channel format, and `NativePcm16Resampler` threw because it accepted only one or two channels.
4. A hardware probe against the current default endpoint confirmed `default_capture=Microphone Array (Intel(R) Smart Sound Technology for Digital Microphones)` and `mix_format=WAVE_FORMAT_EXTENSIBLE IEEE_FLOAT 48000Hz 4 channels 32 bit`.

No private audio or transcript content is needed to reproduce the failure; the channel count alone is sufficient.

## Confirmed cause

`NativePcm16Resampler` explicitly rejected any native format outside 1-2 channels:

```csharp
if (nativeFormat.Channels is < 1 or > 2)
    throw new NotSupportedException($"Unsupported microphone channel count: {nativeFormat.Channels}");
```

Raw-mode WASAPI can expose arrays with more than two channels. When the pinned Razer disappeared, Windows fell back to the 4-channel Intel array, so every recording start failed during converter construction. The exception was caught and displayed as a short error, but no fallback or downmix existed, leaving the app unable to dictate.

## Fix

`NativePcm16Resampler` now builds an equal-weight mono downmix matrix for the endpoint's native channel count and passes it through NAudio's `ChannelMixerSampleProvider` before the existing 16 kHz resampler:

- 1 channel: unchanged (coefficient 1.0).
- 2 channels: same result as the previous `StereoToMonoSampleProvider` (0.5 left + 0.5 right).
- 4 or more channels: every channel contributes `1 / channels`, producing a mono stream with no clipping.

The rest of the capture path is unchanged: raw-mode WASAPI, pre-roll, 16 kHz/16-bit mono output, warm input, and session isolation.

## Verification

- Focused converter tests: 3 passed for 1, 2, and 4 native channels.
- Full solution suite: 850 passed, 0 failed.
- Real-hardware capture test (`LAFAZFLOW_TEST_REAL_MIC=1`) passed against the current default endpoint after the fix, confirming the production `WasapiAudioInputDevice` opens the 4-channel array and delivers 16 kHz mono PCM.
- Manual endpoint probe: current default is the 4-channel Intel array; its native format matches the regression test.

**Limit:** the regression test uses a synthetic float signal. It proves format acceptance, downmix, and resampling, not speech quality on the array. Equal-weight averaging is the general safe downmix; if owner listening or transcripts show array quality problems, test selecting the first channel or a driver-processed endpoint before changing the default.

## Delivery

- Source and tests are committed together.
- Owner-local install is performed with `scripts/install-owner-build.ps1`; the installed executable must report the fix commit and launch from `%LOCALAPPDATA%\Programs\LafazFlow\LafazFlow.Windows.exe`.

## Recurrence procedure

1. Search `%LOCALAPPDATA%\LafazFlow\Logs\lafazflow.log` for `Unsupported microphone channel count`.
2. Run the focused converter tests and the real-microphone hardware test:
   - `dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter FullyQualifiedName~NativePcm16ResamplerTests`
   - `$env:LAFAZFLOW_TEST_REAL_MIC='1'; dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter FullyQualifiedName~WasapiAudioInputDeviceHardwareTests`
3. Confirm the endpoint's native channel count and extend the converter theory with any failing channel count.
4. Do not reintroduce automatic device switching; the microphone choice remains user-controlled. Fix the format handling instead.
