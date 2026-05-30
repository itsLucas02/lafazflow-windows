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

## Never let live preview block final paste
- Pattern: A live preview cleanup path can delay or prevent the final transcription queue, leaving a completed recording visible but never pasted.
- Rule: Treat final transcription/paste as higher priority than preview cleanup; enqueue the final job immediately and stop preview work asynchronously.

## Bind read-only WPF TextBox values one-way
- Pattern: `TextBox.Text` defaults to TwoWay binding even when the control is read-only, so binding it to a getter-only view-model property can crash during layout.
- Rule: For read-only display fields in WPF, use `Mode=OneWay` explicitly or use a non-editable display control.

## Treat clipboard restore as best-effort only
- Pattern: IDEs can leave rich or image clipboard formats that throw `CLIPBRD_E_BAD_DATA` when read, causing dictation paste to fail before LafazFlow writes the transcript.
- Rule: Never let previous-clipboard snapshot or restore failures block transcription paste; skip unreadable formats and continue with the text paste.

## Strip ASR metadata markers before paste
- Pattern: Whisper can emit bracketed non-speech markers such as `[BLANK_AUDIO]`, especially around silence at the end of a recording.
- Rule: Treat bracketed audio-status markers as transcription metadata, not user content, and remove them in the transcript formatter before vocabulary correction and paste.

## Strip known non-speech captions broadly
- Pattern: Whisper can emit bracketed descriptive captions such as `[MUSIC PLAYING]`, `[LAUGHTER]`, or `[APPLAUSE]` that are not dictated user text.
- Rule: Remove known ASR non-speech captions before paste, but do not delete arbitrary bracketed user content such as `[important note]`.

## Make continuation casing context-aware when possible
- Pattern: A new dictation pasted after existing mid-sentence punctuation like `Whatever,` should continue as `hello` instead of forcing `Hello`.
- Rule: Use best-effort focused-field context to lowercase only the first normal word after comma/colon/semicolon context; preserve acronyms and the pronoun `I`, and fall back safely when an app does not expose text context.

## Match the reference model before blaming model quality
- Pattern: If the Windows port feels less accurate than the macOS reference workflow, first check whether it is using the same local model and comparable acceleration/VAD settings.
- Rule: Keep the public default lightweight, but provide a local quality profile that uses the reference `ggml-large-v3-turbo-q5_0` model and focuses optimization work on CUDA/VAD/runtime parity.

## Activate acceleration end to end, not just in code
- Pattern: Installing CUDA and building a CUDA whisper-cli is not enough if the running app still defaults to CPU or cannot see CUDA runtime DLLs.
- Rule: Verify the actual executable with the exact runtime PATH, then configure the app to use the CUDA backend and quality model before claiming CUDA is active.

## Patch every transcription launcher
- Pattern: LafazFlow can launch Whisper from both final transcription and live preview; fixing only one path still leaves user-facing CUDA DLL errors.
- Rule: When changing Whisper process environment or arguments, search every `ProcessStartInfo` path and keep preview and final transcription launch behavior aligned.

## Enforce English-only dictation in decode settings
- Pattern: A multilingual Whisper model can output Malay/Indonesian text from English audio even when `-l en` is present, especially with fallback sampling and a vocabulary-only prompt.
- Rule: For English dictation mode, use deterministic decoding, disable fallback, and prepend an explicit English-only instruction before the vocabulary prompt.

## Respect formatter and vocabulary pipeline order
- Pattern: Formatter punctuation runs before vocabulary correction, so homophone fixes can create question phrases after a period has already been appended.
- Rule: When vocabulary correction creates a conversational question lead-in, also repair the ending punctuation in that same correction pass.

## Capture observed product-name phonetics immediately
- Pattern: The local model can produce new stable phonetic variants for known product names, such as `superbiz` for `Supabase`.
- Rule: Add exact observed product-name variants to the offline vocabulary table with focused regression tests instead of waiting for model changes.

