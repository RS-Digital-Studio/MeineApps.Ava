using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.11 (MVP): Multi-User via simples TCP-Newline-JSON-Protokoll.
/// Hub oeffnet einen <see cref="TcpListener"/> auf dem konfigurierten Port; Clients
/// verbinden mit <see cref="TcpClient"/>. Punkt-Events werden als NDJSON (ein JSON-
/// Object pro Zeile) gesendet — robust gegen WiFi-Direct-Lag und kein zusaetzliches
/// SignalR-Package noetig. Default-Port 5119.</summary>
public sealed class LocalTcpMultiUserService : IMultiUserSessionService, IDisposable
{
    public string DeviceName { get; }
    public bool IsConnected => _hostListener != null || _clientStream != null;
    public IReadOnlyList<string> ConnectedDevices => _connectedDevices.ToArray();
    public event Action<MultiUserPointEvent>? RemotePointAdded;

    private TcpListener? _hostListener;
    private CancellationTokenSource? _hostCts;
    private readonly List<TcpClient> _hostClients = [];
    private readonly object _hostClientsLock = new();

    private TcpClient? _clientTcp;
    private NetworkStream? _clientStream;
    private CancellationTokenSource? _clientCts;

    private readonly List<string> _connectedDevices = [];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public LocalTcpMultiUserService()
    {
        DeviceName = Environment.MachineName;
        _connectedDevices.Add(DeviceName);
    }

    public Task<string> StartHostAsync(int port = 5119, CancellationToken ct = default)
    {
        if (_hostListener != null) return Task.FromResult(GetLocalUrl(port));

        _hostListener = new TcpListener(IPAddress.Any, port);
        _hostListener.Start();
        _hostCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => AcceptLoopAsync(_hostCts.Token));
        return Task.FromResult(GetLocalUrl(port));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _hostListener != null)
        {
            TcpClient client;
            try { client = await _hostListener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { continue; }

            lock (_hostClientsLock) _hostClients.Add(client);
            _ = Task.Run(() => ReadClientLoopAsync(client, ct));
        }
    }

    private async Task ReadClientLoopAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                HandleIncomingLine(line);
            }
        }
        catch { /* Connection lost */ }
        finally
        {
            lock (_hostClientsLock) _hostClients.Remove(client);
            try { client.Close(); } catch { }
        }
    }

    public async Task ConnectAsync(string url, CancellationToken ct = default)
    {
        // url-Format: http://<ip>:<port>/sm  oder  <ip>:<port>
        var (host, port) = ParseUrl(url);
        _clientTcp = new TcpClient();
        await _clientTcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
        _clientStream = _clientTcp.GetStream();
        _clientCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => ReadServerLoopAsync(_clientCts.Token));
    }

    private async Task ReadServerLoopAsync(CancellationToken ct)
    {
        if (_clientStream == null) return;
        try
        {
            using var reader = new StreamReader(_clientStream, Encoding.UTF8, leaveOpen: true);
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                HandleIncomingLine(line);
            }
        }
        catch { /* Connection lost */ }
    }

    private void HandleIncomingLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        try
        {
            var envelope = JsonSerializer.Deserialize<PointEnvelope>(line, JsonOpts);
            if (envelope?.Point == null) return;
            RemotePointAdded?.Invoke(new MultiUserPointEvent(
                envelope.SenderDeviceName ?? "Unknown",
                envelope.Point,
                DateTime.UtcNow));
        }
        catch { /* defektes Paket ignorieren */ }
    }

    public async Task DisconnectAsync()
    {
        try { _hostCts?.Cancel(); } catch { }
        try { _hostListener?.Stop(); } catch { }
        _hostListener = null;
        _hostCts = null;
        lock (_hostClientsLock)
        {
            foreach (var c in _hostClients) try { c.Close(); } catch { }
            _hostClients.Clear();
        }

        try { _clientCts?.Cancel(); } catch { }
        try { _clientStream?.Dispose(); } catch { }
        try { _clientTcp?.Close(); } catch { }
        _clientStream = null;
        _clientTcp = null;
        _clientCts = null;

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task BroadcastPointAsync(SurveyPoint point, CancellationToken ct = default)
    {
        var envelope = new PointEnvelope(DeviceName, point);
        var json = JsonSerializer.Serialize(envelope, JsonOpts) + "\n";
        var bytes = Encoding.UTF8.GetBytes(json);

        // Host: an alle Clients
        List<TcpClient> snapshot;
        lock (_hostClientsLock) snapshot = [.. _hostClients];
        foreach (var c in snapshot)
        {
            try { await c.GetStream().WriteAsync(bytes, ct).ConfigureAwait(false); }
            catch { /* einzelner Client tot — ignorieren */ }
        }

        // Client: an Server
        if (_clientStream != null)
        {
            try { await _clientStream.WriteAsync(bytes, ct).ConfigureAwait(false); }
            catch { /* Server tot — ignorieren */ }
        }
    }

    private static string GetLocalUrl(int port)
    {
        var local = Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        return $"tcp://{local?.ToString() ?? "127.0.0.1"}:{port}";
    }

    private static (string host, int port) ParseUrl(string url)
    {
        var trimmed = url.Replace("tcp://", "").Replace("http://", "").Replace("https://", "");
        var slash = trimmed.IndexOf('/');
        if (slash > 0) trimmed = trimmed[..slash];
        var parts = trimmed.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 5119;
        return (host, port);
    }

    public void Dispose()
    {
        try { DisconnectAsync().GetAwaiter().GetResult(); } catch { }
    }

    /// <summary>Wire-Format der NDJSON-Pakete.</summary>
    private sealed record PointEnvelope(string SenderDeviceName, SurveyPoint Point);
}
