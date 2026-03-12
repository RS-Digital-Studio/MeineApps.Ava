# HandwerkerImperium Visual Upgrade — Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Hybrid-Rendering mit AI-generierten Stylized-Cartoon-Hintergründen (ComfyUI + DreamShaper XL) und prozeduralen Overlays für HandwerkerImperium. Phase 0-2, ~37 Assets.

**Architecture:** Neuer `IGameAssetService` lädt WebP-Assets aus AvaloniaResource/AndroidAsset in einen LRU-SKBitmap-Cache. Bestehende Renderer bekommen eine optionale Bitmap-Hintergrund-Schicht via `Initialize()`-Methode, darüber bleiben prozedurale Overlays (Partikel, Glows, Animationen).

**Tech Stack:** SkiaSharp (SKBitmap), WebP-Format, Avalonia AssetLoader (`avares://HandwerkerImperium.Shared/`) + AndroidAsset, ConcurrentDictionary-Cache mit Lazy<Task>-Deduplication

**Design-Dokument:** `docs/plans/2026-03-07-handwerkerimperium-visual-upgrade-design.md`

---

## Aktueller Status (10. März 2026)

**PLAN ABGESCHLOSSEN. Alle 36 AI-Assets generiert, konvertiert und deployt. Build erfolgreich.**

| Task | Inhalt | Phase | Status |
|------|--------|-------|--------|
| 1 | GameAssetService erstellen (Laden + LRU-Cache) | Infrastruktur | DONE |
| 2 | DI-Registrierung + Plattform-Asset-Loading | Infrastruktur | DONE |
| 3 | ComfyUI Setup: DreamShaper XL + Script | Asset-Gen | DONE |
| 4 | AI-Assets generieren: Phase 0 (City+Hans+Schreiner) | Asset-Gen | DONE |
| 5 | CityRenderer: Hybrid-Hintergrund | Code | DONE |
| 6 | MeisterHansRenderer: AI-Portrait | Code | DONE |
| 7 | WorkshopSceneRenderer + View-Wiring | Code | DONE |
| 8 | Benchmark auf Android-Gerät | Test | OFFEN (nächster Schritt) |
| 9 | AI-Assets generieren: Phase 1+2 (alle restlichen) | Asset-Gen | DONE |
| 10 | WorkshopSceneRenderer: Alle 10 Workshops (AssetNames Dict) | Code | DONE |
| 11 | Splash-Screen: AI Key Visual (direkt AssetLoader) | Code | DONE |
| 12 | Alte prozedurale Hintergründe entfernen | Cleanup | OFFEN (nach Benchmark) |
| 13-14 | (in Task 9 zusammengefasst) | — | — |
| 15 | WorkerAvatarRenderer: AI-Tier-Portraits + Cache-Fix | Code | DONE |
| 16 | Mini-Game Renderer: AI-Hintergründe (alle 10 + Views) | Code | DONE |
| 17 | Build + AppChecker + CLAUDE.md | Final | DONE |

### Asset-Generierung (10. März 2026)

- **ComfyUI + DreamShaper XL Alpha2** (SDXL Checkpoint, 6.5 GB)
- **36 Assets** generiert (je 3 Varianten, v01 ausgewählt)
- **PNG → WebP** konvertiert (Pillow, quality 85, method 6)
- **12.8 MB PNG → 1.2 MB WebP** (90% Kompression)
- Shared-Build + Android-Build: 0 Fehler

### Review-Korrekturen (7. März 2026 — Plan vs. Code-Realität)

| # | Problem im Plan | Korrektur |
|---|-----------------|-----------|
| 1 | Tasks 7/15/16 beschreiben Constructor-Injection | Tatsächlich: `Initialize()` Methode (readonly inline-Felder, kein Constructor-Zugriff) |
| 2 | URI `avares://HandwerkerImperium/` | Korrekt: `avares://HandwerkerImperium.Shared/` (Assembly heißt `.Shared`) |
| 3 | Splash-Pfad `splash.webp` im visuals-Root | Korrekt: `splash/splash.webp` im Unterordner (Konsistenz) |
| 4 | Checkpoint-Name `dreamshaper-xl.safetensors` | Korrekt: `dreamshaperXL_alpha2.safetensors` (6.5 GB, installiert) |
| 5 | 3 separate JSON-Workflows (Task 3) | Tatsächlich: Ein Python-Script `generate_assets.py` mit allen 36 Assets |
| 6 | Strikte Phasen-Trennung (Phase 0 → Benchmark → Phase 1 → Phase 2) | Code ist fertig → alle Assets auf einmal generieren, danach gemeinsam testen |
| 7 | Task 12: "Kein Fallback-System" + 1600 Zeilen löschen | RISKANT — Fallback-Code BEHALTEN als Emergency. Nur löschen nach mehrwöchigem Produktionsbetrieb mit AI-Assets |
| 8 | Worker-Portraits 256x256, aber max Darstellung 128px | Empfehlung: 128x128 generieren (spart 75% RAM pro Portrait). Oder 256 und im Code skalieren (bereits implementiert) |
| 9 | MiniGame-Hintergründe 640x480 (4:3) auf 16:9+ Phones | DrawBitmap streckt → leichte Verzerrung. Akzeptabel für subtile Hintergründe, alternativ 640x960 generieren |

### Optimierte Reihenfolge (restliche Tasks)

Da aller Code fertig ist, fällt die strikte Phasen-Trennung weg:

1. **ComfyUI starten** (Pagefile vorher vergrößern!)
2. **`py generate_assets.py --phase all --variants 5`** — alle 36 Assets in einem Rutsch
3. **Beste Varianten auswählen** → in `Assets/visuals/` kopieren
4. **Desktop visuell testen** (alle Workshops, City, MiniGames, Worker-Avatare)
5. **Android-Build + Benchmark** (Task 8)
6. **Task 12** NUR wenn alles auf dem echten Gerät verifiziert ist
7. **Task 17** (AppChecker + CLAUDE.md)

---

## Task 1: GameAssetService erstellen

**Files:**
- Create: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/IGameAssetService.cs`
- Create: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/GameAssetService.cs`