## Capture near-miss product spellings immediately
- Pattern: Local ASR can get a technical term nearly right but transpose letters or add a familiar suffix, such as `Supabase` becoming `Supabaes` or `Supabease`; prompt bias alone does not guarantee the final spelling.
- Rule: Add observed near-miss product spellings to offline vocabulary corrections with a failing regression first, especially for daily-use developer terms.

## Include new agent tooling names in both vocabulary and prompt
- Pattern: Agent tooling names such as `Context7` can be misheard as ordinary phrases like `contact seven`, and correction-only fixes do not help existing prompt context.
- Rule: Add observed variants to offline vocabulary, add the canonical term to the default prompt, and migrate existing default prompts while preserving custom prompts.

## Update the executable the user actually launches
- Pattern: Publishing a new stable build elsewhere does not help if the Windows taskbar pin points at an older artifact directory.
- Rule: When validating a Windows desktop fix, inspect the running process path and republish/relaunch that exact path or clearly migrate the shortcut.

## Keep mini recorder metadata inside the shell
- Pattern: Floating labels near the mini recorder shell look accidental and make the compact UI feel less polished.
- Rule: Version/status metadata must live inside the mini recorder shell or a deliberate attached panel, never as a loose manually-positioned label.

## Prefer content-aware compact shell layout
- Pattern: Fixed side columns sized for one label can make new metadata such as a version badge crowd the shell edge.
- Rule: Compact shell metadata should use auto-sized side labels, a stable center minimum, symmetric spacing, and bounded shell growth instead of label-specific column widths.

## Never animate WPF width from Auto
- Pattern: Setting a WPF element's `Width` to Auto leaves the property as `NaN`, and `DoubleAnimation` crashes if it tries to animate that property from the default origin.
- Rule: Any element animated with `WidthProperty` must have a concrete numeric `Width` before the animation starts, or the animation must explicitly provide a valid `From` value.

## Log UI crashes before deciding recovery
- Pattern: WPF dispatcher animation failures can terminate the app without reaching LafazFlow's normal service logs.
- Rule: Register app-level exception handlers during startup, write privacy-safe crash metadata to the local log, and only mark narrowly understood UI animation exceptions as recoverable.

## Repair accidental ASR compounds narrowly
- Pattern: Whisper can collapse ordinary phrases into camel-like compounds, such as `consent form` becoming `consenForm`.
- Rule: Fix observed phrase compounds with targeted vocabulary corrections that preserve sentence-start casing; do not add broad camel-case splitting that could corrupt code identifiers.

## Repair Whisper punctuation conservatively
- Pattern: Whisper can insert bad internal sentence breaks before continuation phrases, such as `checklist. And then` or `over again. And there`.
- Rule: Repair only high-confidence continuation boundaries and observed English lexical drift; avoid broad grammar rewriting that would erase intentional sentence starts.

## Treat explicit plan-mode requests as a hard gate
- Pattern: When the owner says to jump into plan mode first, they expect a concrete written plan and approval checkpoint before implementation.
- Rule: For non-trivial LafazFlow work, write the plan in `tasks/todo.md`, present the plan for approval, and only execute after an explicit implementation request.

## Expose build identity in more than one surface
- Pattern: A compact recorder badge alone is not enough to confirm which pinned Windows build is running when Settings or tray are the visible surfaces.
- Rule: Put compact version identity in the shell, Settings, tray tooltip, and tray menu so the user can verify the active build without guessing.

## Keep phrase repairs tied to observed context
- Pattern: ASR can turn repeated test phrases into plausible conversational phrases such as `Let's think`, but globally replacing the phrase would corrupt real dictation.
- Rule: Repair homophone-like phrases only when the surrounding words match the observed dictation context, such as `Let's think` followed by `one two three` or `1 2 3`.

