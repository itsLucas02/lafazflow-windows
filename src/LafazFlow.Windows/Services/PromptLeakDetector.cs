using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

/// <summary>
/// Detects whisper.cpp "prompt continuation" hallucinations where the decode
/// emits the application's vocabulary prompt text (for example "Custom
/// vocabulary," followed by repeated invented words) instead of the user's
/// speech. Prompt text must never reach the clipboard, so the recorder treats
/// a detected leak as a no-speech failure instead of pasting it.
/// </summary>
public static partial class PromptLeakDetector
{
    private const string VocabularyMarker = "custom vocabulary";
    private const int MinimumPromptEchoLength = 40;
    private const int RepetitionRunThreshold = 8;

    public static bool IsPromptLeak(string transcript, string prompt)
    {
        var normalizedTranscript = Normalize(transcript);
        var normalizedPrompt = Normalize(prompt);
        if (normalizedTranscript.Length == 0 || normalizedPrompt.Length == 0)
        {
            return false;
        }

        var startsWithMarker = normalizedTranscript.StartsWith(
            VocabularyMarker,
            StringComparison.Ordinal);
        var transcriptIsPromptEcho = normalizedTranscript.Length >= MinimumPromptEchoLength
            && normalizedPrompt.Contains(normalizedTranscript, StringComparison.Ordinal);
        var hallucinatedRepetition = HasLongRepeatedWordRun(normalizedTranscript);

        return startsWithMarker && (transcriptIsPromptEcho || hallucinatedRepetition)
            || transcriptIsPromptEcho;
    }

    private static bool HasLongRepeatedWordRun(string normalizedText)
    {
        var words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var run = 1;
        for (var index = 1; index < words.Length; index++)
        {
            if (string.Equals(words[index], words[index - 1], StringComparison.Ordinal))
            {
                run++;
                if (run >= RepetitionRunThreshold)
                {
                    return true;
                }
            }
            else
            {
                run = 1;
            }
        }

        return false;
    }

    private static string Normalize(string text)
    {
        return string.Join(
            " ",
            NonWordRegex()
                .Replace(text ?? "", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.ToLowerInvariant()));
    }

    [GeneratedRegex("[^\\p{L}\\p{N}]+")]
    private static partial Regex NonWordRegex();
}
