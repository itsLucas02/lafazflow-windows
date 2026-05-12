# LafazFlow Windows MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first Windows-native LafazFlow offline dictation loop with a VoiceInk-like floating recorder, local WAV recording, local `whisper-cli.exe` transcription, and paste-to-active-window behavior.

**Architecture:** Create a .NET 9 WPF app with small services around hotkeys, recording, transcription, clipboard paste, and local JSON settings. The first Whisper integration uses a process bridge to a user-configured `whisper-cli.exe` and `.bin` model path so the offline flow can be verified before native bindings.

**Tech Stack:** .NET 9, WPF, C#, NAudio, xUnit, Win32 interop, local JSON settings, local `whisper.cpp` CLI.

---

## File Structure

- Create `src/LafazFlow.Windows/LafazFlow.Windows.csproj`: WPF app project.
- Create `src/LafazFlow.Windows/App.xaml` and `src/LafazFlow.Windows/App.xaml.cs`: app startup and shutdown.
- Create `src/LafazFlow.Windows/MainWindow.xaml` and `src/LafazFlow.Windows/MainWindow.xaml.cs`: hidden owner window for app lifetime.
- Create `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml` and `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml.cs`: floating recorder panel.
- Create `src/LafazFlow.Windows/UI/MiniRecorderViewModel.cs`: observable recorder UI state.
- Create `src/LafazFlow.Windows/Core/RecordingState.cs`: shared state enum.
- Create `src/LafazFlow.Windows/Core/HotkeyMode.cs`: hotkey mode enum.
- Create `src/LafazFlow.Windows/Core/AppSettings.cs`: strongly typed settings.
- Create `src/LafazFlow.Windows/Services/SettingsStore.cs`: JSON settings load/save.
- Create `src/LafazFlow.Windows/Services/HotkeyService.cs`: Win32 `RegisterHotKey` shortcut handling.
- Create `src/LafazFlow.Windows/Services/AudioCaptureService.cs`: NAudio WAV capture and meter updates.
- Create `src/LafazFlow.Windows/Services/WhisperCliTranscriptionService.cs`: local process invocation.
- Create `src/LafazFlow.Windows/Services/ClipboardPasteService.cs`: clipboard/paste/restore behavior.
- Create `src/LafazFlow.Windows/Services/RecorderController.cs`: orchestrates the workflow.
- Create `tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj`: xUnit tests.
- Create tests for settings, hotkey transitions, whisper command validation, and transcript cleanup.
- Create `.gitignore`: public repo safety.
- Create `README.md`: local/offline MVP purpose and first-run notes.

## Task 1: Scaffold Solution

**Files:**
- Create: `LafazFlow.Windows.sln`
- Create: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Create: `tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj`
- Create: `.gitignore`
- Create: `README.md`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n LafazFlow.Windows
dotnet new wpf -n LafazFlow.Windows -o src/LafazFlow.Windows -f net9.0
dotnet new xunit -n LafazFlow.Windows.Tests -o tests/LafazFlow.Windows.Tests -f net9.0
dotnet sln LafazFlow.Windows.sln add src/LafazFlow.Windows/LafazFlow.Windows.csproj
dotnet sln LafazFlow.Windows.sln add tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj
dotnet add tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj reference src/LafazFlow.Windows/LafazFlow.Windows.csproj
dotnet add src/LafazFlow.Windows/LafazFlow.Windows.csproj package NAudio
```

Expected: solution and two projects are created, and NAudio is referenced by the app project.

- [ ] **Step 2: Enable WinForms interop for clipboard paste**

Modify `src/LafazFlow.Windows/LafazFlow.Windows.csproj` so the `PropertyGroup` contains:

```xml
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>
```

Expected: the app project can use WPF and `System.Windows.Forms` APIs.

- [ ] **Step 3: Replace generated `.gitignore` content**

Create `.gitignore` with:

```gitignore
bin/
obj/
.vs/
.vscode/
TestResults/
*.user
*.suo
*.log
.env
.env.*
models/
recordings/
artifacts/
local-settings.json
*.wav
*.mp3
*.bin
whisper-cli.exe
```

- [ ] **Step 4: Write first README**

Create `README.md` with:

```markdown
# LafazFlow Windows

Windows-native LafazFlow client, built from the macOS LafazFlow/VoiceInk experience as a reference.

The first milestone is local and offline:

