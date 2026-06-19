# Hotkey Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add privacy-safe local diagnostics that make double Shift responsiveness bugs provable from logs and visible in Settings.

**Architecture:** Keep the existing latency diagnostics intact and add a separate hotkey event stream in the same local log file. Hotkey events record only timing, state, event type, and reason fields; they never record audio, transcript text, clipboard content, or key text beyond the fixed double Shift gesture.

**Tech Stack:** C#/.NET 9 WPF, existing `lafazflow.log`, existing Settings diagnostics tab, xUnit source and view-model tests.

---

## File Structure

- Create `src/LafazFlow.Windows/Services/HotkeyDiagnosticEvent.cs`
  - Immutable parsed event row used by Settings and tests.
- Create `src/LafazFlow.Windows/Services/HotkeyDiagnosticLogStore.cs`
  - Reads and clears `HOTKEY` lines from `%LocalAppData%\LafazFlow\Logs\lafazflow.log`.
- Create `src/LafazFlow.Windows/Services/IHotkeyDiagnostics.cs`
  - Tiny logging interface used by the keyboard hook, main window, controller, and preview service.
- Create `src/LafazFlow.Windows/Services/FileHotkeyDiagnostics.cs`
  - Writes privacy-safe `HOTKEY` lines to the same local log folder.
- Modify `src/LafazFlow.Windows/Services/DoubleShiftHotkeyService.cs`
  - Log detector accepted/rejected events and stale recovery without recording user text.
- Modify `src/LafazFlow.Windows/MainWindow.xaml.cs`
  - Log dispatcher receipt and dispatch latency before `RecorderController.ToggleAsync`.
- Modify `src/LafazFlow.Windows/Services/RecorderController.cs`
  - Log toggle state decisions: start, stop, ignored, validation error, preview start queued.
- Modify `src/LafazFlow.Windows/Services/RollingWhisperLiveTranscriptPreviewService.cs`
  - Log preview lifecycle: start requested, started, stop requested, stopped, cancelled, failed.
- Modify `src/LafazFlow.Windows/UI/SettingsViewModel.cs`
  - Load recent hotkey events, expose summary text, and clear hotkey diagnostics.
- Modify `src/LafazFlow.Windows/UI/SettingsWindow.xaml`
  - Add a separate “Recent Hotkey Events” card under Diagnostics.
- Modify `src/LafazFlow.Windows/UI/SettingsWindow.xaml.cs`
  - Add refresh/clear click handlers for hotkey diagnostics.
- Modify tests in `tests/LafazFlow.Windows.Tests`
  - Add parser, source, view-model, and XAML coverage.

## Log Format

Use one line per event:

```text
[2026-06-19T18:42:10.1234567+08:00] HOTKEY event=detected gesture=DoubleShift accepted=true state=na dispatch_ms=na reason=second_shift target=na
```

Allowed fields:

- `event`: `detected`, `rejected`, `dispatched`, `toggle_start`, `toggle_stop`, `toggle_ignored`, `preview_start`, `preview_started`, `preview_stop`, `preview_stopped`, `preview_cancelled`, `preview_failed`
- `gesture`: always `DoubleShift` for now
- `accepted`: `true`, `false`, or `na`
- `state`: `Idle`, `Recording`, `Starting`, `Transcribing`, `Enhancing`, `Busy`, `Error`, or `na`
- `dispatch_ms`: UI dispatcher delay from hook timestamp to handler start, or `na`
- `reason`: compact safe token like `second_shift`, `repeat`, `already_down`, `busy_state`, `validation_error`, `operation_cancelled`
- `target`: foreground process name only, or `na`

Explicitly forbidden fields:

- transcript text
- clipboard contents
- audio paths
- model paths
- typed characters
- full window titles
- user profile paths

## Task 1: Hotkey Event Parser And Log Store

**Files:**
- Create: `src/LafazFlow.Windows/Services/HotkeyDiagnosticEvent.cs`
- Create: `src/LafazFlow.Windows/Services/HotkeyDiagnosticLogStore.cs`
- Test: `tests/LafazFlow.Windows.Tests/HotkeyDiagnosticLogStoreTests.cs`

- [ ] **Step 1: Write parser tests**

Add tests proving a `HOTKEY` line parses and non-hotkey lines are ignored:

```csharp
[Fact]
public void ParseLineReadsHotkeyFields()
{
    var row = HotkeyDiagnosticLogStore.ParseLine(
        "[2026-06-19T18:42:10.1234567+08:00] HOTKEY event=dispatched gesture=DoubleShift accepted=true state=Recording dispatch_ms=37 reason=second_shift target=Cursor");

    Assert.NotNull(row);
    Assert.Equal("dispatched", row.Event);
    Assert.Equal("DoubleShift", row.Gesture);
    Assert.Equal("true", row.Accepted);
    Assert.Equal("Recording", row.State);
    Assert.Equal("37", row.DispatchMs);
    Assert.Equal("second_shift", row.Reason);
    Assert.Equal("Cursor", row.Target);
}

[Fact]
public void ParseLineIgnoresLatencyRows()
{
    Assert.Null(HotkeyDiagnosticLogStore.ParseLine(
        "[2026-06-19T18:42:10.1234567+08:00] LATENCY id=abc status=completed"));
}
```

