using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

/// <summary>
/// Detects whisper.cpp "prompt continuation" hallucinations where the decode
/// emits the application's vocabulary prompt text (for example "Custom
/// vocabulary," followed by repeated invented words) instead of the user's
/// speech, as well as standalone repetition loops (for example "1.1.1.1.1...").
/// Hallucinated text must never reach the clipboard, so the recorder and the
/// live preview treat a detected leak as a no-speech failure instead.
/// </summary>
public static partial class PromptLeakDetector
{
    private const string VocabularyMarker = "custom vocabulary";
    private const int MinimumPromptEchoLength = 40;
    private const int RepetitionRunThreshold = 8;
    private const int StandaloneRepetitionRunThreshold = 15;
    private const double RepetitionDominanceThreshold = 0.8;

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
        var runawayRepetition = HasDominantRepeatedWordRun(normalizedTranscript);

        return transcriptIsPromptEcho
            || (startsWithMarker && hallucinatedRepetition)
            || runawayRepetition;
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

    /// <summary>
    /// Catches standalone repetition loops (no prompt marker required): the same
    /// normalized token repeated 15+ times consecutively AND dominating the
    /// transcript. A short stutter embedded in real speech is not a hallucination.
    /// </summary>
    private static bool HasDominantRepeatedWordRun(string normalizedText)
    {
        var words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < StandaloneRepetitionRunThreshold)
        {
            return false;
        }

        var run = 1;
        for (var index = 1; index < words.Length; index++)
        {
            if (string.Equals(words[index], words[index - 1], StringComparison.Ordinal))
            {
                run++;
                if (run >= StandaloneRepetitionRunThreshold)
                {
                    var token = words[index];
                    var count = words.Count(word => string.Equals(word, token, StringComparison.Ordinal));
                    return (double)count / words.Length >= RepetitionDominanceThreshold;
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
