# Regression Pack Command Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a first-class local regression pack command for recurring LafazFlow dictation quality checks.

**Architecture:** Extend the existing transcription benchmark instead of creating a second tool. A named pack such as `daily` resolves to a private LocalAppData folder containing `.wav` files paired with exact `.txt` ground truth, and the current benchmark runner/report writer remains the authority for metrics.

**Tech Stack:** .NET 9 console tool, existing LafazFlow transcription services, xUnit tests.

---

### Task 1: Pack Path Resolution

**Files:**
- Create: `tools/LafazFlow.TranscriptionBench/RegressionPackResolver.cs`
- Modify: `tools/LafazFlow.TranscriptionBench/Program.cs`
- Test: `tests/LafazFlow.Windows.Tests/TranscriptionBenchTests.cs`

- [ ] **Step 1: Add tests**

Add tests proving `--pack daily` resolves to a LocalAppData-style pack directory, `--packs-root` overrides the root, and invalid names such as `..\secret` are rejected.

- [ ] **Step 2: Implement resolver**

Create a resolver that allows only letters, numbers, `.`, `_`, and `-` in pack names, then combines the pack root and pack name.

- [ ] **Step 3: Wire CLI options**

Move `BenchOptions` into a public testable file and add `PackName` plus `PacksRoot` parsing. If `--pack` is present, use the resolved pack directory as `RecordingsDirectory`.

### Task 2: Better Regression Reporting Defaults

**Files:**
- Modify: `tools/LafazFlow.TranscriptionBench/BenchmarkRunner.cs`
- Modify: `tools/LafazFlow.TranscriptionBench/Program.cs`
- Test: `tests/LafazFlow.Windows.Tests/TranscriptionBenchTests.cs`

- [ ] **Step 1: Add `Stripe` to key terms**

Ensure benchmark key-term summaries include `Stripe` alongside `Supabase`, `shadcn`, `Context7`, `Luqman`, `MediBrave`, `stale`, `wrapper`, and `align`.

- [ ] **Step 2: Improve empty-pack guidance**

When a pack folder has no `.wav` plus `.txt` pairs, print a concrete message showing where to put files.

### Task 3: Verify And Document

**Files:**
- Modify: `tasks/todo.md`

- [ ] **Step 1: Run focused tests**

Run `dotnet test tests\LafazFlow.Windows.Tests\LafazFlow.Windows.Tests.csproj --filter FullyQualifiedName~TranscriptionBenchTests`.

- [ ] **Step 2: Run full verification**

Run `dotnet test`, `dotnet build`, `git diff --check`, trademark scan, and public-readiness scan.

- [ ] **Step 3: Smoke the command**

Create a private local `daily` pack from the latest curated clips, then run:

```powershell
dotnet run --project tools\LafazFlow.TranscriptionBench\LafazFlow.TranscriptionBench.csproj -- --pack daily --take 4 --configs current-settings
```

Expected: command writes Markdown/CSV reports under `%LOCALAPPDATA%\LafazFlow\Benchmarks`.
