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
