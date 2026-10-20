using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class DictationHistoryStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lafazflow-history-").FullName;

    private string HistoryPath => Path.Combine(_directory, "history.json");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void AppendLoadsNewestFirst()
    {
        var store = new DictationHistoryStore(HistoryPath);

        store.Append("first");
        store.Append("second");
        store.Append("third");

        var entries = store.LoadRecent();
        Assert.Equal(["third", "second", "first"], entries.Select(entry => entry.Text));
    }

    [Fact]
    public void AppendCapsAtMaxEntries()
    {
        var store = new DictationHistoryStore(HistoryPath);
        for (var index = 0; index < DictationHistoryStore.MaxEntries + 5; index++)
        {
            store.Append($"text {index}");
        }

        var entries = store.LoadRecent();
        Assert.Equal(DictationHistoryStore.MaxEntries, entries.Count);
        Assert.Equal($"text {DictationHistoryStore.MaxEntries + 4}", entries[0].Text);
        Assert.Equal("text 5", entries[^1].Text);
    }

    [Fact]
    public void ClearRemovesEverything()
    {
        var store = new DictationHistoryStore(HistoryPath);
        store.Append("one");
        store.Append("two");

        var removed = store.Clear();

        Assert.Equal(2, removed);
        Assert.Empty(store.LoadRecent());
    }

    [Fact]
    public void CorruptFileLoadsEmptyAndRecovers()
    {
        File.WriteAllText(HistoryPath, "{ not valid json");
        var store = new DictationHistoryStore(HistoryPath);

        Assert.Empty(store.LoadRecent());

        store.Append("recovered");
        Assert.Equal(["recovered"], store.LoadRecent().Select(entry => entry.Text));
    }

    [Fact]
    public void BlankTextIsIgnored()
    {
        var store = new DictationHistoryStore(HistoryPath);

        store.Append("   ");

        Assert.Empty(store.LoadRecent());
    }
}