- global hotkey
- floating recorder UI
- microphone recording
- local `whisper.cpp` transcription
- paste transcript into the active app

Cloud transcription and AI enhancement are not part of the MVP.
```

- [ ] **Step 5: Verify scaffold**

Run:

```powershell
dotnet build
dotnet test
```

Expected: both commands pass.

- [ ] **Step 6: Commit scaffold**

Run:

```powershell
git add .gitignore README.md LafazFlow.Windows.sln src tests
git commit -m "Scaffold Windows WPF solution"
```

## Task 2: Add Core Types And Settings

**Files:**
- Create: `src/LafazFlow.Windows/Core/RecordingState.cs`
- Create: `src/LafazFlow.Windows/Core/HotkeyMode.cs`
- Create: `src/LafazFlow.Windows/Core/AppSettings.cs`
- Create: `src/LafazFlow.Windows/Services/SettingsStore.cs`
- Create: `tests/LafazFlow.Windows.Tests/SettingsStoreTests.cs`

- [ ] **Step 1: Add failing settings tests**

Create `tests/LafazFlow.Windows.Tests/SettingsStoreTests.cs`:

```csharp
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenFileDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);

        var settings = store.Load();

        Assert.Equal("Ctrl+Alt+Space", settings.HotkeyGesture);
        Assert.Equal(HotkeyMode.Hybrid, settings.HotkeyMode);
        Assert.True(settings.RestoreClipboardAfterPaste);
        Assert.Equal(250, settings.ClipboardRestoreDelayMs);
        Assert.False(settings.AppendTrailingSpace);
        Assert.False(settings.KeepRecordingsForDiagnostics);
    }

    [Fact]
    public void SaveThenLoadRoundTripsSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var expected = AppSettings.Default with
        {
            HotkeyGesture = "Ctrl+Shift+D",
            HotkeyMode = HotkeyMode.Toggle,
            WhisperCliPath = @"C:\Tools\whisper-cli.exe",
            ModelPath = @"C:\Models\ggml-base.en.bin",
            AppendTrailingSpace = true
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj
```

Expected: fails because `AppSettings`, `HotkeyMode`, and `SettingsStore` do not exist.

- [ ] **Step 3: Add core types**

Create `src/LafazFlow.Windows/Core/RecordingState.cs`:

```csharp
namespace LafazFlow.Windows.Core;

public enum RecordingState
{
    Idle,
    Starting,
    Recording,
    Transcribing,
    Enhancing,
    Busy,
    Error
}
```

Create `src/LafazFlow.Windows/Core/HotkeyMode.cs`:

```csharp
namespace LafazFlow.Windows.Core;

public enum HotkeyMode
{
    Toggle,
    PushToTalk,
    Hybrid
}
```

Create `src/LafazFlow.Windows/Core/AppSettings.cs`:

```csharp
namespace LafazFlow.Windows.Core;

public sealed record AppSettings
{
    public string HotkeyGesture { get; init; } = "Ctrl+Alt+Space";
    public HotkeyMode HotkeyMode { get; init; } = HotkeyMode.Hybrid;
    public string WhisperCliPath { get; init; } = "";
    public string ModelPath { get; init; } = "";
    public bool RestoreClipboardAfterPaste { get; init; } = true;
    public int ClipboardRestoreDelayMs { get; init; } = 250;
    public bool AppendTrailingSpace { get; init; }
    public bool KeepRecordingsForDiagnostics { get; init; }

    public static AppSettings Default { get; } = new();
}
```

- [ ] **Step 4: Add settings store**

Create `src/LafazFlow.Windows/Services/SettingsStore.cs`:

```csharp
using System.Text.Json;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class SettingsStore
{
    private readonly string _settingsPath;

    public SettingsStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LafazFlow");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Default;
        }

        var json = File.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions()) ?? AppSettings.Default;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var json = JsonSerializer.Serialize(settings, JsonOptions());
        File.WriteAllText(_settingsPath, json);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true
    };
}
```

- [ ] **Step 5: Verify settings tests**

Run:

```powershell
dotnet test tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj --filter SettingsStoreTests
```

Expected: tests pass.

- [ ] **Step 6: Commit core settings**

Run:

```powershell
git add src/LafazFlow.Windows/Core src/LafazFlow.Windows/Services/SettingsStore.cs tests/LafazFlow.Windows.Tests/SettingsStoreTests.cs
git commit -m "Add local settings store"
```

## Task 3: Add Whisper CLI Transcription Service

**Files:**
- Create: `src/LafazFlow.Windows/Services/WhisperCliTranscriptionService.cs`
- Create: `tests/LafazFlow.Windows.Tests/WhisperCliTranscriptionServiceTests.cs`

- [ ] **Step 1: Add command construction tests**

Create `tests/LafazFlow.Windows.Tests/WhisperCliTranscriptionServiceTests.cs`:

```csharp
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperCliTranscriptionServiceTests
{
    [Fact]
    public void BuildArgumentsUsesModelInputAndTxtOutput()
    {
        var args = WhisperCliTranscriptionService.BuildArguments(
            @"C:\Models\ggml-base.en.bin",
            @"C:\Audio\sample.wav",
            @"C:\Audio\sample");

        Assert.Contains("-m \"C:\\Models\\ggml-base.en.bin\"", args);
        Assert.Contains("-f \"C:\\Audio\\sample.wav\"", args);
        Assert.Contains("-otxt", args);
        Assert.Contains("-of \"C:\\Audio\\sample\"", args);
    }

    [Fact]
    public void ValidatePathsRejectsMissingCli()
    {
        var error = WhisperCliTranscriptionService.ValidatePaths(
            @"C:\missing\whisper-cli.exe",
            Path.GetTempFileName());

        Assert.Equal("Whisper CLI was not found.", error);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj --filter WhisperCliTranscriptionServiceTests
```

Expected: fails because `WhisperCliTranscriptionService` does not exist.

- [ ] **Step 3: Add Whisper CLI service**

Create `src/LafazFlow.Windows/Services/WhisperCliTranscriptionService.cs`:

```csharp
using System.Diagnostics;

namespace LafazFlow.Windows.Services;

public sealed class WhisperCliTranscriptionService
{
    public static string? ValidatePaths(string whisperCliPath, string modelPath)
    {
        if (!File.Exists(whisperCliPath))
        {
            return "Whisper CLI was not found.";
        }

        if (!File.Exists(modelPath))
        {
            return "Whisper model was not found.";
        }

        return null;
    }

    public static string BuildArguments(string modelPath, string audioPath, string outputBasePath)
    {
        return $"-m {Quote(modelPath)} -f {Quote(audioPath)} -otxt -of {Quote(outputBasePath)}";
    }

    public async Task<string> TranscribeAsync(
        string whisperCliPath,
        string modelPath,
        string audioPath,
        CancellationToken cancellationToken)
    {
        var pathError = ValidatePaths(whisperCliPath, modelPath);
        if (pathError is not null)
        {
            throw new InvalidOperationException(pathError);
        }

        if (!File.Exists(audioPath))
        {
            throw new InvalidOperationException("Audio file was not found.");
        }

        var outputBasePath = Path.Combine(
            Path.GetDirectoryName(audioPath)!,
            Path.GetFileNameWithoutExtension(audioPath));

        var startInfo = new ProcessStartInfo
        {
            FileName = whisperCliPath,
            Arguments = BuildArguments(modelPath, audioPath, outputBasePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start Whisper CLI.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            throw new InvalidOperationException($"Whisper CLI failed: {stderr.Trim()}");
        }

        var textPath = outputBasePath + ".txt";
        if (File.Exists(textPath))
        {
            return CleanTranscript(await File.ReadAllTextAsync(textPath, cancellationToken));
        }

        return CleanTranscript(await stdoutTask);
    }

    public static string CleanTranscript(string text)
    {
        return text.Trim();
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
```

- [ ] **Step 4: Verify Whisper tests**

Run:

```powershell
dotnet test tests/LafazFlow.Windows.Tests/LafazFlow.Windows.Tests.csproj --filter WhisperCliTranscriptionServiceTests
```

Expected: tests pass.

- [ ] **Step 5: Commit Whisper service**

Run:

```powershell
git add src/LafazFlow.Windows/Services/WhisperCliTranscriptionService.cs tests/LafazFlow.Windows.Tests/WhisperCliTranscriptionServiceTests.cs
git commit -m "Add local Whisper CLI service"
```

## Task 4: Add Floating Recorder Shell

**Files:**
- Create: `src/LafazFlow.Windows/UI/MiniRecorderViewModel.cs`
- Create: `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml`
- Create: `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml.cs`
- Modify: `src/LafazFlow.Windows/App.xaml`
- Modify: `src/LafazFlow.Windows/MainWindow.xaml.cs`

- [ ] **Step 1: Add recorder view model**

Create `src/LafazFlow.Windows/UI/MiniRecorderViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.UI;

public sealed class MiniRecorderViewModel : INotifyPropertyChanged
{
    private RecordingState _state = RecordingState.Idle;
    private double _audioLevel;
    private string _statusText = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public RecordingState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            StatusText = value switch
            {
                RecordingState.Transcribing => "Transcribing",
                RecordingState.Enhancing => "Enhancing",
                RecordingState.Error => "Error",
                _ => ""
            };
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsProcessing));
        }
    }

    public double AudioLevel
    {
        get => _audioLevel;
        set
        {
            _audioLevel = Math.Clamp(value, 0, 1);
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsRecording => State == RecordingState.Recording;
    public bool IsProcessing => State is RecordingState.Transcribing or RecordingState.Enhancing;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

- [ ] **Step 2: Add mini recorder XAML**

Create `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml`:

```xml
<Window x:Class="LafazFlow.Windows.UI.MiniRecorderWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Width="300"
        Height="120"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="False"
        Topmost="True"
        ResizeMode="NoResize">
    <Grid VerticalAlignment="Bottom" HorizontalAlignment="Center">
        <Border x:Name="RecorderShell"
                Width="184"
                Height="40"
                CornerRadius="20"
                Background="Black"
                MouseLeftButtonDown="RecorderShell_OnMouseLeftButtonDown">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="44"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="44"/>
                </Grid.ColumnDefinitions>

                <TextBlock Grid.Column="0"
                           Text="✓"
                           Foreground="#99FFFFFF"
                           FontSize="13"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"/>

                <ItemsControl Grid.Column="1" x:Name="Visualizer" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <StackPanel Orientation="Horizontal"/>
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                </ItemsControl>

                <TextBlock Grid.Column="2"
                           Text="✨"
                           Foreground="#99FFFFFF"
                           FontSize="13"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"/>
            </Grid>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 3: Add mini recorder code-behind**

Create `src/LafazFlow.Windows/UI/MiniRecorderWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LafazFlow.Windows.UI;

public partial class MiniRecorderWindow : Window
{
    private readonly MiniRecorderViewModel _viewModel;
    private readonly Rectangle[] _bars;
    private readonly double[] _phases;
    private DateTime _lastRender = DateTime.UtcNow;

    public MiniRecorderWindow(MiniRecorderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _bars = Enumerable.Range(0, 15).Select(_ => CreateBar()).ToArray();
        _phases = Enumerable.Range(0, 15).Select(i => i * 0.4).ToArray();

        foreach (var bar in _bars)
        {
            Visualizer.Items.Add(bar);
        }

        CompositionTarget.Rendering += OnRendering;
    }

    public void ShowBottomCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Bottom - Height - 24;
        Show();
    }

    protected override void OnClosed(EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        base.OnClosed(e);
    }

    private static Rectangle CreateBar()
    {
        return new Rectangle
        {
            Width = 3,
            Height = 4,
            RadiusX = 1.5,
            RadiusY = 1.5,
            Margin = new Thickness(1, 0, 1, 0),
            Fill = new SolidColorBrush(Color.FromArgb(217, 255, 255, 255))
        };
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRender).TotalMilliseconds < 16) return;
        _lastRender = now;

        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var amplitude = Math.Pow(_viewModel.AudioLevel, 0.7);

        for (var index = 0; index < _bars.Length; index++)
        {
            var wave = Math.Sin(time * 8 + _phases[index]) * 0.5 + 0.5;
            var centerDistance = Math.Abs(index - _bars.Length / 2.0) / (_bars.Length / 2.0);
            var centerBoost = 1.0 - centerDistance * 0.4;
            var height = _viewModel.IsRecording
                ? Math.Max(4, 4 + amplitude * wave * centerBoost * 24)
                : 4;
            _bars[index].Height = height;
        }
    }

    private void RecorderShell_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}
```

- [ ] **Step 4: Wire hidden startup shell**

Modify `src/LafazFlow.Windows/MainWindow.xaml.cs` so app startup creates and shows the mini recorder briefly for smoke testing:

```csharp
using System.Windows;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows;

public partial class MainWindow : Window
{
    private readonly MiniRecorderViewModel _miniRecorderViewModel = new();
    private readonly MiniRecorderWindow _miniRecorderWindow;

    public MainWindow()
    {
        InitializeComponent();
        _miniRecorderWindow = new MiniRecorderWindow(_miniRecorderViewModel);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Hide();
        _miniRecorderViewModel.State = RecordingState.Idle;
        _miniRecorderWindow.ShowBottomCenter();
    }
}
```

- [ ] **Step 5: Build and run shell**

Run:

```powershell
dotnet build
dotnet run --project src/LafazFlow.Windows/LafazFlow.Windows.csproj
```

Expected: a black compact recorder pill appears bottom-center. Close it from the debugger or stop the process.

- [ ] **Step 6: Commit recorder shell**

Run:

```powershell
git add src/LafazFlow.Windows/UI src/LafazFlow.Windows/MainWindow.xaml.cs
git commit -m "Add floating recorder shell"
```

## Task 5: Add Capture, Controller, Hotkey, And Paste

**Files:**
- Create: `src/LafazFlow.Windows/Services/AudioCaptureService.cs`
- Create: `src/LafazFlow.Windows/Services/ClipboardPasteService.cs`
- Create: `src/LafazFlow.Windows/Services/HotkeyService.cs`
- Create: `src/LafazFlow.Windows/Services/RecorderController.cs`
- Modify: `src/LafazFlow.Windows/MainWindow.xaml.cs`

- [ ] **Step 1: Implement audio capture service**

Create `src/LafazFlow.Windows/Services/AudioCaptureService.cs`:

```csharp
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public sealed class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;

    public event Action<double>? AudioLevelChanged;

    public string Start(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}.wav");

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };
        _writer = new WaveFileWriter(outputPath, _waveIn.WaveFormat);
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        return outputPath;
    }

    public void Stop()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;
        _writer?.Dispose();
        _writer = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);

        var max = 0;
        for (var index = 0; index < e.BytesRecorded; index += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, index);
            max = Math.Max(max, Math.Abs(sample));
        }

        AudioLevelChanged?.Invoke(Math.Clamp(max / 32768.0, 0, 1));
    }
}
```

- [ ] **Step 2: Implement clipboard paste service**

Create `src/LafazFlow.Windows/Services/ClipboardPasteService.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace LafazFlow.Windows.Services;

