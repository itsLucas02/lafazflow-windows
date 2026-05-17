using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace LafazFlow.Windows.Services;

public sealed class FocusedTargetTextContextService : ITargetTextContextService
{
    private const int MaxContextCharacters = 500;

    public string GetTextBeforeCaret(IntPtr targetWindow)
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null)
            {
                return string.Empty;
            }

            var textPatternContext = TryGetTextPatternContext(focusedElement);
            if (!string.IsNullOrWhiteSpace(textPatternContext))
            {
                return LastCharacters(textPatternContext);
            }

            var valuePatternContext = TryGetValuePatternContext(focusedElement);
            return LastCharacters(valuePatternContext);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryGetTextPatternContext(AutomationElement focusedElement)
    {
        if (!focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)
            || pattern is not TextPattern textPattern)
        {
            return string.Empty;
        }

        var selections = textPattern.GetSelection();
        if (selections.Length == 0)
        {
            return string.Empty;
        }

        var range = selections[0].Clone();
        range.MoveEndpointByRange(
            TextPatternRangeEndpoint.Start,
            textPattern.DocumentRange,
            TextPatternRangeEndpoint.Start);
        return range.GetText(MaxContextCharacters);
    }

    private static string TryGetValuePatternContext(AutomationElement focusedElement)
    {
        if (!focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            || pattern is not ValuePattern valuePattern)
        {
            return string.Empty;
        }

        return valuePattern.Current.Value ?? string.Empty;
    }

    private static string LastCharacters(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= MaxContextCharacters)
        {
            return text;
        }

        return text[^MaxContextCharacters..];
    }
}
