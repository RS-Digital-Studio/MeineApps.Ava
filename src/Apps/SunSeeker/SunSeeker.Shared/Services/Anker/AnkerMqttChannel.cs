using System.Buffers;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using MQTTnet.Adapter;
using MQTTnet.Channel;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Formatter;

namespace SunSeeker.Shared.Services.Anker;

/// <summary>Parameter für den plattform-nativen mTLS-Verbindungsaufbau zum Anker-MQTT-Broker.</summary>
public sealed record AnkerTlsParams(string Host, int Port, byte[] ClientPkcs12, string Pkcs12Password, string RootCaPem);

/// <summary>
/// MQTTnet-Channel über einen BEREITS aufgebauten (TLS-)Stream. Notwendig, weil .NET-Android kein
/// Client-Zertifikat-mTLS über <c>SslStream</c> kann (Interop+AndroidCrypto+SslException) — der
/// TLS-Handshake läuft daher nativ (Android <c>SSLContext</c>/<c>KeyManager</c>), und dieser Channel
/// reicht MQTTnet nur noch den fertigen Stream durch. <see cref="ConnectAsync"/> ist ein No-Op
/// (der Stream ist bereits verbunden); <see cref="MQTTnet.Implementations.MqttTcpChannel"/> taugt
/// dafür nicht, weil dessen <c>ConnectAsync</c> auf die internen TCP-Optionen zugreift.
/// </summary>
public sealed class PreConnectedMqttChannel : IMqttChannel, IDisposable
{
    private readonly Stream _stream;
    private bool _disposed;

    public PreConnectedMqttChannel(Stream stream, EndPoint remoteEndPoint, X509Certificate2 clientCertificate)
    {
        _stream = stream;
        RemoteEndPoint = remoteEndPoint;
        ClientCertificate = clientCertificate;
    }

    public X509Certificate2 ClientCertificate { get; }
    public EndPoint RemoteEndPoint { get; }
    public bool IsSecureConnection => true;

    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_disposed) return 0;
        return await _stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(ReadOnlySequence<byte> buffer, bool isLargeBuffer, CancellationToken cancellationToken)
    {
        foreach (var segment in buffer)
            await _stream.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _stream.Dispose(); } catch { /* ignore */ }
    }
}

/// <summary>
/// Erzeugt einen <see cref="MqttChannelAdapter"/> über einen vorab verbundenen Stream statt selbst
/// TCP/TLS aufzubauen. Bildet die Default-Factory (<see cref="MQTTnet.Implementations.MqttClientAdapterFactory"/>)
/// 1:1 nach — nur der Channel wird ersetzt.
/// </summary>
public sealed class PreConnectedMqttAdapterFactory(Stream stream, EndPoint remoteEndPoint, X509Certificate2 clientCertificate)
    : IMqttClientAdapterFactory
{
    public IMqttChannelAdapter CreateClientAdapter(MqttClientOptions options, MqttPacketInspector packetInspector, IMqttNetLogger logger)
    {
        var channel = new PreConnectedMqttChannel(stream, remoteEndPoint, clientCertificate);
        var bufferWriter = new MqttBufferWriter(options.WriterBufferSize, options.WriterBufferSizeMax);
        var packetFormatterAdapter = new MqttPacketFormatterAdapter(options.ProtocolVersion, bufferWriter);
        return new MqttChannelAdapter(channel, packetFormatterAdapter, logger)
        {
            AllowPacketFragmentation = options.AllowPacketFragmentation,
            PacketInspector = packetInspector,
        };
    }
}
