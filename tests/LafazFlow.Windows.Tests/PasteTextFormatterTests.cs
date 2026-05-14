using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class PasteTextFormatterTests
{
    [Fact]
    public void EnsureTrailingSeparatorAddsSpaceAfterSentence()
    {
        var result = PasteTextFormatter.EnsureTrailingSeparator("Testing, testing, one, two, three, over.");

        Assert.Equal("Testing, testing, one, two, three, over. ", result);
    }

    [Fact]
    public void EnsureTrailingSeparatorDoesNotAddSecondSpace()
    {
        var result = PasteTextFormatter.EnsureTrailingSeparator("Testing. ");

        Assert.Equal("Testing. ", result);
    }

    [Fact]
    public void EnsureTrailingSeparatorPreservesEmptyText()
    {
        var result = PasteTextFormatter.EnsureTrailingSeparator("");

        Assert.Equal("", result);
    }
}
