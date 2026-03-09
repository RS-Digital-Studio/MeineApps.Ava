# RebornSaga Visual Upgrade — Phase 1 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Einen Charakter (Aria) komplett AI-generiert im Spiel haben + Asset-Download-Infrastruktur.

**Architecture:** SpriteCharacterRenderer ersetzt prozedurale Renderer (CharacterParts → 7 Renderer).
AssetDeliveryService lädt Assets von Firebase Storage mit Manifest-basiertem Delta-Update.
SpriteCache hält max 5 Charakter-Atlanten im RAM (LRU).
CharacterRenderer-Fassade bleibt — Scenes merken nichts von der Umstellung.

**Tech Stack:** SkiaSharp (Sprite-Rendering), HttpClient (Firebase Storage REST), WebP (Asset-Format),
ComfyUI + Animagine XL 4.0 (AI-Generierung), IPAdapter + ControlNet (Konsistenz)

**Design-Dokument:** `docs/plans/2026-03-06-reborn-saga-visual-upgrade-design.md`

---

## Task 1: Aria Referenz-Bild generieren (ComfyUI)

**Ziel:** Ein hochwertiges Referenz-Bild von Aria, das als IPAdapter-Anker für alle Emotionen dient.

**Step 1: Workflow-JSON erstellen**

Erstelle `F:\AI\ComfyUI_workflows\aria_reference.json` mit:
- CheckpointLoaderSimple: `animagine-xl-4.0-opt.safetensors`
- Positive Prompt: `1girl, anime style, long red hair, bright green eyes, leather armor with gold accents, confident warrior expression, upper body portrait, facing viewer, simple white background, masterpiece, best quality, very aesthetic, absurdres, detailed face, sharp features`
- Negative Prompt: Standard (siehe Design-Dokument)
- EmptyLatentImage: 832×1216
- KSampler: euler_ancestral, 28 Steps, CFG 7.0, Seed explorieren (5-10 Varianten)
- SaveImage: `aria_reference`

**Step 2: Varianten generieren**

```bash
# Seeds 1-10 durchprobieren, bestes Ergebnis als Referenz auswählen
curl -X POST http://127.0.0.1:8188/prompt -H "Content-Type: application/json" -d @aria_reference.json
```

**Step 3: Bestes Referenz-Bild auswählen**

Output in `F:\AI\ComfyUI_windows_portable\ComfyUI\output\` prüfen.
Bestes Bild kopieren nach `F:\AI\RebornSaga_Assets\references\aria_reference.png`.

**Akzeptanzkriterien:**
- Feuerrotes langes Haar, grüne Augen, Lederrüstung mit Gold
- Sauberer Anime-Stil, keine Artefakte
- Gesicht klar erkennbar für IPAdapter

---

## Task 2: Aria Emotionen generieren (IPAdapter + ControlNet)

**Ziel:** 6 konsistente Head-Varianten (neutral, happy, angry, sad, surprised, determined).

**Step 1: IPAdapter-Workflow erstellen**

Erstelle `F:\AI\ComfyUI_workflows\aria_emotions.json`:
- IPAdapterApply: Referenz-Bild von Task 1, Stärke 0.7
- ControlNet OpenPose: Fixe Frontal-Kopfhaltung
- Prompt variiert NUR die Emotion:
  - `neutral expression, calm face`
  - `happy expression, bright smile, warm eyes`
  - `angry expression, furrowed brows, intense eyes`
  - `sad expression, downcast eyes, slight frown`
  - `surprised expression, wide eyes, open mouth`
  - `determined expression, focused eyes, firm jaw`

**Step 2: Alle 6 Emotionen generieren**

Pro Emotion 3-5 Varianten, beste auswählen.

**Step 3: Body + Fullbody generieren**

Gleicher IPAdapter-Anker, Prompt fokussiert auf Outfit:
- Body (Hals abwärts): `leather armor, gold buckle, sword at hip, upper body, no head`
- Fullbody: `full body, standing pose, leather armor, sword, boots`

**Step 4: Output organisieren**

```
F:\AI\RebornSaga_Assets\aria\
├── reference.png           # Referenz-Bild
├── head_neutral.png        # 6 Head-Varianten
├── head_happy.png
├── head_angry.png
├── head_sad.png
├── head_surprised.png
├── head_determined.png
├── body.png                # Hals abwärts
└── body_fullbody.png       # Ganzkörper
```

**Akzeptanzkriterien:**
- Alle 6 Köpfe sehen nach der GLEICHEN Person aus (IPAdapter-Konsistenz)
- Emotionen sind klar unterscheidbar
- Stil ist konsistent über alle Varianten

---

## Task 3: Aria Layer-Nachbearbeitung + Atlas

**Ziel:** Hintergrund entfernen, Blink/Mouth-Overlays erstellen, Spritesheet packen.

**Step 1: Hintergrund entfernen (rembg)**

```bash
# rembg installieren falls nicht vorhanden
pip install rembg[gpu]

