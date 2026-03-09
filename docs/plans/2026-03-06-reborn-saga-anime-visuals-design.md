# RebornSaga: Anime Visual Novel Sprites — Design-Dokument

> Datum: 2026-03-06
> Status: Genehmigt
> Scope: AI-generierte Anime-Sprites mit Layer-Animation für alle Charaktere
> Voraussetzung: Charakter-Redesign (neue Optik, unabhängig von prozeduralem System)

---

## Zusammenfassung

Ersetze das prozedurale SkiaSharp-Charakter-Rendering (7 Renderer mit Bezier-Paths) durch
AI-generierte Anime-Sprites im Visual Novel Stil. Sprites werden lokal mit Stable Diffusion
(ComfyUI + SDXL Anime-Modell) generiert und als Texture-Atlanten eingebettet.

Leicht animierte Sprites (Idle-Breathing, Blinzeln, Mund beim Sprechen) per SkiaSharp —
kein Live2D, sondern Layer-basierte Code-Animation.

Art Style variiert je nach Kontext: Clean/Warm für Dorf-Szenen, Dunkel/Hart für Dungeon/Bosse,
Dramatisch für Fate-Momente. Ambient-Lighting per SKColorFilter passt Sprites automatisch an.

---

## Hardware

- NVIDIA RTX 4080 (16 GB VRAM) — SDXL nativ, Batch-Generierung möglich
- Intel i9-13900K, 64 GB RAM
- Alles lokal, kein Cloud-Abo nötig

---

## Toolchain

| Tool | Zweck |
|------|-------|
| ComfyUI | Stable Diffusion Frontend (node-basiert, flexibel) |
| SDXL Anime-Modell (Animagine XL 4.0 / Pony Diffusion) | Basis-Bildgenerierung |
| ControlNet | Gleiche Pose/Perspektive über alle Emotionen |
| LoRA | Stil-Konsistenz pro Charakter (trainierbar mit 10-20 Referenzbildern) |
| rembg / Segment Anything | Hintergrund-Entfernung (transparente PNGs) |
| GIMP / Photopea | Layer-Zerlegung (Body/Head-Split, Blink/Mouth-Overlays) |

---

## Asset-Struktur pro Charakter

### Head-basierte Emotionen + kleine Overlays

```
Assets/Characters/{charId}/
├── body.webp                # Hals abwärts (Breathing-Animation)
├── body_fullbody.webp       # Ganzkörper (ClassSelect, Status)
├── head_neutral.webp        # Kompletter Kopf - neutraler Ausdruck
├── head_happy.webp          # Kompletter Kopf - glücklich
├── head_angry.webp          # Kompletter Kopf - wütend
├── head_sad.webp            # Kompletter Kopf - traurig
├── head_surprised.webp      # Kompletter Kopf - überrascht
├── head_determined.webp     # Kompletter Kopf - entschlossen
├── blink.webp               # Nur Augenbereich geschlossen (kleines Overlay)
├── mouth_open.webp          # Nur Mundbereich offen (Sprechen Frame 1)
└── mouth_wide.webp          # Nur Mundbereich weit (Sprechen Frame 2)
```

**11 Layer pro Charakter, ~110 Sprites + ~15 CGs total.**

### Warum Head-basiert statt Einzel-Layer (Augen/Mund separat)

- Emotionen wirken natürlicher (Stirn, Wangen, Brauen passen zusammen)
- Viel einfacher mit AI zu generieren (ein Bild pro Emotion, ControlNet für Konsistenz)
- Blinzeln/Mund sind nur kleine Rechteck-Overlays, trivial in GIMP zu erstellen
- Weniger Layer = weniger DrawImage Calls = besser für Android

### Texture Atlas

Statt 11 einzelne Dateien pro Charakter: **ein Spritesheet pro Charakter**.

```
Assets/Characters/{charId}_atlas.webp    # Alle Layer in einem Grid
Assets/Characters/{charId}_atlas.json    # Source-Rectangles pro Layer
```

- 1 Datei-Load statt 11
- Weniger Memory-Fragmentation
- Bessere WebP-Kompression (ähnliche Bildteile nebeneinander)

---

## Animations-System (SkiaSharp)

```
SpriteCharacterRenderer (NEU — ersetzt prozedurale Renderer)
├── DrawBody()        → sin(time) * 2px vertikal (Idle-Breathing)
├── DrawHead()        → Emotions-Head-Layer je nach aktueller Emotion
├── DrawBlink()       → Overlay über Augenbereich, Timer 3-5s Intervall
├── DrawMouth()       → Wechselt mouth_open/mouth_wide während Typewriter-Text
└── DrawEffects()     → Bestehende SkiaSharp-Effekte (Glow, Aura, Hologramm)
```

### Keine separaten Lighting-Varianten

Sprites werden neutral beleuchtet generiert. BackgroundCompositor Ambient-Lighting
toent per `SKColorFilter.CreateColorMatrix()`:

| Szene | Tönung |
|-------|--------|
| Dorf, Taverne | Warm (+orange/gelb) |
| Wald Tag | Natürlich (minimal) |
| Wald Nacht, Mondlicht | Kalt (+blau) |
| Dungeon | Düster (+rot/dunkel, weniger Sättigung) |
| Boss-Raum | Dramatisch (+kontrast, +rot) |
| ARIA/System | Blau-Glow (+cyan) |

---

## Art Style — Kontextabhängig

