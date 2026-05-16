using LafazFlow.Windows.Services;
using WpfDataFormats = System.Windows.DataFormats;
using WpfIDataObject = System.Windows.IDataObject;

namespace LafazFlow.Windows.Tests;

public sealed class ClipboardDataObjectSnapshotTests
{
    [Fact]
    public void TryCreateSkipsUnreadableFormatsAndKeepsReadableFormats()
    {
        var logs = new List<string>();
        var source = new FakeClipboardDataObject(
            new Dictionary<string, object?>
            {
                [WpfDataFormats.Text] = "hello",
                ["BrokenFormat"] = new InvalidOperationException("bad data")
            });

        var created = ClipboardDataObjectSnapshot.TryCreate(source, logs.Add, out var snapshot);

        Assert.True(created);
        Assert.NotNull(snapshot);
        Assert.Equal("hello", snapshot.GetData(WpfDataFormats.Text, autoConvert: false));
        Assert.Contains(logs, entry => entry.Contains("BrokenFormat", StringComparison.Ordinal));
    }

    [Fact]
    public void TryCreateReturnsFalseWhenAllFormatsAreUnreadable()
    {
        var source = new FakeClipboardDataObject(
            new Dictionary<string, object?>
            {
                ["BrokenFormat"] = new InvalidOperationException("bad data")
            });

        var created = ClipboardDataObjectSnapshot.TryCreate(source, _ => { }, out var snapshot);

        Assert.False(created);
        Assert.Null(snapshot);
    }

    [Fact]
    public void TryCreateReturnsFalseWhenFormatListCannotBeRead()
    {
        var source = new FakeClipboardDataObject(new InvalidOperationException("bad formats"));

        var created = ClipboardDataObjectSnapshot.TryCreate(source, _ => { }, out var snapshot);

        Assert.False(created);
        Assert.Null(snapshot);
    }

    private sealed class FakeClipboardDataObject : WpfIDataObject
    {
        private readonly IReadOnlyDictionary<string, object?>? _data;
        private readonly Exception? _formatsError;

        public FakeClipboardDataObject(IReadOnlyDictionary<string, object?> data)
        {
            _data = data;
        }

        public FakeClipboardDataObject(Exception formatsError)
        {
            _formatsError = formatsError;
        }

        public object? GetData(string format)
        {
            return GetData(format, autoConvert: true);
        }

        public object? GetData(string format, bool autoConvert)
        {
            if (_data is null || !_data.TryGetValue(format, out var value))
            {
                return null;
            }

            return value is Exception error ? throw error : value;
        }

        public object? GetData(Type format)
        {
            return GetData(format.FullName ?? format.Name);
        }

        public bool GetDataPresent(string format)
        {
            return _data?.ContainsKey(format) == true;
        }

        public bool GetDataPresent(string format, bool autoConvert)
        {
            return GetDataPresent(format);
        }

        public bool GetDataPresent(Type format)
        {
            return GetDataPresent(format.FullName ?? format.Name);
        }

        public string[] GetFormats()
        {
            return GetFormats(autoConvert: true);
        }

        public string[] GetFormats(bool autoConvert)
        {
            if (_formatsError is not null)
            {
                throw _formatsError;
            }

            return _data?.Keys.ToArray() ?? [];
        }

        public void SetData(string format, object data, bool autoConvert)
        {
            throw new NotSupportedException();
        }

        public void SetData(string format, object data)
        {
            throw new NotSupportedException();
        }

        public void SetData(Type format, object data)
        {
            throw new NotSupportedException();
        }

        public void SetData(object data)
        {
            throw new NotSupportedException();
        }
    }
}
