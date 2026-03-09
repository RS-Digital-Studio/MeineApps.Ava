namespace RebornSaga.Services;

using SkiaSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Asset-Delivery: Lädt Sprites/Hintergründe von Firebase Storage herunter und cached sie lokal.
/// Delta-Updates via SHA256-Hash-Vergleich. Graceful Fallback bei Offline.
/// </summary>
public interface IAssetDeliveryService
{
    /// <summary>Ob ein Download erforderlich ist (Required-Packs fehlen oder veraltet).</summary>
    bool IsDownloadRequired { get; }

    /// <summary>Formatierte Größe der ausstehenden Downloads (z.B. "12,5 MB").</summary>
    string DownloadSizeFormatted { get; }

    /// <summary>Prüft ob neue Assets verfügbar sind (Manifest von Firebase laden).</summary>
    Task<AssetCheckResult> CheckForUpdatesAsync();

    /// <summary>Lädt alle ausstehenden Assets herunter mit Fortschritts-Reporting.</summary>
    Task<bool> DownloadAssetsAsync(IProgress<AssetDownloadProgress> progress, CancellationToken ct = default);

    /// <summary>Lädt ein gecachtes Bild (WebP/PNG) als SKBitmap.</summary>
    SKBitmap? LoadBitmap(string relativePath);

    /// <summary>Gibt einen FileStream auf ein gecachtes Asset zurück.</summary>
    Stream? GetAssetStream(string relativePath);

    /// <summary>Prüft ob ein Asset lokal vorhanden ist.</summary>
    bool HasAsset(string relativePath);
}

/// <summary>
/// Ergebnis der Update-Prüfung: Welche Packs geändert wurden und wie viel heruntergeladen werden muss.
/// </summary>
public record AssetCheckResult(
    bool UpdateAvailable,
    int FilesToDownload,
    long BytesToDownload,
    string[] ChangedPacks);

/// <summary>
/// Fortschritt während des Asset-Downloads für UI-Anzeige.
/// </summary>
public record AssetDownloadProgress(
    int CurrentFile,
    int TotalFiles,
    long BytesDownloaded,
    long TotalBytes,
    string CurrentFileName);