# Für jedes Bild
rembg i aria/head_neutral.png aria/head_neutral_transparent.png
rembg i aria/body.png aria/body_transparent.png
# ... für alle Bilder
```

**Step 2: Blink-Overlay erstellen**

In GIMP/Photopea:
1. `head_neutral.png` öffnen
2. Augenbereich ausschneiden (kleines Rechteck)
3. Augen übermalen (geschlossene Lider)
4. Als `blink.png` speichern (nur der Augenbereich, transparent drumherum)

**Step 3: Mouth-Overlays erstellen**

In GIMP/Photopea:
1. `head_neutral.png` öffnen
2. Mundbereich ausschneiden
3. Variante 1: Mund leicht offen → `mouth_open.png`
4. Variante 2: Mund weit offen → `mouth_wide.png`

**Step 4: WebP konvertieren**

```bash
# Alle PNGs zu WebP konvertieren (90% Qualität)
for f in aria/*_transparent.png; do
    cwebp -q 90 "$f" -o "${f%.png}.webp"
done
```

**Step 5: Atlas packen**

Python-Script oder manuell in GIMP:
- Alle 11 Layer in ein 4×3 Grid (832×1216 pro Zelle → Atlas ca. 3328×3648)
- JSON-Metadaten mit Source-Rectangles erstellen

```
F:\AI\RebornSaga_Assets\aria\
├── aria_atlas.webp
└── aria_atlas.json
```

`aria_atlas.json`:
```json
{
  "charId": "aria",
  "cellWidth": 832,
  "cellHeight": 1216,
  "layers": {
    "body": { "x": 0, "y": 0 },
    "body_fullbody": { "x": 832, "y": 0 },
    "head_neutral": { "x": 1664, "y": 0 },
    "head_happy": { "x": 2496, "y": 0 },
    "head_angry": { "x": 0, "y": 1216 },
    "head_sad": { "x": 832, "y": 1216 },
    "head_surprised": { "x": 1664, "y": 1216 },
    "head_determined": { "x": 2496, "y": 1216 },
    "blink": { "x": 0, "y": 2432, "w": 200, "h": 100 },
    "mouth_open": { "x": 200, "y": 2432, "w": 150, "h": 100 },
    "mouth_wide": { "x": 350, "y": 2432, "w": 150, "h": 100 }
  },
  "headAnchor": { "x": 416, "y": 350 },
  "blinkAnchor": { "x": 380, "y": 300 },
  "mouthAnchor": { "x": 416, "y": 420 }
}
```

**Akzeptanzkriterien:**
- Alle Layer transparent (kein Hintergrund)
- Atlas lädt als einzelnes SKBitmap
- JSON-Metadaten korrekt (Rectangles stimmen)

---

## Task 4: AssetManifest Model + AssetDeliveryService Interface

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Models/AssetManifest.cs`
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Services/IAssetDeliveryService.cs`

**Step 1: AssetManifest Datenmodell**

```csharp
// Models/AssetManifest.cs
namespace RebornSaga.Models;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed class AssetManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("minAppVersion")]
    public string MinAppVersion { get; set; } = "1.0.0";

    [JsonPropertyName("packs")]
    public Dictionary<string, AssetPack> Packs { get; set; } = new();
}

public sealed class AssetPack
{
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("files")]
    public List<AssetFile> Files { get; set; } = new();
}

public sealed class AssetFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
```

**Step 2: IAssetDeliveryService Interface**

```csharp
// Services/IAssetDeliveryService.cs
namespace RebornSaga.Services;

using SkiaSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public interface IAssetDeliveryService
{
    /// <summary>Prüft ob Assets heruntergeladen werden müssen.</summary>
    bool IsDownloadRequired { get; }

    /// <summary>Formatierte Download-Größe (z.B. "15,3 MB").</summary>
    string DownloadSizeFormatted { get; }

    /// <summary>Prüft Manifest auf Firebase vs. lokalen Cache.</summary>
    Task<AssetCheckResult> CheckForUpdatesAsync();

    /// <summary>Lädt fehlende/geänderte Assets herunter.</summary>
    Task<bool> DownloadAssetsAsync(IProgress<AssetDownloadProgress> progress, CancellationToken ct = default);

    /// <summary>Lädt ein Asset als SKBitmap. Gibt null zurück wenn nicht vorhanden.</summary>
    SKBitmap? LoadBitmap(string relativePath);

    /// <summary>Lädt ein Asset als Stream. Gibt null zurück wenn nicht vorhanden.</summary>
    Stream? GetAssetStream(string relativePath);

    /// <summary>Prüft ob ein Asset lokal gecacht ist.</summary>
    bool HasAsset(string relativePath);
}

public record AssetCheckResult(
    bool UpdateAvailable,
    int FilesToDownload,
    long BytesToDownload,
    string[] ChangedPacks);

public record AssetDownloadProgress(
    int CurrentFile,
    int TotalFiles,
    long BytesDownloaded,
    long TotalBytes,
    string CurrentFileName);
```

**Step 3: Build prüfen**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 4: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Models/AssetManifest.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/Services/IAssetDeliveryService.cs
git commit -m "feat(RebornSaga): AssetManifest Model + IAssetDeliveryService Interface"
```

---

## Task 5: AssetDeliveryService Implementierung

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Services/AssetDeliveryService.cs`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/App.axaml.cs` (DI-Registrierung)

**Step 1: AssetDeliveryService implementieren**

```csharp
// Services/AssetDeliveryService.cs
namespace RebornSaga.Services;

using RebornSaga.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class AssetDeliveryService : IAssetDeliveryService, IDisposable
{
    // Firebase Storage Bucket URL (Public Read)
    private const string BucketBaseUrl = "https://firebasestorage.googleapis.com/v0/b/rebornsaga-assets.firebasestorage.app/o/";
    private const string ManifestUrl = BucketBaseUrl + "manifest.json?alt=media";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _cacheDir;
    private AssetManifest? _remoteManifest;
    private AssetManifest? _localManifest;
    private List<AssetFile>? _filesToDownload;

    public bool IsDownloadRequired => _filesToDownload is { Count: > 0 };
    public string DownloadSizeFormatted => FormatBytes(_filesToDownload?.Sum(f => f.Size) ?? 0);

    public AssetDeliveryService()
    {
        // Persistenter Cache-Ordner (überlebt App-Updates)
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDir = Path.Combine(appData, "RebornSaga", "assets");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<AssetCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            // Remote-Manifest laden
            var json = await _http.GetStringAsync(ManifestUrl);
            _remoteManifest = JsonSerializer.Deserialize<AssetManifest>(json);

            // Lokales Manifest laden (falls vorhanden)
            var localPath = Path.Combine(_cacheDir, "manifest.json");
            if (File.Exists(localPath))
            {
                var localJson = await File.ReadAllTextAsync(localPath);
                _localManifest = JsonSerializer.Deserialize<AssetManifest>(localJson);
            }

            // Delta berechnen: Welche Dateien fehlen oder haben sich geändert?
            _filesToDownload = CalculateDelta();

            var changedPacks = _remoteManifest?.Packs
                .Where(p => p.Value.Required && p.Value.Files.Any(f => _filesToDownload.Any(d => d.Path == f.Path)))
                .Select(p => p.Key)
                .ToArray() ?? Array.Empty<string>();

            return new AssetCheckResult(
                UpdateAvailable: _filesToDownload.Count > 0,
                FilesToDownload: _filesToDownload.Count,
                BytesToDownload: _filesToDownload.Sum(f => f.Size),
                ChangedPacks: changedPacks);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AssetDelivery] Update-Check fehlgeschlagen: {ex.Message}");
            // Offline: mit lokalem Cache weiterarbeiten
            return new AssetCheckResult(false, 0, 0, Array.Empty<string>());
        }
    }

    public async Task<bool> DownloadAssetsAsync(IProgress<AssetDownloadProgress> progress, CancellationToken ct = default)
    {
        if (_filesToDownload == null || _filesToDownload.Count == 0) return true;

        var totalBytes = _filesToDownload.Sum(f => f.Size);
        long downloadedBytes = 0;

        for (var i = 0; i < _filesToDownload.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = _filesToDownload[i];

            progress.Report(new AssetDownloadProgress(
                i + 1, _filesToDownload.Count, downloadedBytes, totalBytes, Path.GetFileName(file.Path)));

            try
            {
                // Datei herunterladen
                var encodedPath = Uri.EscapeDataString(file.Path);
                var url = $"{BucketBaseUrl}{encodedPath}?alt=media";
                var data = await _http.GetByteArrayAsync(url, ct);

                // SHA256 verifizieren
                var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                if (hash != file.Hash)
                {
                    Debug.WriteLine($"[AssetDelivery] Hash-Mismatch: {file.Path} (erwartet {file.Hash}, bekommen {hash})");
                    continue;
                }

                // Lokal speichern
                var localPath = Path.Combine(_cacheDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                await File.WriteAllBytesAsync(localPath, data, ct);

                downloadedBytes += file.Size;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AssetDelivery] Download fehlgeschlagen: {file.Path} — {ex.Message}");
                return false;
            }
        }

        // Lokales Manifest aktualisieren
        if (_remoteManifest != null)
        {
            var manifestPath = Path.Combine(_cacheDir, "manifest.json");
            var json = JsonSerializer.Serialize(_remoteManifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, json, ct);
            _localManifest = _remoteManifest;
        }

        _filesToDownload = null;
        return true;
    }

    public SKBitmap? LoadBitmap(string relativePath)
    {
        var localPath = Path.Combine(_cacheDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(localPath)) return null;

        using var stream = File.OpenRead(localPath);
        return SKBitmap.Decode(stream);
    }

    public Stream? GetAssetStream(string relativePath)
    {
        var localPath = Path.Combine(_cacheDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(localPath) ? File.OpenRead(localPath) : null;
    }

    public bool HasAsset(string relativePath)
    {
        var localPath = Path.Combine(_cacheDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(localPath);
    }

    private List<AssetFile> CalculateDelta()
    {
        if (_remoteManifest == null) return new List<AssetFile>();

        var localFiles = new Dictionary<string, string>(); // path → hash
        if (_localManifest != null)
        {
            foreach (var pack in _localManifest.Packs.Values)
            foreach (var file in pack.Files)
                localFiles[file.Path] = file.Hash;
        }

        var toDownload = new List<AssetFile>();
        foreach (var pack in _remoteManifest.Packs.Values.Where(p => p.Required))
        foreach (var file in pack.Files)
        {
            // Datei fehlt oder Hash stimmt nicht überein
            if (!localFiles.TryGetValue(file.Path, out var localHash) || localHash != file.Hash)
            {
                // Zusätzlich prüfen ob Datei physisch existiert
                var localPath = Path.Combine(_cacheDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localPath) || localHash != file.Hash)
                    toDownload.Add(file);
            }
        }

        return toDownload;
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }

    public void Dispose() => _http.Dispose();
}
```

**Step 2: DI-Registrierung in App.axaml.cs**

In `ConfigureServices()` hinzufügen (nach den Engine-Services):

```csharp
services.AddSingleton<IAssetDeliveryService, AssetDeliveryService>();
```

**Step 3: Build prüfen**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 4: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Services/AssetDeliveryService.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/App.axaml.cs
git commit -m "feat(RebornSaga): AssetDeliveryService Implementierung + DI"
```

---

## Task 6: SpriteDefinitions + SpriteCache

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/SpriteDefinitions.cs`
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Services/SpriteCache.cs`

**Step 1: SpriteDefinitions — Atlas-Metadaten pro Charakter**

```csharp
// Rendering/Characters/SpriteDefinitions.cs
namespace RebornSaga.Rendering.Characters;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Atlas-Metadaten für einen Charakter (aus JSON geladen).
/// </summary>
public sealed class SpriteAtlasData
{
    [JsonPropertyName("charId")]
    public string CharId { get; set; } = "";

    [JsonPropertyName("cellWidth")]
    public int CellWidth { get; set; }

    [JsonPropertyName("cellHeight")]
    public int CellHeight { get; set; }

    [JsonPropertyName("layers")]
    public Dictionary<string, SpriteRect> Layers { get; set; } = new();

    [JsonPropertyName("headAnchor")]
    public SpritePoint HeadAnchor { get; set; } = new();

    [JsonPropertyName("blinkAnchor")]
    public SpritePoint BlinkAnchor { get; set; } = new();

    [JsonPropertyName("mouthAnchor")]
    public SpritePoint MouthAnchor { get; set; } = new();
}

public sealed class SpriteRect
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("w")]
    public int W { get; set; }

    [JsonPropertyName("h")]
    public int H { get; set; }
}

public sealed class SpritePoint
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

/// <summary>
/// Mapping CharId → Asset-Pfade. Zentrale Stelle für alle Sprite-Pfade.
/// </summary>
public static class SpriteDefinitions
{
    public static string GetAtlasImagePath(string charId)
        => $"shared/characters/{charId}_atlas.webp";

    public static string GetAtlasMetaPath(string charId)
        => $"shared/characters/{charId}_atlas.json";

    public static string GetEnemySpritePath(string enemyId)
        => $"{GetPackForEnemy(enemyId)}/enemies/{enemyId}.webp";

    public static string GetBackgroundPath(string sceneKey)
        => $"shared/backgrounds/{sceneKey}.webp";

    /// <summary>Bestimmt welches Asset-Pack einen Gegner enthält.</summary>
    private static string GetPackForEnemy(string enemyId)
    {
        // Prolog-Gegner (P1-P3)
        if (enemyId is "e005" or "b001" or "b002" or "b003" or "b004")
            return "prolog";
        return "arc1";
    }
}
```

**Step 2: SpriteCache — LRU-Cache für Atlanten**

```csharp
// Services/SpriteCache.cs
namespace RebornSaga.Services;

using RebornSaga.Rendering.Characters;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// LRU-Cache für Charakter-Atlanten. Max 5 Charaktere gleichzeitig im RAM.
/// Vorladen während Scene-Transitions möglich.
/// </summary>
public sealed class SpriteCache : IDisposable
{
    private const int MaxCachedCharacters = 5;

    private readonly IAssetDeliveryService _assets;
    private readonly Dictionary<string, CachedAtlas> _cache = new();
    private readonly LinkedList<string> _lruOrder = new();

    public SpriteCache(IAssetDeliveryService assets)
    {
        _assets = assets;
    }

    /// <summary>Lädt Atlas aus Cache oder von Disk.</summary>
    public CachedAtlas? GetAtlas(string charId)
    {
        // Cache-Hit
        if (_cache.TryGetValue(charId, out var cached))
        {
            // LRU aktualisieren
            _lruOrder.Remove(charId);
            _lruOrder.AddFirst(charId);
            return cached;
        }

        // Cache-Miss: laden
        return LoadAtlas(charId);
    }

    /// <summary>Vorladen im Hintergrund (während Transitions).</summary>
    public void Preload(string charId)
    {
        if (_cache.ContainsKey(charId)) return;
        LoadAtlas(charId);
    }

    private CachedAtlas? LoadAtlas(string charId)
    {
        var imagePath = SpriteDefinitions.GetAtlasImagePath(charId);
        var metaPath = SpriteDefinitions.GetAtlasMetaPath(charId);

        if (!_assets.HasAsset(imagePath) || !_assets.HasAsset(metaPath))
        {
            Debug.WriteLine($"[SpriteCache] Atlas nicht gefunden: {charId}");
            return null;
        }

        // Bitmap laden
        var bitmap = _assets.LoadBitmap(imagePath);
        if (bitmap == null) return null;

        // Metadaten laden
        SpriteAtlasData? meta;
        using (var stream = _assets.GetAssetStream(metaPath))
        {
            if (stream == null) { bitmap.Dispose(); return null; }
            meta = JsonSerializer.Deserialize<SpriteAtlasData>(stream);
        }

        if (meta == null) { bitmap.Dispose(); return null; }

        // LRU-Eviction wenn Cache voll
        while (_cache.Count >= MaxCachedCharacters && _lruOrder.Count > 0)
        {
            var evictId = _lruOrder.Last!.Value;
            _lruOrder.RemoveLast();
            if (_cache.Remove(evictId, out var evicted))
            {
                evicted.Bitmap.Dispose();
                Debug.WriteLine($"[SpriteCache] Evicted: {evictId}");
            }
        }

        var atlas = new CachedAtlas(bitmap, meta);
        _cache[charId] = atlas;
        _lruOrder.AddFirst(charId);

        Debug.WriteLine($"[SpriteCache] Geladen: {charId} ({bitmap.Width}x{bitmap.Height})");
        return atlas;
    }

    public void Dispose()
    {
        foreach (var entry in _cache.Values)
            entry.Bitmap.Dispose();
        _cache.Clear();
        _lruOrder.Clear();
    }
}

/// <summary>Gecachter Atlas: SKBitmap + Metadaten.</summary>
public sealed class CachedAtlas
{
    public SKBitmap Bitmap { get; }
    public SpriteAtlasData Meta { get; }

    public CachedAtlas(SKBitmap bitmap, SpriteAtlasData meta)
    {
        Bitmap = bitmap;
        Meta = meta;
    }

    /// <summary>Gibt das Source-Rectangle für einen Layer zurück.</summary>
    public SKRect? GetSourceRect(string layerName)
    {
        if (!Meta.Layers.TryGetValue(layerName, out var rect))
            return null;

        var w = rect.W > 0 ? rect.W : Meta.CellWidth;
        var h = rect.H > 0 ? rect.H : Meta.CellHeight;
        return new SKRect(rect.X, rect.Y, rect.X + w, rect.Y + h);
    }
}
```

**Step 3: DI-Registrierung in App.axaml.cs**

```csharp
services.AddSingleton<SpriteCache>();
```

**Step 4: Build prüfen**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 5: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/SpriteDefinitions.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/Services/SpriteCache.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/App.axaml.cs
git commit -m "feat(RebornSaga): SpriteDefinitions + SpriteCache (LRU, max 5 Chars)"
```

---

## Task 7: SpriteCharacterRenderer

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/SpriteCharacterRenderer.cs`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterRenderer.cs`

**Step 1: SpriteCharacterRenderer implementieren**

```csharp
// Rendering/Characters/SpriteCharacterRenderer.cs
namespace RebornSaga.Rendering.Characters;

using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Rendert AI-generierte Charakter-Sprites mit Layer-Animation.
/// Ersetzt das prozedurale CharacterParts-System.
/// Fallback auf CharacterParts wenn kein Sprite vorhanden.
/// </summary>
public static class SpriteCharacterRenderer
{
    // Gepoolte Paints (kein per-Frame Allokation)
    private static readonly SKPaint _spritePaint = new()
    {
        IsAntialias = true,
        FilterQuality = SKFilterQuality.Medium
    };

    // Blinzel-Timing
    private static float _blinkTimer;
    private static bool _isBlinking;
    private const float BlinkInterval = 4.0f;  // Sekunden zwischen Blinks
    private const float BlinkDuration = 0.15f;  // Blink-Dauer

    // Mund-Animation (Typewriter-Sync)
    private static bool _isSpeaking;
    private static float _mouthTimer;

    /// <summary>Setzt ob gerade gesprochen wird (für Mund-Animation).</summary>
    public static void SetSpeaking(bool speaking) => _isSpeaking = speaking;

    /// <summary>
    /// Zeichnet einen Charakter-Sprite. Gibt false zurück wenn kein Sprite vorhanden → Fallback.
    /// </summary>
    public static bool Draw(SKCanvas canvas, string charId, Emotion emotion,
        float cx, float cy, float scale, float time, RenderMode mode, SpriteCache spriteCache)
    {
        var atlas = spriteCache.GetAtlas(charId);
        if (atlas == null) return false; // Kein Sprite → Fallback auf prozedural

        // Body-Layer bestimmen
        var bodyLayer = mode == RenderMode.FullBody ? "body_fullbody" : "body";
        var bodyRect = atlas.GetSourceRect(bodyLayer);

        // Head-Layer bestimmen (Emotion)
        var headLayer = $"head_{emotion.ToString().ToLowerInvariant()}";
        var headRect = atlas.GetSourceRect(headLayer) ?? atlas.GetSourceRect("head_neutral");

        if (bodyRect == null || headRect == null) return false;

        // Ziel-Größe berechnen
        var targetW = atlas.Meta.CellWidth * scale * 0.5f; // Skalierung an Bildschirm
        var targetH = atlas.Meta.CellHeight * scale * 0.5f;

        // Idle-Breathing: Body bewegt sich leicht vertikal
        var breathOffset = MathF.Sin(time * 1.5f) * 2f * scale;

        // Body zeichnen
        if (mode != RenderMode.Icon)
        {
            var bodyDest = new SKRect(
                cx - targetW / 2f,
                cy - targetH * 0.3f + breathOffset,
                cx + targetW / 2f,
                cy + targetH * 0.7f + breathOffset);
            canvas.DrawBitmap(atlas.Bitmap, bodyRect.Value, bodyDest, _spritePaint);
        }

        // Head zeichnen (leicht weniger Breathing als Body)
        var headBreath = breathOffset * 0.3f;
        var headDest = new SKRect(
            cx - targetW / 2f,
            cy - targetH * 0.7f + headBreath,
            cx + targetW / 2f,
            cy + targetH * 0.3f + headBreath);
        canvas.DrawBitmap(atlas.Bitmap, headRect.Value, headDest, _spritePaint);

        // Blink-Overlay
        UpdateBlink(time);
        if (_isBlinking)
        {
            var blinkRect = atlas.GetSourceRect("blink");
            if (blinkRect != null)
            {
                var anchor = atlas.Meta.BlinkAnchor;
                var blinkScale = targetW / atlas.Meta.CellWidth;
                var blinkDest = new SKRect(
                    cx - targetW / 2f + anchor.X * blinkScale,
                    cy - targetH * 0.7f + anchor.Y * blinkScale + headBreath,
                    cx - targetW / 2f + (anchor.X + blinkRect.Value.Width) * blinkScale,
                    cy - targetH * 0.7f + (anchor.Y + blinkRect.Value.Height) * blinkScale + headBreath);
                canvas.DrawBitmap(atlas.Bitmap, blinkRect.Value, blinkDest, _spritePaint);
            }
        }

        // Mund-Animation (nur wenn gesprochen wird)
        if (_isSpeaking)
        {
            _mouthTimer += 0.016f; // ~60fps
            var mouthLayer = (_mouthTimer % 0.3f) < 0.15f ? "mouth_open" : "mouth_wide";
            var mouthRect = atlas.GetSourceRect(mouthLayer);
            if (mouthRect != null)
            {
                var anchor = atlas.Meta.MouthAnchor;
                var mouthScale = targetW / atlas.Meta.CellWidth;
                var mouthDest = new SKRect(
                    cx - targetW / 2f + anchor.X * mouthScale,
                    cy - targetH * 0.7f + anchor.Y * mouthScale + headBreath,
                    cx - targetW / 2f + (anchor.X + mouthRect.Value.Width) * mouthScale,
                    cy - targetH * 0.7f + (anchor.Y + mouthRect.Value.Height) * mouthScale + headBreath);
                canvas.DrawBitmap(atlas.Bitmap, mouthRect.Value, mouthDest, _spritePaint);
            }
        }
        else
        {
            _mouthTimer = 0;
        }

        return true;
    }

    private static void UpdateBlink(float time)
    {
        _blinkTimer += 0.016f;
        if (_isBlinking)
        {
            if (_blinkTimer >= BlinkDuration)
            {
                _isBlinking = false;
                _blinkTimer = 0;
            }
        }
        else if (_blinkTimer >= BlinkInterval + (MathF.Sin(time) * 1.5f))
        {
            _isBlinking = true;
            _blinkTimer = 0;
        }
    }
}
```

**Step 2: CharacterRenderer-Fassade anpassen**

`CharacterRenderer.cs` — `DrawPortrait()` Methode ändern (Zeile 57):

Ersetze:
```csharp
CharacterParts.DrawCharacter(canvas, def, emotion, cx, cy, scale, time, RenderMode.Portrait);
```
Mit:
```csharp
// Sprite-basiert wenn verfügbar, sonst Fallback auf prozedural
if (!SpriteCharacterRenderer.Draw(canvas, def.Id, emotion, cx, cy, scale, time, RenderMode.Portrait, _spriteCache))
    CharacterParts.DrawCharacter(canvas, def, emotion, cx, cy, scale, time, RenderMode.Portrait);
```

Analog für `DrawFullBody()` (Zeile 73) und `DrawIcon()` (Zeile 84).

**WICHTIG:** `CharacterRenderer` muss eine `SpriteCache`-Referenz bekommen.
Da die Klasse `static` ist, braucht es ein `Initialize(SpriteCache)` Pattern:

```csharp
private static SpriteCache? _spriteCache;

public static void Initialize(SpriteCache spriteCache)
{
    _spriteCache = spriteCache;
}
```

Aufruf in `App.axaml.cs` → `InitializeServicesAsync()`:
```csharp
CharacterRenderer.Initialize(services.GetRequiredService<SpriteCache>());
```

**Step 3: Build prüfen**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 4: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/SpriteCharacterRenderer.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/Rendering/Characters/CharacterRenderer.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/App.axaml.cs
git commit -m "feat(RebornSaga): SpriteCharacterRenderer mit Fallback auf prozedural"
```

---

## Task 8: RebornSagaLoadingPipeline + SplashRenderer

**Files:**
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Loading/RebornSagaLoadingPipeline.cs`
- Create: `src/Apps/RebornSaga/RebornSaga.Shared/Graphics/RebornSagaSplashRenderer.cs`
- Modify: `src/Apps/RebornSaga/RebornSaga.Shared/App.axaml.cs` (Pipeline-Registrierung)

**Step 1: LoadingPipeline mit Asset-Download**

```csharp
// Loading/RebornSagaLoadingPipeline.cs
namespace RebornSaga.Loading;

using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using Microsoft.Extensions.DependencyInjection;
using RebornSaga.Services;
using RebornSaga.ViewModels;
using System.Threading.Tasks;

public sealed class RebornSagaLoadingPipeline : LoadingPipelineBase
{
    public RebornSagaLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();
        var assetService = services.GetRequiredService<IAssetDeliveryService>();

        // Step 1: Asset-Updates prüfen
        AddStep(new LoadingStep
        {
            Name = "CheckAssets",
            DisplayName = loc.GetString("SplashStep_CheckUpdates") ?? "Prüfe Updates...",
            Weight = 10,
            ExecuteAsync = async () =>
            {
                await assetService.CheckForUpdatesAsync();
            }
        });

        // Step 2: Assets herunterladen (wenn nötig)
        AddStep(new LoadingStep
        {
            Name = "DownloadAssets",
            DisplayName = loc.GetString("SplashStep_Download") ?? "Lade Spielinhalte...",
            Weight = 50,
            ExecuteAsync = async () =>
            {
                if (assetService.IsDownloadRequired)
                {
                    // TODO: Bestätigungs-Dialog anzeigen
                    // Vorerst: automatisch herunterladen
                    var progress = new Progress<AssetDownloadProgress>();
                    await assetService.DownloadAssetsAsync(progress);
                }
            }
        });

        // Step 3: Spieldaten + Grafik-Engine
        AddStep(new LoadingStep
        {
            Name = "GameData",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Bereite Spiel vor...",
            Weight = 20,
            ExecuteAsync = async () =>
            {
                var skillTask = services.GetRequiredService<SkillService>().LoadSkillsAsync();
                var itemTask = services.GetRequiredService<InventoryService>().LoadItemsAsync();
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                await Task.WhenAll(skillTask, itemTask, purchaseTask);
            }
        });

        // Step 4: ViewModel initialisieren
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("SplashStep_Init") ?? "Initialisiere...",
            Weight = 20,
            ExecuteAsync = () =>
            {
                services.GetRequiredService<MainViewModel>();
                return Task.CompletedTask;
            }
        });
    }
}
```

**Step 2: SplashRenderer (Anime-themed Loading)**

Basierend auf dem HandwerkerImperium-Pattern (Zahnräder → Anime-Glyphen):

```csharp
// Graphics/RebornSagaSplashRenderer.cs
namespace RebornSaga.Graphics;

using SkiaSharp;
using System;

/// <summary>
/// Anime-themed Loading-Screen: Rotierende Glyphe + System-Blau Glow + Partikel.
/// </summary>
public static class RebornSagaSplashRenderer
{
    private static readonly SKPaint _bgPaint = new() { Color = new SKColor(0x0D, 0x11, 0x17) };
    private static readonly SKPaint _glyphPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f, Color = new SKColor(0x4A, 0x90, 0xD9)
    };
    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f, Color = new SKColor(0x4A, 0x90, 0xD9, 80),
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f)
    };
    private static readonly SKPaint _textPaint = new()
    {
        IsAntialias = true, Color = new SKColor(0x58, 0xA6, 0xFF),
        TextSize = 14f, TextAlign = SKTextAlign.Center
    };
    private static readonly SKPaint _barBgPaint = new()
    {
        Color = new SKColor(0x16, 0x1B, 0x22), Style = SKPaintStyle.Fill
    };
    private static readonly SKPaint _barFillPaint = new()
    {
        Color = new SKColor(0x4A, 0x90, 0xD9), Style = SKPaintStyle.Fill
    };

    public static void Render(SKCanvas canvas, SKRect bounds, float time,
        float progress, string statusText)
    {
        // Hintergrund
        canvas.DrawRect(bounds, _bgPaint);

        var cx = bounds.MidX;
        var cy = bounds.MidY - 40f;

        // Rotierende Hexagon-Glyphe (System-ARIA Stil)
        DrawRotatingGlyph(canvas, cx, cy, 40f, time);

        // Fortschritts-Balken
        var barW = bounds.Width * 0.6f;
        var barH = 6f;
        var barY = cy + 70f;
        var barRect = new SKRect(cx - barW / 2, barY, cx + barW / 2, barY + barH);
        canvas.DrawRoundRect(barRect, 3f, 3f, _barBgPaint);

        var fillRect = new SKRect(barRect.Left, barRect.Top,
            barRect.Left + barRect.Width * Math.Clamp(progress, 0f, 1f), barRect.Bottom);
        canvas.DrawRoundRect(fillRect, 3f, 3f, _barFillPaint);

        // Status-Text
        if (!string.IsNullOrEmpty(statusText))
            canvas.DrawText(statusText, cx, barY + 30f, _textPaint);
    }

    private static void DrawRotatingGlyph(SKCanvas canvas, float cx, float cy, float radius, float time)
    {
        canvas.Save();
        canvas.RotateDegrees(time * 30f, cx, cy);

        // Hexagon
        using var path = new SKPath();
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 3f * i - MathF.PI / 2f;
            var x = cx + radius * MathF.Cos(angle);
            var y = cy + radius * MathF.Sin(angle);
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();

        canvas.DrawPath(path, _glowPaint);
        canvas.DrawPath(path, _glyphPaint);

        canvas.Restore();

        // Innerer Punkt (pulsierend)
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 3f);
        _glyphPaint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(cx, cy, 4f + pulse * 3f, _glyphPaint);
        _glyphPaint.Style = SKPaintStyle.Stroke;
    }
}
```

**Step 3: Pipeline in DI registrieren + in App.axaml.cs verdrahten**

```csharp
services.AddSingleton<ILoadingPipeline, RebornSagaLoadingPipeline>();
```

**Step 4: Build prüfen**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 5: Commit**

```bash
git add src/Apps/RebornSaga/RebornSaga.Shared/Loading/RebornSagaLoadingPipeline.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/Graphics/RebornSagaSplashRenderer.cs
git add src/Apps/RebornSaga/RebornSaga.Shared/App.axaml.cs
git commit -m "feat(RebornSaga): LoadingPipeline mit Asset-Download + Anime SplashRenderer"
```

---

## Task 9: Wolf-Alpha Gegner-Sprite + BattleScene Erweiterung

**Ziel:** Ersten Gegner-Sprite generieren und in BattleScene einbauen.

**Step 1: Wolf-Alpha Sprite generieren (ComfyUI)**

Prompt: `1creature, anime style, giant wolf, dark fur, glowing red eyes, alpha predator, fangs bared, forest background blur, masterpiece, best quality, fantasy monster, menacing pose`

Auflösung: 832×1216 (Portrait), Hintergrund entfernen.

**Step 2: BattleScene erweitern**

In `BattleScene.cs` — `Render()` Methode:
- Obere Bildschirmhälfte: Gegner-Sprite + Name + HP-Balken
- Bestehende untere Hälfte bleibt (Spieler-Stats + Aktions-Buttons)

Neuer Code in BattleScene:
```csharp
private SKBitmap? _enemySprite;

// In OnEnter oder Initialisierung:
var assetService = /* via DI */;
var spritePath = SpriteDefinitions.GetEnemySpritePath(_enemy.Id);
_enemySprite = assetService.LoadBitmap(spritePath);

