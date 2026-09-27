using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using TvOptimizer.App.Services;

namespace TvOptimizer.App.Pages;

public class ConnectPage : ContentPage
{
    private readonly Label _statusLabel;
    private readonly Entry _hostEntry;
    private readonly Entry _portEntry;
    private readonly Switch _tlsSwitch;
    private readonly Button _connectBtn;
    private readonly Button _disconnectBtn;

    public ConnectPage()
    {
        Title = "Підключення";
        Padding = new Thickness(20);

        _statusLabel = new Label
        {
            Text = "Не підключено",
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Colors.Red
        };

        _hostEntry = new Entry
        {
            Placeholder = "адреса ТВ (IP або hostname)",
            Text = "192.168.1.100"
        };

        _portEntry = new Entry
        {
            Placeholder = "порт ADB (за замовчуванням 5555)",
            Text = "5555",
            Keyboard = Keyboard.Numeric
        };

        _tlsSwitch = new Switch
        {
            IsToggled = false
        };

        _connectBtn = new Button
        {
            Text = "Підключитися",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White
        };
        _connectBtn.Clicked += async (s, e) => await OnConnectClicked();

        _disconnectBtn = new Button
        {
            Text = "Відключити",
            BackgroundColor = Colors.Red,
            TextColor = Colors.White,
            IsEnabled = false
        };
        _disconnectBtn.Clicked += async (s, e) => await OnDisconnectClicked();

        var layout = new StackLayout
        {
            Spacing = 15,
            Children =
            {
                new Label { Text = "Підключення до Android TV через ADB", FontSize = 18, HorizontalOptions = LayoutOptions.Center },
                new BoxView { HeightRequest = 1, Color = Colors.LightGray, HorizontalOptions = LayoutOptions.FillAndExpand },
                _statusLabel,
                new Frame { Padding = 15, Content = new StackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Label { Text = "Хост:", FontAttributes = FontAttributes.Bold },
                        _hostEntry,
                        new Label { Text = "Порт:", FontAttributes = FontAttributes.Bold },
                        _portEntry,
                        new Label { Text = "TLS/SSL:", FontAttributes = FontAttributes.Bold },
                        _tlsSwitch
                    }
                }},
                new StackLayout
                {
                    Orientation = StackOrientation.Horizontal,
                    Spacing = 10,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { _connectBtn, _disconnectBtn }
                }
            }
        };

        Content = new ScrollView { Content = layout };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var connected = TvSession.Current.IsConnected;
        _statusLabel.Text = connected ? $"Підключено: {TvSession.Current.DeviceModel}" : "Не підключено";
        _statusLabel.TextColor = connected ? Colors.Green : Colors.Red;
        _connectBtn.IsEnabled = !connected;
        _disconnectBtn.IsEnabled = connected;
        _hostEntry.IsEnabled = !connected;
        _portEntry.IsEnabled = !connected;
        _tlsSwitch.IsEnabled = !connected;
    }

    private async Task OnConnectClicked()
    {
        if (!int.TryParse(_portEntry.Text, out var port) || port < 1 || port > 65535)
        {
            await Toast.Make("Неправильний порт").Show();
            return;
        }

        _connectBtn.IsEnabled = false;
        try
        {
            var model = await TvSession.Current.ConnectAsync(
                _hostEntry.Text.Trim(), port, _tlsSwitch.IsToggled);
            await Toast.Make($"Підключено до {model}").Show();
        }
        catch (Exception ex)
        {
            await Toast.Make($"Помилка: {ex.Message}").Show();
        }
        finally
        {
            UpdateUiState();
            _connectBtn.IsEnabled = true;
        }
    }

    private async Task OnDisconnectClicked()
    {
        await TvSession.Current.DisconnectAsync();
        await Toast.Make("Відключено").Show();
        UpdateUiState();
    }
}