# Contributing to LafazFlow for Windows

Thank you for your interest in LafazFlow. Bug reports, accessibility improvements, documentation, tests, Windows compatibility fixes, performance work, and thoughtful interface improvements all help the project.

Please read this guide and the project's [README](README.md) before contributing.

## Ground rules

- LafazFlow is **GPL-3.0**. By contributing, you agree that your contributions are licensed under the same terms as the project ([LICENSE](LICENSE)).
- LafazFlow is **privacy-first and local-first**. Keep it that way: no cloud transcription as a primary path, no accounts required, and no telemetry that uploads speech or transcripts.
- Keep user data out of the repository. Never commit Whisper models, recordings, transcripts, credentials, settings files, or machine-specific configuration.
- Avoid third-party trademark references in user-facing material, vocabulary defaults, and product copy unless explicitly approved.

## Getting started

### Requirements

- Windows 10 or Windows 11, 64-bit
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A local `whisper.cpp` Windows CLI build (the app or the release workflow can supply one)
- Optional: an NVIDIA GPU and CUDA toolkit for the accelerated quality profile

### Building and testing

```powershell
dotnet restore LafazFlow.Windows.sln
dotnet build LafazFlow.Windows.sln --configuration Release
dotnet test LafazFlow.Windows.sln
```

For the native worker and CUDA quality profile, see `scripts/check-quality-prereqs.ps1` and the docs under `docs/`.

## Making changes

1. **Keep the change focused.** Explain the user-facing reason in the description or commit message. A focused change is far easier to review and verify than a broad one.
2. **Add or update tests when behavior changes.** The test suite is deliberately large (see `tests/`) and is the primary safety net for dictation reliability, formatting, clipboard behavior, and the worker lifecycle. Bug fixes should generally land with a failing test first.
3. **Run the full suite before opening a pull request:**
   ```powershell
   dotnet test LafazFlow.Windows.sln
   dotnet build LafazFlow.Windows.sln --configuration Release
   ```
4. **Do not commit** Whisper models, recordings, transcripts, credentials, settings files, or machine-specific paths. Check `.gitignore` and update it if a new artifact category needs excluding.

## Branching and commits

- Work on a descriptive branch (for example `fix/double-shift-repeat` or `docs/security-policy`) and open a pull request against `main`.
- Keep commit messages concise and prefixed by the area or intent where practical (for example `Fix`, `Add`, `Harden`, `docs:`), followed by a short description of the user-facing or behavioral outcome.
- One logical change per commit; avoid mixing unrelated edits.

## Pull request checklist

Before requesting review, confirm:

- [ ] The change is focused and has a clear user-facing reason.
- [ ] Tests were added or updated for behavior changes.
- [ ] `dotnet test LafazFlow.Windows.sln` passes.
- [ ] `dotnet build LafazFlow.Windows.sln --configuration Release` passes.
- [ ] No models, recordings, transcripts, credentials, or machine-specific files are committed.

## Reporting bugs and proposing features

Use [GitHub Issues](https://github.com/itsLucas02/lafazflow-windows/issues) for reproducible bugs and focused feature proposals. Provide your Windows version, the LafazFlow version (visible in Settings), whether CUDA/Quality mode is active, and steps to reproduce.

For security vulnerabilities, please **do not** open a public issue — follow the private reporting process in [SECURITY.md](SECURITY.md).

## Code of conduct

All participants are expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md). Be respectful, constructive, and patient.
