# LafazFlow Windows

Windows-native LafazFlow client, built from the existing macOS LafazFlow workflow as a reference.

The first milestone is local and offline:

- global hotkey
- floating recorder UI
- microphone recording
- local `whisper.cpp` transcription
- paste transcript into the active app

Cloud transcription and AI enhancement are not part of the MVP.

## Local model upgrade

The app auto-detects local Whisper models in `C:\Models\whisper` and prefers the fastest dictation-friendly option:

1. `ggml-base.en.bin`
2. `ggml-small.en.bin`
3. `ggml-medium.en.bin`
4. `ggml-large-v3-turbo-q5_0.bin`
5. `ggml-large-v3-turbo.bin`

`ggml-base.en.bin` is the default because it keeps short dictations close to real-time on Windows. The larger models can improve accuracy, but without Mac/Metal-style acceleration they feel too slow for everyday dictation.

To install the optional quantized large turbo quality model:

```powershell
.\scripts\install-fast-dictation-model.ps1
```

Model files are ignored by git and must not be committed.

## License

LafazFlow Windows is distributed under the GNU General Public License version 3. See `LICENSE`.

Bundled sound cue assets are attributed in `THIRD_PARTY_NOTICES.md`.
