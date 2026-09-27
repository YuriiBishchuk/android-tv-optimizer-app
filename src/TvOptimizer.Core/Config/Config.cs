// Config.cs — моделі + парсер devices/*.conf та curated *.txt.
// 1-в-1 з форматом сусіднього репо android-tv-optimizer.
using System.Text.RegularExpressions;

namespace TvOptimizer.Core.Config;

public sealed record DeviceConfig(
    string Name,
    string Model = "",
    string Maker = "",
    IReadOnlySet<string>? SafeRemove = null,
    IReadOnlySet<string>? OptionalStreaming = null,
    IReadOnlySet<string>? OptionalOther = null,
    IReadOnlySet<string>? Protected = null,
    IReadOnlySet<string>? DisableOnly = null,
    string StockLauncher = Guard_Defaults.StockLauncher,
    string ProjectivyPkg = "com.spocky.projengmenu")
{
    public IReadOnlySet<string> SafeRemoveSet => SafeRemove ?? new HashSet<string>();
    public IReadOnlySet<string> OptionalStreamingSet => OptionalStreaming ?? new HashSet<string>();
    public IReadOnlySet<string> OptionalOtherSet => OptionalOther ?? new HashSet<string>();
    public IReadOnlySet<string> ProtectedSet => Protected ?? new HashSet<string>();
    public IReadOnlySet<string> DisableOnlySet => DisableOnly ?? new HashSet<string>();
}

internal static class Guard_Defaults
{
    public const string StockLauncher = "com.google.android.apps.tv.launcherx";
}

public static class ConfigUrls
{
    // TODO: поміняй на свій GitHub user/repo
    public static string UrlBase { get; set; } =
        "https://raw.githubusercontent.com/astellias/android-tv-optimizer/main";

    public static string DeviceConf(string name) => $"{UrlBase}/devices/{name}.conf";
    public static string Curated(string name) => $"{UrlBase}/data/community/curated/{name}";

    public static readonly IReadOnlyList<string> KnownDevices = new[] { "xiaomi_a_pro_2026" };
    public static readonly IReadOnlyList<string> CuratedFiles =
        new[] { "xiaomi-gist.txt", "toolkit-tcl.txt", "philips-note.txt" };
}

public static class ConfigParser
{
    private static readonly Regex Quoted = new("\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex ArrayOpen = new(@"^(\w+)=\(\s*(.*)$", RegexOptions.Compiled);

    /// <summary>Парсить вміст devices/*.conf (bash-масиви з "..." + скаляри KEY="...").</summary>
    public static DeviceConfig ParseDeviceConf(string name, string text)
    {
        // Відрізаємо коментарі #... але НЕ всередині лапок (спрощено: коментар починається з # поза "...")
        var arrays = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        string? current = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            var open = ArrayOpen.Match(line);
            if (open.Success)
            {
                current = open.Groups[1].Value;
                arrays.TryAdd(current, new HashSet<string>(StringComparer.Ordinal));
                foreach (var m in Quoted.Matches(open.Groups[2].Value).Cast<Match>())
                    arrays[current].Add(m.Groups[1].Value);
                if (open.Groups[2].Value.Contains(')')) current = null;
                continue;
            }
            if (line == ")") { current = null; continue; }
            if (current != null)
            {
                foreach (var m in Quoted.Matches(line).Cast<Match>())
                    arrays[current].Add(m.Groups[1].Value);
                if (line.Contains(')')) current = null;
            }
        }

        HashSet<string> Arr(string key) =>
            arrays.TryGetValue(key, out var s) ? s : new HashSet<string>(StringComparer.Ordinal);

        string Scalar(string key)
        {
            var m = Regex.Match(text, $"^{key}=\"([^\"]+)\"", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : "";
        }

        var stock = Scalar("STOCK_LAUNCHER");
        if (string.IsNullOrEmpty(stock)) stock = Guard_Defaults.StockLauncher;
        var proj = Scalar("PROJECTIVY_PKG");
        if (string.IsNullOrEmpty(proj)) proj = "com.spocky.projengmenu";

        return new DeviceConfig(
            name,
            Scalar("DEVICE_MODEL"),
            Scalar("DEVICE_MAKER"),
            Arr("SAFE_REMOVE"),
            Arr("OPTIONAL_STREAMING"),
            Arr("OPTIONAL_OTHER"),
            Arr("PROTECTED"),
            Arr("DISABLE_ONLY"),
            stock,
            proj);
    }

    /// <summary>Парсить curated .txt: по пакету на рядок, # коментар, рядки з пробілами — ігнор.</summary>
    public static IReadOnlySet<string> ParseCurated(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;
            if (line.Contains(' ') || line.Contains('\t')) continue;
            set.Add(line);
        }
        return set;
    }

    private static string StripComment(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (line[i] == '#' && !inQuotes) return line[..i];
        }
        return line;
    }
}