// In Render():
if (_enemySprite != null)
{
    var enemyArea = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.MidY);
    var destRect = FitToArea(_enemySprite, enemyArea, 0.7f);
    canvas.DrawBitmap(_enemySprite, destRect);
}
// Fallback: Name-Label wie bisher
```

**Step 3: Build + Test**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(RebornSaga): Wolf-Alpha Sprite + BattleScene Gegner-Portrait"
```

---

## Task 10: Title Key Visual + TitleScene Integration

**Ziel:** Dramatisches Anime Key Visual für den Titelbildschirm.

**Step 1: Key Visual generieren (ComfyUI)**

Prompt: `anime key visual, epic fantasy poster, male swordsman hero center, holographic blue spirit guide behind him, dark villain silhouette on horizon, dramatic lighting, starry sky, masterpiece, best quality, very aesthetic, movie poster composition, dark fantasy atmosphere`

Auflösung: 832×1216 (Portrait), WebP komprimieren.

**Step 2: TitleScene anpassen**

In `TitleScene.cs` — `Render()` Methode (Zeile 68):

```csharp
private SKBitmap? _keyVisual;

// In OnEnter:
_keyVisual = assetService.LoadBitmap("shared/ui/title_keyvisual.webp");

// In Render() — VOR BackgroundCompositor:
if (_keyVisual != null)
{
    var destRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    canvas.DrawBitmap(_keyVisual, destRect, _spritePaint);
}
else
{
    // Fallback auf prozeduralen Hintergrund
    BackgroundCompositor.SetScene("title");
    BackgroundCompositor.RenderBack(canvas, bounds, _time);
}
// Partikel drüber
BackgroundCompositor.RenderFront(canvas, bounds, _time);
```

