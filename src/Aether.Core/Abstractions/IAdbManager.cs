using SharpAdbClient;

namespace Aether.Core.Abstractions;

public interface IAdbManager
{
    IEnumerable<DeviceData> GetConnectedDevices();
    void CreateReverseTunnel(DeviceData device, int remotePort, int localPort);
    void RemoveAllReverseTunnels(DeviceData device);
    void ExecuteCommand(string command, DeviceData device);
    string GetProxySettings(DeviceData device);
    void StartServer();
}
