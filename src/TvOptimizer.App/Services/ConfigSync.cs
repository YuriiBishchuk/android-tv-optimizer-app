using TvOptimizer.Core.Config;

namespace TvOptimizer.App.Services;

/// <summary>
/// Свіжі конфіги з GitHub raw + локальний кеш (працює в гостях без інтернету).
/// Кеш: <AppDataDirectory>/configs/<name>. TTL 7 днів, як в audit.sh-підході.
/// </summary>
public sealed class ConfigSync
{
    public static ConfigSync Current { get; } = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private string CacheDir => Path.Combine(FileSystem.AppDataDirectory, "configs");

    public DeviceConfig? LastDevice { get; private set; }
    public IReadOnlySet<string> CuratedTier2 { get; private set; } = new HashSet<string>();
    public string Status { get; private set; } = "не синхронізовано";

    public async Task SyncAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(CacheDir);
        var tier2 = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in ConfigUrls.CuratedFiles)
        {
            var txt = await GetCachedAsync(ConfigUrls.Curated(f),
                "curated_" + f, ct).ConfigureAwait(false);
            if (txt is not null) tier2.UnionWith(ConfigParser.ParseCurated(txt));
        }
        CuratedTier2 = tier2;

        var sb = new System.Text.StringBuilder($"curated TIER_2: {tier2.Count} pkg. devices:");
        foreach (var d in ConfigUrls.KnownDevices)
        {
            var txt = await GetCachedAsync(ConfigUrls.DeviceConf(d),
                "device_" + d + ".conf", ct).ConfigureAwait(false);
            if (txt is not null)
            {
                var cfg = ConfigParser.ParseDeviceConf(d, txt);
                LastDevice = cfg;
                sb.Append(' ').Append(cfg.Name).Append('(').Append(cfg.Model).Append(')');
            }
            else sb.Append(' ').Append(d).Append("(немає)");
        }
        Status = sb.ToString();
    }

    /// <summary>Підбір конфігу за fingerprint із `getprop`.</summary>
    public DeviceConfig? MatchDevice(string modelProp, string brandProp)
    {
        if (LastDevice is null) return null;
        if (brandProp.ToLowerInvariant().Contains("xiaomi") &&
            LastDevice.Model.Equals(modelProp.Trim(), StringComparison.OrdinalIgnoreCase))
            return LastDevice;
        return null;
    }

    private async Task<string?> GetCachedAsync(string url, string cacheName, CancellationToken ct)
    {
        var path = Path.Combine(CacheDir, cacheName);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(12));
            var text = await Http.GetStringAsync(url, cts.Token).ConfigureAwait(false);
            await File.WriteAllTextAsync(path, text, ct).ConfigureAwait(false);
            return text;
        }
        catch
        {
            // офлайн: беремо кеш якщо молодший за 30 днів
            if (File.Exists(path) &&
                DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(30))
                return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return null;
        }
    }
}
