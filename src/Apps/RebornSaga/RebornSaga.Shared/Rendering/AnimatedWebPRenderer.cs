namespace RebornSaga.Rendering;

using SkiaSharp;
using System;
using System.IO;

/// <summary>
/// Rendert Animated WebP Dateien Frame-für-Frame mit SKCodec.
/// Verwendung für CG-Szenen und Cutscenes.
/// Frame-Bitmap wird gecacht und nur bei Frame-Wechsel neu dekodiert.
/// </summary>
public class AnimatedWebPRenderer : IDisposable
{
    private static readonly SKPaint _renderPaint = new()
    {
        IsAntialias = true
    };

    private SKCodec? _codec;
    private SKBitmap? _currentFrame;
    private SKCodecFrameInfo[]? _frameInfos;
    private int _frameCount;
    private int _currentFrameIndex;
    private float _frameTimeAccumulator;
    private float _currentFrameDurationSec;
    private bool _disposed;

    /// <summary>Ob die Animation abgeschlossen ist (nur relevant wenn nicht looping).</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Endlos-Loop oder einmalige Wiedergabe.</summary>
    public bool IsLooping { get; set; }

    /// <summary>Ob ein Animated WebP geladen ist.</summary>
    public bool IsLoaded => _codec != null && _frameCount > 0;

    /// <summary>Anzahl der Frames im geladenen WebP.</summary>
    public int FrameCount => _frameCount;

    /// <summary>
    /// Lädt ein Animated WebP aus einem Stream.
    /// </summary>
    public void Load(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        Cleanup();

        var codec = SKCodec.Create(stream);
        if (codec == null) throw new InvalidOperationException("SKCodec konnte den Stream nicht dekodieren.");

        InitializeFromCodec(codec);
    }

    /// <summary>
    /// Lädt ein Animated WebP aus einem bereits erstellten SKCodec.
    /// Der Renderer übernimmt Ownership und disposed den Codec.
    /// </summary>
    public void Load(SKCodec codec)
    {
        if (codec == null) throw new ArgumentNullException(nameof(codec));

        Cleanup();
        InitializeFromCodec(codec);
    }

    /// <summary>
    /// Aktualisiert das Frame-Timing. Wechselt zum nächsten Frame wenn die Dauer abgelaufen ist.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!IsLoaded || IsFinished) return;

        _frameTimeAccumulator += deltaTime;

        // Frame(s) vorrücken solange genug Zeit akkumuliert ist
        while (_frameTimeAccumulator >= _currentFrameDurationSec)
        {
            _frameTimeAccumulator -= _currentFrameDurationSec;

            var nextIndex = _currentFrameIndex + 1;

            if (nextIndex >= _frameCount)
            {
                // Letzter Frame erreicht
                if (IsLooping)
                {
                    nextIndex = 0;
                }
                else
                {
                    IsFinished = true;
                    _frameTimeAccumulator = 0;
                    return;
                }
            }

            SetFrame(nextIndex);
        }
    }

    /// <summary>
    /// Zeichnet den aktuellen Frame skaliert in das Ziel-Rechteck.
    /// </summary>
    public void Draw(SKCanvas canvas, SKRect destRect)
    {
        if (_currentFrame == null) return;

        canvas.DrawBitmap(_currentFrame, destRect, _renderPaint);
    }

    /// <summary>
    /// Setzt die Animation auf den ersten Frame zurück.
    /// </summary>
    public void Reset()
    {
        if (!IsLoaded) return;

        IsFinished = false;
        _frameTimeAccumulator = 0;
        SetFrame(0);
    }

    /// <summary>
    /// Gibt alle nativen Ressourcen frei (SKCodec, SKBitmap).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Cleanup();
    }

    /// <summary>
    /// Initialisiert den Renderer mit einem SKCodec. Dekodiert den ersten Frame.
    /// </summary>
    private void InitializeFromCodec(SKCodec codec)
    {
        _codec = codec;
        _frameCount = codec.FrameCount;

        // Frame-Infos auslesen
        if (_frameCount > 0)
        {
            _frameInfos = new SKCodecFrameInfo[_frameCount];
            for (int i = 0; i < _frameCount; i++)
                codec.GetFrameInfo(i, out _frameInfos[i]);
        }
        else
        {
            // Einzelbild-WebP (kein animiertes): als 1-Frame-Animation behandeln
            _frameCount = 1;
            _frameInfos = null;
        }

        IsFinished = false;
        _frameTimeAccumulator = 0;

        // Bitmap für Frame-Rendering vorbereiten
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);
        _currentFrame = new SKBitmap(info);

        // Ersten Frame dekodieren
        SetFrame(0);
    }

    /// <summary>
    /// Dekodiert einen bestimmten Frame in die gecachte Bitmap.
    /// </summary>
    private void SetFrame(int frameIndex)
    {
        if (_codec == null || _currentFrame == null) return;

        _currentFrameIndex = frameIndex;

        // Frame dekodieren
        var options = new SKCodecOptions(frameIndex);
        _codec.GetPixels(_currentFrame.Info, _currentFrame.GetPixels(), options);

        // Frame-Dauer aktualisieren (Millisekunden → Sekunden)
        if (_frameInfos != null && frameIndex < _frameInfos.Length)
        {
            var durationMs = _frameInfos[frameIndex].Duration;
            // Manche WebPs haben 0ms Dauer → Fallback auf ~16ms (60fps)
            _currentFrameDurationSec = durationMs > 0 ? durationMs / 1000f : 0.016f;
        }
        else
        {
            // Einzelbild: unendliche Dauer (kein automatischer Frame-Wechsel)
            _currentFrameDurationSec = float.MaxValue;
        }
    }

    /// <summary>
    /// Räumt geladene Ressourcen auf (für Reload oder Dispose).
    /// </summary>
    private void Cleanup()
    {
        _currentFrame?.Dispose();
        _currentFrame = null;

        _codec?.Dispose();
        _codec = null;

        _frameInfos = null;
        _frameCount = 0;
        _currentFrameIndex = 0;
        _frameTimeAccumulator = 0;
        _currentFrameDurationSec = 0;
        IsFinished = false;
    }
}
