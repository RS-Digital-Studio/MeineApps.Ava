namespace HandwerkerImperium.Services;

using System.Collections.Concurrent;
using Avalonia.Platform;
using SkiaSharp;

/// <summary>
/// Lädt WebP-Assets und cached sie als SKBitmap im LRU-Cache.
/// Desktop: Avalonia AssetLoader (avares://). Android: PlatformAssetLoader (Assets.Open).
/// Thread-safe durch ConcurrentDictionary + Lazy&lt;Task&gt; Deduplication.
/// </summary>
public sealed class GameAssetService : IGameAssetService
{
    /// <summary>
    /// Statischer Zugriff für Views die keinen DI-Zugang haben (SkiaSharp-Rendering).
    /// Wird in App.axaml.cs nach DI-Registrierung gesetzt.
    /// </summary>
    public static IGameAssetService? Current { get; set; }

    // LRU-Cache: assetPath → (Bitmap, letzter Zugriff)
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    // Deduplizierung: Nur EIN Ladevorgang pro Asset (verhindert Race Condition)
    private readonly ConcurrentDictionary<string, Lazy<Task<SKBitmap?>>> _loadingTasks = new();
    private readonly long _maxCacheBytes;
    private long _currentCacheBytes;
    private bool _disposed;

    // Bitmaps die aus dem Cache entfernt wurden, aber noch in Benutzung sein könnten
    private readonly List<SKBitmap> _pendingDispose = new();

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

    public GameAssetService(long maxCacheBytes = 50 * 1024 * 1024) // 50 MB Standard
    {
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

    public Task<SKBitmap?> LoadBitmapAsync(string assetPath)
    {
        // Bereits gecacht?
        if (_cache.TryGetValue(assetPath, out var existing))
        {
            existing.LastAccessTick = Environment.TickCount64;
            return Task.FromResult<SKBitmap?>(existing.Bitmap);
        }

        // Lazy<Task> garantiert: Nur EIN Ladevorgang pro Asset-Key
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
        // NUR aus Cache entfernen, NICHT disposen!
        // Grund: Ein anderer Thread könnte das Bitmap gerade in DrawBitmap() nutzen.
        // Dispose passiert erst bei ClearCache()/App-Shutdown.
        if (_cache.TryRemove(assetPath, out var entry))
        {
            Interlocked.Add(ref _currentCacheBytes, -entry.SizeBytes);
            lock (_pendingDispose)
                _pendingDispose.Add(entry.Bitmap);
        }
        _loadingTasks.TryRemove(assetPath, out _);
    }

    public void ClearCache()
    {
        // ConcurrentDictionary.Keys erstellt intern bereits einen Snapshot -
        // kein ToList() nötig (vermeidet extra List-Allokation)
        foreach (var kvp in _cache)
        {
            if (_cache.TryRemove(kvp.Key, out var entry))
            {
                Interlocked.Add(ref _currentCacheBytes, -entry.SizeBytes);
                entry.Bitmap.Dispose();
            }
        }
        // Jetzt auch pending Bitmaps disposen (App-Shutdown, kein Rendering mehr)
        lock (_pendingDispose)
        {
            foreach (var bmp in _pendingDispose)
                bmp.Dispose();
            _pendingDispose.Clear();
        }
        _loadingTasks.Clear();
    }

    private SKBitmap? LoadBitmapInternal(string assetPath)
    {
        try
        {
            using var stream = GetAssetStream(assetPath);
            if (stream == null) return null;

            // Android-Streams (Assets.Open) sind non-seekable → in MemoryStream kopieren
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

            // LRU-Eviction wenn Cache voll
            while (Interlocked.Read(ref _currentCacheBytes) + sizeBytes > _maxCacheBytes
                   && !_cache.IsEmpty)
            {
                EvictOldest();
            }

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

            // Anderer Thread hat bereits gecacht (sollte mit Lazy<Task> nicht passieren)
            bitmap.Dispose();
            return _cache.TryGetValue(assetPath, out var existing) ? existing.Bitmap : null;
        }
        catch
        {
            // Asset-Ladefehler still behandelt
            return null;
        }
    }

    private void EvictOldest()
    {
        // Linear-Scan O(n) statt OrderBy O(n log n)
        long minTick = long.MaxValue;
        string? minKey = null;

        foreach (var kv in _cache)
        {
            if (kv.Value.LastAccessTick < minTick)
            {
                minTick = kv.Value.LastAccessTick;
                minKey = kv.Key;
            }
        }

        if (minKey != null)
            Evict(minKey);
    }

    private static Stream? GetAssetStream(string assetPath)
    {
        // 1. Plattform-Loader (Android: AssetManager.Open())
        if (PlatformAssetLoader != null)
        {
            var stream = PlatformAssetLoader(assetPath);
            if (stream != null) return stream;
        }

        // 2. Avalonia AssetLoader (Desktop) — nutzt avares:// URI
        try
        {
            var uri = new Uri($"avares://HandwerkerImperium.Shared/Assets/visuals/{assetPath}");
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
