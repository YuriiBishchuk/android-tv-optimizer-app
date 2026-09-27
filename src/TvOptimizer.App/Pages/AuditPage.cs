using TvOptimizer.Core.Audit;
using TvOptimizer.Core.Config;
using TvOptimizer.Core.Safety;
using TvOptimizer.App.Services;

namespace TvOptimizer.App.Pages;

public class AuditPage : ContentPage
{
    private readonly Label _statusLabel;
    private readonly Button _refreshBtn;
    private readonly ListView _resultsList;
    private readonly Button _applyAllBtn;

    public AuditPage()
    {
        Title = "Аудит";
        Padding = new Thickness(10);

        _statusLabel = new Label
        {
            Text = "Готовий до аудиту",
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Start,
            TextColor = Colors.Blue
        };

        _refreshBtn = new Button
        {
            Text = "Оновити",
            BackgroundColor = Colors.Blue,
            TextColor = Colors.White
        };
        _refreshBtn.Clicked += async (s, e) => await OnRefreshClicked();

        _applyAllBtn = new Button
        {
            Text = "Застосувати всі рекомендації",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            IsEnabled = false
        };
        _applyAllBtn.Clicked += async (s, e) => await OnApplyAllClicked();

        _resultsList = new ListView
        {
            HasUnevenRows = true,
            SeparatorVisibility = SeparatorVisibility.None,
            VerticalOptions = LayoutOptions.FillAndExpand
        };
        _resultsList.ItemTemplate = new DataTemplate(typeof(AuditResultCell));
        _resultsList.ItemTapped += async (s, e) =>
        {
            if (e.Item is AuditResult result)
            {
                await DisplayAlert("Деталі", result.Details, "OK");
                ((ListView)s).SelectedItem = null;
            }
        };

        var layout = new StackLayout
        {
            Spacing = 10,
            Children =
            {
                new StackLayout
                {
                    Orientation = StackOrientation.Horizontal,
                    Children =
                    {
                        new Label { Text = "Статус:", FontAttributes = FontAttributes.Bold, WidthRequest = 80 },
                        _statusLabel
                    }
                },
                new StackLayout
                {
                    Orientation = StackOrientation.Horizontal,
                    Spacing = 10,
                    Children = { _refreshBtn, _applyAllBtn }
                },
                new Label { Text = "Результати аудиту:", FontAttributes = FontAttributes.Bold },
                _resultsList
            }
        };

        Content = new ScrollView { Content = layout };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAuditAsync();
    }

    private async Task RefreshAuditAsync()
    {
        if (!TvSession.Current.IsConnected)
        {
            await Toast.Make("Спочатку підключіться до ТВ").Show();
            return;
        }

        _statusLabel.Text = "Виконується аудит...";
        _statusLabel.TextColor = Colors.Orange;
        _refreshBtn.IsEnabled = false;
        _applyAllBtn.IsEnabled = false;

        try
        {
            var raw = await TvSession.Current.ShellAsync("pm list packages -3");
            var packages = raw.Split('\n')
                .Where(line => line.StartsWith("package:"))
                .Select(line => line.Substring("package:".Length).Trim())
                .Where(pkg => !string.IsNullOrWhiteSpace(pkg))
                .ToArray();

            var engine = new AuditEngine();
            var results = engine.AuditPackages(packages)
                .OrderByDescending(r => r.Tier)
                .ThenBy(r => r.PackageName)
                .ToList();

            _resultsList.ItemsSource = results;
            _statusLabel.Text = $"Знайдено {results.Count} пакетів, {results.Count(r => r.NeedsAction)} потребує дії";
            _statusLabel.TextColor = results.Any(r => r.NeedsAction) ? Colors.Orange : Colors.Green;
            _applyAllBtn.IsEnabled = results.Any(r => r.NeedsAction);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Помилка: {ex.Message}";
            _statusLabel.TextColor = Colors.Red;
            await Toast.Make($"Помилка аудиту: {ex.Message}").Show();
        }
        finally
        {
            _refreshBtn.IsEnabled = true;
        }
    }

    private async Task OnApplyAllClicked()
    {
        if (!TvSession.Current.IsConnected)
        {
            await Toast.Make("Спочатку підключіться до ТВ").Show();
            return;
        }

        var answer = await DisplayAlert(
            "Застосувати всі tweaks?",
            "Це змінить налаштування на ТВ. Продовжити?",
            "Так", "Ні");

        if (!answer) return;

        _applyAllBtn.IsEnabled = false;
        _statusLabel.Text = "Застосовується...";
        _statusLabel.TextColor = Colors.Blue;

        try
        {
            await Toast.Make("Застосовано tweaks (заглушка)").Show();
            await RefreshAuditAsync();
        }
        catch (Exception ex)
        {
            await Toast.Make($"Помилка: {ex.Message}").Show();
        }
        finally
        {
            _applyAllBtn.IsEnabled = true;
        }
    }
}