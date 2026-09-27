// Guard.cs — порт NEVER_TOUCH + PROTECTED фільтра з scripts/audit.sh.
// Жоден apply НЕ проходить без Guard.CanRemove().
namespace TvOptimizer.Core.Safety;

public static class Guard
{
    /// <summary>Пакети які НЕ МОЖНА чіпати ніколи (система/HOME/gms/відео-стек/CTS).</summary>
    public static readonly IReadOnlySet<string> NeverTouch = new HashSet<string>
    {
        "android",
        "com.android.systemui",
        "com.android.shell",
        "com.android.tv.settings",
        "com.android.cts.ctsshim",
        "com.android.cts.priv.ctsshim",
        "com.google.android.tv",
        "mitv.service", // УВАГА: без префікса com. — так в прошивці Xiaomi!
        "com.mitv.livetv",
        "com.mitv.setup",
        "com.mitv.videoplayer",
        "com.google.android.gms",
        "com.android.vending",
        "com.google.android.apps.tv.launcherx", // fallback HOME — тільки enabled
        "com.google.android.tungsten.setupwraith",
        "com.android.providers.tv",
        "com.android.providers.media",
        "com.android.providers.downloads",
        "com.google.android.tv.remote.service",
    };

    /// <summary>Універсальний PROTECTED-мінімум для невідомого ТВ (generic-режим).</summary>
    public static readonly IReadOnlySet<string> UniversalProtected = new HashSet<string>
    {
        "android", "com.android.systemui", "com.android.shell",
        "com.android.vending", "com.google.android.gms",
        "com.google.android.apps.tv.launcherx",
        "com.google.android.tungsten.setupwraith",
        "com.android.providers.tv", "com.android.providers.media",
        "com.android.providers.downloads",
        "com.android.tv.settings", "com.google.android.tv.remote.service",
    };

    /// <summary>Універсальний SAFE-allowlist для невідомого ТВ (вендор-нейтральний).</summary>
    public static readonly IReadOnlySet<string> UniversalSafe = new HashSet<string>
    {
        "com.android.tv.feedbackconsent",
        "com.google.android.feedback",
        "com.android.federatedcompute.services",
        "com.android.ondevicepersonalization.services",
        "com.android.adservices.api",
        "com.android.printspooler",
        "com.android.nearby.halfsheet",
        "com.google.android.play.games",
        "com.google.android.videos",
        "com.google.android.music",
        "com.google.android.tvrecommendations",
        "com.google.android.leanbacklauncher.recommendations",
    };

    public const string DefaultStockLauncher = "com.google.android.apps.tv.launcherx";

    public static bool CanRemove(string pkg, IReadOnlySet<string> deviceProtected, string stockLauncher)
    {
        if (NeverTouch.Contains(pkg)) return false;
        if (UniversalProtected.Contains(pkg)) return false;
        if (deviceProtected.Contains(pkg)) return false;
        if (pkg == stockLauncher) return false;
        return true;
    }

    public static (List<string> Allowed, List<string> Blocked) FilterBatch(
        IEnumerable<string> pkgs, IReadOnlySet<string> deviceProtected, string stockLauncher)
    {
        var ok = new List<string>();
        var blocked = new List<string>();
        foreach (var p in pkgs)
        {
            if (CanRemove(p, deviceProtected, stockLauncher)) ok.Add(p);
            else blocked.Add(p);
        }
        return (ok, blocked);
    }
}
