using SkiaSharp;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert die City-Skyline als AI-Hintergrundbild mit Wetter-Overlay.
/// Kein prozeduraler Fallback, kein HitTest (statisches Bild).
/// </summary>
public sealed class CityRenderer : IDisposable
{
    private bool _disposed;

    // AI-Hintergrund
    private IGameAssetService? _assetService;
    private SKBitmap? _cityBackground;
    private bool _cityBackgroundLoaded;

    // Wetter-System (saisonale Effekte über der City-Szene)
    private readonly CityWeatherSystem _weatherSystem = new();
    private bool _weatherInitialized;

    public void Initialize(IGameAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// Rendert die City-Ansicht: AI-Bitmap + Wetter-Overlay.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, GameState state, List<Building> buildings, float deltaTime = 0.016f)
    {
        // Wetter-System (nur bei Medium/High Grafik)
        var gfxQuality = state.GraphicsQuality;
        if (gfxQuality >= GraphicsQuality.Medium)
        {
            if (!_weatherInitialized)
            {
                _weatherSystem.SetWeatherByMonth();
                _weatherInitialized = true;
            }
            _weatherSystem.Update(deltaTime);
        }

        // AI-Hintergrund laden und zeichnen
        if (!_cityBackgroundLoaded && _assetService != null)
        {
            _cityBackground = _assetService.GetBitmap("city/city_background.webp");
            if (_cityBackground == null)
                _ = _assetService.LoadBitmapAsync("city/city_background.webp");
            else
                _cityBackgroundLoaded = true;
        }

        if (_cityBackground != null)
            canvas.DrawBitmap(_cityBackground, bounds);

        // Wetter-Overlay über dem Hintergrund
        if (gfxQuality >= GraphicsQuality.Medium)
            _weatherSystem.Render(canvas, bounds);
    }

    /// <summary>
    /// Bestimmt die Welt-Stufe basierend auf Spieler-Level (1-8).
    /// </summary>
    public static int GetWorldTier(int playerLevel)
    {
        if (playerLevel <= 10) return 1;
        if (playerLevel <= 25) return 2;
        if (playerLevel <= 50) return 3;
        if (playerLevel <= 100) return 4;
        if (playerLevel <= 250) return 5;
        if (playerLevel <= 500) return 6;
        if (playerLevel <= 1000) return 7;
        return 8;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _weatherSystem?.Dispose();
    }
}
