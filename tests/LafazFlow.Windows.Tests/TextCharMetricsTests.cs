using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class TextCharMetricsTests
{
    [Theory]
    [InlineData("Hello.", 6, "punct")]
    [InlineData("hello", 5, "letter")]
    [InlineData("123", 3, "digit")]
    [InlineData("hi ", 3, "space")]
    [InlineData("", 0, "none")]
    [InlineData("caf\u00e9", 4, "letter")]
    public void ReportsCharacterCountAndFinalCategory(string text, int expectedCount, string expectedCategory)
    {
        Assert.Equal(expectedCount, TextCharMetrics.CharacterCount(text));
        Assert.Equal(expectedCategory, TextCharMetrics.FinalCharCategory(text));
    }
}