## Preserve known product and person casing during continuation
- Pattern: Mid-sentence continuation lowercasing can accidentally damage proper nouns and product names, such as `Supabase` becoming `supabase`.
- Rule: Before lowercasing a continuation token, preserve known product/person names and acronyms that are intentionally cased.

## Bias and repair ambiguous domain words together
- Pattern: ASR can choose a valid common word like `rappers` when the user means a domain word like `wrappers`.
- Rule: Add both prompt bias and context-bound offline repair for ambiguous domain words, and include negative tests for the valid common-word meaning.

## Preserve valid acronyms when repairing homophones
- Pattern: ASR can turn short words into acronym-like tokens, such as `theirs` becoming `DRs`, but the acronym can also be legitimate.
- Rule: Repair acronym-like homophones only in observed phrase contexts and add negative tests for legitimate acronym use.

## Show the full patch version in user-facing build identity
- Pattern: Major/minor-only labels hide patch releases, making it hard to confirm whether the running pinned build includes the latest fix.
- Rule: Display full semantic patch format in all user-facing build identity surfaces while keeping file/product metadata aligned.

## Keep common-word repairs domain-bound
- Pattern: ASR can confuse domain words such as `stale` with valid common words like `still` or `steel`.
- Rule: Repair only in narrow domain phrases, such as `stale document/docs/file`, and include negative tests for normal common-word usage.

## Keep payment-provider homophones context-bound
- Pattern: ASR can hear `Stripe` as `strike` or lowercase `stripe`, but both are valid English words outside payment/developer contexts.
- Rule: Repair `strike`/`stripe` to `Stripe` only near payment, checkout, billing, webhook, integration, or explicit app-action words, with negative tests for normal usage.

## Do not hide calibrated user audio behind extra gain cuts
- Pattern: A user-facing volume slider is already the owner's calibrated loudness control; adding quiet per-cue multipliers made sound cues barely audible.
- Rule: Keep cue playback at the configured volume unless there is a measured clipping or harshness problem, and verify volume math with regression tests before release.

## Keep hotkey cue assets short
- Pattern: Even when playback starts immediately, cue files with long trailing silence feel sluggish and make start/stop feedback seem delayed or missing.
- Rule: Start and stop cue assets should stay under roughly half a second, with tests guarding decoded duration.

## Avoid MP3 for tiny UI cues
- Pattern: Tightly trimmed MP3 cues can crackle or break up at playback boundaries even when they do not clip.
- Rule: Use short PCM WAV assets with small fades for hotkey/start/stop cues, and test that those cue files remain PCM WAV.

## Keep UI audio on a persistent output path
- Pattern: Opening a new audio output device for each short cue can crackle or break up during recording and transcription handoff.
- Rule: Decode cues once, cache them, and play through a persistent mixer/output device so start/stop feedback is not competing with device initialization.

## Tune one sound cue at a time
- Pattern: Global cue loudness changes can regress cues that already feel right while leaving a specific cue too quiet.
- Rule: When the owner reports one cue is too subtle, adjust only that cue gain and keep the other cue gains covered by tests.

## Do not let question heuristics override commands
- Pattern: ASR can add a question mark to imperative reminders such as `Don't forget to commit and push`, and generic question-starter rules can misread `Do not...` as a question.
- Rule: Before adding or preserving question punctuation, guard high-confidence command/reminder lead-ins and keep them declarative unless they are explicitly phrased as a real question.

## Repair idioms as complete phrases
- Pattern: ASR can break idioms into individually plausible words, such as `best bang for buck` becoming `best bank for bug`.
- Rule: Repair observed idiom failures as complete phrase patterns with nearby context, never as broad single-word homophone rewrites.

## Keep semantic verb repairs domain-bound
- Pattern: ASR can substitute a plausible helper verb in domain questions, such as `How much storage would it take?` becoming `How much storage would it be?`.
- Rule: Do not deterministically rewrite between two valid phrases such as `would it be` and `would it take` in the default post-processor; without audio confidence or explicit user rules, preserve the model's wording.
