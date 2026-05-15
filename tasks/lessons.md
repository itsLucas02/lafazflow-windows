# Lessons

## Prefer local/offline transcription for LafazFlow
- Pattern: When discussing the Windows port, keep the default workflow privacy-first and local/offline.
- Rule: Do not suggest cloud transcription as the primary path unless the user explicitly asks for cloud features.

## Verify Git working directory before initialization
- Pattern: A Git initialization command ran from the parent folder instead of the intended project folder.
- Rule: For repository creation or destructive cleanup, verify the exact working directory and resulting `.git` path before proceeding.

## Preserve LafazFlow muscle memory on Windows
- Pattern: The owner prefers a fast double Shift gesture over conventional shortcuts such as `Ctrl+Alt+Space`.
- Rule: Treat double Shift as the default Windows dictation toggle unless the owner explicitly changes the hotkey strategy.

## Answer readiness questions directly
- Pattern: When the owner asks whether required tools are installed, do not jump ahead to implementation choices.
- Rule: Check the local machine and report a clear ready/missing/not-needed-yet matrix before asking to proceed.

## Make setup instructions concrete
- Pattern: The owner had already downloaded required components but the next setup step was unclear.
- Rule: When components exist locally, install/extract them into stable paths, verify exact executable/model paths, and run an end-to-end smoke test before explaining next steps.

## Do not restore clipboard too aggressively in Cursor
- Pattern: Cursor terminal paste can fail silently while LafazFlow restores the previous clipboard, leaving the owner with no pasted text and no manual clipboard fallback.
- Rule: For Cursor/VS Code targets, keep the transcript on the clipboard after attempting paste so manual `Ctrl+V` remains available if the app-specific paste event is swallowed.

## Verify Win32 interop structure sizes
- Pattern: `SendInput` can silently fail if the managed `INPUT` struct is smaller than the native Win32 structure, even when clipboard transcription succeeds.
- Rule: Add regression tests for native struct sizes and check `SendInput` return values instead of ignoring them.

## Optimize for VoiceInk-like latency before maximum accuracy
- Pattern: Full `large-v3-turbo` made Windows dictation feel much slower than VoiceInk, even though it stayed local and offline.
- Rule: On this Windows `whisper.cpp` setup, prefer `ggml-base.en.bin` for default dictation latency and use vocabulary correction for technical terms; Q5 is optional quality mode, not the default.

## Keep brand/product names in local vocabulary
- Pattern: Fast local models can hear uncommon product names phonetically, such as `MediBrave` becoming `Maddy Breath`, `medibrief`, or `Mad brave`.
- Rule: Add high-value owner/product vocabulary variants to deterministic offline corrections with regression tests.

## Do not use Windows notification sounds as app cues
- Pattern: Windows system sounds like `Hand`, `Exclamation`, and `Asterisk` feel like OS error/notification alerts, not soft VoiceInk-style feedback.
- Rule: Keep app cues muted until proper gentle bundled sounds are designed or sourced.

## Keep the recorder shell layout fixed
- Pattern: Secondary content such as transcript previews or processing details can accidentally push the main recorder controls away from their expected position.
- Rule: The main mini recorder shell must keep fixed dimensions and a fixed bottom-center anchor; supplemental content should overlay around it without participating in shell layout.

## Handle Cursor terminal paste as a distinct shortcut
- Pattern: Cursor's integrated terminal can reject plain `Ctrl+V` paste and show clipboard/image paste errors, while manual terminal paste uses `Ctrl+Shift+V`.
- Rule: Treat Cursor/VS Code-like targets as terminal-safe paste targets and dispatch `Ctrl+Shift+V` instead of plain `Ctrl+V`.

## Keep homophone corrections contextual
- Pattern: Fast local Whisper can hear dictated `test` as `that's`, especially in testing phrases.
- Rule: Correct `that's` to `test` only in testing-dictation patterns; do not globally rewrite ordinary `that's` sentences.
