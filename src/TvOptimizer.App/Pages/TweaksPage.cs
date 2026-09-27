using TvOptimizer.App.Services;

namespace TvOptimizer.App.Pages;

public class TweaksPage : ContentPage
{
    private readonly Label _statusLabel;
    private readonly Button _refreshBtn;
    private readonly ListView _tweaksList;
    private readonly Button _applySelectedBtn;

    private readonly List<TweakItem> _items = new();

    public TweaksPage()
    {
        Title = "Tweaks";
        Padding = new Thickness(10);

        _statusLabel = new Label
        {
            Text = "Готовий",
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Blue
        };
        _refreshBtn = new Button
        {
            Text = "Перевірити стан",
            BackgroundColor = Colors.Blue,
            TextColor = Colors.White
        };
        _refreshBtn.Clicked += async (s, e) => await LoadTweaksAsync();
        _applySelectedBtn = new Button
        {
            Text = "Застосувати вибране",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            IsEnabled = false
        };
        _applySelectedBtn.Clicked += async (s, e) => await ApplySelectedAsync();

        _tweaksList = new ListView
        {
            HasUnevenRows = true,
            SelectionMode = ListViewSelectionMode.Multiple
        };
        _tweaksList.ItemTemplate = new DataTemplate(typeof(TweakCell));

        Content = new StackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Доступні tweaks:", FontAttributes = FontAttributes.Bold },
                _statusLabel,
                _refreshBtn,
                _tweaksList,
                _applySelectedBtn
            }
        };
    }

    protected override async void OnAppearing()
    {
        await LoadTweaksAsync();
    }

    private async Task LoadTweaksAsync()
    {
        _statusLabel.Text = "Перевірка...";
        _refreshBtn.IsEnabled = false;
        try
        {
            if (!TvSession.Current.IsConnected)
            {
                _statusLabel.Text = "Не підключено";
                return;
            }
            var anim = await TvSession.Current.ShellAsync("settings list global | grep -E 'animation|animator'");
            _items.Clear();
            _items.Add(new TweakItem
            {
                Name = "Animation scale",
                Category = "Animation",
                CurrentValue = anim,
                TargetValue = "0.5"
            });
            _items.Add(new TweakItem
            {
                Name = "Doze disabled",
                Category = "Doze",
                CurrentValue = "checking",
                TargetValue = "disabled"
            });
            _tweaksList.SelectedItem = null;
            _tweaksList.ItemsView = _items;
            _statusLabel.Text = "Знайдено " + _items.Count;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Помилка: " + ex.Message;
        }
        finally
        {
            _refreshBtn.IsEnabled = true;
        }
    }

    private async Task ApplySelectedAsync()
    {
        var selected = _tweaksList.SelectedItem as TweakItem;
        if (selected == null) return;
        if (!await DisplayAlert("Підтвердити", "Застосувати вибране?", "Так", "Ні")) return;
        _applySelectedBtn.IsEnabled = false;
        try
        {
            await TvSession.Current.ShellAsync(selected.GetCommand());
        }
        catch { }
        _applySelectedBtn.IsEnabled = true;
    }
}

public class TweakItem
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string CurrentValue { get; set; } = "";
    public string TargetValue { get; set; } = "";
    public string Description { get; set; } = "";
    public string GetCommand() => Category switch
    {
        "Animation" => "settings put global " + Name.Replace(" ", "_").ToLower() + " " + TargetValue,
        _ => ""
    };
}

public class TweakCell : ViewCell
{
    public TweakCell()
    {
        var grid = new Grid { Padding = new Thickness(10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridUnitType.Star));
        grid.Add(new Label { Text = "{Binding Name}", FontAttributes = FontAttributes.Bold }, 0, 0);
        grid.Add(new Label { Text = "{Binding CurrentValue}", TextColor = Colors.Orange }, 0, 1);
        View = grid;
    }
}