| Kontext | Stil | Charakter |
|---------|------|-----------|
| ARIA/System-UI | Clean, blauer Glow, technisch | SystemAria |
| Dorf, Taverne, NPCs | Warm, weich, freundlich | Luna, Aldric, Shop-NPCs |
| Wald, Overworld | Natürlich, atmosphärisch | Exploration, Campfire |
| Dungeon, Kampf | Dunkler, härter, mehr Kontrast | Gegner, BattleScene |
| Boss-Enthüllung, Fate-Momente | Dramatisch, intensive Farben | Nihilus, Xaroth |

Der Art Style wird über den **Stable Diffusion Prompt** gesteuert, nicht über separate Assets.
Ambient-Lighting macht den Rest.

---

## SpriteCache Service

```csharp
public class SpriteCache : IDisposable
{
    // LRU-Cache: max 5 Charaktere gleichzeitig im RAM
    // PreloadAsync(charId) — lädt Atlas im Hintergrund während Transition
    // GetAtlas(charId) — gibt gecachten Atlas zurück
    // Automatisches Dispose bei LRU-Eviction
}
```

- Aktueller Szene-Kontext (StoryNode) sagt welche Charaktere als nächstes kommen
- Preloading während Scene-Transitions (Fade/Slide dauert 300-500ms)
- Fallback: Synchrones Laden wenn Cache-Miss

---

## Code-Änderungen

### Ersetzt

| Bestehend | Ersetzt durch |
|-----------|---------------|
| FaceRenderer.cs | SpriteCharacterRenderer |
| EyeRenderer.cs | SpriteCharacterRenderer |
| HairRenderer.cs | SpriteCharacterRenderer |
| BodyRenderer.cs | SpriteCharacterRenderer |
| ClothingRenderer.cs | SpriteCharacterRenderer |
| AccessoryRenderer.cs | SpriteCharacterRenderer |
| CharacterParts.cs (Orchestrator) | SpriteCharacterRenderer |
| CharacterDefinitions.cs (prozedurale Defs) | SpriteDefinitions.cs (Atlas-Pfade, Ankerpunkte) |

### Bleibt unverändert

| Komponente | Grund |
|------------|-------|
| CharacterRenderer.cs (Fassade) | Delegiert an SpriteCharacterRenderer statt CharacterParts |
| CharacterEffects.cs (Glow, Aura, Hologramm) | Wird über Sprites gelegt |
| CharacterLayout.cs | Wird vereinfacht, bleibt als Positionssystem |
| BackgroundCompositor + 14 Szenen | Komplett unverändert |
| Alle Scenes/Overlays | Rufen weiterhin CharacterRenderer auf |
| EmotionSet.cs | Steuert welcher Head-Layer gezeichnet wird |

---

## CG-Szenen (Vollbild-Illustrationen)

Für besondere Story-Momente: ~10-15 Vollbild-Illustrationen.

| Moment | Beschreibung |
|--------|-------------|
| Erwachen | Protagonist öffnet Augen in der Game-World |
| Klassenwahl | Drei Waffen/Symbole zur Auswahl |
| Boss-Enthüllungen | Nihilus, Xaroth — dramatische Einführung |
| Fate-Änderungen | Schicksals-Wendepunkte |
| Endings | Je nach Karma-Pfad unterschiedlich |

CGs werden als einzelne WebP in `Assets/CG/` gespeichert und in Cutscene-Nodes angezeigt.

---

## Phasen

### Phase 0: Charakter-Design
- Character Design Document: Aussehen, Outfit, Persönlichkeit pro Charakter
- Art Style Guide: Linien, Schattierung, Farbsättigung (gilt für ALLE)
- Referenz-Prompts: Stable Diffusion Prompt-Templates

### Phase 1: Toolchain + Proof of Concept
- ComfyUI installieren + konfigurieren
- SDXL Anime-Modell + ControlNet + LoRA Setup
- **Einen Charakter** komplett durchspielen (generieren → zerlegen → Atlas → ins Spiel)
- SpriteCharacterRenderer implementieren
- SpriteCache Service implementieren
- Ergebnis: Ein Charakter lebt animiert im Spiel

### Phase 2: Alle Charaktere
- Alle 10 Charaktere generieren + Layer zerlegen + Atlanten erstellen
- SpriteDefinitions für alle Charaktere
- Prozedurale Renderer entfernen
- Integration testen (alle Scenes/Overlays)

### Phase 3: CG-Szenen
- ~10-15 Vollbild-Illustrationen generieren
- CG-Viewer in Cutscene-Nodes integrieren
- Optional: CG-Galerie freischaltbar

---

## Geschätzte Größen

| Asset-Typ | Anzahl | Geschätzte Größe |
|-----------|--------|-----------------|
| Charakter-Atlanten | 10 | ~8-10 MB |
| Fullbody-Sprites | 10 | ~2-3 MB |
| CG-Szenen | 15 | ~3-5 MB |
| **Total** | | **~13-18 MB** |

Aktuelle APK ohne Sprites: ~30-40 MB. Mit Sprites: ~50-60 MB. Normal für ein Spiel.

---

## Nicht im Scope

- Live2D / Spine Animation (zu komplex, zu wenig Mehrwert vs. Layer-Animation)
- Charakter-spezifische LoRA-Trainings (erst wenn Basis-Workflow steht, optional in Phase 2)
- Story-Überarbeitung (separater Plan, nach visueller Basis)
- Equipment-abhängige Sprite-Varianten (spätere Erweiterung)