**Step 1: Interface definieren**

```csharp
// IGameAssetService.cs
namespace HandwerkerImperium.Services;

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
    /// Lädt Asset asynchron und cached es. Gibt das Bitmap zurück.
    /// </summary>
    Task<SKBitmap?> LoadBitmapAsync(string assetPath);

    /// <summary>
    /// Vorladen mehrerer Assets (z.B. beim Szenen-Wechsel).
    /// </summary>
    Task PreloadAsync(IEnumerable<string> assetPaths);

    /// <summary>
    /// Entfernt ein Asset aus dem Cache und disposed das Bitmap.
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
```

**Step 2: GameAssetService implementieren**

> **Review-Korrekturen angewendet:**
> - `Lazy<Task>` statt nacktem `Task.Run` (verhindert paralleles Laden desselben Assets)
> - `SKBitmap.Decode(stream)` direkt statt SKCodec-Umweg (Android non-seekable Streams)
> - MemoryStream-Kopie für Android-Streams (CanSeek=false)
> - Evict entfernt nur aus Cache, disposed NICHT sofort (verhindert Crash bei concurrent GetBitmap+Evict)
> - Dispose erst bei ClearCache/App-Shutdown
> - Avalonia `AssetLoader.Open()` statt `GetManifestResourceStream()` für Desktop

```csharp
// GameAssetService.cs
namespace HandwerkerImperium.Services;

using System.Collections.Concurrent;
using Avalonia.Platform.Storage;
using SkiaSharp;

public sealed class GameAssetService : IGameAssetService
{
    // LRU-Cache: assetPath → (Bitmap, letzterZugriff)
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    // Deduplizierung: Nur EIN Ladevorgang pro Asset (verhindert Race Condition)
    private readonly ConcurrentDictionary<string, Lazy<Task<SKBitmap?>>> _loadingTasks = new();
    private readonly long _maxCacheBytes;
    private long _currentCacheBytes;
    private bool _disposed;

    // Plattform-spezifische Lade-Funktion (wird von Android gesetzt)
    public static Func<string, Stream?>? PlatformAssetLoader { get; set; }

    private sealed class CacheEntry
    {
        public SKBitmap Bitmap { get; init; } = null!;
        public long SizeBytes { get; init; }
        public long LastAccessTick { get; set; }
    }

    public GameAssetService(long maxCacheBytes = 50 * 1024 * 1024) // 50 MB Standard
    {
        _maxCacheBytes = maxCacheBytes;
    }

    public long CacheSizeBytes => _currentCacheBytes;

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
            _pendingDispose.Add(entry.Bitmap);
        }
        _loadingTasks.TryRemove(assetPath, out _);
    }

    // Bitmaps die aus dem Cache entfernt wurden, aber noch in Benutzung sein könnten
    private readonly List<SKBitmap> _pendingDispose = new();

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
        // Jetzt auch pending Bitmaps disposen (App-Shutdown, kein Rendering mehr)
        foreach (var bmp in _pendingDispose)
            bmp.Dispose();
        _pendingDispose.Clear();
        _loadingTasks.Clear();
    }

    private SKBitmap? LoadBitmapInternal(string assetPath)
    {
        try
        {
            using var stream = GetAssetStream(assetPath);
            if (stream == null) return null;

            // Android-Streams sind non-seekable → in MemoryStream kopieren
            Stream decodeStream = stream;
            MemoryStream? ms = null;
            if (!stream.CanSeek)
            {
                ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                decodeStream = ms;
            }

            // SKBitmap.Decode direkt (nicht SKCodec-Umweg, robuster bei verschiedenen Streams)
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

            // Anderer Thread hat bereits gecacht (sollte mit Lazy<Task> nicht mehr passieren)
            bitmap.Dispose();
            return _cache.TryGetValue(assetPath, out var existing2) ? existing2.Bitmap : null;
        }
        catch
        {
            return null;
        }
    }

    private void EvictOldest()
    {
        var oldest = _cache
            .OrderBy(kv => kv.Value.LastAccessTick)
            .FirstOrDefault();

        if (oldest.Key != null)
            Evict(oldest.Key);
    }

    private static Stream? GetAssetStream(string assetPath)
    {
        // 1. Plattform-Loader (Android: AssetManager.Open())
        if (PlatformAssetLoader != null)
        {
            var stream = PlatformAssetLoader(assetPath);
            if (stream != null) return stream;
        }

        // 2. Avalonia AssetLoader (Desktop) — nutzt avares:// URI statt EmbeddedResource
        try
        {
            var uri = new Uri($"avares://HandwerkerImperium/Assets/visuals/{assetPath}");
            return Avalonia.Platform.Storage.AssetLoader.Open(uri);
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
```

**Step 3: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/IGameAssetService.cs
git add src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Services/GameAssetService.cs
git commit -m "feat(HandwerkerImperium): GameAssetService mit LRU-Cache für AI-Assets"
```

---

## Task 2: DI-Registrierung + Plattform-Asset-Loading

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/App.axaml.cs` — DI-Registrierung
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Android/MainActivity.cs` — PlatformAssetLoader setzen

**Step 1: DI-Registrierung in App.axaml.cs**

In der `ConfigureServices`-Methode (oder wo Services registriert werden) hinzufügen:

```csharp
services.AddSingleton<IGameAssetService, GameAssetService>();
```

**Step 2: Android PlatformAssetLoader in MainActivity.cs**

In `OnCreate()` vor dem Base-Aufruf oder nach App-Init:

```csharp
// AI-Asset-Loading für Android (aus assets/ Ordner)
GameAssetService.PlatformAssetLoader = assetPath =>
{
    try
    {
        var fullPath = $"visuals/{assetPath}";
        return Assets?.Open(fullPath);
    }
    catch (Java.IO.FileNotFoundException)
    {
        return null;
    }
};
```

**Step 3: Asset-Ordner und Build Actions konfigurieren**

> **Review-Korrektur:** Assets nur EINMAL ablegen (im Shared-Projekt), nicht duplizieren.
> Android-Projekt referenziert sie per MSBuild-Link.

Assets NUR in Shared-Projekt:
```
HandwerkerImperium.Shared/Assets/visuals/
├── city/
├── workshops/
├── workers/
├── minigames/
├── meister_hans/
└── splash/
```

In `HandwerkerImperium.Shared.csproj` — AvaloniaResource für Desktop (avares://):
```xml
<!-- WebP-Assets als AvaloniaResource (Desktop nutzt AssetLoader.Open) -->
<ItemGroup>
  <AvaloniaResource Include="Assets\visuals\**\*.webp" />
