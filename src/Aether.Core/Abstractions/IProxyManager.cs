namespace Aether.Core.Abstractions;

public interface IProxyManager
{
    void Start(int port);
    void Stop();
    bool IsRunning { get; }
    string ManualProxy { get; set; }
}