- [ ] **Step 2: Run parser tests red**

Run:

```powershell
dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter HotkeyDiagnosticLogStoreTests
```

Expected: fail because `HotkeyDiagnosticLogStore` does not exist.

- [ ] **Step 3: Implement parser and store**

Create `HotkeyDiagnosticEvent`:

```csharp
namespace LafazFlow.Windows.Services;

public sealed record HotkeyDiagnosticEvent(
    DateTimeOffset Timestamp,
    string Event,
    string Gesture,
    string Accepted,
    string State,
    string DispatchMs,
    string Reason,
    string Target);
```

Create `HotkeyDiagnosticLogStore` following `LatencyDiagnosticLogStore` patterns:

```csharp
public sealed partial class HotkeyDiagnosticLogStore
{
    public const int DefaultLimit = 20;
    private readonly string _logPath;

    public HotkeyDiagnosticLogStore(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Logs",
            "lafazflow.log");
    }

    public IReadOnlyList<HotkeyDiagnosticEvent> LoadRecent(int limit = DefaultLimit)
    {
        if (!File.Exists(_logPath))
        {
            return [];
        }

        return ReadAllLinesShared(_logPath)
            .Select(ParseLine)
            .Where(row => row is not null)
            .Cast<HotkeyDiagnosticEvent>()
            .OrderByDescending(row => row.Timestamp)
            .Take(Math.Max(0, limit))
            .ToArray();
    }

    public int ClearHotkeyLines()
    {
        if (!File.Exists(_logPath))
        {
            return 0;
        }

        var lines = ReadAllLinesShared(_logPath);
        var retained = lines.Where(line => !IsHotkeyLine(line)).ToArray();
        File.WriteAllLines(_logPath, retained);
        return lines.Length - retained.Length;
    }
}
```

- [ ] **Step 4: Run parser tests green**

Run:

```powershell
dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter HotkeyDiagnosticLogStoreTests
```

Expected: pass.

## Task 2: Privacy-Safe Hotkey Logger

**Files:**
- Create: `src/LafazFlow.Windows/Services/IHotkeyDiagnostics.cs`
- Create: `src/LafazFlow.Windows/Services/FileHotkeyDiagnostics.cs`
- Test: `tests/LafazFlow.Windows.Tests/FileHotkeyDiagnosticsTests.cs`

- [ ] **Step 1: Write logger tests**

Add tests proving fields are compact and unsafe values are sanitized:

```csharp
[Fact]
public void LogWritesPrivacySafeHotkeyLine()
{
    var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "lafazflow.log");
    var diagnostics = new FileHotkeyDiagnostics(logPath);

    diagnostics.Log(new HotkeyDiagnosticWrite(
        Event: "toggle_start",
        Gesture: "DoubleShift",
        Accepted: "true",
        State: "Recording",
        DispatchMs: "12",
        Reason: "second shift with spaces",
        Target: "Cursor"));

    var line = File.ReadAllText(logPath);
    Assert.Contains("HOTKEY event=toggle_start", line);
    Assert.Contains("reason=second_shift_with_spaces", line);
    Assert.DoesNotContain(Environment.UserName, line);
}
```

- [ ] **Step 2: Implement interface and writer**

Define the write DTO and interface:

```csharp
public sealed record HotkeyDiagnosticWrite(
    string Event,
    string Gesture = "DoubleShift",
    string Accepted = "na",
    string State = "na",
    string DispatchMs = "na",
    string Reason = "na",
    string Target = "na");

public interface IHotkeyDiagnostics
{
    void Log(HotkeyDiagnosticWrite entry);
}
```

Implement `FileHotkeyDiagnostics` with the same local folder as the existing log writer and sanitize every value to `[A-Za-z0-9_.-]`, replacing other characters with `_`.

- [ ] **Step 3: Run logger tests**

Run:

```powershell
dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter FileHotkeyDiagnosticsTests
```

Expected: pass.

## Task 3: Hook And Controller Event Logging

**Files:**
- Modify: `src/LafazFlow.Windows/Services/DoubleShiftHotkeyService.cs`
- Modify: `src/LafazFlow.Windows/MainWindow.xaml.cs`
- Modify: `src/LafazFlow.Windows/Services/RecorderController.cs`
- Test: `tests/LafazFlow.Windows.Tests/HotkeyDiagnosticsSourceTests.cs`

