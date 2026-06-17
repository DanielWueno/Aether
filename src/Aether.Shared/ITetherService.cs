namespace Aether.Shared;

public interface ITetherService
{
    Task StartAsync(string deviceSerial);
    Task StopAsync();
    string ManualProxy { get; set; }
    event Action<DeviceInfo>? DeviceStatusChanged;
}
