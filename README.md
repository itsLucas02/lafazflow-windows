# LafazFlow Windows

Windows-native LafazFlow client, built from the macOS LafazFlow/VoiceInk experience as a reference.

The first milestone is local and offline:

- global hotkey
- floating recorder UI
- microphone recording
- local `whisper.cpp` transcription
- paste transcript into the active app

Cloud transcription and AI enhancement are not part of the MVP.

## Local model upgrade

The app auto-detects local Whisper models in `C:\Models\whisper` and prefers the fast VoiceInk-like local option:

1. `ggml-large-v3-turbo-q5_0.bin`
2. `ggml-large-v3-turbo.bin`
3. `ggml-medium.en.bin`
4. `ggml-small.en.bin`
5. `ggml-base.en.bin`

To install the preferred fast quantized large turbo model:

```powershell
.\scripts\install-fast-voiceink-model.ps1
```

Model files are ignored by git and must not be committed.
