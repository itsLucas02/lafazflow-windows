using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class LocalModelCatalogTests
{
    [Fact]
    public void CatalogContainsExpectedWhisperModelsInUserFacingOrder()
    {
        var ids = LocalModelCatalog.Models.Select(model => model.Id).ToArray();

        Assert.Equal(
            ["ggml-base.en", "ggml-small.en", "ggml-medium.en", "ggml-large-v3-turbo-q5_0"],
            ids);
    }

    [Fact]
    public void CatalogModelsHaveUniqueIdsAndFileNames()
    {
        Assert.Equal(
            LocalModelCatalog.Models.Count,
            LocalModelCatalog.Models.Select(model => model.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            LocalModelCatalog.Models.Count,
            LocalModelCatalog.Models.Select(model => model.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void CatalogModelsExposeRequiredCardMetadata()
    {
        foreach (var model in LocalModelCatalog.Models)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
            Assert.EndsWith(".bin", model.FileName);
            Assert.False(string.IsNullOrWhiteSpace(model.SizeLabel));
            Assert.False(string.IsNullOrWhiteSpace(model.LanguageLabel));
            Assert.InRange(model.SpeedScore, 0.01, 1);
            Assert.InRange(model.AccuracyScore, 0.01, 1);
            Assert.False(string.IsNullOrWhiteSpace(model.RamLabel));
            Assert.False(string.IsNullOrWhiteSpace(model.Description));
            Assert.StartsWith("https://", model.DownloadUrl);
            Assert.True(Uri.TryCreate(model.DownloadUrl, UriKind.Absolute, out _));
        }
    }
}
