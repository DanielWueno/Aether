using System.Windows;
using Aether.Shared;
using SharpAdbClient;

namespace Aether.Desktop;

public partial class MainWindow : Window
{
    private readonly ITetherService _tetherService;

    public MainWindow(ITetherService tetherService)
    {
        InitializeComponent();
        _tetherService = tetherService;
        _tetherService.DeviceStatusChanged += OnDeviceStatusChanged;
        
        // Use a system icon as a fallback
        MyNotifyIcon.Icon = System.Drawing.SystemIcons.Shield; 
    }

    private void OnDeviceStatusChanged(DeviceInfo info)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = $"Status: {info.Status}";
            DeviceText.Text = info.Status == ConnectionStatus.Disconnected ? "No device active" : $"{info.Model} ({info.Serial})";
            
            if (info.Status == ConnectionStatus.Connected)
            {
                MenuStart.Visibility = Visibility.Collapsed;
                MenuStop.Visibility = Visibility.Visible;
                BtnToggle.Content = "Stop Tethering";
                BtnToggle.Background = System.Windows.Media.Brushes.Crimson;
                MyNotifyIcon.ToolTipText = $"Aether: Connected to {info.Model}";
            }
            else
            {
                MenuStart.Visibility = Visibility.Visible;
                MenuStop.Visibility = Visibility.Collapsed;
                BtnToggle.Content = "Start Tethering";
                BtnToggle.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                MyNotifyIcon.ToolTipText = "Aether: Disconnected";
            }
        });
    }

    private void OnRefreshDevices(object sender, RoutedEventArgs e)
    {
        var adb = new AdbClient();
        try 
        {
            var devices = adb.GetDevices();
            if (devices.Count > 0)
            {
                DeviceText.Text = $"Detected: {devices[0].Model} ({devices[0].Serial})";
                DeviceText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                DeviceText.Text = "No devices detected. Check USB Debugging.";
                DeviceText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing: {ex.Message}");
        }
    }

    private void OnDiagnostics(object sender, RoutedEventArgs e)
    {
        try
        {
            var systemProxy = System.Net.WebRequest.GetSystemWebProxy();
            var testUri = new Uri("https://www.google.com");
            var proxyUri = systemProxy.GetProxy(testUri);

            bool hasSystemProxy = proxyUri != null && proxyUri != testUri;
            
            string envProxy = Environment.GetEnvironmentVariable("HTTP_PROXY") ?? Environment.GetEnvironmentVariable("http_proxy") ?? "None";

            string msg = $"System Default Proxy: {(hasSystemProxy ? proxyUri!.ToString() : "None detected")}\n" +
                         $"Environment HTTP_PROXY: {envProxy}";

            MessageBox.Show(msg, "Aether Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Diagnostic error: {ex.Message}", "Aether Diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnStartTethering(object sender, RoutedEventArgs e)
    {
        if (BtnToggle.Content.ToString() == "Stop Tethering")
        {
            await _tetherService.StopAsync();
            return;
        }

        var adb = new AdbClient();
        var devices = adb.GetDevices();
        
        if (devices.Count == 0)
        {
            MessageBox.Show("No devices detected. Please connect your Pixel and enable USB Debugging.", "Aether", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Assign manual proxy if user typed one
        _tetherService.ManualProxy = TxtManualProxy.Text;

        await _tetherService.StartAsync(devices[0].Serial);
    }

    private async void OnStopTethering(object sender, RoutedEventArgs e)
    {
        await _tetherService.StopAsync();
    }

    private void OnHideToTray(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        _tetherService.StopAsync().Wait();
        Application.Current.Shutdown();
    }
}
