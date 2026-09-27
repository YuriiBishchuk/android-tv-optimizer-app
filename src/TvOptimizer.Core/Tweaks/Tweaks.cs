// Tweaks.cs — безпечні твіки продуктивності через settings/pm/cmd.
// Жодних disable на відео-стек. Все відкотне (BackupCommands).
namespace TvOptimizer.Core.Tweaks;

public enum AnimSpeed { Off, Fast05, Stock1x }

public sealed record TweakCmd(string Command, string Revert, string Label);

public static class TweaksEngine
{
    public static string AnimValue(AnimSpeed s) => s switch
    {
        AnimSpeed.Off => "0",
        AnimSpeed.Fast05 => "0.5",
        _ => "1.0",
    };

    /// <summary>Анімації: window/transition/animator scale. Миттєвий ефект швидкості меню.</summary>
    public static IReadOnlyList<TweakCmd> Animation(AnimSpeed speed)
    {
        var v = AnimValue(speed);
        string[] keys = { "window_animation_scale", "transition_animation_scale", "animator_duration_scale" };
        return keys.Select(k => new TweakCmd(
            $"settings put global {k} {v}",
            $"settings put global {k} 1.0",
            $"Анімація {k} = {v}")).ToList();
    }

    /// <summary>Ліміт фонових процесів (0-4 / standard). Обережно: 0 може вбивати фонове відтворення.</summary>
    public static TweakCmd BackgroundLimit(int max = 4) => new(
        $"settings put global activity_manager_max_proc {max}",
        "settings delete global activity_manager_max_proc",
        $"Фонові процеси ≤ {max}");

    /// <summary>Doze завжди вкл — економить простій. Безпечно.</summary>
    public static TweakCmd DozeOn() => new(
        "dumpsys deviceidle enable",
        "dumpsys deviceidle disable",
        "Doze: увімкнути економію в простої");

    /// <summary>Скрінсейвер: вимкнути (бонус до відгуку) або залишити.</summary>
    public static TweakCmd ScreensaverOff() => new(
        "settings put secure screensaver_enabled 0",
        "settings put secure screensaver_enabled 1",
        "Скрінсейвер: вимкнути");

    /// <summary>Тільки читання: роздільна здатність / HDR / RAM / uptime для звіту.</summary>
    public static readonly IReadOnlyList<string> InfoCommands = new[]
    {
        "wm size; wm density",
        "dumpsys display | grep -i hdr",
        "cat /proc/meminfo | head -n 3",
        "uptime",
        "getprop ro.product.model; getprop ro.product.brand; getprop ro.build.version.sdk",
    };

    /// <summary>Набір "Прийшов у гості": анімації 0.5 + фон 4 + doze. Все відкотне.</summary>
    public static IReadOnlyList<TweakCmd> GuestPreset() =>
        Animation(AnimSpeed.Fast05).Append(BackgroundLimit(4)).Append(DozeOn()).ToList();
}
