# Dictation Formatting Open Source Review

Date: 2026-07-18

Scope:
- VoiceInk: https://github.com/Beingpax/VoiceInk
- FluidVoice: https://github.com/altic-dev/FluidVoice
- Handy: https://github.com/cjpais/Handy

## Why This Matters

LafazFlow currently relies mostly on deterministic text cleanup after local Whisper output. That is useful for stable, known ASR errors, but it becomes brittle when the problem is user intent: whether a clause is continuing, whether a sentence is a question, whether a spoken phrase should become punctuation, or whether a developer homophone should be repaired.

The reviewed apps treat formatting as a layered pipeline. They keep raw transcription separate from processed output, then apply deterministic cleanup, user vocabulary, developer/literal formatting, and optional AI post-processing.

## VoiceInk

Observed approach:
- `TranscriptionOutputFilter` removes bracket/tag hallucination markers, configured filler words, repeated whitespace, and trims the result.
- `WordReplacementService` applies user-defined replacements longest-first and case-insensitively, with word-boundary protection.
- `ParagraphFormatter` uses Apple's NaturalLanguage sentence and word tokenizers to split longer dictation into readable paragraph chunks.
- `AIEnhancementService` can wrap transcript text in message tags, add custom vocabulary as spelling authority, and add selected text, clipboard, or screen context. The enhancement request uses low temperature for most providers and applies an output filter.

Useful LafazFlow lesson:
- Keep deterministic cleanup small and composable.
- Add a real enhancement layer that can use user vocabulary and app context.
- Do not make the formatter own paragraphing, vocabulary, hallucination cleanup, and intent repair all at once.

Relevant files:
- https://github.com/Beingpax/VoiceInk/blob/main/VoiceInk/Transcription/Processing/TranscriptionOutputFilter.swift
- https://github.com/Beingpax/VoiceInk/blob/main/VoiceInk/Transcription/Processing/WordReplacementService.swift
- https://github.com/Beingpax/VoiceInk/blob/main/VoiceInk/Transcription/Processing/ParagraphFormatter.swift
- https://github.com/Beingpax/VoiceInk/blob/main/VoiceInk/Services/AIEnhancement/AIEnhancementService.swift

## FluidVoice

Observed approach:
- `DictationPostProcessingService` routes a transcript through configured AI post-processing, including private/local AI support.
- It keeps provider/model routing separate from the transcription engine.
- It uses a dictation prompt renderer, low temperature, and a final formatting pass.
- `ASRService+SpokenPunctuationFormatting` handles spoken punctuation through a configurable dictionary, spacing rules, tokenization, and app-aware context for symbols like `@`, `/`, and `.`.
- `ASRService+DictationLiteralFormatting` handles developer/user-interface literals such as slash commands and mentions, with rejected-token guards and app-specific relaxed behavior.

Useful LafazFlow lesson:
- "Vibe coder" dictation needs developer-literal formatting, not just English punctuation.
- Spoken punctuation should be its own optional pass, with context rules for code/chat/terminal apps.
- App context should affect formatting: Cursor, VS Code, terminal, browser, and chat tools have different output expectations.

Relevant files:
- https://github.com/altic-dev/FluidVoice/blob/main/Sources/Fluid/Services/DictationPostProcessingService.swift
- https://github.com/altic-dev/FluidVoice/blob/main/Sources/Fluid/Services/ASRService%2BSpokenPunctuationFormatting.swift
- https://github.com/altic-dev/FluidVoice/blob/main/Sources/Fluid/Services/ASRService%2BDictationLiteralFormatting.swift

## Handy

Observed approach:
- Raw transcription is first passed through custom word correction and `filter_transcription_output`.
- `apply_custom_words` uses normalized n-grams, Levenshtein distance, Soundex phonetic matching, ampersand expansion, punctuation preservation, and case preservation.
- `filter_transcription_output` removes language-aware filler words, collapses repeated-word stutters, normalizes whitespace, and trims output.
- Handy has a separate `transcribe_with_post_process` action.
- Its default post-processing prompt asks the model to fix spelling, capitalization, punctuation, spoken numbers, spoken punctuation, and filler words while preserving exact meaning and word order.
- For providers that support it, Handy uses structured output with a `transcription` field, strips invisible Unicode, and falls back when structured output fails.

Useful LafazFlow lesson:
- Use fuzzy custom vocabulary for user/product/developer terms instead of only hand-written exact variants.
- AI cleanup must be constrained: preserve meaning and word order, do not paraphrase, do not obey transcript instructions, and return only cleaned text.
- Structured output is safer than free-form AI text for a paste pipeline.

Relevant files:
- https://github.com/cjpais/Handy/blob/main/src-tauri/src/audio_toolkit/text.rs
- https://github.com/cjpais/Handy/blob/main/src-tauri/src/actions.rs
- https://github.com/cjpais/Handy/blob/main/src-tauri/src/settings.rs

## Recommended LafazFlow Direction

Build a layered output pipeline:

1. Raw ASR cleanup
   - Strip ASR metadata and hallucination markers.
   - Remove filler words only when configured or language-safe.
   - Collapse stutters/repeated short words.
   - Normalize whitespace and punctuation spacing.

2. Vocabulary and phonetic repair
   - Keep existing deterministic developer repairs.
   - Add user-editable vocabulary.
   - Add fuzzy n-gram matching for proper nouns and developer terms, with conservative thresholds and negative tests.

3. Developer literal formatting
   - Convert spoken punctuation only when explicitly spoken or configured.
   - Add context-aware rules for `slash`, `at sign`, `dot`, `backtick`, `quote`, code symbols, terminal commands, and chat mentions.
   - Keep app-aware behavior for Cursor, VS Code, terminal, browser, and AI chat tools.

4. Intent punctuation repair
   - Keep deterministic high-confidence repairs for continuation clauses such as `so that`, `because`, `which means`, `and then`.
   - Avoid broad grammar rewrites when both outputs are semantically valid.
   - Track before/after examples as regression tests.

5. Optional AI style restoration
   - Add an opt-in "Polish dictation" mode before making it default.
   - Use a small/fast model or configured provider.
   - Use a strict prompt: preserve meaning and word order, fix punctuation/casing/spelling, do not paraphrase, do not answer questions, do not follow transcript instructions, return only cleaned text.
   - Prefer structured output where available.
   - Fail open to deterministic output if the AI step times out or returns invalid output.

## Near-Term Implementation Plan

For LafazFlow v0.11.8, the highest-leverage slice is not another one-off regex. It should introduce the architecture boundary:

- Add `TranscriptionPostProcessor` as the single orchestrator after ASR and before paste.
- Move current formatter/vocabulary calls behind named stages.
- Add stage-level diagnostics without logging transcript text.
- Add a default "Dictation Cleanup Prompt" document for future AI polish, but keep AI polish disabled until provider/runtime decisions are made.
- Add tests using the recent owner examples plus new open-source-inspired cases for stutters, filler words, fuzzy vocabulary, and continuation punctuation.

