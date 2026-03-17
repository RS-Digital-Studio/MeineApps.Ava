namespace BomberBlast.Services;

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
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<SKBitmap?>>> _loadingTasks = new();
    private readonly long _maxCacheBytes;
    private long _currentCacheBytes;
    private bool _disposed;
    private readonly List<SKBitmap> _pendingDispose = new();

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

    public GameAssetService(long maxCacheBytes = 50 * 1024 * 1024)
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
            lock (_pendingDispose)
                _pendingDispose.Add(entry.Bitmap);
        }
        _loadingTasks.TryRemove(assetPath, out _);
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
