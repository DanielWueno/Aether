using System.Diagnostics;
using Aether.Shared;
using Aether.Core.Abstractions;
using SharpAdbClient;

namespace Aether.Core.Services;

public class TetherService : ITetherService, IDisposable
{
    private readonly IAdbManager _adbManager;
    private readonly IProxyManager _proxyManager;
    private readonly int _proxyPort = 8888;
    private bool _isRunning;
    private DeviceData? _activeDevice;
    private DeviceMonitor? _deviceMonitor;

    public string ManualProxy { get; set; } = string.Empty;

    public event Action<DeviceInfo>? DeviceStatusChanged;

    public TetherService(IAdbManager adbManager, IProxyManager proxyManager)
    {
        _adbManager = adbManager;
        _proxyManager = proxyManager;
        
        _adbManager.StartServer();
        StartDeviceMonitor();
    }

    private void StartDeviceMonitor()
    {
        try
        {
            var adbSocket = new AdbSocket(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, AdbClient.AdbServerPort));
            _deviceMonitor = new DeviceMonitor(adbSocket);
            _deviceMonitor.DeviceDisconnected += OnDeviceDisconnected;
            _deviceMonitor.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Aether] Could not start DeviceMonitor: {ex.Message}");
        }
    }

    private void OnDeviceDisconnected(object? sender, DeviceDataEventArgs e)
    {
        if (_activeDevice != null && e.Device.Serial == _activeDevice.Serial)
        {
            Debug.WriteLine($"[Aether] Device {_activeDevice.Serial} was physically disconnected.");
            // We cannot clean the device settings because it's disconnected,
            // but we must clean the PC state and UI.
            _ = StopLocalStateAsync();
        }
    }

    private async Task StopLocalStateAsync()
    {
        _proxyManager.Stop();
        _isRunning = false;
        _activeDevice = null;
        NotifyStatus(ConnectionStatus.Disconnected);
        await Task.CompletedTask;
    }

    public async Task StartAsync(string deviceSerial)
    {
        if (_isRunning) return;

        try
        {
            _activeDevice = _adbManager.GetConnectedDevices().FirstOrDefault(d => d.Serial == deviceSerial);
            if (_activeDevice == null) throw new Exception("Device not found.");

            // 1. Setup Local Proxy
            if (_proxyManager is ProxyManager pm && !string.IsNullOrWhiteSpace(ManualProxy))
            {
                pm.ManualProxy = ManualProxy;
            }
            _proxyManager.Start(_proxyPort);

            // 2. Setup ADB Reverse Tunnel
            _adbManager.RemoveAllReverseTunnels(_activeDevice);
            _adbManager.CreateReverseTunnel(_activeDevice, _proxyPort, _proxyPort);

            // 3. Inject Android Settings
            _adbManager.ExecuteCommand($"settings put global http_proxy 127.0.0.1:{_proxyPort}", _activeDevice);
            _adbManager.ExecuteCommand($"settings put global global_http_proxy_host 127.0.0.1", _activeDevice);
            _adbManager.ExecuteCommand($"settings put global global_http_proxy_port {_proxyPort}", _activeDevice);
            
            Debug.WriteLine($"[Aether] Stealth Hybrid Engine ready on port {_proxyPort}.");

            _isRunning = true;
            NotifyStatus(ConnectionStatus.Connected);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting tether: {ex.Message}");
            NotifyStatus(ConnectionStatus.Error);
            await StopAsync();
        }
    }

    public async Task StopAsync()
    {
        if (_activeDevice != null)
        {
            try
            {
                _adbManager.ExecuteCommand("settings put global http_proxy :0", _activeDevice);
                _adbManager.RemoveAllReverseTunnels(_activeDevice);
            }
            catch { /* Ignore cleanup errors on disconnected devices */ }
        }

        _proxyManager.Stop();

        _isRunning = false;
        _activeDevice = null;
        NotifyStatus(ConnectionStatus.Disconnected);
        await Task.CompletedTask;
    }

    private void NotifyStatus(ConnectionStatus status)
    {
        DeviceStatusChanged?.Invoke(new DeviceInfo
        {
            Serial = _activeDevice?.Serial ?? "",
            Model = _activeDevice?.Model ?? "None",
            Status = status
        });
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        if (_proxyManager is IDisposable disposableProxy)
        {
            disposableProxy.Dispose();
        }
    }
}