</ItemGroup>
```

In `HandwerkerImperium.Android.csproj` — AndroidAsset per Link aus Shared:
```xml
<!-- AI-Assets aus Shared-Projekt als AndroidAsset einbinden (keine Duplizierung) -->
<ItemGroup>
  <AndroidAsset Include="..\HandwerkerImperium.Shared\Assets\visuals\**\*.webp"
                Link="Assets\visuals\%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

Prüfen ob bestehendes `<AvaloniaResource Include="Assets\**" />` in der Shared-csproj
die neuen WebP-Dateien bereits abdeckt (dann kein extra ItemGroup nötig).

**Step 4: Build prüfen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Android`
Expected: BUILD SUCCEEDED (beide)

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): DI + Android PlatformAssetLoader für GameAssetService"
```

---

## Task 3: ComfyUI Setup — DreamShaper XL + Workflow

**Files:**
- Create: `F:\AI\ComfyUI_workflows\handwerkerimperium\workshop_scene.json`
- Create: `F:\AI\ComfyUI_workflows\handwerkerimperium\character_portrait.json`
- Create: `F:\AI\ComfyUI_workflows\handwerkerimperium\city_background.json`
- Download: DreamShaper XL Checkpoint → `F:\AI\ComfyUI_windows_portable\ComfyUI\models\checkpoints\`

**Step 1: DreamShaper XL herunterladen**

Download von CivitAI oder HuggingFace:
- Modell: `DreamShaperXL_v2_1_Turbo_DPMpp_SDE.safetensors` (oder aktuelle Version)
- Ziel: `F:\AI\ComfyUI_windows_portable\ComfyUI\models\checkpoints\dreamshaper-xl.safetensors`
- Größe: ~6.5 GB

**Step 2: Workshop-Scene Workflow erstellen**

ComfyUI API-Workflow (JSON) mit:
- Checkpoint: DreamShaper XL
- Positive: `stylized cartoon illustration, warm lighting, cozy {workshop_type} workshop interior, soft shading, vibrant warm colors, digital painting, game asset, detailed tools and workbenches, amber warm tones, professional craftsmanship atmosphere`
- Negative: `photorealistic, anime, pixel art, dark, gloomy, cold colors, text, watermark, person, human, character`
- Sampler: DPM++ 2M Karras, 30 Steps, CFG 7.0
- Resolution: 512x384
- Output: `F:\AI\ComfyUI_windows_portable\ComfyUI\output\handwerkerimperium\workshops\`

**Step 3: Character-Portrait Workflow erstellen**

- Positive: `stylized cartoon illustration, warm lighting, portrait of a {tier_description} craftsman, friendly face, soft shading, vibrant warm colors, game character art, bust shot, simple warm background`
- Resolution: 512x512

**Step 4: City-Background Workflow erstellen**

- Positive: `stylized cartoon illustration, panoramic view of a cozy craftsman village, warm sunset lighting, small workshop buildings, chimneys with smoke, cobblestone streets, amber warm tones, game background art, horizontal composition`
- Resolution: 960x540

**Step 5: ComfyUI starten und Workflows testen**

Run: `F:\AI\ComfyUI_windows_portable\run_nvidia_gpu.bat`
Teste jeden Workflow mit 1 Testbild. Prüfe Stil-Konsistenz.

**Step 6: Commit Workflows**

```bash
git add F:\AI\ComfyUI_workflows\handwerkerimperium\
git commit -m "feat(ComfyUI): DreamShaper XL Workflows für HandwerkerImperium"
```

---

## Task 4: AI-Assets generieren — City + Hans + Schreiner (Phase 0)

**MANUELLER SCHRITT — Nicht automatisierbar**

Dieser Task erfordert manuelle Arbeit in ComfyUI:

**Step 1: City-Hintergrund generieren**

- Workflow: `city_background.json`
- Generiere 10-20 Varianten, wähle die beste
- Prüfe: Passt zum Amber-Farbschema (#D97706)? Warme Stimmung? Cartoon-Stil konsistent?
- Nachbearbeitung in Krita falls nötig (Farb-Korrektur, Ausschnitt)
- Exportiere als WebP (Qualität 85): `city_background.webp`

**Step 2: Meister Hans generieren (4 Moods)**

- Workflow: `character_portrait.json`
- Prompt-Varianten für 4 Moods:
  - happy: `...smiling warmly, cheerful expression...`
  - proud: `...confident smile, chest puffed, proud expression...`
  - concerned: `...worried look, furrowed brow, concerned expression...`
  - excited: `...wide eyes, enthusiastic grin, excited expression...`
- Konsistenz: Gleicher Charakter (gelber Schutzhelm, grauer Bart, freundliches Gesicht)
- ggf. img2img mit Referenz-Seed für Konsistenz
- Exportiere als WebP: `happy.webp`, `proud.webp`, `concerned.webp`, `excited.webp`

**Step 3: Schreiner-Workshop generieren**

- Workflow: `workshop_scene.json`
- Prompt: `...cozy carpenter workshop interior, wooden workbench, sawdust, wood planks, hand tools hanging on wall, warm amber lighting...`
- Exportiere als WebP: `carpenter.webp`

**Step 4: Assets in Projekt kopieren**

```
HandwerkerImperium.Shared/Assets/visuals/
├── city/city_background.webp
├── meister_hans/happy.webp
├── meister_hans/proud.webp
├── meister_hans/concerned.webp
├── meister_hans/excited.webp
└── workshops/carpenter.webp
```

> **Review-Korrektur:** Keine Duplizierung. Assets NUR im Shared-Projekt.
> Android referenziert per MSBuild-Link (siehe Task 2, Step 3).

**Step 5: Build Action prüfen**

- Shared: `<AvaloniaResource Include="Assets\visuals\**\*.webp" />` (oder bestehendes `Assets\**` Glob)
- Android: Linked AndroidAsset aus Shared (konfiguriert in Task 2)

**Step 6: Commit**

```bash
git add -A
git commit -m "assets(HandwerkerImperium): Phase-0 AI-Assets (City, Hans, Schreiner)"
```

---

## Task 5: CityRenderer — Hybrid-Hintergrund

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/CityRenderer.cs`
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Views/DashboardView.axaml.cs`

**ACHTUNG:** CityRenderer wird in `DashboardView.axaml.cs` (NICHT IsometricWorldView!) instanziiert als `readonly`-Feld mit Inline-Initializer:
```csharp
private readonly CityRenderer _cityRenderer = new();
```
Da das Feld `readonly` ist und inline initialisiert wird (vor DI-Container), kann man KEINEN Constructor-Parameter nutzen. Stattdessen: `Initialize()`-Methode.

**Step 1: Initialize-Methode in CityRenderer hinzufügen**

```csharp
// Am Anfang der Klasse (neue Felder)
private IGameAssetService? _assetService;
private SKBitmap? _cityBackground;
private bool _cityBackgroundLoaded;

