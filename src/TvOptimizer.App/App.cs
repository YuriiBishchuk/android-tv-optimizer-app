using TvOptimizer.App.Pages;

namespace TvOptimizer.App;

public class App : Application
{
    public App()
    {
        var shell = new Shell
        {
            FlyoutBehavior = FlyoutBehavior.Disabled,
        };
        shell.Items.Add(new ShellContent
        {
            Title = "TV",
            ContentTemplate = new DataTemplate(typeof(ConnectPage)),
        });
        shell.Items.Add(new ShellContent
        {
            Title = "Audit",
            ContentTemplate = new DataTemplate(typeof(AuditPage)),
        });
        shell.Items.Add(new ShellContent
        {
            Title = "Tweaks",
            ContentTemplate = new DataTemplate(typeof(TweaksPage)),
        });
        MainPage = shell;
    }
}
