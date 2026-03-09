# RebornSaga: Visual Upgrade + Cloud Asset Delivery — Design-Dokument

> Datum: 2026-03-06
> Status: Genehmigt
> Scope: AI-generierte Assets für alle visuellen Bereiche + Firebase Storage Delivery
> Voraussetzung: ComfyUI läuft, Animagine XL 4.0 getestet, Firebase-Erfahrung vorhanden

---

## Zusammenfassung

Kompletter visueller Upgrade für RebornSaga: Alle prozeduralen SkiaSharp-Grafiken werden durch
AI-generierte Anime-Assets (Stable Diffusion / Animagine XL 4.0) ersetzt. Assets werden über
Firebase Storage ausgeliefert und beim App-Start heruntergeladen (Delta-Updates via SHA256-Hashes).

**Kern-Prinzip:** Die SkiaSharp-Engine bleibt als Animations- und Effekt-Layer erhalten.
AI-Bilder sind die statische Basis, SkiaSharp liefert Dynamik (Partikel, Lighting, Breathing, Blinzeln).

---

## 1. Asset-Übersicht

### 1.1 Charakter-Sprites (10 Charaktere, ~150-180 Generierungen)

**Impact: 10/10** — 80% der Spielzeit sind Dialoge mit Charakter-Portraits.

