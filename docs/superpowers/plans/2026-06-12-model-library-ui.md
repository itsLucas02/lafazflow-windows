# Model Library UI Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

Goal: Replace the raw model-path settings experience with a polished offline model library that shows model choices, speed, accuracy, size, install state, download progress, and clear actions.

Architecture: Add a model catalog layer that describes supported local models separately from user settings. Add a model manager service that detects installed files, downloads missing models into C:\Models\whisper, and exposes UI-safe card state. Redesign Settings > Models around model cards while keeping executable/model path controls in an Advanced section for power users.

Tech Stack: WPF/XAML, .NET 9, existing SettingsViewModel, SettingsStore, WhisperCliTranscriptionService, xUnit tests.

---

## Scope

This plan only changes the Settings > Models experience and related model state/download plumbing. It does not change the mini recorder shell, dictation animations, audio cues, or transcription formatting.

## Files

- Create: src/LafazFlow.Windows/Core/LocalModelDefinition.cs
  - Static metadata for known offline models: id, display name, filename, size, language, speed score, accuracy score, RAM estimate, description, download URL, and recommended order.
- Create: src/LafazFlow.Windows/Services/LocalModelCatalog.cs
  - Returns supported model definitions in a stable order.
- Create: src/LafazFlow.Windows/Services/LocalModelLibraryService.cs
  - Detects install state, resolves model paths, downloads model files with progress, deletes installed models, and imports local .bin files.
- Create: src/LafazFlow.Windows/UI/ModelCardViewModel.cs
  - UI state for one model card: status, progress, active state, install path, and actions.
- Modify: src/LafazFlow.Windows/UI/SettingsViewModel.cs
  - Expose ModelCards, selected/active model state, download/delete/import actions, and advanced path visibility.
- Modify: src/LafazFlow.Windows/UI/SettingsWindow.xaml
  - Replace the top Models page with card-based model selection and move raw paths into an Advanced Runtime Paths section.
- Modify: src/LafazFlow.Windows/UI/SettingsWindow.xaml.cs
  - Wire browse/import/open-folder events to the view model.
- Modify: src/LafazFlow.Windows/Core/AppSettings.cs
  - Add SelectedLocalModelId if needed while preserving existing ModelPath, QualityModelPath, TranscriptionProfile, and WhisperBackend.
- Modify: src/LafazFlow.Windows/Services/SettingsStore.cs
  - Migrate existing path-based settings into the closest model-card selection.
- Test: tests/LafazFlow.Windows.Tests/LocalModelCatalogTests.cs
- Test: tests/LafazFlow.Windows.Tests/LocalModelLibraryServiceTests.cs
- Test: tests/LafazFlow.Windows.Tests/SettingsViewModelTests.cs
- Test: tests/LafazFlow.Windows.Tests/SettingsWindowXamlTests.cs

---

## Task 1: Model Catalog

- [ ] Create LocalModelDefinition with Id, DisplayName, FileName, SizeLabel, LanguageLabel, SpeedScore, AccuracyScore, RamLabel, Description, DownloadUrl, and IsRecommended.
- [ ] Create LocalModelCatalog with these initial local models:
  - ggml-base.en: Fast, English, 142 MB, high speed, medium accuracy.
  - ggml-large-v3-turbo-q5_0: Balanced Quality, multilingual, 547 MB, medium speed, high accuracy.
  - ggml-small.en: Better English, 466 MB, medium speed, better accuracy.
  - ggml-medium.en: High Accuracy English, 1.5 GB, slower, high accuracy.
- [ ] Add tests proving catalog order, required fields, valid URLs, unique IDs, and unique filenames.
- [ ] Run: dotnet test --filter LocalModelCatalogTests.

## Task 2: Install State And File Management

- [ ] Create LocalModelLibraryService.
- [ ] Resolve default model root as C:\Models\whisper.
- [ ] Detect installed catalog models by expected .bin filename.
- [ ] Detect imported models for .bin files that do not match catalog definitions.
- [ ] Delete only files inside the configured model directory.
- [ ] Import local .bin files by copying them into the model directory.
- [ ] Add tests for installed, missing, imported, delete safety, and import behavior.
- [ ] Run: dotnet test --filter LocalModelLibraryServiceTests.

## Task 3: Download Manager

- [ ] Add async model download with progress from 0.0 to 1.0.
- [ ] Download to a temporary .download file first.
- [ ] Move to final .bin only after successful completion.
- [ ] Delete partial files on failure or cancellation.
- [ ] Add a testable HTTP abstraction so tests do not hit the network.
- [ ] Add tests for success, progress, cancellation cleanup, and failed download cleanup.
- [ ] Run: dotnet test --filter LocalModelLibraryServiceTests.

## Task 4: Settings View Model Cards

- [ ] Create ModelCardViewModel.
- [ ] Expose card metadata, IsInstalled, IsActive, IsDownloading, DownloadProgressPercent, PrimaryActionLabel, CanDownload, CanUse, and CanDelete.
- [ ] Implement Download, Use Model, Delete, Open Folder, and Import Local Model actions.
- [ ] Map fast model cards to TranscriptionProfile.Fast and ModelPath.
- [ ] Map quality model cards to TranscriptionProfile.Quality and QualityModelPath.
- [ ] Preserve CUDA/backend setting when selecting a quality model.
- [ ] Add tests for card labels, active state, use-model mapping, and progress state.
- [ ] Run: dotnet test --filter SettingsViewModelTests.

## Task 5: Models Page Redesign

- [ ] Replace the top Models page with a current model summary.
- [ ] Add Recommended model cards and Local/imported model cards.
- [ ] Show speed and accuracy dot meters.
- [ ] Show status pills: Installed, Active, Missing, Downloading.
- [ ] Show primary actions: Download, Use Model, Downloading.
- [ ] Show secondary actions: Delete and Open Folder.
- [ ] Move raw path controls into Advanced Runtime Paths.
- [ ] Add XAML tests for model-card bindings, speed/accuracy labels, download/use actions, and Advanced Runtime Paths.
- [ ] Run: dotnet test --filter SettingsWindowXamlTests.

## Task 6: Version, Verification, Publish

- [ ] Bump version from 0.10.23 to 0.11.0.
- [ ] Run: dotnet test.
- [ ] Run: dotnet build.
- [ ] Run: git diff --check.
- [ ] Publish stable-single and stable-cuda-quality artifacts.
- [ ] Relaunch stable-single and confirm Settings title shows v0.11.0.
- [ ] Confirm installed ggml-base.en.bin is detected.
- [ ] Confirm installed ggml-large-v3-turbo-q5_0.bin is detected if present.
- [ ] Confirm Download buttons appear only for missing catalog models.
- [ ] Run public repo safety scans for forbidden public references, credentials, model binaries, logs, and local settings.
- [ ] Commit and push with message: feat: add local model library.

---

## Release Split

Build this in two slices.

v0.11.0 Model Library MVP:
- catalog
- install detection
- model cards
- use model
- import/open/delete
- advanced paths retained

v0.11.1 Download Polish:
- in-app download progress
- cancellation cleanup
- download failure UX
- optional checksum metadata if we decide to pin checksums

This split is intentional. It makes the page useful immediately without making the first slice depend on every network-download edge case.

## Self-Review

- Covers model metadata, speed, accuracy, size, status, download, use, delete, import, and advanced paths.
- Keeps transcription local/offline by default.
- Does not touch the mini recorder shell.
- Avoids committing models or user-local files.
- Uses tests before UI claims are considered complete.
