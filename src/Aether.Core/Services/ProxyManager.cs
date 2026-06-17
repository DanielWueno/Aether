using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using Aether.Core.Abstractions;

namespace Aether.Core.Services;

public class ProxyManager : IProxyManager, IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    public bool IsRunning { get; private set; }
    public string ManualProxy { get; set; } = string.Empty;

    public void Start(int port)
    {
        if (IsRunning) return;

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _cts = new CancellationTokenSource();
        IsRunning = true;

        Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        Debug.WriteLine($"[Aether] Chained Relay active on port {port}");
    }

    private async Task AcceptConnectionsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                _ = HandleChainedClientAsync(client, token);
            }
            catch { break; }
        }
    }

    private async Task HandleChainedClientAsync(TcpClient client, CancellationToken token)
    {
        string host = "unknown";
        int port = 0;
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[8192];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead <= 0) return;

                var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                bool isHttps = request.StartsWith("CONNECT", StringComparison.OrdinalIgnoreCase);

                if (isHttps)
                {
                    var target = request.Split(' ')[1].Split(':');
                    host = target[0];
                    port = target.Length > 1 ? int.Parse(target[1]) : 443;
                }
                else
                {
                    var match = System.Text.RegularExpressions.Regex.Match(request, @"Host:\s*([^\r\n]+)");
                    if (match.Success)
                    {
                        var target = match.Groups[1].Value.Trim().Split(':');
                        host = target[0];
                        port = target.Length > 1 ? int.Parse(target[1]) : 80;
                    }
                }

                if (string.IsNullOrEmpty(host) || host == "unknown") return;

                // Detectar el proxy de la empresa de forma más agresiva
                Uri? proxyUri = null;
                var systemProxy = WebRequest.GetSystemWebProxy();
                Uri targetUri = new Uri($"{(isHttps ? "https" : "http")}://{host}:{port}");
                
                // Intento 0: Proxy Manual desde la UI
                if (!string.IsNullOrWhiteSpace(ManualProxy))
                {
                    string mProxy = ManualProxy.Trim();
                    if (!mProxy.StartsWith("http")) mProxy = "http://" + mProxy;
                    proxyUri = new Uri(mProxy);
                }

                // Intento 1: GetSystemWebProxy (Lee la configuración de Windows IE/Chrome)
                if (proxyUri == null)
                {
                    var tempUri = systemProxy.GetProxy(targetUri);
                    if (tempUri != targetUri) proxyUri = tempUri;
                }

                // Intento 2: Variables de entorno (Común en empresas)
                if (proxyUri == null)
                {
                    string envProxy = Environment.GetEnvironmentVariable("HTTP_PROXY") ?? Environment.GetEnvironmentVariable("http_proxy") ?? "";
                    if (!string.IsNullOrEmpty(envProxy))
                    {
                        if (!envProxy.StartsWith("http")) envProxy = "http://" + envProxy;
                        proxyUri = new Uri(envProxy);
                    }
                }

                using (var targetClient = new TcpClient())
                {
                    // Si encontramos un proxy corporativo
                    if (proxyUri != null)
                    {
                        await targetClient.ConnectAsync(proxyUri.Host, proxyUri.Port, token);
                        using (var targetStream = targetClient.GetStream())
                        {
                            // Le pedimos al proxy de la empresa que nos conecte al destino
                            var chainRequest = Encoding.ASCII.GetBytes($"CONNECT {host}:{port} HTTP/1.1\r\nHost: {host}:{port}\r\nProxy-Connection: Keep-Alive\r\n\r\n");
                            await targetStream.WriteAsync(chainRequest, 0, chainRequest.Length, token);

                            // Leer respuesta del proxy corporativo
                            var respBuf = new byte[8192];
                            int respBytes = await targetStream.ReadAsync(respBuf, 0, respBuf.Length, token);
                            var resp = Encoding.ASCII.GetString(respBuf, 0, respBytes);

                            if (resp.Contains("200"))
                            {
                                Debug.WriteLine($"[Chain] TUNNELED via {proxyUri.Host} -> {host}:{port}");
                                await EstablishRelay(stream, targetStream, isHttps, buffer, bytesRead, token);
                            }
                            else
                            {
                                Debug.WriteLine($"[Chain] REJECTED by Proxy ({proxyUri.Host}): {host} -> {resp.Split("\r\n")[0]}");
                                // Opcional: Fallback a conexión directa si el proxy lo rechaza
                            }
                        }
                    }
                    else
                    {
                        // Conexión directa si ABSOLUTAMENTE no hay proxy configurado en Windows
                        await targetClient.ConnectAsync(host, port, token);
                        using (var targetStream = targetClient.GetStream())
                        {
                            Debug.WriteLine($"[Chain] DIRECT Connection -> {host}:{port}");
                            await EstablishRelay(stream, targetStream, isHttps, buffer, bytesRead, token);
                        }
                    }
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[Chain Error] {host} -> {ex.Message}"); }
    }

    private async Task EstablishRelay(NetworkStream clientStream, NetworkStream targetStream, bool isHttps, byte[] initialBuffer, int initialBytes, CancellationToken token)
    {
        if (isHttps)
        {
            var success = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
            await clientStream.WriteAsync(success, 0, success.Length, token);
        }
        else
        {
            await targetStream.WriteAsync(initialBuffer, 0, initialBytes, token);
        }

        var t1 = clientStream.CopyToAsync(targetStream, token);
        var t2 = targetStream.CopyToAsync(clientStream, token);
        await Task.WhenAny(t1, t2);
    }

    public void Stop() { _cts?.Cancel(); _listener?.Stop(); IsRunning = false; }
    public void Dispose() => Stop();
}
