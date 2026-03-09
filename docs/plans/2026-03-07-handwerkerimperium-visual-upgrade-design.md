# HandwerkerImperium Visual Upgrade — Design

## Übersicht

Visuelles Upgrade von 100% prozeduralem SkiaSharp-Rendering zu **Hybrid-Rendering** mit AI-generierten Stylized-Cartoon-Hintergründen (ComfyUI + DreamShaper XL) und prozeduralen Overlays.

**Scope:** Phase 0-2, ~37 Assets, ~3-5 MB WebP, direkt in APK gebundled.

---

## Art Direction

| Eigenschaft | Wert |
|-------------|------|
| Stil | Stylized Cartoon (Hay Day / Township Ästhetik) |
| Modell | DreamShaper XL (SDXL Checkpoint, ~6.5 GB) |
| Farbpalette | Amber-Basis (#D97706), warme Holztöne, goldenes Licht |
| Style-LoRA | Nicht in Phase 0-1, Evaluation nach Phase 1 |
| Nachbearbeitung | Manuelles Review + Krita bei Bedarf |

### Prompt-Templates

**Stil-Prefix (alle Assets):**
```
stylized cartoon illustration, warm lighting, cozy workshop atmosphere,
soft shading, vibrant warm colors, digital painting, game asset, simple background
```

**Negative (alle Assets):**
```
photorealistic, anime, pixel art, dark, gloomy, cold colors, text, watermark
```

**Sampler-Settings:**
- DPM++ 2M Karras, 30 Steps, CFG 7.0
- Auflösung je nach Kategorie (siehe unten)

---

## Phasen

### Phase 0: Proof of Concept (~6 Assets)

Ziel: Stil validieren, RAM/Framerate messen, Hybrid-Rendering testen.

| Asset | Auflösung | Format | Ersetzt Renderer |
|-------|-----------|--------|-----------------|
| City-Hintergrund (vorkomposiert, 1 Bild) | 960x540 | WebP | CityRenderer (Hintergrund-Layer) |
| Meister Hans — happy | 512x512 | WebP | MeisterHansRenderer |
| Meister Hans — proud | 512x512 | WebP | MeisterHansRenderer |
| Meister Hans — concerned | 512x512 | WebP | MeisterHansRenderer |
| Meister Hans — excited | 512x512 | WebP | MeisterHansRenderer |
| Workshop-Szene Schreiner | 512x384 | WebP | WorkshopSceneRenderer |

**Erfolgs-Kriterien:**
- RAM-Zunahme < 5 MB für Phase-0-Assets
- Framerate >= 24fps auf Low-End-Gerät (z.B. Galaxy A15)
- Visueller Stil passt zum Amber-Farbschema
- Prozedurale Overlays (Partikel, Worker-Figuren) harmonieren mit AI-Hintergrund

### Phase 1: City + Workshops (~11 Assets)

| Asset | Auflösung | Format | Anzahl |
|-------|-----------|--------|--------|
| Workshop-Hintergründe (alle 10 Typen) | 512x384 | WebP | 10 |
| Splash-Screen | 1080x1920 | WebP | 1 |

- Alte prozedurale Workshop-Hintergründe werden **gelöscht** (kein Fallback)
- Prozedurale Overlays bleiben: Partikel, Worker-Figuren, Werkzeuge, Level-Effekte

### Phase 2: Portraits + Mini-Games (~20 Assets)

| Asset | Auflösung | Format | Anzahl |
|-------|-----------|--------|--------|
| Worker-Portraits Tier 1-10 | 256x256 | WebP | 10 |
| Mini-Game-Hintergründe (alle 10 Games) | 640x480 | WebP | 10 |

**Worker-Portraits — Tier-Progression:**

| Tier | Charakter | Beschreibung |
|------|-----------|-------------|
| 1 | Lehrling | Jung, unsicher, einfache Kleidung |
| 2 | Anfänger | Etwas erfahrener, erstes Werkzeug |
| 3 | Geselle | Selbstbewusst, ordentliche Arbeitskleidung |
| 4 | Facharbeiter | Kompetent, spezialisiertes Werkzeug |
| 5 | Vorarbeiter | Führungsstärke, Helm + Klemmbrett |
| 6 | Meister | Erfahren, Meisterbrief, edle Werkzeuge |
| 7 | Experte | Souverän, goldene Akzente |
| 8 | Veteran | Legendärer Ruf, Premium-Ausrüstung |
| 9 | Großmeister | Ikonisch, Aura von Autorität |
| 10 | Legende | Mythisch, goldener Glanz, Krone/Lorbeerkranz |

---

## Architektur

### Hybrid-Rendering (2-Layer)

```
┌─────────────────────────────────────┐
│  Layer 2: Prozedurale Overlays      │  Partikel, Glows, Worker-Figuren,
│           (SkiaSharp bleibt)        │  Zahlen, Progress-Bars, Level-FX
├─────────────────────────────────────┤
│  Layer 1: AI-Hintergrund            │  1x DrawBitmap (gecacht aus LRU)
│           (SKBitmap aus WebP)       │
└─────────────────────────────────────┘
```

### Asset-Loading (neuer Service)

```csharp
// IGameAssetService — lädt und cached AI-Assets
public interface IGameAssetService
{
    SKBitmap? GetCachedBitmap(string assetName);
    Task<SKBitmap?> LoadBitmapAsync(string assetName);
    void EvictFromCache(string assetName);
}
```

- **Android:** Assets aus `assets/` Ordner (AndroidAsset Build Action)
- **Desktop:** EmbeddedResource aus Shared-Projekt
- **LRU-Cache:** Max 50 MB, nur sichtbare Szene geladen
- **Lazy Loading:** Assets werden beim ersten Zugriff geladen, nicht beim App-Start

### Asset-Ordnerstruktur

```
HandwerkerImperium.Shared/
└── Assets/
    └── visuals/
        ├── city/
        │   └── city_background.webp
        ├── workshops/
        │   ├── carpenter.webp
        │   ├── plumber.webp
        │   ├── electrician.webp
        │   └── ... (10 Typen)
        ├── workers/
        │   ├── tier_01.webp
        │   ├── tier_02.webp
        │   └── ... (Tier 1-10)
        ├── minigames/
        │   ├── sawing_bg.webp
        │   ├── pipe_puzzle_bg.webp
        │   └── ... (10 Games)
        ├── meister_hans/
        │   ├── happy.webp
        │   ├── proud.webp
        │   ├── concerned.webp
        │   └── excited.webp
        └── splash.webp
```

### Renderer-Umbau (pro Phase)

Bestehende Renderer werden modifiziert, nicht neu geschrieben:

```csharp
// Vorher (WorkshopSceneRenderer.Render):
private void DrawBackground(SKCanvas canvas, ...)
{
    // 80-120 Draw-Calls für Hintergrund-Geometrie
    canvas.DrawRect(...);
    canvas.DrawPath(...);
    // ...
}

// Nachher:
private void DrawBackground(SKCanvas canvas, ...)
{
    var bg = _assetService.GetCachedBitmap($"workshops/{workshopType}");
    if (bg != null)
    {
        canvas.DrawBitmap(bg, destRect);
        return; // 1 Draw-Call statt 80-120
    }
    // Kein Fallback — Asset muss vorhanden sein
}
```

---

## Was NICHT geändert wird

Alle dynamischen/animierten Renderer bleiben vollständig prozedural:

| Renderer | Grund |
|----------|-------|
| OdometerRenderer | Animierte rollende Ziffern |
| RarityFrameRenderer | Dynamische Glows, Shimmer, Pulsation |
| FireworksRenderer | 400 Partikel-System |
| RewardCeremonyRenderer | Confetti, Feuerwerk (120 Partikel) |
| ScreenTransitionRenderer | Wipe/Fade-Transitions |
| GameTabBarRenderer | Prozedural mit Holz-Textur |
| GameCardRenderer | Dynamische Karten-UI |
| ResearchTreeRenderer | Interaktiver Baum mit Bezier-Linien |
| Alle Guild-Renderer | Dynamische Gilden-UI |
| PrestigeRoadmapRenderer | Dynamische Progression |
| Alle Mini-Game-Spielelemente | Nur Hintergrund wird AI, Gameplay bleibt |

---

## Metriken

| Metrik | Ziel |
|--------|------|
| Gesamte Asset-Größe | < 5 MB (WebP) |
| APK-Wachstum | 5 MB → 8-10 MB |
| RAM-Impact (Phase 0-2 komplett) | < 25 MB zusätzlich |
| Framerate (Low-End) | >= 24fps |
| Anzahl Assets | ~37 |

---

## Risiko-Mitigierung

| Risiko | Maßnahme |
|--------|----------|
| RAM/OOM auf Low-End | Halbe Auflösungen, LRU 50MB, Benchmark vor jeder Phase |
| Stil-Inkonsistenz | Manuelles Review, einheitliche Prompt-Templates, ggf. Krita |
| Nutzer-Reaktion | Staged Rollout (10%→50%→100%), Changelog mit Vorschau |
| Performance-Regression | Framerate-Benchmark auf echtem Low-End-Gerät |
| DreamShaper XL passt nicht | Phase 0 ist PoC — Checkpoint-Wechsel noch möglich |

---

## ComfyUI Setup

| Eigenschaft | Wert |
|-------------|------|
| Pfad | `F:\AI\ComfyUI_windows_portable\` |
| Neues Checkpoint | DreamShaper XL → `ComfyUI/models/checkpoints/` |
| Output | `F:\AI\ComfyUI_windows_portable\ComfyUI\output/handwerkerimperium/` |
| Workflow-Scripts | `F:\AI\ComfyUI_workflows/handwerkerimperium/` |

---

## Abgrenzung

- **Kein Play Asset Delivery** — Assets direkt in APK
- **Kein Fallback-System** — Alte prozedurale Hintergründe werden gelöscht
- **Kein Style-LoRA** in Phase 0-1 — erst evaluieren wenn genug Material da ist
- **Keine Icon-Überarbeitung** (222+ GameIcons bleiben SVG-Paths)
- **Keine Guild/Research-Szenen** — nur wenn Phase 2 erfolgreich
- **BomberBlast nicht in Scope** — eigenes Projekt, eigenes Design