| Charakter | Typ | Haarfarbe | Augenfarbe | Outfit | Waffe |
|-----------|-----|-----------|------------|--------|-------|
| Protagonist_Sword | Spieler | Dunkelblau (#2C3E50), kurz/wild | System-Blau (#4A90D9) | Dunkelblau-Grau + rote Akzente | Schwert |
| Protagonist_Mage | Spieler | Lila (#6C3C97), lang/glatt | Mystisch-Lila (#9B59B6) | Dunkelblau + Lila-Akzente | Stab |
| Protagonist_Assassin | Spieler | Schwarz (#1C1C1C), kurz | Gift-Grün (#2ECC71) | Sehr Dunkel + Grün-Akzente | Dolche |
| Aria | Kriegerin | Feuerrot (#CC3333), lang | Grasgrün (#2ECC71) | Braunes Leder + Gold | Schwert |
| Luna | Heilerin | Hellblau (#5DAEE3), sehr lang/Zopf | Lavendel (#AD8BFA) | Weiß + Hellblau | Heilstab |
| Kael | Rivale/Dieb | Braun (#8B6514), mittellang/wild | Amber (#F39C12) | Dunkelbraun + Gold | Dolche |
| Aldric | Erzmagier | Weiß/Silber (#E0E0E8), lang | Leuchtendes Blau (#58A6FF) | Dunkles Lila + Gold | Stab |
| Vex | Dieb/Händler | Schwarz (#2C2C2C), sehr kurz | Rot (#E74C3C) | Dunkelbraun + Gold | Dolche |
| System_ARIA | KI-Geist | Blau (#58A6FF), transparent | Leuchtendes Blau + Glow | Blau-transparent | Keine |
| Nihilus | Endgegner | Schwarz (#0A0A0A), wild | Dunkelrot (#8B0000) + Glow | Dunkle Robe + Blutrot | Keine |

**Layer pro Charakter (11 Stück):**

```
Assets/Characters/{charId}/
├── body.webp                # Hals abwärts (Breathing-Animation per SkiaSharp)
├── body_fullbody.webp       # Ganzkörper (ClassSelect, Status, Codex)
├── head_neutral.webp        # Kompletter Kopf — neutraler Ausdruck
├── head_happy.webp          # Kompletter Kopf — glücklich
├── head_angry.webp          # Kompletter Kopf — wütend
├── head_sad.webp            # Kompletter Kopf — traurig
├── head_surprised.webp      # Kompletter Kopf — überrascht
├── head_determined.webp     # Kompletter Kopf — entschlossen
├── blink.webp               # Augenbereich geschlossen (Overlay)
├── mouth_open.webp          # Mundbereich offen (Sprechen Frame 1)
└── mouth_wide.webp          # Mundbereich weit (Sprechen Frame 2)
```

**Texture Atlas:** Pro Charakter wird ein Spritesheet + JSON-Metadaten erstellt:
```
{charId}_atlas.webp    # Alle Layer in einem Grid
{charId}_atlas.json    # Source-Rectangles pro Layer
```

### 1.2 Gegner-Sprites (21 Gegner, ~50-60 Generierungen)

**Impact: 9/10** — Kämpfe gegen Text-Labels sind nicht tragbar.

| Typ | Anzahl | Format | Beispiele |
|-----|--------|--------|-----------|
| Reguläre Gegner | 5 | Half-Body Portrait | Schattenwolf, Waldschleim, Goblin, Festungswache, Schattenspäher |
| Standard-Bosse | 12 | Full Portrait | Wolf-Alpha, Garak, Erdross, Sentinel, Morthos |
| Mehrstufige Bosse | 4 (extra Phase) | Full Portrait (Variante) | Malachar Phase 2, Nihilus Entfesselt, Xaroth Evolved |

**Asset-Struktur:**
```
Assets/Enemies/
├── e001_shadow_wolf.webp
├── b005_wolf_alpha.webp
├── b004_nihilus.webp
├── b004_nihilus_phase2.webp
└── ...
```

### 1.3 Szenen-Hintergründe (14 Szenen, ~25-35 Generierungen)

**Impact: 7/10** — Hybrid: AI-Basis + SkiaSharp-Partikel/Lighting/Foreground drüber.

| Szene | Stil | Beschreibung |
|-------|------|-------------|
| SystemVoid | Sci-Fi, dunkel | Digitale Leere, Matrix-artig |
| Title | Episch, dunkel | Sternenfeld, mystisch |
| ForestDay | Warm, grün | Sonnendurchfluteter Anime-Wald |
| ForestNight | Kalt, blau | Mondlicht-Wald, mysteriös |
| Campfire | Warm, orange | Lagerfeuer unter Sternenhimmel |
| VillageSquare | Warm, gemütlich | Mittelalterliches Anime-Dorf |
| VillageTavern | Innen, warm | Holzbalken, Kerzenlicht, Fässer |
| DungeonHalls | Dunkel, kalt | Steinhallen, Fackeln |
| DungeonBoss | Dramatisch, rot | Boss-Raum mit leuchtenden Runen |
| TowerLibrary | Mystisch, lila | Bücherregale, magische Orbs |
| TowerSummit | Episch, Nacht | Offener Himmel, Runenkreis |
| Battlefield | Destruktiv, rot | Verwüstetes Schlachtfeld |
| CastleHall | Majestätisch, gold | Thronsaal mit Bannern |
| Dreamworld | Surreal, lila | Fragmentierte Geometrie, Glitch |

**Hybrid-Ansatz:** AI-Bild ersetzt SkyRenderer + ElementRenderer + GroundRenderer.
SkiaSharp-Overlay bleibt: LightingRenderer, SceneParticleRenderer, ForegroundRenderer.

**Asset-Struktur:**
```
Assets/Backgrounds/
├── system_void.webp
├── forest_day.webp
├── forest_night.webp
└── ...
```

**Auflösung:** 832x1216 (Portrait, SDXL-nativ). Wird zur Laufzeit auf Bildschirmgröße skaliert.

### 1.4 CG-Szenen (15 Vollbild-Illustrationen, ~50-75 Generierungen)

**Impact: 8/10** — Emotionale Höhepunkte müssen visuell explodieren.

| Nr | Moment | Szene | Charaktere |
|----|--------|-------|------------|
| 1 | Prolog-Erwachen | P1 | Protagonist, System_ARIA |
| 2 | Team versammelt | P1 | Alle 5 Alliierte |
| 3 | Kael opfert sich | P2 | Kael, Protagonist |
| 4 | Luna bricht zusammen | P2 | Luna, Aria |
| 5 | Nihilus-Konfrontation | P3 | Nihilus, Protagonist |
| 6 | Aldric-Zeitmagie | P3 | Aldric, Protagonist |
| 7 | Erwachen im Wald | K1 | Protagonist allein |
| 8 | Klassenwahl | K1 | 3 Waffen/Symbole |
| 9 | Aria-Begegnung | K2 | Aria, Protagonist |
| 10 | Banditen-Kampf | K2 | Aria, Protagonist, Garak |
| 11 | Kael im Wald | K3 | Kael, Protagonist |
| 12 | Alptraum-Nihilus | K3 | Nihilus-Echo |
| 13 | Kristall-Entdeckung | K3 | Protagonist, System_ARIA |
| 14 | Good Ending (Arc 1) | K10 | Team, warmes Licht |
| 15 | Dark Ending (Arc 1) | K10 | Protagonist allein |

**Asset-Struktur:**
```
Assets/CG/
├── cg_prologue_awakening.webp
├── cg_nihilus_confrontation.webp
└── ...
```

### 1.5 Title Screen Key Visual (~5-10 Generierungen)

**Impact: 8/10** — Erster Eindruck, Store-Screenshots.

Dramatisches Anime-Kinoplakat: Protagonist (Schwertmeister) mit gezogenem Schwert,
System_ARIA als holographischer Geist dahinter, Nihilus' Schatten am Horizont.
Wird in TitleScene als Hintergrund-Layer unter dem Logo gerendert.

```
Assets/UI/title_keyvisual.webp
```

### 1.6 Item-Icons (65 Items, ~80-100 Generierungen)

**Impact: 6/10** — Shop/Inventar von Textliste zu visueller RPG-Erfahrung.

| Kategorie | Anzahl | Stil |
|-----------|--------|------|
| Waffen | 15 | Tier-Progression (Holz→Mithril→Held, zunehmender Glow) |
| Rüstungen | 10 | Leicht→Schwer, Farbe nach Material |
| Accessoires | 10 | Amulett, Ring, Stiefel — detailliert |
| Consumables | 12 | RPG-Phiolen, Tränke, Scrolls |
| Key Items | 8 | Besonderer Glow, Story-relevant |
| Materialien | 3 | Rohe Materialien |

**Auflösung:** 128x128 px (skaliert auf 64x64 in-game).

```
Assets/Items/
├── w_iron_sword.webp
├── c_hp_potion_small.webp
└── ...
```

### 1.7 Skill-Icons (15 Basis + Tier-Varianten, ~25-35 Generierungen)

**Impact: 5/10** — Skill-Select visuell unterscheidbar.

15 Basis-Icons (5 Skill-Linien × 3 Klassen). Tier-Varianten (1-5) werden per
SkiaSharp-Nachbearbeitung erzeugt (zunehmende Aura-Intensität, Gold-Rand ab Tier 4).

**Auflösung:** 128x128 px.

```
Assets/Skills/
├── sw_heavy_strike.webp
├── mg_fireball.webp
└── ...
```

### 1.8 Overworld-Map Hintergründe (13 Kapitel-Maps, ~25-35 Generierungen)

**Impact: 5/10** — Atmosphärische Maps statt identischer dunkler Gradient.

Pro Kapitel ein stilisierter Map-Hintergrund (Fantasy-Karten-Stil, illustriert).

```
Assets/Maps/
├── map_p1.webp
├── map_k1.webp
└── ...
```

### 1.9 Dialog-UI Texturen (~8-12 Generierungen)

**Impact: 4/10** — Polish.

```
Assets/UI/
├── dialog_box.webp          # Tileable Textbox-Hintergrund
├── dialog_namefield.webp    # Sprecher-Namensfeld
├── title_keyvisual.webp     # (siehe 1.5)
└── loading_tips/            # Charakter-Vignetten für Loading-Hints
    ├── tip_aria.webp
    └── ...
```

---

## 2. Cloud Asset Delivery (Firebase Storage)

### 2.1 Architektur

```
Firebase Storage Bucket: rebornsaga-assets
├── manifest.json
├── shared/                  # Immer benötigt
│   ├── characters/          # Charakter-Atlanten (10 Stück)
│   ├── ui/                  # Title Key Visual, Dialog-Texturen
│   ├── items/               # Item-Icons
│   └── skills/              # Skill-Icons
├── prolog/                  # Pflicht-Download
│   ├── backgrounds/         # Szenen-Hintergründe für P1-P3
│   ├── enemies/             # Gegner-Sprites für P1-P3
│   └── cg/                  # CG-Szenen für P1-P3
├── arc1/                    # Pflicht-Download
│   ├── backgrounds/         # Szenen-Hintergründe für K1-K10
│   ├── enemies/             # Gegner-Sprites für K1-K10
│   └── cg/                  # CG-Szenen für K1-K10
└── arc2/                    # Spätere Erweiterung (on-demand)
    └── ...
```

### 2.2 Manifest-Format

```json
{
  "version": 1,
  "minAppVersion": "1.0.0",
  "packs": {
    "shared": {
      "required": true,
      "totalSize": 8500000,
      "files": [
        {
          "path": "shared/characters/aria_atlas.webp",
          "hash": "sha256:a1b2c3...",
          "size": 450000
        }
      ]
    },
    "prolog": {
      "required": true,
      "totalSize": 5200000,
      "files": [ ]
    },
    "arc1": {
      "required": true,
      "totalSize": 7800000,
      "files": [ ]
    }
  }
}
```

**Versionierung:** `manifest.version` wird bei jeder Asset-Änderung inkrementiert.
`minAppVersion` verhindert Download von Assets die eine neuere App-Version brauchen.

### 2.3 AssetDeliveryService

```csharp
public interface IAssetDeliveryService
{
    // Status
    bool IsDownloadRequired { get; }
    long TotalDownloadSize { get; }
    string DownloadSizeFormatted { get; }

    // Lifecycle
    Task<AssetCheckResult> CheckForUpdatesAsync();
    Task<bool> DownloadAssetsAsync(IProgress<AssetDownloadProgress> progress, CancellationToken ct);

    // Asset-Zugriff
    Stream? GetAsset(string relativePath);
    SKBitmap? LoadBitmap(string relativePath);
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

### 2.4 Download-Flow in LoadingPipeline

```
RebornSagaLoadingPipeline:

Step 1: "Prüfe Updates" (10%)
  → AssetDeliveryService.CheckForUpdatesAsync()
  → Manifest von Firebase laden
  → Lokalen Cache vergleichen (SHA256-Hashes)

Step 2: "Lade Spielinhalte" (50%) — nur wenn Updates vorhanden
  → Bestätigung anzeigen:
    "Neue Spielinhalte verfügbar ({size} MB). Herunterladen?"
    [Ja] → Download mit Fortschrittsbalken
    [Nein] + kein Cache → "Ohne Download kann das Spiel nicht gestartet werden."
    [Nein] + Cache vorhanden → Mit bestehenden Assets fortfahren
  → HttpClient-Download pro Datei
  → SHA256-Verifikation nach Download
  → In FileSystem.AppDataDirectory/assets/ speichern

Step 3: "Bereite Grafiken vor" (20%)
  → SpriteCache: Charakter-Atlanten für aktuelle Szene vorladen
  → Shader-Kompilierung (bestehend)

Step 4: "Initialisiere Spiel" (20%)
  → ViewModel + Services (bestehend)
  → PurchaseService.InitializeAsync() (bestehend)
```

### 2.5 Lokaler Cache

```
{AppDataDirectory}/assets/
├── manifest.json            # Lokale Kopie des Manifests
├── shared/
│   ├── characters/
│   └── ...
├── prolog/
└── arc1/
```

- **Überlebt App-Updates** (AppDataDirectory ist persistent)
- **Gelöscht bei Deinstallation** (normal für App-Daten)
- **Delta-Updates:** Nur Dateien mit geändertem SHA256-Hash werden neu geladen
- **Offline-fähig:** Nach erstem Download kein Internet nötig

### 2.6 Firebase Storage Setup

Neues Firebase-Projekt: `rebornsaga-assets` (oder im bestehenden Projekt ein neuer Bucket).

**Kein Firebase SDK nötig.** Download per direkter URL:
```
https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{encodedPath}?alt=media
```

**Upload-Workflow (Entwickler):**
```bash
# Upload via Firebase CLI oder REST API
firebase storage:upload manifest.json gs://rebornsaga-assets/
firebase storage:upload shared/ gs://rebornsaga-assets/shared/ --recursive
```

**Security Rules:**
```
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {
    match /{allPaths=**} {
      allow read;           // Öffentlicher Lese-Zugriff (Assets sind nicht geheim)
      allow write: if false; // Nur über Firebase Console/CLI
    }
  }
}
```

---

## 3. AI-Generierungs-Workflow (ComfyUI)

### 3.1 Hardware

- NVIDIA RTX 4080 (16 GB VRAM)
- Intel i9-13900K, 64 GB RAM
- ComfyUI 0.16.3, PyTorch 2.10.0+cu126

### 3.2 Installierte Modelle

| Modell | Zweck |
|--------|-------|
| Animagine XL 4.0 Opt | Basis SDXL Anime-Generierung |
| ControlNet OpenPose SDXL | Konsistente Posen über Emotionen |
| IPAdapter Plus Face SDXL | Gesichts-Konsistenz (Referenz → Varianten) |
| CLIP Vision Encoder | Für IPAdapter benötigt |

### 3.3 Bewährte Einstellungen

| Parameter | Wert |
|-----------|------|
| Sampler | euler_ancestral |
| Steps | 28 |
| CFG | 7.0 |
| Auflösung (Portrait) | 832 × 1216 |
| Auflösung (Landscape) | 1216 × 832 |
| Auflösung (Icons) | 512 × 512 (downscale auf 128×128) |

### 3.4 Konsistenz-Workflow pro Charakter

```
Phase A: Referenz-Bild generieren
  → Detaillierter Prompt mit Charakter-Beschreibung
  → Seed fixieren bei gutem Ergebnis
  → Dieses Bild wird als IPAdapter-Referenz verwendet

Phase B: Emotionen generieren (IPAdapter + ControlNet)
  → IPAdapter: Referenz-Bild als Gesichts-Anker (Stärke 0.6-0.8)
  → ControlNet OpenPose: Fixe Kopfhaltung
  → Prompt variiert nur die Emotion (happy, angry, sad, surprised, determined)
  → Ergebnis: 6 konsistente Head-Varianten

Phase C: Body generieren
  → Gleicher IPAdapter-Anker
  → Prompt fokussiert auf Outfit/Waffe statt Gesicht
  → Zwei Varianten: Portrait (Hals abwärts) + Fullbody

Phase D: Nachbearbeitung
  → Hintergrund-Entfernung (rembg oder Segment Anything)
  → Layer-Zerlegung in GIMP/Photopea (Head vom Body trennen)
  → Blink/Mouth-Overlays erstellen (kleine Bereiche übermalen)
  → Atlas-Packing (alle Layer in ein Spritesheet)
```

### 3.5 Automatisierungs-Pipeline

```python
# comfyui_batch.py — Wird entwickelt in Phase 1
# Sendet Workflows per REST API an ComfyUI
# Iteriert über Charakter-Definitionen + Emotionen
# Speichert Output nach Assets/{charId}/

for char in characters:
    # 1. Referenz generieren
    ref = generate(char.prompt, seed=char.seed)
    # 2. Emotionen mit IPAdapter
    for emotion in emotions:
        generate(emotion.prompt, ipadapter_ref=ref, controlnet=pose)
    # 3. Body
    generate(char.body_prompt, ipadapter_ref=ref)
```

---

## 4. Code-Änderungen

### 4.1 Neue Dateien

| Datei | Zweck |
|-------|-------|
| `Services/AssetDeliveryService.cs` | Firebase Storage Download + Caching |
| `Services/IAssetDeliveryService.cs` | Interface |
| `Models/AssetManifest.cs` | Manifest JSON-Modell |
| `Rendering/Characters/SpriteCharacterRenderer.cs` | Ersetzt CharacterParts (7 Renderer) |
| `Rendering/Characters/SpriteDefinitions.cs` | Atlas-Pfade + Ankerpunkte pro Charakter |
| `Services/SpriteCache.cs` | LRU-Cache für Charakter-Atlanten |
| `Loading/RebornSagaLoadingPipeline.cs` | Loading mit Asset-Download |
| `Graphics/RebornSagaSplashRenderer.cs` | Anime-Style Loading Screen |

### 4.2 Geänderte Dateien

| Datei | Änderung |
|-------|----------|
| `CharacterRenderer.cs` | Delegiert an SpriteCharacterRenderer statt CharacterParts |
| `BackgroundCompositor.cs` | Lädt AI-Hintergrund als Basis-Layer, Partikel/Lighting drüber |
| `BattleScene.cs` | Gegner-Sprite-Bereich (obere Hälfte) hinzufügen |
| `OverworldRenderer.cs` | Kapitel-Map-Hintergrund laden |
| `App.axaml.cs` | AssetDeliveryService DI-Registrierung |
| `InventoryScene.cs` | Item-Icons aus Assets laden |
| `StatusScene.cs` | Skill-Icons aus Assets laden |

### 4.3 Entfällt (nach Migration)

| Datei | Grund |
|-------|-------|
| `Renderers/FaceRenderer.cs` | Durch Sprite ersetzt |
| `Renderers/EyeRenderer.cs` | Durch Sprite ersetzt |
| `Renderers/HairRenderer.cs` | Durch Sprite ersetzt |
| `Renderers/BodyRenderer.cs` | Durch Sprite ersetzt |
| `Renderers/ClothingRenderer.cs` | Durch Sprite ersetzt |
| `Renderers/AccessoryRenderer.cs` | Durch Sprite ersetzt |
| `CharacterParts.cs` | Durch SpriteCharacterRenderer ersetzt |
| `CharacterDefinitions.cs` | Durch SpriteDefinitions ersetzt |
| `Backgrounds/Layers/SkyRenderer.cs` | Im AI-Hintergrund enthalten |
| `Backgrounds/Layers/ElementRenderer.cs` | Im AI-Hintergrund enthalten |
| `Backgrounds/Layers/GroundRenderer.cs` | Im AI-Hintergrund enthalten |

### 4.4 Bleibt unverändert

| Komponente | Grund |
|------------|-------|
| `LightingRenderer.cs` | Ambient-Tönung über AI-Hintergründe + Charaktere |
| `SceneParticleRenderer.cs` | Partikel-Overlay über AI-Hintergründe |
| `ForegroundRenderer.cs` | Nebel/Gras-Overlay über alles |
| `CharacterEffects.cs` | Glow, Aura, Hologramm über Sprites |
| `EmotionSet.cs` | Steuert welcher Head-Layer gezeigt wird |
| Alle Scenes/Overlays | Rufen CharacterRenderer auf (Fassade ändert sich nicht) |
| ParticleSystem, GlitchEffect, ScreenShake | Effekt-Layer bleiben |

---

## 5. Geschätzte Größen

| Asset-Typ | Anzahl | Geschätzte Größe (WebP) |
|-----------|--------|------------------------|
| Charakter-Atlanten | 10 | ~8-10 MB |
| Fullbody-Sprites | 10 | ~2-3 MB |
| Gegner-Sprites | 25 | ~3-4 MB |
| CG-Szenen | 15 | ~3-5 MB |
| Hintergründe | 14 | ~2-3 MB |
| Item-Icons | 65 | ~1-2 MB |
| Skill-Icons | 15 | ~0.3 MB |
| Map-Hintergründe | 13 | ~2-3 MB |
| Title Key Visual | 1 | ~0.3 MB |
| UI-Texturen | 5 | ~0.2 MB |
| **Gesamt** | **~170** | **~22-33 MB** |

**APK bleibt bei ~35 MB** (keine eingebetteten Assets). Assets werden on-demand geladen.

**Download-Pakete:**
| Paket | Inhalt | Größe |
|-------|--------|-------|
| shared | Charaktere, Items, Skills, UI | ~12-16 MB |
| prolog | P1-P3 Hintergründe, Gegner, CGs | ~4-6 MB |
| arc1 | K1-K10 Hintergründe, Gegner, CGs | ~6-11 MB |
| **Erst-Download** | shared + prolog + arc1 | **~22-33 MB** |

---

## 6. Phasen-Plan

### Phase 1: Proof of Concept (3-4 Tage)

**Ziel:** Ein Charakter lebt animiert im Spiel + Asset-Download funktioniert.

1. **Aria komplett generieren** (Referenz → 6 Emotionen → Body → Fullbody)
2. **Layer zerlegen** (Hintergrund entfernen, Head/Body trennen, Blink/Mouth)
3. **Atlas erstellen** (Spritesheet + JSON-Metadaten)
4. **SpriteCharacterRenderer implementieren** (DrawBody, DrawHead, DrawBlink, DrawMouth)
5. **SpriteCache implementieren** (LRU, 5 Chars, Preload)
6. **AssetDeliveryService Grundgerüst** (lokaler Zugriff, noch ohne Firebase)
7. **Wolf-Alpha als erster Gegner** (Battle-Sprite generieren + in BattleScene einbauen)
8. **Title Key Visual generieren**

### Phase 2: Kern-Assets (4-5 Tage)

**Ziel:** Alle Charaktere + Gegner + wichtigste CGs.

1. Verbleibende 9 Charakter-Sprites (IPAdapter-Pipeline steht aus Phase 1)
2. Alle 21 Gegner-Sprites
3. 5 wichtigste CG-Szenen (Nihilus-Konfrontation, Klassenwahl, Erwachen, etc.)
4. BattleScene visuell umbauen (Gegner-Portrait-Bereich)
5. Firebase Storage einrichten + AssetDeliveryService komplett
6. RebornSagaLoadingPipeline mit Download-Integration

### Phase 3: Welt-Assets (3-4 Tage)

**Ziel:** Hintergründe, Items, Skills — die Welt füllt sich.

1. 14 Szenen-Hintergründe generieren
2. BackgroundCompositor auf Hybrid umbauen (AI-Basis + SkiaSharp-Overlay)
3. 65 Item-Icons generieren (Batch)
4. 15 Skill-Icons generieren (Batch)
5. InventoryScene + ShopScene + StatusScene mit Icons
6. Verbleibende 10 CG-Szenen

### Phase 4: Polish (2-3 Tage)

**Ziel:** Alles rund machen.

1. 13 Overworld-Map-Hintergründe
2. Dialog-UI-Texturen
3. CG-Galerie implementieren (freigeschaltete CGs als Thumbnails)
4. Codex mit Charakter-Portraits + Bestiary
5. Loading-Screen Charakter-Vignetten
6. Save-Slot Charakter-Portraits
7. Manifest finalisieren + Firebase hochladen + End-to-End testen

---

## 7. Nicht im Scope

- Live2D / Spine Animation (zu komplex)
- Charakter-spezifische LoRA-Trainings (erst bei Bedarf)
- Equipment-abhängige Sprite-Varianten (spätere Erweiterung)
- Story-Überarbeitung (separater Plan)
- Desktop-spezifische Assets (Android ist Fokus)
- Bezahlte Cloud-Infrastruktur (Firebase Free Tier reicht)
