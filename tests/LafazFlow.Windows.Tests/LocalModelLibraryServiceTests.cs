using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class LocalModelLibraryServiceTests
{
    [Fact]
    public void DetectsInstalledAndMissingCatalogModels()
    {
        var root = CreateRoot();
        var service = new LocalModelLibraryService(root);
        var baseModel = LocalModelCatalog.Models.First(model => model.Id == "ggml-base.en");
        File.WriteAllText(Path.Combine(root, baseModel.FileName), "model");

        Assert.True(service.IsInstalled(baseModel));
        Assert.False(service.IsInstalled(LocalModelCatalog.Models.First(model => model.Id == "ggml-small.en")));
    }

    [Fact]
    public void DetectsImportedModelsNotInCatalog()
    {
        var root = CreateRoot();
        File.WriteAllText(Path.Combine(root, "custom-medical.bin"), "model");
        var service = new LocalModelLibraryService(root);

        var imported = Assert.Single(service.GetImportedModels());

        Assert.Equal("imported:custom-medical", imported.Id);
        Assert.Equal("custom-medical", imported.DisplayName);
        Assert.True(imported.IsImported);
    }

    [Fact]
    public void DoesNotListAuxiliaryVadModelsAsImportedTranscriptionModels()
    {
        var root = CreateRoot();
        File.WriteAllText(Path.Combine(root, "ggml-silero-v5.1.2.bin"), "vad");
        File.WriteAllText(Path.Combine(root, "custom-vad-helper.bin"), "vad");
        File.WriteAllText(Path.Combine(root, "custom-medical.bin"), "model");
        var service = new LocalModelLibraryService(root);

        var imported = service.GetImportedModels();

        var model = Assert.Single(imported);
        Assert.Equal("custom-medical", model.DisplayName);
    }

    [Fact]
    public void ImportCopiesOnlyBinFilesIntoModelDirectory()
    {
        var root = CreateRoot();
        var source = Path.Combine(CreateRoot(), "custom.bin");
        File.WriteAllText(source, "model");
        var service = new LocalModelLibraryService(root);

        var importedPath = service.ImportModel(source);

        Assert.Equal(Path.Combine(root, "custom.bin"), importedPath);
        Assert.Equal("model", File.ReadAllText(importedPath));
    }

    [Fact]
    public void ImportRejectsNonBinFiles()
    {
        var root = CreateRoot();
        var source = Path.Combine(CreateRoot(), "custom.txt");
        File.WriteAllText(source, "model");
        var service = new LocalModelLibraryService(root);

        var error = Assert.Throws<InvalidOperationException>(() => service.ImportModel(source));

        Assert.Equal("Only .bin model files can be imported.", error.Message);
    }

    [Fact]
    public void DeleteRefusesPathsOutsideModelDirectory()
    {
        var root = CreateRoot();
        var outside = Path.Combine(CreateRoot(), "outside.bin");
        File.WriteAllText(outside, "model");
        var service = new LocalModelLibraryService(root);

        var error = Assert.Throws<InvalidOperationException>(() => service.DeleteModel(outside));

        Assert.Contains("outside the model directory", error.Message);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task DownloadMovesCompletedFileToFinalPath()
    {
        var root = CreateRoot();
        var client = new FakeDownloadClient("model");
        var service = new LocalModelLibraryService(root, client);
        var model = LocalModelCatalog.Models.First();
        var values = new List<double>();

        var path = await service.DownloadAsync(model, new Progress<double>(values.Add), CancellationToken.None);

        Assert.Equal(Path.Combine(root, model.FileName), path);
        Assert.Equal("model", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".download"));
        Assert.Contains(1, values);
    }

    [Fact]
    public async Task DownloadDeletesPartialFileOnFailure()
    {
        var root = CreateRoot();
        var client = new FakeDownloadClient("partial", throwAfterWrite: true);
        var service = new LocalModelLibraryService(root, client);
        var model = LocalModelCatalog.Models.First();

        await Assert.ThrowsAsync<IOException>(() =>
            service.DownloadAsync(model, new Progress<double>(), CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(root, model.FileName)));
        Assert.False(File.Exists(Path.Combine(root, model.FileName) + ".download"));
    }

    private static string CreateRoot()
    {
        return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
    }

    private sealed class FakeDownloadClient : IModelDownloadClient
    {
        private readonly string _content;
        private readonly bool _throwAfterWrite;

        public FakeDownloadClient(string content, bool throwAfterWrite = false)
        {
            _content = content;
            _throwAfterWrite = throwAfterWrite;
        }

        public async Task DownloadAsync(
            Uri source,
            string destinationPath,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            progress.Report(0.5);
            await File.WriteAllTextAsync(destinationPath, _content, cancellationToken);
            if (_throwAfterWrite)
            {
                throw new IOException("download failed");
            }

            progress.Report(1);
        }
    }
}
