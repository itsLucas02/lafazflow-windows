using System.IO;
using LafazFlow.Windows.Core;
using System.Net.Http;

namespace LafazFlow.Windows.Services;

public interface IModelDownloadClient
{
    Task DownloadAsync(
        Uri source,
        string destinationPath,
        IProgress<double> progress,
        CancellationToken cancellationToken);
}

public sealed class HttpModelDownloadClient : IModelDownloadClient
{
    private readonly HttpClient _httpClient;

    public HttpModelDownloadClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadAsync(
        Uri source,
        string destinationPath,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            source,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destinationPath);
        var buffer = new byte[128 * 1024];
        long written = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;
            if (totalBytes is > 0)
            {
                progress.Report(Math.Clamp((double)written / totalBytes.Value, 0, 1));
            }
        }

        progress.Report(1);
    }
}

public sealed class LocalModelLibraryService
{
    private readonly IModelDownloadClient _downloadClient;

    public LocalModelLibraryService(
        string modelDirectory = @"C:\Models\whisper",
        IModelDownloadClient? downloadClient = null)
    {
        ModelDirectory = Path.GetFullPath(modelDirectory);
        _downloadClient = downloadClient ?? new HttpModelDownloadClient();
    }

    public string ModelDirectory { get; }

    public IReadOnlyList<LocalModelDefinition> Catalog => LocalModelCatalog.Models;

    public string GetModelPath(LocalModelDefinition model)
    {
        return Path.Combine(ModelDirectory, model.FileName);
    }

    public bool IsInstalled(LocalModelDefinition model)
    {
        return File.Exists(GetModelPath(model));
    }

    public IReadOnlyList<LocalModelDefinition> GetImportedModels()
    {
        if (!Directory.Exists(ModelDirectory))
        {
            return [];
        }

        var knownFiles = Catalog
            .Select(model => model.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Directory
            .EnumerateFiles(ModelDirectory, "*.bin", SearchOption.TopDirectoryOnly)
            .Where(path => !knownFiles.Contains(Path.GetFileName(path)))
            .Where(path => IsLikelyTranscriptionModelFile(Path.GetFileName(path)))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var fileName = Path.GetFileName(path);
                var name = Path.GetFileNameWithoutExtension(path);
                return new LocalModelDefinition(
                    $"imported:{name}",
                    name,
                    fileName,
                    FormatBytes(new FileInfo(path).Length),
                    "Local",
                    0.5,
                    0.5,
                    "Unknown",
                    "Imported local Whisper model.",
                    "",
                    false,
                    false,
                    true);
            })
            .ToList();
    }

    public async Task<string> DownloadAsync(
        LocalModelDefinition model,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (model.IsImported || string.IsNullOrWhiteSpace(model.DownloadUrl))
        {
            throw new InvalidOperationException("Imported models cannot be downloaded.");
        }

        Directory.CreateDirectory(ModelDirectory);
        var finalPath = GetModelPath(model);
        var tempPath = finalPath + ".download";
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            await _downloadClient.DownloadAsync(new Uri(model.DownloadUrl), tempPath, progress, cancellationToken);
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath);
            progress.Report(1);
            return finalPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public string ImportModel(string sourcePath)
    {
        if (!string.Equals(Path.GetExtension(sourcePath), ".bin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only .bin model files can be imported.");
        }

        Directory.CreateDirectory(ModelDirectory);
        var destinationPath = Path.Combine(ModelDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destinationPath, true);
        return destinationPath;
    }

    public void DeleteModel(string modelPath)
    {
        var fullPath = Path.GetFullPath(modelPath);
        if (!IsInsideModelDirectory(fullPath))
        {
            throw new InvalidOperationException("Refusing to delete a model outside the model directory.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private bool IsInsideModelDirectory(string fullPath)
    {
        var root = ModelDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyTranscriptionModelFile(string fileName)
    {
        return !fileName.Contains("silero", StringComparison.OrdinalIgnoreCase)
            && !fileName.Contains("vad", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatBytes(long bytes)
    {
        var mb = bytes / 1024d / 1024d;
        return mb < 1024
            ? $"{mb:0.#} MB"
            : $"{mb / 1024d:0.#} GB";
    }
}
