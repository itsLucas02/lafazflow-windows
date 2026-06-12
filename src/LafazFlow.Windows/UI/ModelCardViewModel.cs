using System.ComponentModel;
using System.Runtime.CompilerServices;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.UI;

public sealed class ModelCardViewModel : INotifyPropertyChanged
{
    private bool _isDownloading;
    private double _downloadProgress;

    public ModelCardViewModel(
        LocalModelDefinition definition,
        string installPath,
        bool isInstalled,
        bool isActive)
    {
        Definition = definition;
        InstallPath = installPath;
        IsInstalled = isInstalled;
        IsActive = isActive;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalModelDefinition Definition { get; }

    public string Id => Definition.Id;

    public string DisplayName => Definition.DisplayName;

    public string FileName => Definition.FileName;

    public string Description => Definition.Description;

    public string SizeLabel => Definition.SizeLabel;

    public string ModelFileLabel => $"{SizeLabel} model";

    public string LanguageLabel => Definition.LanguageLabel;

    public string RamLabel => Definition.RamLabel;

    public string MemoryLabel => $"Memory {RamLabel}";

    public double SpeedScore => Definition.SpeedScore;

    public double AccuracyScore => Definition.AccuracyScore;

    public int SpeedPercent => (int)Math.Round(SpeedScore * 100);

    public int AccuracyPercent => (int)Math.Round(AccuracyScore * 100);

    public string SpeedLabel => $"{SpeedPercent}%";

    public string AccuracyLabel => $"{AccuracyPercent}%";

    public string SpeedScoreLabel => $"{SpeedScore * 10:0.0}";

    public string AccuracyScoreLabel => $"{AccuracyScore * 10:0.0}";

    public string SpeedDots => BuildDotScore(SpeedScore);

    public string AccuracyDots => BuildDotScore(AccuracyScore);

    public string InstallPath { get; }

    public bool IsInstalled { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsImported => Definition.IsImported;

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (_isDownloading == value)
            {
                return;
            }

            _isDownloading = value;
            NotifyStateChanged();
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        private set
        {
            if (Math.Abs(_downloadProgress - value) < 0.001)
            {
                return;
            }

            _downloadProgress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DownloadProgressPercent));
            OnPropertyChanged(nameof(DownloadProgressLabel));
        }
    }

    public double DownloadProgressPercent => Math.Round(DownloadProgress * 100);

    public string DownloadProgressLabel => $"{DownloadProgressPercent:0}%";

    public string StatusLabel
    {
        get
        {
            if (IsDownloading)
            {
                return "Downloading";
            }

            if (IsActive)
            {
                return "Active";
            }

            return IsInstalled ? "Installed" : "Missing";
        }
    }

    public string StatusBackground
    {
        get
        {
            if (IsDownloading)
            {
                return "#14233F";
            }

            if (IsActive)
            {
                return "#0E3B2E";
            }

            return IsInstalled ? "#123B3D" : "#3A2D12";
        }
    }

    public string StatusBorder
    {
        get
        {
            if (IsDownloading)
            {
                return "#2C6FE3";
            }

            if (IsActive)
            {
                return "#28C76F";
            }

            return IsInstalled ? "#3DCCC7" : "#D99A22";
        }
    }

    public string StatusForeground
    {
        get
        {
            if (IsDownloading)
            {
                return "#9FC1FF";
            }

            if (IsActive)
            {
                return "#9DF2BE";
            }

            return IsInstalled ? "#9CEAEF" : "#F8D08A";
        }
    }

    public string PrimaryActionLabel
    {
        get
        {
            if (IsDownloading)
            {
                return "Downloading";
            }

            return IsInstalled ? "Use Model" : "Download";
        }
    }

    public bool CanDownload => !Definition.IsImported && !IsInstalled && !IsDownloading;

    public bool CanUse => IsInstalled && !IsActive && !IsDownloading;

    public bool CanPrimaryAction => CanDownload || CanUse;

    public bool CanDelete => IsInstalled && !IsActive && !IsDownloading;

    public bool CanOpenFolder => IsInstalled;

    public void MarkDownloading(double progress)
    {
        IsDownloading = true;
        DownloadProgress = Math.Clamp(progress, 0, 1);
    }

    public void MarkInstalled(bool active)
    {
        IsInstalled = true;
        IsActive = active;
        IsDownloading = false;
        DownloadProgress = 0;
        NotifyStateChanged();
    }

    public void MarkIdle()
    {
        IsDownloading = false;
        DownloadProgress = 0;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusBorder));
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanUse));
        OnPropertyChanged(nameof(CanPrimaryAction));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanOpenFolder));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string BuildDotScore(double score)
    {
        var filled = Math.Clamp((int)Math.Round(score * 5), 0, 5);
        return new string('●', filled) + new string('○', 5 - filled);
    }
}
