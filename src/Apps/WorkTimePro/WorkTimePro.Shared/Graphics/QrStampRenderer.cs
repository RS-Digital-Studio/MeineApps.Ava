using Net.Codecrete.QrCodeGenerator;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// Stempel-QR-Code: enthält den Deep-Link "worktimepro://stamp". Beim Scannen mit der
/// Geräte-Kamera öffnet Android die App und stempelt automatisch ein bzw. aus
/// (Intent-Filter in MainActivity → MainViewModel.HandleStampScanAsync).
/// </summary>
public static class QrStampRenderer
{
    /// <summary>
    /// Deep-Link-URI im QR-Code. Der Intent-Filter in MainActivity.cs
    /// (DataScheme/DataHost) muss dazu passen.
    /// </summary>
    public const string StampUri = "worktimepro://stamp";

    /// <summary>Ruhezone in Modulen je Seite (QR-Spezifikation empfiehlt 4, 2 reicht für Bildschirm/Druck mit weißem Umfeld).</summary>
    private const int QuietZone = 2;

    private static readonly SKPaint _modulePaint = new() { Color = SKColors.Black, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _backgroundPaint = new() { Color = SKColors.White, Style = SKPaintStyle.Fill };

    // Inhalt ist konstant → QR-Matrix einmal berechnen und cachen
    private static readonly QrCode _qr = QrCode.EncodeText(StampUri, QrCode.Ecc.Medium);

    /// <summary>
    /// Zeichnet den QR-Code (mit Ruhezone) zentriert in den Zeichenbereich.
    /// Bewusst Schwarz auf Weiß statt Theme-Farben: QR-Codes brauchen maximalen
    /// Kontrast für Scanner und werden ausgedruckt.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds)
    {
        int gridSize = _qr.Size + QuietZone * 2;

        var modulePx = MathF.Floor(MathF.Min(bounds.Width, bounds.Height) / gridSize);
        if (modulePx < 1f) modulePx = 1f;
        var total = modulePx * gridSize;
        var left = bounds.MidX - total / 2f;
        var top = bounds.MidY - total / 2f;

        canvas.DrawRect(left, top, total, total, _backgroundPaint);

        for (var y = 0; y < _qr.Size; y++)
        {
            for (var x = 0; x < _qr.Size; x++)
            {
                if (!_qr.GetModule(x, y)) continue;
                canvas.DrawRect(
                    left + (x + QuietZone) * modulePx,
                    top + (y + QuietZone) * modulePx,
                    modulePx, modulePx, _modulePaint);
            }
        }
    }

    /// <summary>
    /// Erzeugt den QR-Code als PNG (zum Teilen/Ausdrucken).
    /// </summary>
    public static byte[] CreatePngBytes(int pixelSize = 1024)
    {
        using var surface = SKSurface.Create(new SKImageInfo(pixelSize, pixelSize));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        Render(canvas, new SKRect(0, 0, pixelSize, pixelSize));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
