// Audit.cs — порт scripts/audit.sh: TIER_1/2/HEURISTIC/PROTECTED/UNIDENTIFIED.
// Чистий C#, детермінований, покритий тестами. Жодного ADB тут — тільки множини.
using System.Text.RegularExpressions;
using TvOptimizer.Core.Config;
using TvOptimizer.Core.Safety;

namespace TvOptimizer.Core.Audit;

public enum Tier { Safe, Review, Heuristic, Protected, Unidentified }

public sealed record AuditResult(
    bool GenericMode,
    string ModeName,
    IReadOnlyList<string> SafePresent,
    IReadOnlyList<string> SafeGone,
    IReadOnlyList<string> ReviewPresent,
    IReadOnlyList<string> HeuristicHits,
    IReadOnlyList<string> ProtectedPresent,
    IReadOnlyList<string> ProtectedMissing,
    IReadOnlyList<string> UnidentifiedShown,
    int UnidentifiedTotal);

public static class AuditEngine
{
    private static readonly Regex HeuristicRe = new(
        @"analytics|telemetry|tracker|[^a-z]acr([^a-z]|$)|adservice|recommend|promo|demo|retail|partnercustomizer|printspooler|nearby\.halfsheet|feedback|federated|personalization",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NoiseRe = new(
        @"^(android|com\.android|com\.google\.android\.(overlay|ext|module|ondevice|federated|adservices)|mediatek\.factorymenu)\b",
        RegexOptions.Compiled);
    private static readonly Regex OverlayNoiseRe = new(
        @"(overlay|rro|resoverlay|auto_generated)\b", RegexOptions.Compiled);

    /// <param name="installed">Встановлені пакети з `pm list packages`.</param>
    /// <param name="device">Конфіг пристрою або null (generic-режим).</param>
    /// <param name="curatedTier2">Об'єднаний curated TIER_2.</param>
    public static AuditResult Run(
        IReadOnlySet<string> installed,
        DeviceConfig? device,
        IReadOnlySet<string> curatedTier2)
    {
        bool generic = device is null;
        IReadOnlySet<string> tier1 = generic
            ? Guard.UniversalSafe
            : new HashSet<string>(device!.SafeRemoveSet, StringComparer.Ordinal);
        IReadOnlySet<string> deviceProtected = generic
            ? Guard.UniversalProtected
            : new HashSet<string>(device!.ProtectedSet, StringComparer.Ordinal);

        // TIER_2 = curated + (device OPTIONAL, якщо не generic)
        var tier2 = new HashSet<string>(curatedTier2, StringComparer.Ordinal);
        if (!generic)
        {
            tier2.UnionWith(device!.OptionalStreamingSet);
            tier2.UnionWith(device!.OptionalOtherSet);
        }

        // Фільтр tier1/tier2 від NEVER_TOUCH/protected (як filter_lists в audit.sh)
        tier1 = tier1.Where(p => Guard.CanRemove(p, deviceProtected,
                generic ? Guard.DefaultStockLauncher : device!.StockLauncher)).ToHashSet(StringComparer.Ordinal);
        var t1snap = new HashSet<string>(tier1, StringComparer.Ordinal);
        tier2 = tier2.Where(p => Guard.CanRemove(p, deviceProtected,
                generic ? Guard.DefaultStockLauncher : device!.StockLauncher) && !t1snap.Contains(p))
            .ToHashSet(StringComparer.Ordinal);

        var safePresent = tier1.Where(installed.Contains).OrderBy(x => x).ToList();
        var safeGone = tier1.Where(p => !installed.Contains(p)).OrderBy(x => x).ToList();
        var reviewPresent = tier2.Where(installed.Contains).OrderBy(x => x).ToList();
        var heuristic = installed.Where(p => HeuristicRe.IsMatch(p)).OrderBy(x => x).ToList();

        IReadOnlyList<string> protCheck = generic
            ? Guard.UniversalProtected.OrderBy(x => x).ToList()
            : new[] {
                "mitv.service", "com.mitv.livetv", "com.mitv.setup", "com.mitv.videoplayer",
                "com.google.android.apps.tv.launcherx",
                "com.google.android.tungsten.setupwraith",
                "com.android.systemui", "android" };
        var protPresent = protCheck.Where(installed.Contains).ToList();
        var protMissing = protCheck.Where(p => !installed.Contains(p)).ToList();

        var known = new HashSet<string>(StringComparer.Ordinal);
        known.UnionWith(tier1); known.UnionWith(tier2);
        known.UnionWith(deviceProtected); known.UnionWith(Guard.NeverTouch);
        known.UnionWith(Guard.UniversalProtected);

        var unid = installed.Where(p => !known.Contains(p)).OrderBy(x => x).ToList();
        var shown = unid.Where(p => !NoiseRe.IsMatch(p) && !OverlayNoiseRe.IsMatch(p)).ToList();

        string modeName = generic
            ? "GENERIC (невідомий ТВ, тільки UNIVERSAL_SAFE + curated)"
            : $"DEVICE ({device!.Name})";
        return new AuditResult(generic, modeName, safePresent, safeGone, reviewPresent,
            heuristic, protPresent, protMissing, shown, unid.Count);
    }
}
