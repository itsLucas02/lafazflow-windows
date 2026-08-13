# LafazFlow.WhisperWorker

Crash-isolated native Whisper worker for LafazFlow. Loads the selected whisper.cpp
model, CUDA backend, and VAD once at startup and serves sequential transcription
requests with a fresh decode state per request. No WPF dependency and no network
listener.

## Source and build

- whisper.cpp revision: `968eebe77225d25e57a3f981da7c696310f0e881` (pinned; matches the owner's CUDA CLI source revision for controlled equivalence).
- License: MIT (whisper.cpp); see `THIRD_PARTY_NOTICES.md`.

Build:

```powershell
.\scripts\build-whisper-worker.ps1 -Backend Cuda   # or -Backend Cpu
```

## M3 protocol (proof of concept)

Commands on stdin: `T <id> <wav-path>`, `C <id>` (cooperative abort),
`PING`, `Q` (shutdown). Responses: `READY`, `LOAD`, `R <id> <text>`,
`M <id> ...timings...`, `F <id> <reason>`, `E <id> <reason>`, `A <id>`, `PONG`.

M4 replaces this control channel with the versioned, current-user-only Windows
named-pipe protocol.
