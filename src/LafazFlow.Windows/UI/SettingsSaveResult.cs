namespace LafazFlow.Windows.UI;

public sealed record SettingsSaveResult(bool Success, IReadOnlyList<string> Errors)
{
    public static SettingsSaveResult Ok { get; } = new(true, []);

    public static SettingsSaveResult Failed(IReadOnlyList<string> errors)
    {
        return new SettingsSaveResult(false, errors);
    }
}
