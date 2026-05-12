using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Zentrale Farb-Konstanten fuer BomberBlast-Renderer (Audit L02).
///
/// <para>Ersetzt wiederkehrende <c>new SKColor(...)</c>-Literale in Render-Code. Welt- und
/// PowerUp-spezifische Einzelfarben bleiben thematisch in ihren Dateien — hier nur die
/// brand-/markenrelevanten und mehrfach verwendeten Farben.</para>
///
/// <para>Synchron mit <c>Themes/AppPalette.axaml</c> (XAML-DynamicResource-Keys). Bei
/// Aenderungen muessen beide Stellen gemeinsam aktualisiert werden.</para>
/// </summary>
public static class BomberBlastColors
{
    // ═══════════════════════════════════════════════════════════════════════
    // BRAND PRIMARY — Synchron mit AppPalette.PrimaryColor (#FF6B35)
    // ═══════════════════════════════════════════════════════════════════════
    public static readonly SKColor Primary = new(0xFF, 0x6B, 0x35);
    public static readonly SKColor PrimaryHover = new(0xFF, 0x88, 0x55);
    public static readonly SKColor PrimaryPressed = new(0xE0, 0x55, 0x20);

    // ═══════════════════════════════════════════════════════════════════════
    // ACCENT — Synchron mit AppPalette.AccentColor / Feature-Accents
    // ═══════════════════════════════════════════════════════════════════════
    public static readonly SKColor AccentCyan = new(0x22, 0xD3, 0xEE);
    public static readonly SKColor PowerUpCyan = new(0x00, 0xE5, 0xFF); // Shield/PowerUp-Akzent
    public static readonly SKColor PowerUpCyanAlt = new(0x38, 0xE8, 0xFF);

    // ═══════════════════════════════════════════════════════════════════════
    // PREMIUM / REWARD — Gold + Glow
    // ═══════════════════════════════════════════════════════════════════════
    public static readonly SKColor Gold = new(0xFF, 0xD7, 0x00); // Premium / Legendary / Highlight
    public static readonly SKColor GoldGlow = new(0xFF, 0xE0, 0x82);
    public static readonly SKColor GoldDeep = new(0xFF, 0xDD, 0x33); // Secondary-Brand

    // ═══════════════════════════════════════════════════════════════════════
    // SEMANTIC — synchron mit AppPalette.{Success/Warning/Error/Info}Color
    // ═══════════════════════════════════════════════════════════════════════
    public static readonly SKColor Success = new(0x22, 0xC5, 0x5E);
    public static readonly SKColor Warning = new(0xF5, 0x9E, 0x0B);
    public static readonly SKColor Error = new(0xEF, 0x44, 0x44);
    public static readonly SKColor Info = new(0x3B, 0x82, 0xF6);

    // ═══════════════════════════════════════════════════════════════════════
    // ECONOMY — synchron mit AppPalette.{Coin/Gem}AccentColor
    // ═══════════════════════════════════════════════════════════════════════
    public static readonly SKColor CoinAmber = new(0xF5, 0x9E, 0x0B);
    public static readonly SKColor GemCyan = new(0x00, 0xBC, 0xD4);

    // ═══════════════════════════════════════════════════════════════════════
    // BACKGROUND / SURFACE
    // ═══════════════════════════════════════════════════════════════════════
    public static readonly SKColor Background = new(0x14, 0x14, 0x22);
    public static readonly SKColor Surface = new(0x1C, 0x1C, 0x35);
    public static readonly SKColor Card = new(0x28, 0x28, 0x45);

    // ═══════════════════════════════════════════════════════════════════════
    // NEUTRAL — fuer FloatingText/Subtitles
    // ═══════════════════════════════════════════════════════════════════════
    public static readonly SKColor White = SKColors.White;
    public static readonly SKColor Black = SKColors.Black;
    public static readonly SKColor TextPrimary = new(0xF0, 0xF0, 0xFF);
    public static readonly SKColor TextSecondary = new(0xB0, 0xB0, 0xCC);
    public static readonly SKColor TextMuted = new(0x80, 0x80, 0xAA);
}
