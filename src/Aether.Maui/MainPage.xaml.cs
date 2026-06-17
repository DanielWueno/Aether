using Aether.Shared;
using SharpAdbClient;
using Microsoft.Maui.Dispatching;

namespace Aether.Maui;

public partial class MainPage : ContentPage
{
    private readonly ITetherService _tetherService;

    public MainPage(ITetherService tetherService)
    {
        InitializeComponent();
        _tetherService = tetherService;
        _tetherService.DeviceStatusChanged += OnDeviceStatusChanged;
    }

    private void OnDeviceStatusChanged(Aether.Shared.DeviceInfo info)
    {
        Dispatcher.Dispatch(() =>
        {
            StatusText.Text = $"Status: {info.Status}";
            DeviceText.Text = info.Status == ConnectionStatus.Disconnected ? "No device active" : $"{info.Model} ({info.Serial})";
            
            if (info.Status == ConnectionStatus.Connected)
            {
                ToggleBtn.Text = "Stop Tethering";
                ToggleBtn.BackgroundColor = Colors.Crimson;
            }
            else
            {
                ToggleBtn.Text = "Start Tethering";
                ToggleBtn.BackgroundColor = Color.FromArgb("#0078D4");
            }
        });
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        var adb = new AdbClient();
        try 
        {
            var devices = adb.GetDevices();
            if (devices.Count > 0)
            {
                DeviceText.Text = $"Detected: {devices[0].Model} ({devices[0].Serial})";
                DeviceText.TextColor = Colors.Green;
            }
            else
            {
                DeviceText.Text = "No devices detected. Check USB Debugging.";
                DeviceText.TextColor = Colors.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", $"Error refreshing: {ex.Message}", "OK");
        }
    }

    private async void OnToggleClicked(object sender, EventArgs e)
    {
        if (ToggleBtn.Text == "Stop Tethering")
        {
            await _tetherService.StopAsync();
            return;
        }

        var adb = new AdbClient();
        var devices = adb.GetDevices();
        
        if (devices.Count == 0)
        {
            await DisplayAlert("Aether", "No devices detected. Please connect your Android device and enable USB Debugging.", "OK");
            return;
        }

        _tetherService.ManualProxy = ManualProxyEntry.Text ?? "";
        await _tetherService.StartAsync(devices[0].Serial);
    }
}