/// <summary>
/// Initialisiert den Renderer mit dem Asset-Service.
/// Muss nach DI-Container-Erstellung aufgerufen werden.
/// </summary>
public void Initialize(IGameAssetService assetService)
{
    _assetService = assetService;
}
```

**Step 2: Hintergrund-Layer durch AI-Asset ersetzen**

In der Render-Methode, VOR den dynamischen Layern:

```csharp
// Vor DrawSkyLayer(), DrawStars(), DrawDistantHills(), DrawClouds(), DrawNearHills()
if (!_cityBackgroundLoaded && _assetService != null)
{
    _cityBackground = _assetService.GetBitmap("city/city_background.webp");
    if (_cityBackground == null)
        _ = _assetService.LoadBitmapAsync("city/city_background.webp");
    else
        _cityBackgroundLoaded = true;
}

if (_cityBackground != null)
{
    // AI-Hintergrund als einzelnes Bild (ersetzt 5 Parallax-Layer)
    var destRect = new SKRect(0, 0, bounds.Width, bounds.Height * 0.7f);
    canvas.DrawBitmap(_cityBackground, destRect);
    // Weiter mit dynamischen Elementen (Gebäude, Figuren, Wetter)
}
else
{
    // Prozedurale Hintergründe (bestehender Code) — nur bis Asset geladen
    DrawSkyLayer(canvas, bounds, ...);
    DrawStars(canvas, bounds, ...);
    DrawDistantHills(canvas, bounds, ...);
    DrawClouds(canvas, bounds, ...);
    DrawNearHills(canvas, bounds, ...);
}
```

**Step 3: DashboardView.axaml.cs — Initialize nach DI aufrufen**

Das `readonly`-Feld `_cityRenderer` bleibt unverändert. Die Initialisierung erfolgt nach dem DI-Container:

```csharp
// In DashboardView.axaml.cs — z.B. in OnAttachedToVisualTree oder OnLoaded:
// (NICHT im Constructor, da App.Services dort noch null sein kann)
var assetService = App.Services?.GetService<IGameAssetService>();
if (assetService != null)
    _cityRenderer.Initialize(assetService);
```

**Step 4: Build + Visuell testen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Run: `dotnet run --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop`

Prüfe: AI-Hintergrund sichtbar, prozedurale Gebäude/Figuren/Wetter darüber.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): CityRenderer Hybrid-Rendering mit AI-Hintergrund"
```

---

## Task 6: MeisterHansRenderer — AI-Portrait

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/MeisterHansRenderer.cs`

**Step 1: Static-Klasse um Asset-Support erweitern**

> **Review-Korrekturen angewendet:**
> - Kein `s_moodBitmaps` (Bitmaps sind bereits im GameAssetService gecacht, doppeltes Caching sinnlos)
> - Methoden-Signatur korrigiert: `Render` mit 5 Parametern (nicht `Draw` mit 4)

MeisterHansRenderer ist eine `static class`. Für AI-Assets eine statische Referenz nutzen:

```csharp
// Oben in der Klasse
private static IGameAssetService? s_assetService;

public static void Initialize(IGameAssetService assetService)
{
    s_assetService = assetService;
}
```

**Step 2: Render-Methode anpassen**

Die bestehende Signatur ist `Render(SKCanvas canvas, SKRect bounds, string mood, float elapsed, bool isBlinking)`.
Nur am Anfang der Methode den AI-Check einfügen:

```csharp
public static void Render(SKCanvas canvas, SKRect bounds, string mood, float elapsed, bool isBlinking)
{
    var assetPath = $"meister_hans/{mood}.webp";

    if (s_assetService != null)
    {
        var bitmap = s_assetService.GetBitmap(assetPath);
        if (bitmap == null)
        {
            // Async laden, beim nächsten Frame verfügbar
            _ = s_assetService.LoadBitmapAsync(assetPath);
        }
        else
        {
            // AI-Portrait zeichnen
            canvas.DrawBitmap(bitmap, bounds);
            return; // Kein prozedurales Rendering nötig
        }
    }

    // Fallback: Prozedurales Portrait (bestehender Code, unverändert)
    // ... DrawHead(), DrawHelmet(), DrawBeard(), DrawEyes(mood, isBlinking), etc.
}
```

**Step 3: Initialize() in App-Startup aufrufen**

> **Review-Korrektur:** NICHT `services.BuildServiceProvider()` aufrufen (erstellt zweiten Provider).
> Stattdessen NACH `App.Services = provider` initialisieren.

In `App.axaml.cs` NACH dem DI-Container-Aufbau:

```csharp
// NACH: App.Services = services.BuildServiceProvider();
// (oder wo der finale ServiceProvider gesetzt wird)
var assetService = App.Services.GetService<IGameAssetService>();
if (assetService != null)
{
    MeisterHansRenderer.Initialize(assetService);
    WorkerAvatarRenderer.InitializeAssetService(assetService); // Task 15
}
```

**Step 4: Build + Test**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Expected: BUILD SUCCEEDED

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): MeisterHansRenderer mit AI-Portrait-Support"
```

