namespace LafazFlow.Windows.Core;

public sealed record LocalModelDefinition(
    string Id,
    string DisplayName,
    string FileName,
    string SizeLabel,
    string LanguageLabel,
    double SpeedScore,
    double AccuracyScore,
    string RamLabel,
    string Description,
    string DownloadUrl,
    bool IsRecommended,
    bool IsQualityProfile,
    bool IsImported = false);
