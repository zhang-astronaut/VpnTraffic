namespace VpnTraffic.Services;

/// <summary>
/// Stable file locations under LocalApplicationData (packaged apps redirect this).
/// Never use Utilities.BaseSettingsPath alone: it can return a bare directory (LocalState)
/// which JsonSettingsManager cannot persist to, causing settings to vanish after restart.
/// </summary>
public static class AppPaths
{
    public const string FolderName = "VpnTraffic";

    public static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName);

    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");
    public static string SubscriptionsFile => Path.Combine(RootDirectory, "subscriptions.json");

    public static string HistoryFile(string subscriptionId) =>
        Path.Combine(RootDirectory, "history-" + Sanitize(subscriptionId) + ".json");

    public static string LegacyHistoryFile => Path.Combine(RootDirectory, "history.json");

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(RootDirectory);
    }

    public static string Sanitize(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "default";
        }

        Span<char> buffer = stackalloc char[id.Length];
        var n = 0;
        foreach (var c in id)
        {
            buffer[n++] = char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_';
        }

        return new string(buffer[..n]);
    }
}
