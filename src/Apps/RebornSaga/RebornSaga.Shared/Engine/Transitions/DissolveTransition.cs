namespace RebornSaga.Engine.Transitions;

using SkiaSharp;
using System;

/// <summary>
/// Partikel-Auflösung: Die alte Szene "zerfällt" in Pixel-Blöcke.
/// Eleganter Übergang (800ms) für Story-Szenen.
/// </summary>
public class DissolveTransition : TransitionEffect
{
    private static readonly SKPaint _blockPaint = new() { IsAntialias = false };
    private const int BlockSize = 8;
    private static readonly Random _random = new();

    // Vorgeneriertes Noise-Pattern für deterministische Auflösung
    private float[]? _noiseGrid;
    private int _gridCols;
    private int _gridRows;

    public DissolveTransition() : base(0.8f) { }

    public override void Render(SKCanvas canvas, SKRect bounds,
        Action<SKCanvas, SKRect> renderOldScene,
        Action<SKCanvas, SKRect> renderNewScene)
    {
        var eased = Ease(Progress);

        // Noise-Grid beim ersten Aufruf erstellen
        var cols = (int)MathF.Ceiling(bounds.Width / BlockSize);
        var rows = (int)MathF.Ceiling(bounds.Height / BlockSize);
        if (_noiseGrid == null || _gridCols != cols || _gridRows != rows)
        {
            _gridCols = cols;
            _gridRows = rows;
            _noiseGrid = new float[cols * rows];
            for (int i = 0; i < _noiseGrid.Length; i++)
                _noiseGrid[i] = (float)_random.NextDouble();
        }

        // Neue Szene als Hintergrund
        renderNewScene(canvas, bounds);

        if (eased >= 1f) return;

        // Alte Szene als Overlay - Blöcke die noch nicht aufgelöst sind
        canvas.Save();
        // Clip auf die noch sichtbaren Blöcke
        using var clipPath = new SKPath();

        for (int row = 0; row < _gridRows; row++)
        {
            for (int col = 0; col < _gridCols; col++)
            {
                var noise = _noiseGrid[row * _gridCols + col];
                // Block verschwindet wenn eased seinen Noise-Schwellwert übersteigt
                if (noise > eased)
                {
                    var x = bounds.Left + col * BlockSize;
                    var y = bounds.Top + row * BlockSize;
                    clipPath.AddRect(new SKRect(x, y, x + BlockSize, y + BlockSize));
                }
            }
        }

        canvas.ClipPath(clipPath);
        renderOldScene(canvas, bounds);
        canvas.Restore();
    }
}