---

## Task 7: WorkshopSceneRenderer — Schreiner-PoC

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/WorkshopSceneRenderer.cs`

**Step 1: Initialize-Methode + AssetNames Dictionary**

> **KORREKTUR:** Kein Constructor-Parameter möglich (readonly inline-Feld in View).
> Stattdessen `Initialize()` wie bei CityRenderer.

```csharp
// Explizites Mapping (GeneralContractor → "general_contractor" usw.)
private static readonly Dictionary<WorkshopType, string> AssetNames = new()
{
    { WorkshopType.Carpenter, "carpenter" },
    // ... alle 10 Typen (MasterSmith → "master_smith", etc.)
};

private IGameAssetService? _assetService;

public void Initialize(IGameAssetService assetService)
{
    _assetService = assetService;
}
```

**Step 2: Hintergrund-Layer in Render() einfügen**

```csharp
// AI-Hintergrund versuchen (Dictionary statt ToString(), da Enum-Werte ≠ Dateinamen)
var assetName = AssetNames.GetValueOrDefault(workshop.Type, "carpenter");
var assetPath = $"workshops/{assetName}.webp";
var bgBitmap = _assetService?.GetBitmap(assetPath);

if (bgBitmap == null && _assetService != null)
    _ = _assetService.LoadBitmapAsync(assetPath);

if (bgBitmap != null)
{
    canvas.DrawBitmap(bgBitmap, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    DrawLevelEffects(canvas, bounds, level, phase);
    // Partikel-Callbacks MÜSSEN weiter feuern
    if (isProducing && p % 2.0f < 0.05f)
        addWorkParticle?.Invoke(bounds.MidX, bounds.Top, GetWorkshopColor(workshop.Type));
    if (isProducing && p % 5.0f < 0.05f)
        addCoinParticle?.Invoke(bounds.MidX, bounds.Top);
    return;
}
// Prozeduraler Fallback (bestehender switch-Code)
```

**Step 3: WorkshopView.axaml.cs — Initialize nach DI aufrufen**

```csharp
// In OnDataContextChanged (NICHT Constructor — App.Services kann null sein):
var assetService = App.Services?.GetService<IGameAssetService>();
if (assetService != null)
    _sceneRenderer.Initialize(assetService);
```

**Step 4: Build + Visuell testen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Run: `dotnet run --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop`

Prüfe: Schreiner-Workshop zeigt AI-Hintergrund. Andere Workshops zeigen prozedural.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): WorkshopSceneRenderer Hybrid für Schreiner-PoC"
```

---

## Task 8: Benchmark auf Android-Gerät

**MANUELLER SCHRITT**

**Step 1: Debug-Build auf Android-Gerät deployen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Android -t:Install`

**Step 2: RAM messen**

Vor AI-Assets und nach AI-Assets vergleichen:
- Android Studio → Profiler → Memory
- Oder: `adb shell dumpsys meminfo de.robertsapps.handwerkerimperium`

**Step 3: Framerate messen**

In CityRenderer/WorkshopSceneRenderer temporär Stopwatch einbauen:

```csharp
#if DEBUG
private readonly System.Diagnostics.Stopwatch _sw = new();
private int _frameCount;
private long _totalMs;

// Am Anfang von Render():
_sw.Restart();
// Am Ende:
_sw.Stop();
_totalMs += _sw.ElapsedMilliseconds;
_frameCount++;
if (_frameCount % 60 == 0)
    System.Diagnostics.Debug.WriteLine($"[CityRenderer] Avg: {_totalMs / _frameCount}ms/frame");
#endif
```

**Step 4: Ergebnisse dokumentieren**

Erwartete Zielwerte:
- RAM-Zunahme durch Phase-0-Assets: < 5 MB
- Render-Zeit CityRenderer: < 16ms (60fps) oder < 42ms (24fps)
- Kein OOM auf Test-Gerät

**Step 5: Entscheidung**

- PASS → Weiter mit Phase 1 (Task 9)
- FAIL RAM → Auflösungen reduzieren (480x270 City, 256x192 Workshop)
- FAIL Performance → DrawBitmap-Skalierung prüfen, ggf. Bitmap auf Ziel-Größe vorab skalieren

---

## Task 9: AI-Assets generieren — 9 Workshops + Splash (Phase 1)

**MANUELLER SCHRITT**

**Step 1: 9 verbleibende Workshop-Szenen generieren**

Prompts pro Workshop-Typ (Stil-Prefix beibehalten):

| Workshop | Spezifischer Prompt-Teil |
|----------|-------------------------|
| plumber | `...plumber workshop, copper pipes on walls, wrenches, water faucets, blue accent tiles...` |
| electrician | `...electrician workshop, circuit boards, wire spools, electrical panels, blue spark accents...` |
| painter | `...painter workshop, paint cans, brushes, color swatches on wall, splatter accents...` |
| roofer | `...roofer workshop, roof tiles stacked, wooden beams, ladders, terracotta accents...` |
| contractor | `...construction office, blueprints on desk, hard hats, building models...` |
| architect | `...architect studio, drafting table, compass, ruler, building sketches, clean modern...` |
| general_contractor | `...large construction office, project boards, multiple screens, professional...` |
| master_smith | `...master blacksmith forge, anvil, glowing metal, hammers, medieval warmth...` |
| innovation_lab | `...modern innovation lab, holographic displays, futuristic tools, purple-gold accents...` |

10-20 Varianten pro Typ, beste auswählen, ggf. Krita-Nachbearbeitung.

**Step 2: Splash-Screen generieren**

- Auflösung: 1080x1920 (Portrait)
- Prompt: `...stylized cartoon game title screen, cozy craftsman village at sunset, anvil and hammer centerpiece, warm amber glow, "Handwerker Imperium" text space at top, professional game art...`

**Step 3: Assets in Projekt kopieren**

```
HandwerkerImperium.Shared/Assets/visuals/workshops/
├── carpenter.webp (bereits vorhanden)
├── plumber.webp
├── electrician.webp
├── painter.webp
├── roofer.webp
├── contractor.webp
├── architect.webp
├── general_contractor.webp
├── master_smith.webp
└── innovation_lab.webp
```

Gleiches für Android `Assets/visuals/workshops/` und `splash/`.

**Step 4: Commit**

```bash
git add -A
git commit -m "assets(HandwerkerImperium): Phase-1 AI-Assets (10 Workshops + Splash)"
```

---

## Task 10: WorkshopSceneRenderer — Alle 10 Workshops

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/WorkshopSceneRenderer.cs`

**Step 1: Workshop-Typ zu Asset-Pfad Mapping prüfen**

Der in Task 7 eingebaute Code nutzt `workshopType.ToString().ToLowerInvariant()`. Prüfe ob die Workshop-Enum-Werte den Dateinamen entsprechen. Falls nicht, Dictionary-Mapping erstellen:

```csharp
private static readonly Dictionary<WorkshopType, string> AssetNames = new()
{
    { WorkshopType.Carpenter, "carpenter" },
    { WorkshopType.Plumber, "plumber" },
    { WorkshopType.Electrician, "electrician" },
    { WorkshopType.Painter, "painter" },
    { WorkshopType.Roofer, "roofer" },
    { WorkshopType.Contractor, "contractor" },
    { WorkshopType.Architect, "architect" },
    { WorkshopType.GeneralContractor, "general_contractor" },
    { WorkshopType.MasterSmith, "master_smith" },
    { WorkshopType.InnovationLab, "innovation_lab" },
};
```

**Step 2: Vorladen beim Szenen-Wechsel**

In der View oder im ViewModel, wenn ein Workshop geöffnet wird:

```csharp
// Beim Öffnen einer Workshop-Szene
var assetPath = $"workshops/{AssetNames[workshopType]}.webp";
await _assetService.LoadBitmapAsync(assetPath);
```

**Step 3: Build + Alle 10 Workshops visuell testen**

Run: `dotnet run --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop`

Jeden Workshop öffnen, prüfen:
- AI-Hintergrund wird angezeigt
- Level-Effekte (Glow, Sterne, Premium-Aura) erscheinen darüber
- Keine visuellen Artefakte

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): Alle 10 Workshops mit AI-Hintergründen"
```

---

## Task 11: Splash-Screen — AI Key Visual

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/HandwerkerImperiumSplashRenderer.cs`

