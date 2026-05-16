using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MiniRecorderViewModel _viewModel;
    private readonly Action _openSettings;
    private readonly Action _openLogs;
    private readonly Action _exit;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;

    public TrayIconService(
        MiniRecorderViewModel viewModel,
        Action openSettings,
        Action openLogs,
        Action exit)
    {
        _viewModel = viewModel;
        _openSettings = openSettings;
        _openLogs = openLogs;
        _exit = exit;
        _menu = BuildMenu();
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _openSettings();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateText();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => _openSettings());
        menu.Items.Add("Open Logs", null, (_, _) => _openLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit LafazFlow", null, (_, _) => _exit());
        return menu;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MiniRecorderViewModel.State)
            or nameof(MiniRecorderViewModel.PendingTranscriptionCount)
            or nameof(MiniRecorderViewModel.HasPendingTranscriptions))
        {
            UpdateText();
        }
    }

    private void UpdateText()
    {
        _notifyIcon.Text = TrimTooltip(TrayStatusText.FromViewModel(_viewModel));
    }

    private static string TrimTooltip(string text)
    {
        return text.Length <= 63 ? text : text[..63];
    }

    private static Icon LoadIcon()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                var icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    public static string LogsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LafazFlow",
        "Logs");

    public static void OpenLogsFolder()
    {
        Directory.CreateDirectory(LogsFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = Quote(LogsFolder),
            UseShellExecute = true
        });
    }

    private static string Quote(string path)
    {
        return $"\"{path.Replace("\"", "\\\"")}\"";
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}
