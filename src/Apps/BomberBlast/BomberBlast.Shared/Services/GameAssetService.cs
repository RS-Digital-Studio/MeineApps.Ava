namespace BomberBlast.Services;

using System.Collections.Concurrent;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

/// <summary>
/// Lädt WebP-Assets und cached sie als SKBitmap im LRU-Cache.
/// Desktop: Avalonia AssetLoader (avares://). Android: PlatformAssetLoader (Assets.Open).
/// Thread-safe durch ConcurrentDictionary + Lazy&lt;Task&gt; Deduplication.
/// </summary>
public sealed class GameAssetService : IGameAssetService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<SKBitmap?>>> _loadingTasks = new();
    private readonly long _maxCacheBytes;
    private long _currentCacheBytes;
    private bool _disposed;

    // Pending-Dispose-Queue: Verdraengte Bitmaps werden nicht sofort disposed,
    // weil der UI-Render-Thread sie u.U. noch in einer lokalen Variable haelt.
    // Nach DrainAgeMs sind garantiert mehrere Frames vergangen -> sicher zu disposen.
    private readonly ConcurrentQueue<(SKBitmap Bitmap, long EnqueuedTick)> _pendingDispose = new();
    private const long DrainAgeMs = 200;

    /// <summary>
    /// Statischer Zugriff für Renderer (statische Klassen ohne DI).
    /// Wird in App.axaml.cs nach ServiceProvider-Build gesetzt.
    /// </summary>
    public static IGameAssetService? Current { get; set; }

    /// <summary>
    /// Plattform-spezifische Lade-Funktion.
    /// Android: In MainActivity auf Assets.Open() setzen.
    /// Desktop: Nicht nötig (nutzt Avalonia AssetLoader).
    /// </summary>
    public static Func<string, Stream?>? PlatformAssetLoader { get; set; }

    private sealed class CacheEntry
    {
        public required SKBitmap Bitmap { get; init; }
        public long SizeBytes { get; init; }
        public long LastAccessTick { get; set; }
    }

    public GameAssetService(long maxCacheBytes = 30 * 1024 * 1024)
    {
        // 30 MB Default: Sicherheitsmarge fuer 200-EUR-Mid-Tier-Android (3 GB RAM, ~1.2 GB pro App).
        // Native Pixel-Footprint nach WebP-Decode kann bei 1024x1024 RGBA 4 MB pro Bitmap erreichen.
        _maxCacheBytes = maxCacheBytes;
    }

    public long CacheSizeBytes => Interlocked.Read(ref _currentCacheBytes);

    public SKBitmap? GetBitmap(string assetPath)
    {
        if (_cache.TryGetValue(assetPath, out var entry))
        {
            entry.LastAccessTick = Environment.TickCount64;
            return entry.Bitmap;
        }
        return null;
    }

    /// <summary>
    /// Nicht vorgeladene Assets die bereits einen Load-Versuch hatten
    /// und wo kein File gefunden wurde (vermeidet wiederholte Load-Versuche).
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _notFound = new();

    public SKBitmap? GetOrLoadBitmap(string assetPath)
    {
        if (_cache.TryGetValue(assetPath, out var entry))
        {
            entry.LastAccessTick = Environment.TickCount64;
            return entry.Bitmap;
        }

        // Kein wiederholter Load-Versuch für Assets die nicht existieren
        if (_notFound.ContainsKey(assetPath))
            return null;

        // Async Load triggern, nächster Frame hat das Bitmap
        _ = LoadBitmapAsync(assetPath).ContinueWith(t =>
        {
            if (t.Result == null)
                _notFound.TryAdd(assetPath, true);
        }, TaskContinuationOptions.ExecuteSynchronously);

        return null;
    }

    public Task<SKBitmap?> LoadBitmapAsync(string assetPath)
    {
        if (_cache.TryGetValue(assetPath, out var existing))
        {
            existing.LastAccessTick = Environment.TickCount64;
            return Task.FromResult<SKBitmap?>(existing.Bitmap);
        }

        var lazy = _loadingTasks.GetOrAdd(assetPath,
            key => new Lazy<Task<SKBitmap?>>(() => Task.Run(() => LoadBitmapInternal(key))));
        return lazy.Value;
    }

    public async Task PreloadAsync(IEnumerable<string> assetPaths)
    {
        var tasks = assetPaths
            .Where(p => !_cache.ContainsKey(p))
            .Select(LoadBitmapAsync);
        await Task.WhenAll(tasks);
    }

    public void Evict(string assetPath)
    {
        if (_cache.TryRemove(assetPath, out var entry))
        {
            Interlocked.Add(ref _currentCacheBytes, -entry.SizeBytes);
            // Bitmap nicht direkt disposen: Render-Thread (UI) koennte es noch halten.
            // Nach DrainAgeMs sicher zu disposen.
            _pendingDispose.Enqueue((entry.Bitmap, Environment.TickCount64));
        }
        _loadingTasks.TryRemove(assetPath, out _);
    }

    /// <summary>
    /// Disposed alle Bitmaps in der Pending-Queue, die aelter als DrainAgeMs sind.
    /// Wird automatisch nach jeder Eviction in LoadBitmapInternal aufgerufen.
    /// Kann zusaetzlich vom UI-Thread (z.B. GameView.OnPaintSurface) aufgerufen werden.
    /// </summary>
    public void DrainPendingDispose()
    {
        long now = Environment.TickCount64;
        // Snapshot-Iteration: TryDequeue + Re-Enqueue der zu jungen Eintraege
        int initialCount = _pendingDispose.Count;
        for (int i = 0; i < initialCount; i++)
        {
            if (!_pendingDispose.TryDequeue(out var item)) break;

            if (now - item.EnqueuedTick >= DrainAgeMs)
            {
                item.Bitmap.Dispose();
            }
            else
            {
                // Noch zu jung -> wieder einreihen
                _pendingDispose.Enqueue(item);
                // Da Queue FIFO: wenn der hier zu jung ist, sind alle Folge-Eintraege auch zu jung
                break;
            }
        }
    }

    public void ClearCache()
    {
        foreach (var key in _cache.Keys.ToList())
        {
            if (_cache.TryRemove(key, out var entry))
            {
                Interlocked.Add(ref _currentCacheBytes, -entry.SizeBytes);
                entry.Bitmap.Dispose();
            }
        }
        // Pending-Queue zwangsleeren (App-Shutdown -> kein Render mehr)
        while (_pendingDispose.TryDequeue(out var item))
            item.Bitmap.Dispose();
        _loadingTasks.Clear();
    }

    private SKBitmap? LoadBitmapInternal(string assetPath)
    {
        try
        {
            using var stream = GetAssetStream(assetPath);
            if (stream == null) return null;

            // Android-Streams (Assets.Open) sind non-seekable
            Stream decodeStream = stream;
            MemoryStream? ms = null;
            if (!stream.CanSeek)
            {
                ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                decodeStream = ms;
            }

            var bitmap = SKBitmap.Decode(decodeStream);
            ms?.Dispose();

            if (bitmap == null) return null;

            var sizeBytes = (long)bitmap.ByteCount;

            while (Interlocked.Read(ref _currentCacheBytes) + sizeBytes > _maxCacheBytes
                   && !_cache.IsEmpty)
            {
                EvictOldest();
            }

            // Verdraengte Bitmaps werden auf dem UI-Thread disposed:
            // LoadBitmapInternal laeuft auf Background-Thread, ein Direkt-Dispose hier
            // koennte mit aktivem canvas.DrawBitmap() im Render-Thread kollidieren (use-after-free).
            // Dispatcher.UIThread.Post() serialisiert den Drain mit Render-Calls.
            if (!_pendingDispose.IsEmpty)
                Dispatcher.UIThread.Post(DrainPendingDispose);

            var entry = new CacheEntry
            {
                Bitmap = bitmap,
                SizeBytes = sizeBytes,
                LastAccessTick = Environment.TickCount64
            };

            if (_cache.TryAdd(assetPath, entry))
            {
                Interlocked.Add(ref _currentCacheBytes, sizeBytes);
                return bitmap;
            }

            bitmap.Dispose();
            return _cache.TryGetValue(assetPath, out var existing) ? existing.Bitmap : null;
        }
        catch
        {
            return null;
        }
    }

    private void EvictOldest()
    {
        string? oldestKey = null;
        long oldestTick = long.MaxValue;
        foreach (var kv in _cache)
        {
            if (kv.Value.LastAccessTick < oldestTick)
            {
                oldestTick = kv.Value.LastAccessTick;
                oldestKey = kv.Key;
            }
        }
        if (oldestKey != null)
            Evict(oldestKey);
    }

    private static Stream? GetAssetStream(string assetPath)
    {
        if (PlatformAssetLoader != null)
        {
            var stream = PlatformAssetLoader(assetPath);
            if (stream != null) return stream;
        }

        try
        {
            var uri = new Uri($"avares://BomberBlast.Shared/Assets/visuals/{assetPath}");
            return AssetLoader.Open(uri);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearCache();
    }
}
