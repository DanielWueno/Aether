using SharpAdbClient;
using Aether.Core.Abstractions;

namespace Aether.Core.Services;

public class AdbManager : IAdbManager
{
    private readonly AdbClient _client;

    public AdbManager()
    {
        _client = new AdbClient();
    }

    public void StartServer()
    {
        string adbPath = ResolveAdbPath();
        var server = new AdbServer();
        server.StartServer(adbPath, restartServerIfNewer: false);
    }

    private string ResolveAdbPath()
    {
        // 0. Priority: Local 'adb' folder in the application directory (Portability)
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string localPath = Path.Combine(baseDir, "adb", "adb.exe");
        if (File.Exists(localPath)) return localPath;

        // 1. Try to find in PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, "adb.exe");
            if (File.Exists(fullPath)) return fullPath;
        }

        // 2. Common Android SDK locations on Windows
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] commonPaths = {
            Path.Combine(localAppData, @"Android\Sdk\platform-tools\adb.exe"),
            @"C:\adb\adb.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path)) return path;
        }

        // 3. Fallback to just "adb.exe" if everything fails (might still fail SharpAdbClient check)
        return "adb.exe";
    }

    public IEnumerable<DeviceData> GetConnectedDevices()
    {
        return _client.GetDevices();
    }

    public void CreateReverseTunnel(DeviceData device, int remotePort, int localPort)
    {
        _client.CreateReverseForward(device, $"tcp:{remotePort}", $"tcp:{localPort}", true);
    }

    public void RemoveAllReverseTunnels(DeviceData device)
    {
        _client.RemoveAllReverseForwards(device);
    }

    public void ExecuteCommand(string command, DeviceData device)
    {
        _client.ExecuteRemoteCommand(command, device, null);
    }

    public string GetProxySettings(DeviceData device)
    {
        var receiver = new ConsoleOutputReceiver();
        _client.ExecuteRemoteCommand("settings get global http_proxy", device, receiver);
        return receiver.ToString().Trim();
    }
}