**Step 3: Build + Test**

```bash
dotnet build src/Apps/RebornSaga/RebornSaga.Shared
```

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(RebornSaga): Title Key Visual mit Fallback auf prozedural"
```

---

## Task 11: Lokaler Test (End-to-End ohne Firebase)

**Ziel:** Aria-Sprite + Wolf-Alpha + Key Visual im Spiel testen OHNE Firebase.

**Step 1: Test-Assets lokal bereitstellen**

Assets manuell in den Cache-Ordner kopieren:
```bash
# Windows AppData-Pfad
mkdir -p "$LOCALAPPDATA/RebornSaga/assets/shared/characters/"
mkdir -p "$LOCALAPPDATA/RebornSaga/assets/shared/ui/"
mkdir -p "$LOCALAPPDATA/RebornSaga/assets/arc1/enemies/"

cp F:/AI/RebornSaga_Assets/aria/aria_atlas.webp "$LOCALAPPDATA/RebornSaga/assets/shared/characters/"
cp F:/AI/RebornSaga_Assets/aria/aria_atlas.json "$LOCALAPPDATA/RebornSaga/assets/shared/characters/"
cp F:/AI/RebornSaga_Assets/wolf_alpha.webp "$LOCALAPPDATA/RebornSaga/assets/arc1/enemies/b005_wolf_alpha.webp"
cp F:/AI/RebornSaga_Assets/title_keyvisual.webp "$LOCALAPPDATA/RebornSaga/assets/shared/ui/"
```

**Step 2: Test-Manifest erstellen**

```json
{
  "version": 1,
  "minAppVersion": "1.0.0",
  "packs": {
    "shared": {
      "required": true,
      "totalSize": 500000,
      "files": [
        { "path": "shared/characters/aria_atlas.webp", "hash": "sha256:TODO", "size": 450000 },
        { "path": "shared/characters/aria_atlas.json", "hash": "sha256:TODO", "size": 2000 },
        { "path": "shared/ui/title_keyvisual.webp", "hash": "sha256:TODO", "size": 300000 }
      ]
    },
    "arc1": {
      "required": true,
      "totalSize": 200000,
      "files": [
        { "path": "arc1/enemies/b005_wolf_alpha.webp", "hash": "sha256:TODO", "size": 200000 }
      ]
    }
  }
}
```

Manifest ebenfalls in den Cache-Ordner kopieren.

**Step 3: Desktop-App starten und testen**

```bash
dotnet run --project src/Apps/RebornSaga/RebornSaga.Desktop
```

**Prüfpunkte:**
- [ ] Titelbildschirm zeigt Key Visual statt prozeduralem Hintergrund
- [ ] Dialogue-Szene mit Aria zeigt AI-Sprite statt Bezier-Charakter
- [ ] Aria blinzelt periodisch (3-5s Intervall)
- [ ] Aria bewegt sich leicht (Idle-Breathing)
- [ ] Mund animiert während Typewriter-Text
- [ ] Battle mit Wolf-Alpha zeigt Sprite statt Text-Label
- [ ] Andere Charaktere (ohne Sprite) fallen auf prozedurales Rendering zurück

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(RebornSaga): Phase 1 PoC — Aria Sprite + Wolf-Alpha + Title Visual"
```

---

## Zusammenfassung

| Task | Typ | Geschätzte Dauer |
|------|-----|-----------------|
| 1. Aria Referenz-Bild | AI-Generierung | 1-2h |
| 2. Aria Emotionen + Body | AI-Generierung | 2-3h |
| 3. Layer-Nachbearbeitung + Atlas | Manuell (GIMP) | 2-3h |
| 4. AssetManifest + Interface | Code | 30min |
| 5. AssetDeliveryService | Code | 1h |
| 6. SpriteDefinitions + SpriteCache | Code | 1h |
| 7. SpriteCharacterRenderer | Code | 1-2h |
| 8. LoadingPipeline + SplashRenderer | Code | 1h |
| 9. Wolf-Alpha + BattleScene | Mixed | 1-2h |
| 10. Title Key Visual + TitleScene | Mixed | 1h |
| 11. Lokaler End-to-End Test | Test | 1h |
| **Gesamt** | | **~12-18h (2-3 Tage)** |
