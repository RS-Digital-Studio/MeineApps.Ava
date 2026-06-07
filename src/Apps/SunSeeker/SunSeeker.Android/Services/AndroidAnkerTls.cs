using Java.Security;
using Javax.Net.Ssl;
using SunSeeker.Shared.Services.Anker;

namespace SunSeeker.Android.Services;

/// <summary>
/// Baut die mTLS-Verbindung zum Anker-MQTT-Broker NATIV über Androids <see cref="SSLContext"/> +
/// <see cref="KeyManagerFactory"/> auf. Notwendig, weil .NET-Android-<c>SslStream</c> keine
/// Client-Zertifikate beherrscht (Interop+AndroidCrypto+SslException). Liefert einen fertig
/// verbundenen Duplex-Stream zurück, den die plattformneutrale Anker-Logik via
/// <c>PreConnectedMqttAdapterFactory</c> an MQTTnet durchreicht.
/// </summary>
public static class AndroidAnkerTls
{
    public static Task<Stream> ConnectAsync(AnkerTlsParams p, CancellationToken ct)
        => Task.Run<Stream>(() =>
        {
            // Client-Zertifikat + privater Schlüssel (PKCS12) in einen Java-KeyStore laden.
            var clientStore = KeyStore.GetInstance("PKCS12")!;
            using (var pfx = new MemoryStream(p.ClientPkcs12))
                clientStore.Load(pfx, p.Pkcs12Password.ToCharArray());

            var kmf = KeyManagerFactory.GetInstance(KeyManagerFactory.DefaultAlgorithm!)!;
            kmf.Init(clientStore, p.Pkcs12Password.ToCharArray());

            // Server-Vertrauen: der Broker (AWS-IoT-Fronting) ist mit Amazon Root CA 1 signiert, die im
            // Android-System-Truststore liegt → Default-TrustManager (null) genügt.
            var sslContext = SSLContext.GetInstance("TLSv1.2")!;
            sslContext.Init(kmf.GetKeyManagers(), null, null);

            ct.ThrowIfCancellationRequested();
            var factory = sslContext.SocketFactory!;
            var socket = (SSLSocket)factory.CreateSocket(p.Host, p.Port)!;
            socket.StartHandshake(); // Handshake jetzt erzwingen → Fehler hier sichtbar statt später

            return new DuplexSocketStream(socket);
        }, ct);
}

/// <summary>
/// Duplex-<see cref="Stream"/> über einen verbundenen <see cref="SSLSocket"/> (getrennte In-/Out-Streams
/// von Java zu einem lesbaren+schreibbaren Stream zusammengefasst). Schließt beim Dispose den Socket.
/// </summary>
internal sealed class DuplexSocketStream(SSLSocket socket) : Stream
{
    private readonly Stream _in = socket.InputStream!;
    private readonly Stream _out = socket.OutputStream!;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => _in.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _in.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _in.ReadAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => _out.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _out.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _out.WriteAsync(buffer, cancellationToken);

    public override void Flush() => _out.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _out.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _in.Dispose(); } catch { /* ignore */ }
            try { _out.Dispose(); } catch { /* ignore */ }
            try { socket.Close(); } catch { /* ignore */ }
            try { socket.Dispose(); } catch { /* ignore */ }
        }
        base.Dispose(disposing);
    }
}
