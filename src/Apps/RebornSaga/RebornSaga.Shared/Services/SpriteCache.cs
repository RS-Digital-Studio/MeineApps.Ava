namespace RebornSaga.Services;

using RebornSaga.Rendering.Characters;
using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// LRU-Cache für Charakter-Sprites (Einzelbilder, kein Atlas).
/// Hält maximal <see cref="MaxCachedImages"/> Bilder gleichzeitig im Speicher.
/// Thread-safe über lock auf _syncRoot.
/// Berechnet Content-Bounds (nicht-transparenter Bereich) pro Sprite beim ersten Load.
/// </summary>
public sealed class SpriteCache : IDisposable
{
    /// <summary>
    /// Maximale Anzahl gleichzeitig gecachter Sprite-Bilder.
    /// 30 reicht fuer Dialogszenen (~15-20 Eintraege) mit LRU-Eviction.
    /// Hoehere Werte verbrauchen zu viel RAM auf Android (~9MB pro Sprite).
    /// </summary>
    public const int MaxCachedImages = 30;

    /// <summary>
    /// Maximale Cache-Groesse in Bytes (~80MB). Verhindert OOM auf Geraeten mit wenig RAM.
    /// Bei Ueberschreitung werden aelteste Eintraege verdraengt.
    /// </summary>
    public const long MaxCacheSizeBytes = 80 * 1024 * 1024;

    private long _currentCacheSize;

    private readonly IAssetDeliveryService _assets;
    private readonly object _syncRoot = new();

    // LRU-Tracking: zuletzt verwendeter Pfad vorne
    private readonly LinkedList<string> _lruOrder = new();
    // Lookup: relativePath → Cache-Eintrag + LRU-Node
    private readonly Dictionary<string, (SKBitmap Bitmap, LinkedListNode<string> Node)> _cache = new();

    // Content-Bounds Cache: Nicht-transparenter Bereich pro Sprite (einmal pro Load berechnet)
    private readonly Dictionary<string, SKRectI> _contentBounds = new();

    private bool _disposed;

    public SpriteCache(IAssetDeliveryService assets)
    {
        _assets = assets;
    }

    /// <summary>
    /// Gibt ein gecachtes Charakter-Sprite zurück (komplettes Bild für Pose+Emotion).
    /// Lädt bei Cache-Miss automatisch von Disk.
    /// Gibt null zurück wenn kein Asset vorhanden ist.
    /// </summary>
    public SKBitmap? GetSprite(string charId, Pose pose, Emotion emotion)
    {
        var path = SpriteAssetPaths.GetCharacterSpritePath(charId, pose, emotion);
        return GetBitmap(path);
    }

    /// <summary>
    /// Gibt das Blinzel-Overlay für einen Charakter zurück (gecacht).
    /// </summary>
    public SKBitmap? GetBlinkOverlay(string charId)
    {
        var path = SpriteAssetPaths.GetBlinkOverlayPath(charId);
        return GetBitmap(path);
    }

    /// <summary>
    /// Gibt das Mund-Overlay für einen Charakter zurück (gecacht).
    /// </summary>
    public SKBitmap? GetMouthOverlay(string charId, bool wide)
    {
        var path = SpriteAssetPaths.GetMouthOverlayPath(charId, wide);
        return GetBitmap(path);
    }

    /// <summary>
    /// Gibt ein Gegner-Sprite zurück (gecacht).
    /// </summary>
    public SKBitmap? GetEnemySprite(string enemyId)
    {
        var path = SpriteAssetPaths.GetEnemySpritePath(enemyId);
        return GetBitmap(path);
    }

    /// <summary>
    /// Gibt ein Hintergrund-Bild zurück (gecacht).
    /// </summary>
    public SKBitmap? GetBackground(string sceneKey)
    {
        var path = SpriteAssetPaths.GetBackgroundPath(sceneKey);
        return GetBitmap(path);
    }

    /// <summary>
    /// Gibt ein Item-Icon zurück (gecacht).
    /// </summary>
    public SKBitmap? GetItemIcon(string category, string itemId)
    {
        var path = SpriteAssetPaths.GetItemIconPath(category, itemId);
        return GetBitmap(path);
    }

    /// <summary>
    /// Gibt ein Map-Node-Icon zurück (gecacht). Pfad z.B. "map/nodes/boss.webp".
    /// </summary>
    public SKBitmap? GetMapNodeIcon(string nodeKey)
    {
        var path = $"{nodeKey}.webp";
        return GetBitmap(path);
    }

    /// <summary>
    /// Lädt ein Sprite vorab in den Cache (z.B. während einer Szenen-Transition).
    /// </summary>
    public void Preload(string charId, Pose pose, Emotion emotion)
    {
        _ = GetSprite(charId, pose, emotion);
    }

    /// <summary>
    /// Gibt die Content-Bounds eines Charakter-Sprites zurück (nicht-transparenter Bereich).
    /// Wird beim ersten Load berechnet und gecacht. Für content-aware Skalierung und Positionierung.
    /// </summary>
    public SKRectI GetSpriteContentBounds(string charId, Pose pose, Emotion emotion)
    {
        var path = SpriteAssetPaths.GetCharacterSpritePath(charId, pose, emotion);
        lock (_syncRoot)
        {
            if (_contentBounds.TryGetValue(path, out var bounds))
                return bounds;
        }
        // Fallback: Gesamtes Sprite (wenn noch nicht geladen)
        return new SKRectI(0, 0, 1248, 1824);
    }