- [ ] **Step 1: Add source-level regression tests**

Add tests that assert the hook, main window, and controller call `IHotkeyDiagnostics.Log`:

```csharp
[Fact]
public void DoubleShiftHotkeyServiceLogsAcceptedAndRejectedEvents()
{
    var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LafazFlow.Windows", "Services", "DoubleShiftHotkeyService.cs"));

    Assert.Contains("IHotkeyDiagnostics", source);
    Assert.Contains("event=detected", source);
    Assert.Contains("event=rejected", source);
}
```

- [ ] **Step 2: Inject diagnostics**

Use optional constructor parameters so existing tests and app startup stay simple:

```csharp
private readonly IHotkeyDiagnostics _hotkeyDiagnostics;

public DoubleShiftHotkeyService(IHotkeyDiagnostics? hotkeyDiagnostics = null)
{
    _hotkeyDiagnostics = hotkeyDiagnostics ?? new FileHotkeyDiagnostics();
    _proc = HookCallback;
}
```

Apply the same optional pattern to `RecorderController`.

- [ ] **Step 3: Log hook decisions**

In `DoubleShiftHotkeyService.HookCallback`:

- accepted second Shift: `event=detected accepted=true reason=second_shift`
- repeat Shift: `event=rejected accepted=false reason=repeat`
- already down: `event=rejected accepted=false reason=already_down`

If the current `DoubleShiftDetector.RegisterKeyDown` cannot expose rejection reasons, add `DoubleShiftDetectionResult` with:

```csharp
public sealed record DoubleShiftDetectionResult(bool Triggered, string Reason);
```

- [ ] **Step 4: Log dispatcher and recorder decisions**

In `MainWindow.OnDoubleShiftPressed`, log:

```text
event=dispatched accepted=true dispatch_ms=<elapsed> reason=begin_invoke target=na
```

In `RecorderController.ToggleAsync`, log:

