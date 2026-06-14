namespace RebornSaga.Services;

using RebornSaga.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Asset-Delivery via Firebase Storage REST API.
/// Lädt Manifest + Assets herunter, verifiziert SHA256-Hashes, cached lokal.
/// Delta-Updates: nur Dateien mit geändertem Hash werden erneut geladen.
/// Thread-safe über SemaphoreSlim. Graceful Fallback bei Offline/Fehlern.
/// </summary>
public sealed class AssetDeliveryService : IAssetDeliveryService, IDisposable
{
    // Firebase Storage REST URL (URL-encoded Pfad + ?alt=media für Datei-Download)
    private const string FirebaseBaseUrl =
        "https://firebasestorage.googleapis.com/v0/b/rebornsaga-671b6.firebasestorage.app/o/";

    private const string ManifestFileName = "assets/asset_manifest.json";
    private const string LocalManifestFileName = "manifest.local.json";

    private readonly HttpClient _httpClient;
    private readonly string _cacheDir;
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);

    // Aktuelles Manifest (nach CheckForUpdatesAsync geladen)
    private AssetManifest? _remoteManifest;
    private AssetManifest? _localManifest;

    // Liste der Dateien die heruntergeladen werden müssen
    private List<AssetFile> _pendingFiles = new();
    private long _pendingBytes;

    public bool IsDownloadRequired => _pendingFiles.Count > 0;
    public string DownloadSizeFormatted => FormatBytes(_pendingBytes);

    public AssetDeliveryService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Lokaler Cache-Ordner: AppData/RebornSaga/assets/
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDir = Path.Combine(appData, "RebornSaga", "assets");

        // Cache-Verzeichnis erstellen falls nicht vorhanden
        Directory.CreateDirectory(_cacheDir);

        // Lokales Manifest laden (falls vorhanden)
        LoadLocalManifest();
    }

    /// <summary>
    /// Prüft ob neue Assets verfügbar sind. Lädt das Remote-Manifest von Firebase
    /// und vergleicht Hashes mit dem lokalen Cache. Bei Offline/Fehler: leeres Ergebnis.
    /// </summary>
    public async Task<AssetCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            // Manifest von Firebase laden
            var manifestUrl = $"{FirebaseBaseUrl}{Uri.EscapeDataString(ManifestFileName)}?alt=media";
            var json = await _httpClient.GetStringAsync(manifestUrl);
            _remoteManifest = JsonSerializer.Deserialize<AssetManifest>(json);

            if (_remoteManifest == null)
                return new AssetCheckResult(false, 0, 0, Array.Empty<string>());

            // Delta berechnen: nur Dateien mit geändertem oder fehlendem Hash
            _pendingFiles.Clear();
            _pendingBytes = 0;
            var changedPacks = new List<string>();

            foreach (var (packName, pack) in _remoteManifest.Packs)
            {
                var packHasChanges = false;

                foreach (var file in pack.Files)
                {
                    if (NeedsDownload(file))
                    {
                        _pendingFiles.Add(file);
                        _pendingBytes += file.Size;
                        packHasChanges = true;
                    }
                }

                if (packHasChanges)
                    changedPacks.Add(packName);
            }

            return new AssetCheckResult(
                UpdateAvailable: _pendingFiles.Count > 0,
                FilesToDownload: _pendingFiles.Count,
                BytesToDownload: _pendingBytes,
                ChangedPacks: changedPacks.ToArray());
        }
        catch (Exception)
        {
            // Offline oder Fehler: mit bestehendem Cache weiterarbeiten
            _pendingFiles.Clear();
            _pendingBytes = 0;
            return new AssetCheckResult(false, 0, 0, Array.Empty<string>());
        }
    }

    /// <summary>
    /// Lädt alle ausstehenden Assets parallel herunter (max 6 gleichzeitig).
    /// SHA256-Hash wird inline beim Stream-Kopieren berechnet (kein doppeltes File-I/O).
    /// Thread-safe über SemaphoreSlim. Fortschritt via Interlocked-Operationen.
    /// </summary>
    public async Task<bool> DownloadAssetsAsync(
        IProgress<AssetDownloadProgress> progress,
        CancellationToken ct = default)
    {
        if (_pendingFiles.Count == 0)
            return true;

        await _downloadSemaphore.WaitAsync(ct);
        try
        {
            var totalFiles = _pendingFiles.Count;
            var totalBytes = _pendingBytes;
            long bytesDownloaded = 0;
            int filesCompleted = 0;
            int failedCount = 0;

            // Verzeichnisse vorab erstellen (vermeidet Race-Conditions bei parallelen Downloads)
            PreCreateDirectories(_pendingFiles);

            // Paralleler Download mit max 6 gleichzeitigen Verbindungen
            using var throttle = new SemaphoreSlim(6, 6);

            var tasks = _pendingFiles.Select(file => Task.Run(async () =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    ct.ThrowIfCancellationRequested();

                    var success = await DownloadFileAsync(file, ct);
                    if (!success)
                    {
                        Interlocked.Increment(ref failedCount);
                        return;
                    }

                    // Fortschritt thread-safe aktualisieren
                    var newBytes = Interlocked.Add(ref bytesDownloaded, file.Size);
                    var completed = Interlocked.Increment(ref filesCompleted);

                    progress.Report(new AssetDownloadProgress(
                        CurrentFile: completed,
                        TotalFiles: totalFiles,
                        BytesDownloaded: newBytes,
                        TotalBytes: totalBytes,
                        CurrentFileName: Path.GetFileName(file.Path)));
                }
                finally
                {
                    throttle.Release();
                }
            }, ct)).ToArray();

            await Task.WhenAll(tasks);

            // Bei Fehlern abbrechen
            if (failedCount > 0)
                return false;

            // Abschluss melden
            progress.Report(new AssetDownloadProgress(
                CurrentFile: totalFiles,
                TotalFiles: totalFiles,
                BytesDownloaded: totalBytes,
                TotalBytes: totalBytes,
                CurrentFileName: ""));

            // Lokales Manifest aktualisieren
            if (_remoteManifest != null)
            {
                _localManifest = _remoteManifest;
                SaveLocalManifest();
            }

            _pendingFiles.Clear();
            _pendingBytes = 0;

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Erstellt alle Zielverzeichnisse vorab (einmalig statt pro Datei).
    /// </summary>
    private void PreCreateDirectories(List<AssetFile> files)
    {
        var directories = new HashSet<string>();
        foreach (var file in files)
        {
            var localPath = GetLocalPath(file.Path);
            var dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir))
                directories.Add(dir);
        }

        foreach (var dir in directories)
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Lädt ein gecachtes Bild (WebP/PNG) als SKBitmap.
    /// Gibt null zurück wenn die Datei nicht existiert oder nicht gelesen werden kann.
    /// </summary>
    /// <param name="relativePath">Pfad des Assets relativ zum Cache-Ordner.</param>
    /// <param name="maxHeight">
    /// Obergrenze für die dekodierte Pixelhöhe (Akku/RAM). Sprites, die höher sind als diese
    /// Grenze, werden beim Dekodieren proportional verkleinert (Seitenverhältnis exakt erhalten,
    /// nur Downscale — NIE hochskaliert). <c>0</c> = keine Begrenzung (volle Auflösung).
    /// </param>
    public SKBitmap? LoadBitmap(string relativePath, int maxHeight = 0)
    {
        var fullPath = GetLocalPath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            // Ohne Grenze: direkter Decode in voller Auflösung (Rückwärtskompatibilität).
            if (maxHeight <= 0)
            {
                using var fullStream = File.OpenRead(fullPath);
                return SKBitmap.Decode(fullStream);
            }

            return DecodeDownsampled(fullPath, maxHeight);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Dekodiert ein Bild und skaliert es dabei auf höchstens <paramref name="maxHeight"/> Pixel
    /// Höhe herunter. Zweistufig: SKCodec liefert eine günstige Integer-Downscale-Stufe
    /// (subsampled decode — spart Speicher und CPU), danach wird bei Bedarf einmalig per
    /// Linear/Mipmap-Resampling auf die exakte Zielgröße verfeinert. Sprites, die bereits
    /// kleiner als das Ziel sind, werden unverändert in voller Auflösung dekodiert (kein Upscale).
    /// </summary>
    private static SKBitmap? DecodeDownsampled(string fullPath, int maxHeight)
    {
        using var stream = File.OpenRead(fullPath);
        using var codec = SKCodec.Create(stream);
        if (codec == null)
            return null;

        var srcInfo = codec.Info;
        int srcW = srcInfo.Width, srcH = srcInfo.Height;

        // Bereits klein genug → unverändert in voller Auflösung dekodieren (NIE hochskalieren).
        if (srcH <= maxHeight || srcH <= 0)
            return DecodeFull(codec, srcInfo);

        // Exakte Zielgröße (Seitenverhältnis erhalten, mindestens 1px).
        var ratio = (float)maxHeight / srcH;
        int targetW = Math.Max(1, (int)MathF.Round(srcW * ratio));
        int targetH = Math.Max(1, maxHeight);

        // Günstige Codec-Downscale-Stufe ermitteln (subsampled decode spart Speicher + CPU).
        var scaledSize = codec.GetScaledDimensions(ratio);

        // Die Decode-Stufe darf NICHT unter die Zielhöhe fallen (Leitplanke: lieber zu wenig
        // downsamplen als weiche Sprites). Liefert der Codec eine zu kleine oder ungültige Stufe,
        // wird voll dekodiert und exakt auf die Zielgröße herunterskaliert (beste Qualität).
        SKBitmap decoded;
        if (scaledSize.Width <= 0 || scaledSize.Height < targetH)
        {
            var full = DecodeFull(codec, srcInfo);
            if (full == null)
                return null;
            decoded = full;
        }
        else
        {
            var decodeInfo = srcInfo.WithSize(scaledSize.Width, scaledSize.Height);
            decoded = new SKBitmap(decodeInfo);
            var result = codec.GetPixels(decodeInfo, decoded.GetPixels());

            // GetPixels kann InvalidScale liefern, wenn der Codec die Stufe doch nicht unterstützt.
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            {
                decoded.Dispose();
                var full = DecodeFull(codec, srcInfo);
                if (full == null)
                    return null;
                decoded = full;
            }
        }

        // Schon auf Zielhöhe (oder darunter ist hier ausgeschlossen)? Dann kein Resample nötig.
        if (decoded.Height <= targetH)
            return decoded;

        // Feinskalierung auf die exakte Zielgröße (qualitativ hochwertiges Linear/Mipmap-Sampling).
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        var resized = decoded.Resize(new SKImageInfo(targetW, targetH), sampling);

        // Resize kann null liefern (z.B. zu wenig Speicher) → dann die größere Stufe behalten.
        if (resized == null)
            return decoded;

        decoded.Dispose();
        return resized;
    }

    /// <summary>Dekodiert das gesamte Bild in voller Auflösung aus einem bereits erstellten Codec.</summary>
    private static SKBitmap? DecodeFull(SKCodec codec, SKImageInfo info)
    {
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
            bitmap.Dispose();
            return null;
        }
        return bitmap;
    }

    /// <summary>
    /// Gibt einen FileStream auf ein gecachtes Asset zurück.
    /// Gibt null zurück wenn die Datei nicht existiert.
    /// </summary>
    public Stream? GetAssetStream(string relativePath)
    {
        var fullPath = GetLocalPath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            return File.OpenRead(fullPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Prüft ob ein Asset lokal im Cache vorhanden ist.
    /// </summary>
    public bool HasAsset(string relativePath)
    {
        var fullPath = GetLocalPath(relativePath);
        return File.Exists(fullPath);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _downloadSemaphore.Dispose();
    }

    // --- Private Hilfsmethoden ---

    /// <summary>
    /// Prüft ob eine Datei heruntergeladen werden muss (fehlt lokal oder Hash stimmt nicht überein).
    /// </summary>
    private bool NeedsDownload(AssetFile file)
    {
        var localPath = GetLocalPath(file.Path);

        // Datei existiert nicht lokal
        if (!File.Exists(localPath))
            return true;

        // Hash aus lokalem Manifest vergleichen (schneller als Datei neu hashen)
        if (_localManifest != null)
        {
            var localFile = FindInLocalManifest(file.Path);
            if (localFile != null && localFile.Hash == file.Hash)
                return false; // Hash identisch, kein Download nötig
        }

        // Kein lokales Manifest oder Datei nicht drin: Datei-Hash berechnen
        try
        {
            var localHash = ComputeFileHash(localPath);
            return !string.Equals(localHash, file.Hash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true; // Im Zweifel herunterladen
        }
    }

    /// <summary>
    /// Sucht eine Datei im lokalen Manifest anhand des Pfads.
    /// </summary>
    private AssetFile? FindInLocalManifest(string path)
    {
        if (_localManifest == null)
            return null;

        foreach (var pack in _localManifest.Packs.Values)
        {
            var found = pack.Files.FirstOrDefault(f =>
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Lädt eine einzelne Datei von Firebase Storage herunter mit Inline-SHA256-Hashing.
    /// Hash wird WÄHREND des Stream-Kopierens berechnet (kein doppeltes File-I/O).
    /// 3 Retry-Versuche mit exponentiellem Backoff. Thread-safe temp-Dateinamen.
    /// </summary>
    private async Task<bool> DownloadFileAsync(AssetFile file, CancellationToken ct)
    {
        const int maxRetries = 3;
        var localPath = GetLocalPath(file.Path);
        // Thread-safe: eindeutiger temp-Name pro Task (parallele Downloads)
        var tempPath = localPath + $".{Environment.CurrentManagedThreadId}.tmp";

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // Exponentieller Backoff ab dem 2. Versuch (1s, 2s, 4s)
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);

            try
            {
                // Firebase Storage URL: Pfad URL-encoded + ?alt=media
                var encodedPath = Uri.EscapeDataString($"assets/{file.Path}");
                var url = $"{FirebaseBaseUrl}{encodedPath}?alt=media";

                // Stream-basierter Download mit Inline-SHA256-Hashing
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                await using var networkStream = await response.Content.ReadAsStreamAsync(ct);
                using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 81920, useAsync: true);

                // Hash inline berechnen während wir in die Datei schreiben
                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await networkStream.ReadAsync(buffer, ct)) > 0)
                {
                    sha256.AppendData(buffer, 0, bytesRead);
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                }

                await fileStream.FlushAsync(ct);
                fileStream.Close();

                // Hash finalisieren und vergleichen (kein erneutes File-Lesen nötig)
                var hashBytes = sha256.GetHashAndReset();
                var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                if (!string.Equals(hash, file.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(tempPath);
                    continue;
                }

                // Atomar: temporäre Datei an Zielort verschieben
                TryDeleteFile(localPath);
                File.Move(tempPath, localPath);
                return true;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(tempPath);
                throw;
            }
            catch (Exception)
            {
                TryDeleteFile(tempPath);
                if (attempt == maxRetries - 1)
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Löscht eine Datei ohne Exception bei Fehler (z.B. wenn sie nicht existiert).
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* Ignoriert - nicht kritisch */ }
    }

    /// <summary>
    /// Berechnet den SHA256-Hash einer lokalen Datei als Hex-String (lowercase).
    /// </summary>
    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gibt den vollständigen lokalen Pfad für ein Asset zurück.
    /// </summary>
    private string GetLocalPath(string relativePath)
    {
        // Forward-Slashes normalisieren für plattformübergreifende Kompatibilität
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_cacheDir, normalized);
    }

    /// <summary>
    /// Lädt das lokale Manifest aus dem Cache (falls vorhanden).
    /// </summary>
    private void LoadLocalManifest()
    {
        var path = Path.Combine(_cacheDir, LocalManifestFileName);
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            _localManifest = JsonSerializer.Deserialize<AssetManifest>(json);
        }
        catch
        {
            // Korruptes Manifest: ignorieren, wird beim nächsten Download neu erstellt
            _localManifest = null;
        }
    }

    /// <summary>
    /// Speichert das aktuelle Manifest als lokale Kopie.
    /// </summary>
    private void SaveLocalManifest()
    {
        if (_localManifest == null)
            return;

        try
        {
            var path = Path.Combine(_cacheDir, LocalManifestFileName);
            var json = JsonSerializer.Serialize(_localManifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Schreibfehler: nicht kritisch, wird beim nächsten Start erneut versucht
        }
    }

    /// <summary>
    /// Formatiert Bytes in menschenlesbare Größe (B, KB, MB).
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }
}
