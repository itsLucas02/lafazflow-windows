# Security Policy

LafazFlow for Windows is a privacy-first, local-first dictation app. It records microphone audio, transcribes it locally with a persistent `whisper.cpp` engine, and pastes the result into the application you were already using. There is no cloud transcription, no required account, and no upload of your recordings.

## Supported versions

The latest release on the [Releases](https://github.com/itsLucas02/lafazflow-windows/releases) page is the only supported version. Security fixes are delivered as new releases; please update to the newest version before reporting an issue.

| Version | Supported |
| ------- | --------- |
| latest release | Yes |
| older releases | No |

## Reporting a vulnerability

Please **do not open a public issue** for security vulnerabilities.

To report a vulnerability privately:

1. Open the repository's **Security** tab and use **Report a vulnerability** (GitHub private advisory workflow).
2. Include as much of the following as possible:
   - The affected version (visible in Settings and in the file's product version).
   - Steps to reproduce, including the Windows version and whether CUDA/Quality mode is active.
   - The expected behavior and the observed behavior.
   - Whether any local files (logs, recordings, settings) were involved.

You will receive an acknowledgement within a reasonable time, and we will work with you to confirm the issue before a coordinated disclosure. Please do not share details publicly until a fix is released.

## Security model and expectations

LafazFlow is a local desktop application that deliberately has deep access to your machine: it installs a low-level keyboard hook (double-Shift detection), records from the microphone, reads and writes the clipboard, simulates paste keystrokes into the previously active application, and reads on-screen context for dictation support.

The core privacy guarantee is that **your speech and transcripts stay on your device**:

- Audio is captured, transcribed, and pasted locally.
- Recordings and logs are retained only when you explicitly enable **Keep recordings for diagnostics** in **Settings > Diagnostics**.
- App logs redact Windows paths, the current user name, and known transcript/clipboard/audio payload patterns before writing to disk.
- Hotkey diagnostics record gesture events and reasons only, never transcript, clipboard, audio, or path content.
- Model files are downloaded separately from the [whisper.cpp Hugging Face repository](https://huggingface.co/ggerganov/whisper.cpp) (or imported by you) and are never bundled into release packages.

### What we consider in scope

- Privacy or data-handling defects: recorded audio or transcripts written where they should not be, clipboard content leaking into logs, prompt text leaking into pasted output, or diagnostic data that includes speech content.
- Reliability defects that could paste a transcript twice, deliver a wrong transcript, or leak a vocabulary prompt into a document.
- Unsafe handling of model downloads or bundled binaries (for example, missing integrity or provenance checks).
- Escalation from the deep system access above (keyboard hook, clipboard, simulated input).

### What we consider out of scope

- The absence of a code-signing certificate. Releases are currently unsigned and trigger a Windows SmartScreen warning; this is a known limitation, not a vulnerability.
- Platform-level protections (for example, other local malware that already has the same user rights).
- Feature requests and general bug reports — please use [GitHub Issues](https://github.com/itsLucas02/lafazflow-windows/issues) for those.

## Supply chain notes

- Every release embeds a provenance manifest (`LafazFlow-artifact-manifest.json`) recording the SHA-256 of each shipped binary and the exact `whisper.cpp` source revision or release identity used to produce it.
- The release workflow pins every GitHub Action to an immutable commit SHA and builds the bundled native worker from a pinned `whisper.cpp` revision rather than an unrecorded "latest".
- If you distribute LafazFlow binaries yourself, prefer building from source and verifying against the provenance manifest in the official release.
