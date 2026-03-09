namespace RebornSaga.Services;

using RebornSaga.Rendering.Characters;
using SkiaSharp;
using System;
using System.Collections.Generic;

/// <summary>
/// LRU-Cache für Charakter-Sprites (Einzelbilder, kein Atlas).
/// Hält maximal <see cref="MaxCachedImages"/> Bilder gleichzeitig im Speicher.
/// Thread-safe über lock auf _syncRoot.
/// </summary>
public sealed class SpriteCache : IDisposable
{
    /// <summary>Maximale Anzahl gleichzeitig gecachter Sprite-Bilder.</summary>
    public const int MaxCachedImages = 30;

    private readonly IAssetDeliveryService _assets;
    private readonly object _syncRoot = new();

    // LRU-Tracking: zuletzt verwendeter Pfad vorne
    private readonly LinkedList<string> _lruOrder = new();
    // Lookup: relativePath → Cache-Eintrag + LRU-Node
    private readonly Dictionary<string, (SKBitmap Bitmap, LinkedListNode<string> Node)> _cache = new();

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

            // LRU-Eviction: Ältesten entfernen wenn Cache voll
            while (_cache.Count >= MaxCachedImages && _lruOrder.Last != null)
            {
                var evictKey = _lruOrder.Last.Value;
                _lruOrder.RemoveLast();

                if (_cache.TryGetValue(evictKey, out var evicted))
                {
                    evicted.Bitmap.Dispose();
                    _cache.Remove(evictKey);
                }
            }

            // Neuen Eintrag hinzufügen
            var node = _lruOrder.AddFirst(relativePath);
            _cache[relativePath] = (bitmap, node);
            return bitmap;
        }
    }
}
