namespace BomberBlast.Services;

using SkiaSharp;

/// <summary>
/// Lädt und cached AI-generierte Bitmap-Assets (WebP).
/// LRU-Cache mit konfigurierbarem Speicherlimit.
/// </summary>
public interface IGameAssetService : IDisposable
{
    /// <summary>
    /// Gibt gecachtes Bitmap zurück oder null wenn nicht geladen.
    /// Caller darf das Bitmap NICHT disposen!
    /// </summary>
    SKBitmap? GetBitmap(string assetPath);

    /// <summary>
    /// Gibt gecachtes Bitmap zurück. Bei Cache-Miss wird async Laden getriggert
    /// und null zurückgegeben (nächster Aufruf hat das Bitmap dann).
    /// Ideal für Renderer die pro Frame aufgerufen werden.
    /// </summary>
    SKBitmap? GetOrLoadBitmap(string assetPath);

    /// <summary>
    /// Lädt Asset asynchron und cached es. Gibt das Bitmap zurück.
    /// </summary>
    Task<SKBitmap?> LoadBitmapAsync(string assetPath);

    /// <summary>
    /// Vorladen mehrerer Assets (z.B. beim Szenen-Wechsel).
    /// </summary>
    Task PreloadAsync(IEnumerable<string> assetPaths);

    /// <summary>
    /// Entfernt ein Asset aus dem Cache.
    /// </summary>
    void Evict(string assetPath);

    /// <summary>
    /// Entfernt alle Assets aus dem Cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Aktueller RAM-Verbrauch des Caches in Bytes.
    /// </summary>
    long CacheSizeBytes { get; }
}
