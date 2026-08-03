# LafazFlow for Windows

LafazFlow is a privacy-first dictation app for Windows. Press a global hotkey, speak naturally, and have the transcript pasted into the application you were already using.

Transcription runs locally with `whisper.cpp`. Your recordings and transcripts do not need to leave your computer, there is no required cloud account, and the core dictation workflow remains available offline.

## Why LafazFlow?

- **Private by default** — speech recognition runs locally instead of uploading every recording to a transcription service.
- **Works where you already write** — dictate into editors, browsers, messaging apps, documents, and other Windows applications.
- **Fast daily workflow** — a global double-Shift hotkey, compact floating recorder, automatic transcription, and cursor-aware paste keep interruptions short.
- **Choose speed or quality** — use lightweight CPU-friendly models or a CUDA-accelerated quality profile on supported NVIDIA hardware.
- **Built for real dictation** — voice activity detection, custom vocabulary, correction rules, developer-literal formatting, and conservative on-device text cleanup improve practical output.
- **Transparent and local** — runtime checks, latency diagnostics, optional retained recordings, and open-source code make the transcription pipeline inspectable.

## Features

- Global double-Shift recording hotkey
- Compact floating recorder with live audio feedback
- Local `whisper.cpp` transcription
- CPU and NVIDIA CUDA transcription profiles
- Optional Silero voice activity detection
- Local Whisper model library and model selection
- Live transcript preview
- Custom vocabulary and correction rules
- On-device dictation post-processing
- Automatic paste into the previously active application
- Optional clipboard restoration after paste
- Configurable recording, completion, and error sounds
- Runtime, hotkey, and transcription-latency diagnostics
- Local settings, logs, and recordings management

## Technology

LafazFlow is a native Windows desktop application—not React Native or Electron.

- **Application:** C# and .NET 9
- **Interface:** Windows Presentation Foundation (WPF) with XAML
- **Audio capture:** NAudio
- **Speech recognition:** local `whisper.cpp` CLI
- **GPU acceleration:** NVIDIA CUDA through a CUDA-enabled `whisper.cpp` build
- **Windows integration:** Win32 APIs, Windows Forms tray components, clipboard APIs, and UI Automation
- **Tests:** xUnit

This stack produces a genuine Windows executable and gives LafazFlow direct access to global keyboard hooks, microphones, the system tray, native windows, and the clipboard.

## Getting started from source

### Requirements

- Windows 10 or Windows 11, 64-bit
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A local `whisper.cpp` Windows CLI build
- A compatible local Whisper model
- Optional: an NVIDIA GPU and CUDA toolkit for the accelerated quality profile

Clone the repository and run:

```powershell
dotnet restore
dotnet run --project .\src\LafazFlow.Windows\LafazFlow.Windows.csproj
```

Before configuring CUDA quality mode, check the local toolchain and native runtime:

```powershell
.\scripts\check-quality-prereqs.ps1
```

The app looks for local Whisper models in `C:\Models\whisper`. The recommended everyday model depends on your hardware:

- `ggml-base.en.bin` for lightweight CPU dictation
- `ggml-large-v3-turbo-q5_0.bin` for higher-quality CUDA-accelerated dictation

Model files are intentionally excluded from Git and must not be committed.

## Privacy

The core dictation pipeline is local. LafazFlow records microphone audio, invokes the configured local Whisper runtime, post-processes the result on the device, and pastes it into the selected Windows application. No cloud transcription provider or account is required.

Diagnostic recordings are optional and controlled through the application settings. Review those settings before sharing logs or diagnostic files.

## Contributing

Contributions are welcome. Bug reports, accessibility improvements, documentation, tests, Windows compatibility fixes, performance work, and thoughtful interface improvements all help the project.

Before opening a pull request:

1. Keep the change focused and explain the user-facing reason.
2. Add or update tests when behavior changes.
3. Run `dotnet test LafazFlow.Windows.sln`.
4. Run `dotnet build LafazFlow.Windows.sln --configuration Release`.
5. Do not commit Whisper models, recordings, transcripts, credentials, or machine-specific configuration.

Please use [GitHub Issues](https://github.com/itsLucas02/lafazflow-windows/issues) for reproducible bugs and focused feature proposals.

## License

LafazFlow for Windows is free and open-source software distributed under the [GNU General Public License v3.0](LICENSE).

Bundled sound cue assets and third-party notices are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