    /// <summary>
    /// Prüft ob ein Charakter-Sprite verfügbar ist (lokal heruntergeladen).
    /// </summary>
    public bool HasSprite(string charId, Pose pose, Emotion emotion)
    {
        var path = SpriteAssetPaths.GetCharacterSpritePath(charId, pose, emotion);
        return _assets.HasAsset(path);
    }

    /// <summary>
    /// Gibt alle gecachten Bitmaps frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_syncRoot)
        {
            foreach (var (_, (bitmap, _)) in _cache)
                bitmap.Dispose();

            _cache.Clear();
            _lruOrder.Clear();
            _contentBounds.Clear();
        }
    }

    /// <summary>
    /// Interner Bitmap-Cache mit LRU-Eviction.
    /// </summary>
    private SKBitmap? GetBitmap(string relativePath)
    {
        if (_disposed) return null;

        lock (_syncRoot)
        {
            // Cache-Hit: An den Anfang der LRU-Liste verschieben
            if (_cache.TryGetValue(relativePath, out var entry))
            {
                _lruOrder.Remove(entry.Node);
                _lruOrder.AddFirst(entry.Node);
                return entry.Bitmap;
            }
        }

        // Cache-Miss: Bitmap laden (außerhalb von lock, da I/O)
        if (!_assets.HasAsset(relativePath))
            return null;

        var bitmap = _assets.LoadBitmap(relativePath);
        if (bitmap == null) return null;

        lock (_syncRoot)
        {
            // Doppel-Check nach Lock
            if (_cache.TryGetValue(relativePath, out var existing))
            {
                bitmap.Dispose();
                _lruOrder.Remove(existing.Node);
                _lruOrder.AddFirst(existing.Node);
                return existing.Bitmap;
            }

            // LRU-Eviction: Aelteste entfernen wenn Cache voll oder zu gross
            long bitmapSize = (long)bitmap.Width * bitmap.Height * bitmap.BytesPerPixel;
            while ((_cache.Count >= MaxCachedImages ||
                    _currentCacheSize + bitmapSize > MaxCacheSizeBytes) &&
                   _lruOrder.Last != null)
            {
                var evictKey = _lruOrder.Last.Value;
                _lruOrder.RemoveLast();

                if (_cache.TryGetValue(evictKey, out var evicted))
                {
                    _currentCacheSize -= (long)evicted.Bitmap.Width * evicted.Bitmap.Height * evicted.Bitmap.BytesPerPixel;
                    evicted.Bitmap.Dispose();
                    _cache.Remove(evictKey);
                    _contentBounds.Remove(evictKey);
                }
            }

            // Content-Bounds berechnen (einmal pro Sprite, für content-aware Rendering)
            if (!_contentBounds.ContainsKey(relativePath))
                _contentBounds[relativePath] = ComputeContentBounds(bitmap);

            // Neuen Eintrag hinzufuegen + Cache-Groesse tracken
            _currentCacheSize += bitmapSize;
            var node = _lruOrder.AddFirst(relativePath);
            _cache[relativePath] = (bitmap, node);
            return bitmap;
        }
    }

    /// <summary>
    /// Berechnet die Bounding Box des nicht-transparenten Bereichs (Content-Bounds).
    /// Sampling mit Schritt 4 für Performance (~142K statt 2.3M Pixel bei 1248x1824).
    /// Verwendet PeekPixels() für direkten Speicherzugriff (kein JNI-Overhead pro Pixel).
    /// Wird einmal pro Sprite beim ersten Load aufgerufen und gecacht.
    /// </summary>
    private static SKRectI ComputeContentBounds(SKBitmap bitmap)
    {
        int w = bitmap.Width, h = bitmap.Height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        const int step = 4;

        // PeekPixels vermeidet JNI-Overhead von GetPixel (~142K Aufrufe)
        var pixmap = bitmap.PeekPixels();
        if (pixmap != null)
        {
            for (int y = 0; y < h; y += step)
            {
                for (int x = 0; x < w; x += step)
                {
                    if (pixmap.GetPixelColor(x, y).Alpha > 10)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }
        else
        {
            // Fallback auf GetPixel (z.B. bei GPU-Backend ohne Pixmap-Zugriff)
            for (int y = 0; y < h; y += step)
            {
                for (int x = 0; x < w; x += step)
                {
                    if (bitmap.GetPixel(x, y).Alpha > 10)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }

        // Kein Content gefunden → Fallback auf gesamtes Bitmap
        if (maxX < 0) return new SKRectI(0, 0, w, h);

        // Margin für Sampling-Lücken (step Pixel in jede Richtung)
        return new SKRectI(
            Math.Max(0, minX - step),
            Math.Max(0, minY - step),
            Math.Min(w, maxX + step + 1),
            Math.Min(h, maxY + step + 1));
    }
}
