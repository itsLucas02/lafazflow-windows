using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LafazFlow.Windows.Tests;

public sealed class ArtifactManifestTests
{
    private const string WorkerRevision = "968eebe77225d25e57a3f981da7c696310f0e881";

    [Fact]
    public async Task LocalCudaPackagingWritesLocalCudaProvenanceWithoutPrivatePaths()
    {
        var root = CreateTempPackage(out var appPath, out var workerPath, out var cliPath);
        var outputPath = Path.Combine(root, "manifest.json");

        await RunManifestScriptAsync(
            appPath,
            workerPath,
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: WorkerRevision,
            cliReleaseIdentity: "",
            outputPath);

        var json = await File.ReadAllTextAsync(outputPath);
        using var document = JsonDocument.Parse(json);
        var cli = document.RootElement.GetProperty("cli");
        var worker = document.RootElement.GetProperty("worker");

        Assert.Equal("LocalCuda", cli.GetProperty("source").GetString());
        Assert.Equal(WorkerRevision, cli.GetProperty("revision").GetString());
        Assert.Equal(JsonValueKind.Null, cli.GetProperty("release_identity").ValueKind);
        Assert.Equal(Sha256("CLI-CONTENT"), cli.GetProperty("sha256").GetString());
        Assert.Equal(WorkerRevision, worker.GetProperty("revision").GetString());
        Assert.Equal(Sha256("WORKER-CONTENT"), worker.GetProperty("sha256").GetString());
        Assert.Equal(Sha256("APP-CONTENT"), document.RootElement.GetProperty("app").GetProperty("sha256").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("schema_version").GetInt32());
        Assert.DoesNotContain(root, json);
        Assert.DoesNotContain("C:\\", json);
    }

    [Fact]
    public async Task OfficialCpuPackagingWritesReleaseIdentityWithoutPrivatePaths()
    {
        var root = CreateTempPackage(out var appPath, out var workerPath, out var cliPath);
        var outputPath = Path.Combine(root, "manifest.json");
        const string releaseIdentity = "v1.7.4 https://github.com/ggml-org/whisper.cpp/releases/download/v1.7.4/whisper-bin-x64.zip";

        await RunManifestScriptAsync(
            appPath,
            workerPath,
            cliPath,
            cliSource: "OfficialCpu",
            cliRevision: "",
            cliReleaseIdentity: releaseIdentity,
            outputPath);

        var json = await File.ReadAllTextAsync(outputPath);
        using var document = JsonDocument.Parse(json);
        var cli = document.RootElement.GetProperty("cli");

        Assert.Equal("OfficialCpu", cli.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, cli.GetProperty("revision").ValueKind);
        Assert.Equal(releaseIdentity, cli.GetProperty("release_identity").GetString());
        Assert.Equal(Sha256("CLI-CONTENT"), cli.GetProperty("sha256").GetString());
        Assert.DoesNotContain(root, json);
        Assert.DoesNotContain("C:\\", json);
    }

    [Fact]
    public void ThirdPartyNoticesNoLongerContradictTheLocalCudaPackage()
    {
        var repoRoot = FindRepoRoot();
        var notices = File.ReadAllText(Path.Combine(repoRoot, "THIRD_PARTY_NOTICES.md"));

        Assert.DoesNotContain("not redistributed", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Local CUDA CLI", notices);
        Assert.Contains(WorkerRevision, notices);
        Assert.Contains("LafazFlow-artifact-manifest.json", notices);
        Assert.Contains("Official CPU CLI", notices);
    }

    private static string CreateTempPackage(
        out string appPath,
        out string workerPath,
        out string cliPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "lafazflow-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        appPath = Path.Combine(root, "LafazFlow.Windows.exe");
        workerPath = Path.Combine(root, "lafazflow-whisper-worker.exe");
        cliPath = Path.Combine(root, "whisper-cli.exe");
        File.WriteAllText(appPath, "APP-CONTENT");
        File.WriteAllText(workerPath, "WORKER-CONTENT");
        File.WriteAllText(cliPath, "CLI-CONTENT");
        return root;
    }

    private static async Task RunManifestScriptAsync(
        string appPath,
        string workerPath,
        string cliPath,
        string cliSource,
        string cliRevision,
        string cliReleaseIdentity,
        string outputPath)
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "New-LafazFlowArtifactManifest.ps1");
        var arguments =
            $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
            $"-AppPath \"{appPath}\" -WorkerPath \"{workerPath}\" -CliPath \"{cliPath}\" " +
            $"-CliSource {cliSource} -CliRevision \"{cliRevision}\" " +
            $"-CliReleaseIdentity \"{cliReleaseIdentity}\" -Version 1.0.0 " +
            $"-OutputPath \"{outputPath}\"";
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe"),
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(
            process.ExitCode == 0,
            $"Manifest script failed (exit {process.ExitCode}). Out: {standardOutput} Err: {standardError}");
    }

    private static string Sha256(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LafazFlow.Windows.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the LafazFlow repository root.");
    }
}