**ACHTUNG:** Der SplashRenderer wird in `App.axaml.cs:CreateSplash()` erstellt — das passiert VOR dem DI-Container! `App.Services` ist noch null, `IGameAssetService` nicht verfügbar. Daher: **Direkt aus AvaloniaResource laden**, OHNE GameAssetService.

**Step 1: Splash-Hintergrund synchron aus AvaloniaResource laden**

```csharp
// Neue Felder in HandwerkerImperiumSplashRenderer
private SKBitmap? _splashBackground;

// Im Constructor ODER beim ersten Render laden (kein DI nötig!)
private SKBitmap? LoadSplashFromResource()
{
    try
    {
        // AvaloniaResource braucht kein DI — statischer Zugriff
        var uri = new Uri("avares://HandwerkerImperium.Shared/Assets/visuals/splash/splash.webp");
        using var stream = Avalonia.Platform.AssetLoader.Open(uri);

        // AvaloniaResource-Streams sind seekable → direkt decodieren
        return SKBitmap.Decode(stream);
    }
    catch
    {
        // Asset fehlt → prozeduraler Hintergrund bleibt
        return null;
    }
}
```

**Step 2: In OnRender als Hintergrund nutzen**

```csharp
protected override void OnRender(SKCanvas canvas, SKRect bounds)
{
    var w = bounds.Width;
    var h = bounds.Height;

    InitializeEmbers(w, h);
    _titleGlowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    // AI-Splash als Hintergrund (einmalig laden)
    _splashBackground ??= LoadSplashFromResource();

    if (_splashBackground != null)
    {
        canvas.DrawBitmap(_splashBackground, new SKRect(0, 0, w, h));
    }
    else
    {
        // Bestehender prozeduraler Hintergrund
        RenderBackground(canvas, bounds, w, h);
    }

    // Zahnräder, Amboss, Hammer, Funken DARÜBER (bleiben)
    RenderTitle(canvas, w, h);
    RenderGears(canvas, w, h);
    RenderGearSparks(canvas, w, h);
    RenderAnvil(canvas, w, h);
    RenderHammer(canvas, w, h);
    RenderImpactSparks(canvas, w, h);
    RenderEmbers(canvas, w, h);

    // Fortschrittsbalken + Status-Text + Version
    var barWidth = Math.Min(260f, w * 0.6f);
    DrawProgressBar(canvas, w, h * 0.72f, barWidth, 8f, 4f, CraftOrange, GoldColor, BgBottom);
    DrawStatusText(canvas, w, h * 0.77f);
    DrawVersion(canvas, w, h * 0.92f);
}
```

**Step 3: Dispose erweitern**

```csharp
protected override void OnDispose()
{
    _splashBackground?.Dispose();
    // ... bestehende Dispose-Logik
}
```

