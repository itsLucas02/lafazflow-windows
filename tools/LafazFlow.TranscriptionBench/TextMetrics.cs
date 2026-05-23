namespace LafazFlow.TranscriptionBench;

public sealed record TextComparisonMetrics(
    double NormalizedEditDistance,
    int ExpectedKeyTermCount,
    int ActualKeyTermCount,
    IReadOnlyList<string> MissingKeyTerms);

public static class TextMetrics
{
    public static TextComparisonMetrics Compare(
        string expected,
        string actual,
        IReadOnlyList<string> keyTerms)
    {
        var expectedTerms = keyTerms
            .Where(term => ContainsTerm(expected, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(term => term, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actualTerms = expectedTerms
            .Where(term => ContainsTerm(actual, term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingTerms = expectedTerms
            .Where(term => !actualTerms.Contains(term))
            .ToArray();

        return new TextComparisonMetrics(
            NormalizedEditDistance(expected, actual),
            expectedTerms.Length,
            actualTerms.Count,
            missingTerms);
    }

    private static bool ContainsTerm(string text, string term)
    {
        return text.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static double NormalizedEditDistance(string expected, string actual)
    {
        if (expected.Length == 0 && actual.Length == 0)
        {
            return 0;
        }

        var distance = EditDistance(expected, actual);
        return (double)distance / Math.Max(expected.Length, actual.Length);
    }

    private static int EditDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var cost = char.ToUpperInvariant(left[row - 1]) == char.ToUpperInvariant(right[column - 1]) ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
