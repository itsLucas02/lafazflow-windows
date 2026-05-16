using WpfDataObject = System.Windows.DataObject;
using WpfIDataObject = System.Windows.IDataObject;

namespace LafazFlow.Windows.Services;

public static class ClipboardDataObjectSnapshot
{
    public static bool TryCreate(
        WpfIDataObject source,
        Action<string>? logMessage,
        out WpfIDataObject? snapshot)
    {
        snapshot = null;

        string[] formats;
        try
        {
            formats = source.GetFormats(autoConvert: false);
        }
        catch (Exception error)
        {
            logMessage?.Invoke($"Clipboard restore snapshot skipped because formats could not be read: {error.GetType().Name}.");
            return false;
        }

        var dataObject = new WpfDataObject();
        var copiedAnyFormat = false;
        foreach (var format in formats)
        {
            try
            {
                var data = source.GetData(format, autoConvert: false);
                if (data is null)
                {
                    continue;
                }

                dataObject.SetData(format, data);
                copiedAnyFormat = true;
            }
            catch (Exception error)
            {
                logMessage?.Invoke($"Clipboard restore snapshot skipped unreadable format {SafeFormatName(format)}: {error.GetType().Name}.");
            }
        }

        if (!copiedAnyFormat)
        {
            return false;
        }

        snapshot = dataObject;
        return true;
    }

    private static string SafeFormatName(string format)
    {
        return string.IsNullOrWhiteSpace(format)
            ? "unknown"
            : format.Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }
}