public sealed class ClipboardPasteService
{
    private const byte VirtualKeyControl = 0x11;
    private const byte VirtualKeyV = 0x56;
    private const uint KeyEventKeyUp = 0x0002;

    public async Task PasteAsync(string text, bool restoreClipboard, int restoreDelayMs, CancellationToken cancellationToken)
    {
        var previousText = restoreClipboard && Clipboard.ContainsText() ? Clipboard.GetText() : null;
        Clipboard.SetText(text);
        await Task.Delay(50, cancellationToken);
        keybd_event(VirtualKeyControl, 0, 0, UIntPtr.Zero);
        keybd_event(VirtualKeyV, 0, 0, UIntPtr.Zero);
        keybd_event(VirtualKeyV, 0, KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(VirtualKeyControl, 0, KeyEventKeyUp, UIntPtr.Zero);

        if (restoreClipboard && previousText is not null)
        {
            await Task.Delay(Math.Max(restoreDelayMs, 250), cancellationToken);
            Clipboard.SetText(previousText);
        }
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
```

- [ ] **Step 3: Implement recorder controller**

Create `src/LafazFlow.Windows/Services/RecorderController.cs`:

```csharp
using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Services;

public sealed class RecorderController
{
    private readonly MiniRecorderViewModel _viewModel;
    private readonly MiniRecorderWindow _window;
    private readonly AudioCaptureService _audioCapture;
    private readonly WhisperCliTranscriptionService _transcription;
    private readonly ClipboardPasteService _clipboardPaste;
    private readonly SettingsStore _settingsStore;
    private string? _currentAudioPath;
    private CancellationTokenSource? _runCancellation;

    public RecorderController(
        MiniRecorderViewModel viewModel,
        MiniRecorderWindow window,
        AudioCaptureService audioCapture,
        WhisperCliTranscriptionService transcription,
        ClipboardPasteService clipboardPaste,
        SettingsStore settingsStore)
    {
        _viewModel = viewModel;
        _window = window;
        _audioCapture = audioCapture;
        _transcription = transcription;
        _clipboardPaste = clipboardPaste;
        _settingsStore = settingsStore;
        _audioCapture.AudioLevelChanged += level => _viewModel.AudioLevel = level;
    }

    public async Task ToggleAsync()
    {
        if (_viewModel.State == RecordingState.Recording)
        {
            await StopAndTranscribeAsync();
            return;
        }

        if (_viewModel.State is RecordingState.Transcribing or RecordingState.Busy)
        {
            return;
        }

        StartRecording();
    }

    public void StartRecording()
    {
        var settings = _settingsStore.Load();
        var validationError = WhisperCliTranscriptionService.ValidatePaths(settings.WhisperCliPath, settings.ModelPath);
        if (validationError is not null)
        {
            _viewModel.State = RecordingState.Error;
            return;
        }

        _runCancellation = new CancellationTokenSource();
        var recordingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Recordings");
        _currentAudioPath = _audioCapture.Start(recordingsRoot);
        _viewModel.State = RecordingState.Recording;
        _window.ShowBottomCenter();
    }

    public async Task StopAndTranscribeAsync()
    {
        if (_currentAudioPath is null) return;

        var settings = _settingsStore.Load();
        var cancellationToken = _runCancellation?.Token ?? CancellationToken.None;
        _audioCapture.Stop();
        _viewModel.State = RecordingState.Transcribing;

        try
        {
            var transcript = await _transcription.TranscribeAsync(
                settings.WhisperCliPath,
                settings.ModelPath,
                _currentAudioPath,
                cancellationToken);

            if (settings.AppendTrailingSpace)
            {
                transcript += " ";
            }

            await _clipboardPaste.PasteAsync(
                transcript,
                settings.RestoreClipboardAfterPaste,
                settings.ClipboardRestoreDelayMs,
                cancellationToken);

            _window.Hide();
            _viewModel.State = RecordingState.Idle;
        }
        catch
        {
            _viewModel.State = RecordingState.Error;
        }
        finally
        {
            if (!settings.KeepRecordingsForDiagnostics)
            {
                TryDelete(_currentAudioPath);
            }
            _currentAudioPath = null;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { }
    }
}
```

- [ ] **Step 4: Defer full hotkey service if needed**

If `RegisterHotKey` integration takes longer than expected, wire a temporary keyboard gesture in `MainWindow` for `Ctrl+Alt+Space` and document it in the review. The production follow-up must replace it with `HotkeyService` before calling the MVP complete.

- [ ] **Step 5: Build and manually smoke test**

Run:

```powershell
dotnet build
dotnet run --project src/LafazFlow.Windows/LafazFlow.Windows.csproj
```

Expected: with valid settings paths, the app can record, transcribe locally, and paste text. With invalid settings paths, no recording starts.

- [ ] **Step 6: Commit workflow**

Run:

```powershell
git add src/LafazFlow.Windows/Services src/LafazFlow.Windows/MainWindow.xaml.cs
git commit -m "Wire offline dictation workflow"
```

## Task 6: Final Verification For MVP

**Files:**
- Modify: `tasks/todo.md`
- Modify: `README.md`

- [ ] **Step 1: Run automated checks**

Run:

```powershell
dotnet build
dotnet test
```

Expected: both pass.

- [ ] **Step 2: Run manual privacy check**

Run:

```powershell
rg -n -i "(api[_-]?key|secret|token|password|passwd|private[_-]?key|client[_-]?secret|connectionstring|bearer|gho_|github_pat|sk-[A-Za-z0-9])" .
```

Expected: no credentials. Documentation may contain plain words such as "secret" only when describing the scan.

- [ ] **Step 3: Run manual app test**

Open Notepad, run the app, trigger recording, speak one short sentence, stop recording, and confirm the transcript pastes into Notepad.

- [ ] **Step 4: Document results**

Update `tasks/todo.md` review section with:

```markdown
## Review
- `dotnet build`: pass
- `dotnet test`: pass
- Secret scan: pass, no credentials found
- Manual Notepad dictation test: pass
- Default flow remains local/offline
```

- [ ] **Step 5: Commit verification notes**

Run:

```powershell
git add README.md tasks/todo.md
git commit -m "Document MVP verification"
git push
```

## Execution Choice

Plan complete. The recommended execution route is inline execution for Task 1 and Task 2, then a review checkpoint before audio/hotkey work. The repo instruction says subagents are only used when the owner explicitly requests them, so do not dispatch subagents unless the owner asks for parallel agent work.