- `event=toggle_stop state=Recording`
- `event=toggle_start state=Idle`
- `event=toggle_ignored state=Transcribing reason=busy_state`

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter "HotkeyDiagnosticsSourceTests|DoubleShiftDetectorTests|RecorderControllerTests"
```

Expected: pass.

## Task 4: Preview Lifecycle Diagnostics

**Files:**
- Modify: `src/LafazFlow.Windows/Services/RollingWhisperLiveTranscriptPreviewService.cs`
- Modify: `src/LafazFlow.Windows/Services/RecorderController.cs`
- Test: `tests/LafazFlow.Windows.Tests/RollingWhisperLiveTranscriptPreviewServiceTests.cs`

- [ ] **Step 1: Add preview diagnostic injection**

Add optional `IHotkeyDiagnostics? hotkeyDiagnostics = null` to `RollingWhisperLiveTranscriptPreviewService`.

- [ ] **Step 2: Log preview lifecycle**

Log these safe events:

- `preview_start`
- `preview_started`
- `preview_stop`
- `preview_stopped`
- `preview_cancelled`
- `preview_failed`

Use reasons:

- `start_requested`
- `loop_started`
- `stop_requested`
- `stop_completed`
- `operation_cancelled`
- exception type name sanitized, such as `InvalidOperationException`

- [ ] **Step 3: Add preview tests**

Use a fake diagnostics sink and assert:

```csharp
Assert.Contains(events, entry => entry.Event == "preview_start");
Assert.Contains(events, entry => entry.Event == "preview_stopped");
```

- [ ] **Step 4: Run preview tests**

Run:

```powershell
dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter RollingWhisperLiveTranscriptPreviewServiceTests
```

Expected: pass.

## Task 5: Settings Diagnostics UI

**Files:**
- Modify: `src/LafazFlow.Windows/UI/SettingsViewModel.cs`
- Modify: `src/LafazFlow.Windows/UI/SettingsWindow.xaml`
- Modify: `src/LafazFlow.Windows/UI/SettingsWindow.xaml.cs`
- Test: `tests/LafazFlow.Windows.Tests/SettingsViewModelTests.cs`
- Test: `tests/LafazFlow.Windows.Tests/SettingsWindowXamlTests.cs`

- [ ] **Step 1: Add view-model tests**

Add tests proving Settings loads and clears hotkey rows:

```csharp
[Fact]
public void LoadPopulatesRecentHotkeyRows()
{
    var logPath = CreateLatencyLog("[2026-06-19T18:42:10.1234567+08:00] HOTKEY event=toggle_stop gesture=DoubleShift accepted=true state=Recording dispatch_ms=12 reason=second_shift target=Cursor");
    var viewModel = SettingsViewModel.Load(
        new SettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
        hotkeyDiagnostics: new HotkeyDiagnosticLogStore(logPath));

    Assert.Single(viewModel.RecentHotkeyRows);
    Assert.Equal("Showing latest 1 hotkey event.", viewModel.HotkeyDiagnosticsMessage);
}
```

- [ ] **Step 2: Add Settings properties**

Add:

```csharp
public ObservableCollection<HotkeyDiagnosticEvent> RecentHotkeyRows { get; } = [];
public string HotkeyDiagnosticsMessage { get; private set; } = "";
public string LatestHotkeySummary { get; private set; } = "";
```

Add:

```csharp
public void RefreshHotkeyDiagnostics()
public void ClearHotkeyDiagnostics()
```

- [ ] **Step 3: Add XAML tests**

Assert the diagnostics tab contains:

```csharp
Assert.Contains("ItemsSource=\"{Binding RecentHotkeyRows}\"", xaml);
Assert.Contains("Text=\"{Binding HotkeyDiagnosticsMessage}\"", xaml);
Assert.Contains("Click=\"RefreshHotkey_OnClick\"", xaml);
Assert.Contains("Click=\"ClearHotkey_OnClick\"", xaml);
```

- [ ] **Step 4: Add Settings card**

Under Diagnostics, add a card titled `Recent Hotkey Events` with columns:

- Event
- State
- Dispatch
- Reason
- Target

Do not use transcript/audio/clipboard columns.

- [ ] **Step 5: Run Settings tests**

Run:

```powershell
dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter "SettingsViewModelTests|SettingsWindowXamlTests"
```

Expected: pass.

## Task 6: Version, Verification, Publish, Commit

**Files:**
- Modify: `src/LafazFlow.Windows/LafazFlow.Windows.csproj`
- Modify: `tasks/todo.md`
- Modify: `tasks/lessons.md`

- [ ] **Step 1: Bump version**

Change:

```xml
<Version>0.11.4</Version>
```

to:

```xml
<Version>0.11.5</Version>
```

- [ ] **Step 2: Update lessons**

Add:

```markdown
## Keep hotkey diagnostics separate from latency summaries
- Pattern: Latency rows explain completed dictation timing, but hotkey bugs need accepted/rejected gesture events and dispatcher state.
- Rule: Log privacy-safe hotkey events as a separate local stream with compact state/reason fields and no transcript, clipboard, audio, or path data.
```

- [ ] **Step 3: Run verification**

Run sequentially:

```powershell
dotnet test
dotnet build
git diff --check
```

Expected:

- all tests pass
- build passes with `0 Warning(s)` and `0 Error(s)`
- diff check returns exit code `0`

- [ ] **Step 4: Publish and relaunch stable app**

Run:

```powershell
$repo = (Resolve-Path .).Path
Get-Process LafazFlow.Windows -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$repo*" } | Stop-Process -Force
dotnet publish src\LafazFlow.Windows\LafazFlow.Windows.csproj -c Release -r win-x64 --self-contained false -o artifacts\stable-single\LafazFlow.Windows
dotnet publish src\LafazFlow.Windows\LafazFlow.Windows.csproj -c Release -r win-x64 --self-contained false -o artifacts\stable-cuda-quality\LafazFlow.Windows
Start-Process artifacts\stable-single\LafazFlow.Windows\LafazFlow.Windows.exe -WindowStyle Hidden
```

Expected: stable-single app starts and reports file version `0.11.5.0`.

- [ ] **Step 5: Run public safety scans**

Run:

```powershell
rg -n "<forbidden-public-reference-pattern>" . --glob '!bin/**' --glob '!obj/**' --glob '!artifacts/**'
rg -n "(?i)(api[_-]?key|secret|token|password|bearer|sk-[a-z0-9]|ghp_[a-z0-9]|github_pat_|AKIA[0-9A-Z]{16})" . --glob '!bin/**' --glob '!obj/**' --glob '!artifacts/**' --glob '!*.dll' --glob '!*.exe' --glob '!*.pdb'
```

Expected: no forbidden public reference mentions; credential scan only reports known GPL/docs/code words.

- [ ] **Step 6: Commit and push**

Run:

```powershell
git status --short
git add src tests tasks docs
git commit -m "feat: add hotkey diagnostics"
git push
```

Expected: clean push to `main`.

## Self-Review

- Spec coverage: the plan covers log creation, parsing, Settings display, clearing, hook/controller/preview instrumentation, versioning, verification, publish, safety scan, commit, and push.
- Privacy coverage: the plan explicitly forbids audio, transcripts, clipboard contents, paths, typed characters, and window titles.
- Type consistency: the plan consistently uses `HotkeyDiagnosticEvent`, `HotkeyDiagnosticLogStore`, `IHotkeyDiagnostics`, `FileHotkeyDiagnostics`, and `HotkeyDiagnosticWrite`.
- Scope check: this is one subsystem, diagnostics, with no changes to transcription models or mini recorder visual behavior.
