using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public sealed partial class LiveTranscriptStabilizer
{
    private const double RegressiveLengthRatio = 0.82;

    private string _lastAccepted = "";

    public string LastSuppressionReason { get; private set; } = "";

    public bool TryAccept(string text, out string preview)
    {
        LastSuppressionReason = "";
        preview = Normalize(text);
        if (preview.Length == 0)
        {
            LastSuppressionReason = "empty";
            return false;
        }

        if (string.Equals(preview, _lastAccepted, StringComparison.Ordinal))
        {
            preview = "";
            LastSuppressionReason = "duplicate";
            return false;
        }

        if (IsRegressive(preview))
        {
            preview = "";
            LastSuppressionReason = "regressive";
            return false;
        }

        _lastAccepted = preview;
        return true;
    }

    public void Reset()
    {
        _lastAccepted = "";
        LastSuppressionReason = "";
    }

    private bool IsRegressive(string preview)
    {
        if (_lastAccepted.Length == 0 || preview.Length >= _lastAccepted.Length)
        {
            return false;
        }

        if (_lastAccepted.StartsWith(preview, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return preview.Length < _lastAccepted.Length * RegressiveLengthRatio;
    }

    private static string Normalize(string text)
    {
        return WhitespaceRegex().Replace(text.Trim(), " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
