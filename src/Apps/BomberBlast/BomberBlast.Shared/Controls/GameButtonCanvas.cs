using Avalonia;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using BomberBlast.Graphics;
using SkiaSharp;

namespace BomberBlast.Controls;

/// <summary>
/// SkiaSharp-basierter "Torn Metal" Button-Hintergrund.
/// Erzeugt einen prozedural beschaedigten Metall-Look per Button.
/// Deterministisch: gleicher Seed → gleiches Muster (aber jeder Button einzigartig).
///
/// Verwendung in XAML:
/// <![CDATA[
/// <Panel>
///   <controls:GameButtonCanvas ButtonColor="#00BCD4" DamageLevel="0.5" ButtonSeed="42" />
///   <TextBlock Text="Play" HorizontalAlignment="Center" VerticalAlignment="Center" />
/// </Panel>
/// ]]>
///
/// Farb-Varianten:
/// - Primary (Cyan #00BCD4): Standard-Aktionen
/// - Gold (#FFD700): Premium/Belohnungen
/// - Rot (#F44336): Gefahr/Destruktiv
/// - Gruen (#4CAF50): Erfolg/Bestaetigung
/// - Violett (#9C27B0): Dungeon/Spezial
/// - Orange (#FF9800): Warnung/Sekundaer
/// </summary>
public class GameButtonCanvas : SKCanvasView
{
    // === StyledProperties fuer XAML-Binding ===

    /// <summary>Basis-Metallfarbe des Buttons</summary>
    public static readonly StyledProperty<Color> ButtonColorProperty =
        AvaloniaProperty.Register<GameButtonCanvas, Color>(nameof(ButtonColor), Colors.Teal);

    /// <summary>Schadens-Intensitaet (0.0 = subtil, 1.0 = stark beschaedigt)</summary>
    public static readonly StyledProperty<double> DamageLevelProperty =
        AvaloniaProperty.Register<GameButtonCanvas, double>(nameof(DamageLevel), 0.5);

    /// <summary>Deterministischer Seed fuer das Riss-Muster (jeder Button braucht einen eigenen)</summary>
    public static readonly StyledProperty<int> ButtonSeedProperty =
        AvaloniaProperty.Register<GameButtonCanvas, int>(nameof(ButtonSeed), 0);

    public Color ButtonColor
    {
        get => GetValue(ButtonColorProperty);
        set => SetValue(ButtonColorProperty, value);
    }

    public double DamageLevel
    {
        get => GetValue(DamageLevelProperty);
        set => SetValue(DamageLevelProperty, value);
    }

    public int ButtonSeed
    {
        get => GetValue(ButtonSeedProperty);
        set => SetValue(ButtonSeedProperty, value);
    }

    static GameButtonCanvas()
    {
        // Bei Property-Aenderung neu zeichnen
        ButtonColorProperty.Changed.AddClassHandler<GameButtonCanvas>((x, _) => x.InvalidateSurface());
        DamageLevelProperty.Changed.AddClassHandler<GameButtonCanvas>((x, _) => x.InvalidateSurface());
        ButtonSeedProperty.Changed.AddClassHandler<GameButtonCanvas>((x, _) => x.InvalidateSurface());
    }

    public GameButtonCanvas()
    {
        // Kein Hit-Test - der eigentliche Button liegt darueber
        IsHitTestVisible = false;
        PaintSurface += OnPaintSurface;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        var bounds = canvas.LocalClipBounds;

        if (bounds.Width < 4 || bounds.Height < 4) return;

        // Avalonia Color → SkiaSharp SKColor
        var color = new SKColor(ButtonColor.R, ButtonColor.G, ButtonColor.B, ButtonColor.A);

        TornMetalRenderer.Render(
            canvas,
            bounds.Width,
            bounds.Height,
            color,
            ButtonSeed,
            (float)DamageLevel);
    }
}
