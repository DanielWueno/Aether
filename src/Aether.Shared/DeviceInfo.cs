namespace Aether.Shared;

public class DeviceInfo
{
    public string Serial { get; set; } = string.Empty;
    public string Model { get; set; } = "Unknown Device";
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;
}
