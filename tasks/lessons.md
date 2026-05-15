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

## Optimize for rapid dictation latency before maximum accuracy
- Pattern: Full `large-v3-turbo` made Windows dictation feel much slower than the macOS reference app, even though it stayed local and offline.
- Rule: On this Windows `whisper.cpp` setup, prefer `ggml-base.en.bin` for default dictation latency and use vocabulary correction for technical terms; Q5 is optional quality mode, not the default.

## Keep brand/product names in local vocabulary
- Pattern: Fast local models can hear uncommon product names phonetically, such as `MediBrave` becoming `Maddy Breath`, `medibrief`, or `Mad brave`.
- Rule: Add high-value owner/product vocabulary variants to deterministic offline corrections with regression tests.

## Do not use Windows notification sounds as app cues
- Pattern: Windows system sounds like `Hand`, `Exclamation`, and `Asterisk` feel like OS error/notification alerts, not soft the macOS reference app-style feedback.
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

## Prefer rapid queueing over blocking the recorder
- Pattern: Awaiting the full transcription and paste path before returning to idle makes rapid dictation feel slower than the macOS reference app.
- Rule: Stop recording quickly, enqueue the audio for sequential background processing, and allow the next recording while previous jobs are still transcribing.

## Keep clipboard calls on the STA UI thread
- Pattern: Background queue workers run on MTA threads, but WPF clipboard/OLE APIs require an STA thread and fail with `Current thread must be set to single thread apartment`.
- Rule: Run transcription work in the background, but marshal clipboard read/write and paste dispatch through the WPF UI dispatcher.

## Keep coding homophones constrained
- Pattern: Coding terms such as `commit` and `shadcn` can be misheard as normal speech like `come in`, `comes in`, or `Chat CN`.
- Rule: Add deterministic vocabulary for coding terms, but avoid globally rewriting ordinary English phrases when the phrase has common non-coding meaning.

## Avoid third-party trademark references in public materials
- Pattern: Public README, docs, task notes, tests, and script names can accidentally preserve reference-product names.
- Rule: Use neutral wording such as `macOS reference workflow` and do not add vocabulary corrections that emit third-party product names unless explicitly approved for public use.

## Make hotkeys forgiving under real dictation timing
- Pattern: A double-tap window that works while starting may feel unreliable when stopping after speech, especially if the second tap is slightly slower or a key-up event is missed.
- Rule: Prefer a forgiving double-tap window and stale key-down recovery over a brittle modifier sequence.

## Capture repeated ASR phonetics exactly
- Pattern: Uncommon coding terms can produce several stable phonetic outputs from the fast local model even when the owner tries multiple pronunciations.
- Rule: Add regression tests from the owner’s actual observed transcript variants and correct them offline instead of assuming one pronunciation will solve it.