**Step 4: Build + testen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): Splash-Screen mit AI Key Visual"
```

---

## Task 12: Alte prozedurale Hintergründe entfernen (Phase 1 Abschluss)

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/WorkshopSceneRenderer.cs`
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/CityRenderer.cs`

### WICHTIG: Was BEHALTEN wird (NICHT löschen!)

Folgende Methoden in `WorkshopSceneRenderer.cs` sind **dynamische Overlay-Elemente**, KEINE statischen Hintergründe:

| Methode | Zeile | Grund zum Behalten |
|---------|-------|--------------------|
| `RenderIdle()` | ~93 | Zeichnet gedimmten Leerlauf-Zustand (0 Worker): Dim-Overlay + Idle-Tools + Warnsymbol |
| `DrawIdleTools()` | ~740 | Zeichnet stillliegende Werkzeuge je Workshop-Typ (Switch über 10 Typen) |
| `DrawLevelEffects()` | div. | Level-basierte Glow/Sterne/Aura/Shimmer-Overlays |
| `DrawShadow()` + `DrawCircleShadow()` | ~144/153 | Hilfs-Methoden für Drop-Shadows |
| Alle `DrawXxx()`-Hilfsmethoden | div. | Prozedurale Overlay-Elemente (Flammen, Dampf, Werkzeuge, Partikel) |

**Partikel-Callbacks** (`addWorkParticle?.Invoke()`, `addCoinParticle?.Invoke()`) MÜSSEN auch mit AI-Hintergrund weiter aufgerufen werden (bereits in Task 7 korrigiert).

### Was GELÖSCHT wird

NUR die 10 statischen Hintergrund-Zeichenmethoden, die durch AI-Assets ersetzt werden:

| Methode | Beschreibung |
|---------|-------------|
| `DrawCarpenterScene()` | Statischer Schreiner-Werkstatt-Hintergrund |
| `DrawPlumberScene()` | Statischer Klempner-Werkstatt-Hintergrund |
| `DrawElectricianScene()` | ... usw. für alle 10 Workshop-Typen |
| `DrawPainterScene()` | |
| `DrawRooferScene()` | |
| `DrawContractorScene()` | |
| `DrawArchitectScene()` | |
| `DrawGeneralContractorScene()` | |
| `DrawMasterSmithScene()` | |
| `DrawInnovationLabScene()` | |

**Step 1: WorkshopSceneRenderer — NUR die 10 DrawXxxScene()-Methoden entfernen**

Die 10 `DrawXxxScene()`-Methoden (Zeilen ~821-2482) entfernen. Der `switch`-Block der diese aufruft wird zum Emergency-Fallback:

```csharp
// Statt dem kompletten switch-Block mit DrawXxxScene()-Aufrufen:
if (bgBitmap == null)
{
    // Asset fehlt — sollte nicht passieren (ist in APK gebundled)
    System.Diagnostics.Debug.WriteLine($"[WorkshopScene] Asset fehlt: {assetPath}");
    // Einfacher farbiger Hintergrund als Emergency-Fallback
    canvas.DrawColor(new SKColor(0xFF2D1B0E)); // Dunkles Holzbraun
}
```

Das entfernt ~1600 Zeilen. **RenderIdle(), DrawIdleTools(), DrawLevelEffects() und alle Hilfsmethoden BLEIBEN.**

**Step 2: CityRenderer — Prozedurale Parallax-Layer entfernen**

`DrawSkyLayer()`, `DrawStars()`, `DrawDistantHills()`, `DrawClouds()`, `DrawNearHills()` entfernen. Gleicher Emergency-Fallback. Dynamische Elemente (Gebäude, Figuren, Wetter, Tap-Labels) bleiben.

**Step 3: Nicht mehr benötigte SKPaint/SKPath/SKMaskFilter entfernen**

Prüfe welche gecachten Ressourcen NUR von den entfernten `DrawXxxScene()`-Methoden genutzt wurden → aus Dispose() und Feld-Deklarationen entfernen. **SKPaints die von RenderIdle/DrawIdleTools/DrawLevelEffects genutzt werden BEHALTEN** (z.B. `_fillPaint`, `_strokePaint`).

**Step 4: Build + visuell testen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Run: `dotnet run --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop`

Alle Workshops + City prüfen. Besonders testen:
- Workshop mit 0 Workern → RenderIdle() muss Dim-Overlay + Idle-Tools + Warnsymbol zeigen
- Workshop mit Workern → AI-Hintergrund + prozedurale Level-Effekte + Partikel
- City-Ansicht → AI-Hintergrund + Gebäude/Figuren/Wetter darüber

**Step 5: Commit**

```bash
git add -A
git commit -m "refactor(HandwerkerImperium): Prozedurale Hintergründe entfernt (ersetzt durch AI-Assets)"
```

---

## Task 13: AI-Assets generieren — Worker-Portraits Tier 1-10 (Phase 2)

**MANUELLER SCHRITT**

**Step 1: 10 Tier-Portraits generieren**

| Tier | Prompt-Teil |
|------|------------|
| 1 (Lehrling) | `young apprentice craftsman, nervous smile, simple work clothes, no hat, holding broom` |
| 2 (Anfänger) | `beginner craftsman, gaining confidence, basic tool belt, simple cap` |
| 3 (Geselle) | `journeyman craftsman, confident stance, proper work uniform, leather apron` |
| 4 (Facharbeiter) | `skilled worker, determined look, specialized tools, safety goggles on head` |
| 5 (Vorarbeiter) | `foreman, leadership aura, yellow hard hat, clipboard, whistle` |
| 6 (Meister) | `master craftsman, wise experienced face, master certificate on wall, fine tools` |
| 7 (Experte) | `expert craftsman, calm authority, golden tool accents, premium work jacket` |
| 8 (Veteran) | `veteran master, legendary reputation, ornate leather apron, golden buckles` |
| 9 (Großmeister) | `grand master craftsman, iconic presence, elaborate outfit, medals, authority aura` |
| 10 (Legende) | `legendary craftsman, mythical aura, golden glow, laurel crown, masterwork tools` |

Alle 512x512 WebP.

**Step 2: Assets in Projekt kopieren**

```
Assets/visuals/workers/
├── tier_01.webp
├── tier_02.webp
├── ...
└── tier_10.webp
```

**Step 3: Commit**

```bash
git add -A
git commit -m "assets(HandwerkerImperium): Phase-2 Worker-Portraits (Tier 1-10)"
```

---

## Task 14: AI-Assets generieren — 10 Mini-Game-Hintergründe (Phase 2)

**MANUELLER SCHRITT**

**Step 1: 10 Mini-Game-Hintergründe generieren**

| Mini-Game | Prompt-Teil |
|-----------|------------|
| sawing | `...carpenter workshop background, wooden workbench with wood grain, sawdust in air...` |
| pipe_puzzle | `...plumbing basement, concrete walls, exposed pipes, puddles, industrial lighting...` |
| wiring | `...electrical room, fuse box on wall, cable trays, warning signs, blue-white lighting...` |
| painting | `...empty room ready for painting, bare plaster walls, paint cans on floor, drop cloth...` |
| blueprint | `...architect desk, blueprint paper texture, compass and ruler, technical lighting...` |
| roof_tiling | `...roof construction view, wooden roof frame, sky visible, tiles stacked nearby...` |
| design_puzzle | `...modern design studio, grid paper, architectural models, clean minimalist...` |
| inspection | `...construction site, concrete structure, safety barriers, inspection checklist...` |
| forge | `...blacksmith forge, glowing embers, anvil, dark interior with fire glow, medieval...` |
| invent | `...invention workshop, gears and circuits, purple glow, futuristic-meets-vintage...` |

Alle 640x480 WebP.

**Step 2: Assets kopieren + Commit**

```bash
git add -A
git commit -m "assets(HandwerkerImperium): Phase-2 Mini-Game-Hintergründe (10 Games)"
```

---

## Task 15: WorkerAvatarRenderer — AI-Tier-Portraits

**Files:**
- Modify: `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Graphics/WorkerAvatarRenderer.cs`

**Step 1: Tier-Portrait-Integration**

Der bestehende Cache nutzt Key `"{idSeed}|{tier}|{moodBucket}|{size}|{gender}"`. Für AI-Portraits nur den Tier verwenden:

```csharp
// Neue statische Referenz
private static IGameAssetService? s_assetService;

