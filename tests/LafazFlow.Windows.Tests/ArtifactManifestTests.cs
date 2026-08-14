using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LafazFlow.Windows.Tests;

public sealed class ArtifactManifestTests
{
    private const string WorkerRevision = "968eebe77225d25e57a3f981da7c696310f0e881";
    private const string OtherRevision = "592feef04a1802b18cbeffd0fd0eb5d02570c2ec";
    private const string WorkerVersionLine =
        "lafazflow-whisper-worker 0.2.0 protocol=1 backend=cuda whisper=968eebe7";

    [Fact]
    public async Task LocalCudaWithoutCliRevisionFails()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out _);

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            workerPath: "",
            workerRevision: "",
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: "",
            cliReleaseIdentity: "",
            Path.Combine(root, "manifest.json"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("CliRevision", standardError);
    }

    [Fact]
    public async Task LocalCudaWithMalformedCliRevisionFails()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out _);

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            workerPath: "",
            workerRevision: "",
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: "968eebe7",
            cliReleaseIdentity: "",
            Path.Combine(root, "manifest.json"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("CliRevision", standardError);
    }

    [Fact]
    public async Task LocalCudaWithValidRevisionSucceedsAndHashesSelectedCli()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out _);

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            workerPath: "",
            workerRevision: "",
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: WorkerRevision,
            cliReleaseIdentity: "",
            Path.Combine(root, "manifest.json"));

        Assert.True(exitCode == 0, standardError);
        var json = await File.ReadAllTextAsync(Path.Combine(root, "manifest.json"));
        using var document = JsonDocument.Parse(json);
        var cli = document.RootElement.GetProperty("cli");
        Assert.Equal("LocalCuda", cli.GetProperty("source").GetString());
        Assert.Equal(WorkerRevision, cli.GetProperty("revision").GetString());
        Assert.Equal("explicit package/build provenance", cli.GetProperty("revision_evidence_type").GetString());
        Assert.Equal(Sha256("CLI-CONTENT"), cli.GetProperty("sha256").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("worker").ValueKind);
        Assert.Equal(Sha256("APP-CONTENT"), document.RootElement.GetProperty("app").GetProperty("sha256").GetString());
        Assert.DoesNotContain(root, json);
        Assert.DoesNotContain("C:\\", json);
    }

    [Fact]
    public async Task WorkerWithoutSuppliedRevisionFails()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out var workerCmd);

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            workerCmd,
            workerRevision: "",
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: WorkerRevision,
            cliReleaseIdentity: "",
            Path.Combine(root, "manifest.json"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("WorkerRevision", standardError);
    }

    [Fact]
    public async Task WorkerThatCannotReportRevisionFails()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out _);
        var inertWorker = Path.Combine(root, "lafazflow-whisper-worker.exe");
        File.WriteAllText(inertWorker, "not-an-executable");

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            inertWorker,
            workerRevision: WorkerRevision,
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: WorkerRevision,
            cliReleaseIdentity: "",
            Path.Combine(root, "manifest.json"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("did not report", standardError);
    }

    [Fact]
    public async Task WorkerWithMismatchedReportedAndSuppliedRevisionFails()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out var workerCmd);

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            workerCmd,
            workerRevision: OtherRevision,
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: WorkerRevision,
            cliReleaseIdentity: "",
            Path.Combine(root, "manifest.json"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("does not begin with", standardError);
    }

    [Fact]
    public async Task WorkerWithMatchingReportedAndSuppliedRevisionSucceeds()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out var workerCmd);

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            workerCmd,
            workerRevision: WorkerRevision,
            cliPath,
            cliSource: "LocalCuda",
            cliRevision: WorkerRevision,
            cliReleaseIdentity: "",
            Path.Combine(root, "manifest.json"));

        Assert.True(exitCode == 0, standardError);
        var json = await File.ReadAllTextAsync(Path.Combine(root, "manifest.json"));
        using var document = JsonDocument.Parse(json);
        var worker = document.RootElement.GetProperty("worker");
        Assert.Equal(WorkerRevision, worker.GetProperty("revision").GetString());
        Assert.Equal("968eebe7", worker.GetProperty("reported_version").GetString());
        Assert.Equal(Sha256(await File.ReadAllBytesAsync(workerCmd)), worker.GetProperty("sha256").GetString());
        Assert.DoesNotContain(root, json);
        Assert.DoesNotContain("C:\\", json);
    }

    [Fact]
    public async Task OfficialCpuRecordsReleaseIdentityWithoutInventedRevision()
    {
        var root = CreateTempPackage(out var appPath, out var cliPath, out var workerCmd);
        const string releaseIdentity =
            "v1.7.4 https://github.com/ggml-org/whisper.cpp/releases/download/v1.7.4/whisper-bin-x64.zip";

        var (exitCode, _, standardError) = await RunManifestScriptAsync(
            appPath,
            workerCmd,
            workerRevision: WorkerRevision,
            cliPath,
            cliSource: "OfficialCpu",
            cliRevision: "",
            cliReleaseIdentity: releaseIdentity,
            Path.Combine(root, "manifest.json"));

        Assert.True(exitCode == 0, standardError);
        var json = await File.ReadAllTextAsync(Path.Combine(root, "manifest.json"));
        using var document = JsonDocument.Parse(json);
        var cli = document.RootElement.GetProperty("cli");
        Assert.Equal("OfficialCpu", cli.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, cli.GetProperty("revision").ValueKind);
        Assert.Equal(JsonValueKind.Null, cli.GetProperty("revision_evidence_type").ValueKind);
        Assert.Equal(releaseIdentity, cli.GetProperty("release_identity").GetString());
        Assert.Equal(Sha256("CLI-CONTENT"), cli.GetProperty("sha256").GetString());
        Assert.Equal(WorkerRevision, document.RootElement.GetProperty("worker").GetProperty("revision").GetString());
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
        out string cliPath,
        out string workerCmdPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "lafazflow-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        appPath = Path.Combine(root, "LafazFlow.Windows.exe");
        cliPath = Path.Combine(root, "whisper-cli.exe");
        workerCmdPath = Path.Combine(root, "lafazflow-whisper-worker.cmd");
        File.WriteAllText(appPath, "APP-CONTENT");
        File.WriteAllText(cliPath, "CLI-CONTENT");
        File.WriteAllText(workerCmdPath, $"@echo off\r\necho {WorkerVersionLine}\r\n");
        return root;
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunManifestScriptAsync(
        string appPath,
        string workerPath,
        string workerRevision,
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
            $"-AppPath \"{appPath}\" -CliPath \"{cliPath}\" " +
            $"-CliSource {cliSource} -Version 1.0.0 " +
            $"-OutputPath \"{outputPath}\"";
        if (workerPath.Length > 0)
        {
            arguments += $" -WorkerPath \"{workerPath}\"";
        }

        if (workerRevision.Length > 0)
        {
            arguments += $" -WorkerRevision \"{workerRevision}\"";
        }

        if (cliRevision.Length > 0)
        {
            arguments += $" -CliRevision \"{cliRevision}\"";
        }

        if (cliReleaseIdentity.Length > 0)
        {
            arguments += $" -CliReleaseIdentity \"{cliReleaseIdentity}\"";
        }

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
        return (process.ExitCode, standardOutput, standardError);
    }

    private static string Sha256(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static string Sha256(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
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
