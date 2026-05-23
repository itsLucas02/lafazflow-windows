namespace LafazFlow.TranscriptionBench;

public static class RegressionPackResolver
{
    public static string DefaultPacksRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "RegressionPacks");
    }

    public static string Resolve(string packName, string packsRoot)
    {
        if (string.IsNullOrWhiteSpace(packName))
        {
            throw new ArgumentException("Pack name cannot be blank.", nameof(packName));
        }

        if (packName.Any(character => !IsAllowedPackNameCharacter(character)))
        {
            throw new ArgumentException(
                "Pack name can only contain letters, numbers, dots, dashes, and underscores.",
                nameof(packName));
        }

        return Path.Combine(packsRoot, packName);
    }

    private static bool IsAllowedPackNameCharacter(char character)
    {
        return char.IsLetterOrDigit(character)
            || character is '.' or '-' or '_';
    }
}
