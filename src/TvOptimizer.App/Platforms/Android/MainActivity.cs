using Android.App;
using Content = Android.Content;

namespace TvOptimizer.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(new[] { Content.Intent.ActionMain },
    Categories = new[] { "android.intent.category.LAUNCHER" })]
public class MainActivity : MauiAppCompatActivity
{
}