public static void InitializeAssetService(IGameAssetService assetService)
{
    s_assetService = assetService;
}

// In der Render/GetAvatar-Methode, VOR dem prozeduralen Rendering:
private static SKBitmap? GetTierPortrait(WorkerTier tier)
{
    var tierIndex = (int)tier + 1; // 0-basiert → 1-basiert
    var assetPath = $"workers/tier_{tierIndex:D2}.webp";
    return s_assetService?.GetBitmap(assetPath);
}
```

**Step 2: Portrait in Render-Logik einbauen**

```csharp
// Wenn AI-Portrait verfügbar, statt prozeduraler Generierung:
var portrait = GetTierPortrait(workerTier);
if (portrait != null)
{
    // Skaliert in die Ziel-Bounds zeichnen
    var srcRect = new SKRect(0, 0, portrait.Width, portrait.Height);
    canvas.DrawBitmap(portrait, srcRect, destBounds);
    return;
}

// Fallback: Bestehende prozedurale Generierung (Übergangsphase)
// ... existierender Code
```

**Step 3: Initialize in App-Startup**

```csharp
WorkerAvatarRenderer.InitializeAssetService(assetService);
```

**Step 4: Build + Test**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): WorkerAvatarRenderer mit AI-Tier-Portraits"
```

---

## Task 16: Mini-Game Renderer — AI-Hintergründe

**Files:**
- Modify: Alle 10 Mini-Game-Renderer in `Graphics/`

**Step 1: Pattern für alle Mini-Games**

Gleiches Pattern für jeden der 10 Mini-Game-Renderer:

```csharp
// 1. Feld + Constructor-Parameter
private readonly IGameAssetService? _assetService;
private SKBitmap? _background;

public XxxGameRenderer(IGameAssetService? assetService = null)
{
    _assetService = assetService;
    // ... bestehend
}

// 2. Am Anfang der Render-Methode
_background ??= _assetService?.GetBitmap("minigames/xxx_bg.webp");
if (_background == null && _assetService != null)
    _ = _assetService.LoadBitmapAsync("minigames/xxx_bg.webp");

if (_background != null)
    canvas.DrawBitmap(_background, new SKRect(0, 0, bounds.Width, bounds.Height));
else
    DrawProceduralBackground(canvas, bounds); // Bestehender Hintergrund-Code

// 3. Gameplay-Elemente DARÜBER weiterhin zeichnen
```

**Step 2: Mapping Mini-Game → Asset-Dateiname**

| Renderer | Asset |
|----------|-------|
| SawingGameRenderer | `minigames/sawing_bg.webp` |
| PipePuzzleRenderer | `minigames/pipe_puzzle_bg.webp` |
| WiringGameRenderer | `minigames/wiring_bg.webp` |
| PaintingGameRenderer | `minigames/painting_bg.webp` |
| BlueprintGameRenderer | `minigames/blueprint_bg.webp` |
| RoofTilingRenderer | `minigames/roof_tiling_bg.webp` |
| DesignPuzzleRenderer | `minigames/design_puzzle_bg.webp` |
| InspectionGameRenderer | `minigames/inspection_bg.webp` |
| ForgeGameRenderer | `minigames/forge_bg.webp` |
| InventGameRenderer | `minigames/invent_bg.webp` |

**Step 3: Alle 10 Views anpassen (IGameAssetService durchreichen)**

**Step 4: Build + Alle 10 Mini-Games visuell testen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`
Run: `dotnet run --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop`

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): Alle 10 Mini-Games mit AI-Hintergründen"
```

---

## Task 17: Build + AppChecker + Abschluss

**Step 1: Gesamte Solution bauen**

Run: `dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln`
Expected: BUILD SUCCEEDED (0 Errors)

**Step 2: AppChecker laufen lassen**

Run: `dotnet run --project tools/AppChecker HandwerkerImperium`
Expected: Keine kritischen Fehler

**Step 3: Android-Build testen**

Run: `dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Android`
Expected: BUILD SUCCEEDED

**Step 4: APK-Größe prüfen**

Die APK/AAB sollte um ~3-5 MB gewachsen sein (37 WebP-Assets).
Ziel: < 15 MB gesamt.

**Step 5: CLAUDE.md aktualisieren**

App-spezifische CLAUDE.md (`src/Apps/HandwerkerImperium/CLAUDE.md`) aktualisieren:
- Neuer GameAssetService dokumentieren
- Hybrid-Rendering-Pattern dokumentieren
- Asset-Ordnerstruktur dokumentieren
- Neue DI-Registrierungen

**Step 6: Commit**

```bash
git add -A
git commit -m "feat(HandwerkerImperium): Visual Upgrade Phase 0-2 abgeschlossen"
```
