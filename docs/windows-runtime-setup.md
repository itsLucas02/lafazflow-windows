# LafazFlow Windows Runtime Setup

LafazFlow is a private, offline-first dictation app for Windows. This guide covers everything a normal user needs to go from download to first dictation.

## What you need

- Windows 10 or Windows 11 (64-bit).
- A working microphone.
- No .NET installation and no account. Releases are self-contained, and dictation stays on your PC.

## Install

1. Open the [Releases](https://github.com/itsLucas02/lafazflow-windows/releases) page and download the latest version.
2. **Portable ZIP**: extract the folder anywhere (Desktop is fine), open it, and double-click `LafazFlow.Windows.exe`.
3. **Installer**: run the setup file and follow the prompts. LafazFlow appears in the Start Menu, with an optional desktop shortcut.

> If Windows SmartScreen shows **"Windows protected your PC"**, click *More info* then *Run anyway*. The app is currently unsigned, which is normal for new open-source software.

## First run

- LafazFlow starts quietly in the system tray (the small up-arrow near the clock) and shows **"LafazFlow is ready."**
- On the very first run, the Settings window opens automatically.
- If Windows asks for microphone permission, choose **Allow**.

## Add a speech model

Go to **Settings > Models** and choose a model:

- **Base English (142 MB)** — fast and good for everyday dictation. Recommended for the first model.
- **Small English (466 MB)** — noticeably more accurate.
- **Large v3 Turbo (547 MB)** — best accuracy; needs a stronger PC or NVIDIA GPU.

Click **Download** and wait for it to finish. Models are stored in your user folder, so no administrator access is needed. Everything after that works offline.

## Dictate

1. Click into the app where you want text (an editor, browser, chat, document).
2. **Double-press Shift** to start recording.
3. Speak naturally; the compact recorder and live preview appear at the bottom of the screen.
4. **Double-press Shift** again to stop. LafazFlow transcribes locally and pastes the text into the app you were using.

## Troubleshooting

- **"Microphone input was silent"** — open Windows Settings > Privacy > Microphone, make sure access is on, and check that the correct input device and volume are selected.
- **Model download fails** — check your internet connection and retry, or import a local `.bin` model via **Settings > Models > Import local model**.
- **Sound cues missing or quiet** — adjust them in **Settings > Sound**.
- **Higher quality mode** — **Settings > Dictation** offers a Quality profile. CUDA acceleration requires an NVIDIA GPU and a CUDA-enabled `whisper-cli.exe`; the default CPU profile works everywhere.
- **Recordings and logs** are only kept when you enable *Keep recordings for diagnostics* in **Settings > Diagnostics**.

## Privacy

Audio is recorded, transcribed, and pasted entirely on your device. There is no cloud transcription, no account, and no upload of your recordings.
