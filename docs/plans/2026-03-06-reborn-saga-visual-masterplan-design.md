# RebornSaga Visual Masterplan - AAA Anime Isekai RPG

## Vision

RebornSaga wird visuell auf dem Niveau professioneller Anime Visual Novels (Arknights, Genshin Story-Modus). AI-generierte Charaktere mit LoRA-Konsistenz, animierte Cutscenes als Animated WebP, atmosphärische Hybrid-Hintergründe. Kein prozeduraler Fallback - alle visuellen Assets sind AI-generiert und Pflicht-Download.

---

## Architektur: Drei Säulen

### 1. LoRA Character Pipeline
Pro Charakter ein eigenes SDXL-LoRA trainieren. Damit beliebig viele konsistente Posen, Emotionen und Szenen generierbar. Skaliert für Arc 2, 3, etc.

### 2. AnimateDiff Video Pipeline
CG-Szenen, Cutscene-Knoten und Kapitel-Übergänge als animierte Clips. Nutzt Character-LoRAs für konsistente Charaktere in den Videos. Auslieferung als Animated WebP.

### 3. Hybrid Background Rendering
AI-generierte Basis-Hintergrundbilder + SkiaSharp-Overlays (Partikel, dynamische Beleuchtung, Foreground-Elemente). Ein Hintergrund, viele Stimmungen per LightingRenderer.

---

## 1. LoRA Character Pipeline

### Toolchain
- **Modell:** Animagine XL 4.0 Opt (SDXL)
- **Training:** kohya_ss (SDXL LoRA)
- **Generierung:** ComfyUI (lokal, REST API)
- **GPU:** RTX 4080 (lokal)

### Workflow pro Charakter

**Phase A: Referenz-Sheet**
1. txt2img mit vielen Seeds, bestes Einzelbild manuell auswählen
2. Aus dem Einzelbild per img2img ein Multi-Angle-Sheet: Front, 3/4, Seite, Rücken, Detail-Close-ups
3. Trainingsbilder mit Outfit-Variationen generieren (Kampfrüstung, Casual, beschädigt)

**Phase B: LoRA Training**
- ~20 kuratierte Trainingsbilder pro Charakter
- Booru-Style Caption-Tags (z.B. `aria_rebornsaga, 1girl, red hair, green eyes, leather armor, standing, happy`)
- kohya_ss: 1500-2000 Steps, Learning Rate 1e-4, Rank 32
- Trainingszeit: ~30-60 Min pro Charakter

**Phase C: Validierung**
- Test-Prompts: verschiedene Posen, Emotionen, Outfits, Beleuchtungen
- Prüfen ob Charakter-Identität über alle Varianten konsistent bleibt

**Phase D: Produktion**
- `<lora:aria_rebornsaga:0.8>` im Prompt für jede gewünschte Szene
- Story-getriebene Generierung: Jede Szene definiert Charakter + Pose + Emotion + Outfit

### LoRA lernt den Charakter, nicht das Outfit
Das LoRA lernt Gesichtszüge, Körperbau, Haarfarbe/Stil. Outfit wird per Prompt gesteuert und kann sich je nach Story-Fortschritt ändern:
- `aria_rebornsaga, leather armor, battle stance` (Kampf)
- `aria_rebornsaga, casual clothes, sitting in tavern` (Taverne)
- `aria_rebornsaga, damaged armor, kneeling` (nach Kampf)

### Spezialfälle
- **System_ARIA** (holografisch): LoRA für Basis-Design, Hologramm-Effekt per SkiaSharp-Shader (Scanlines, Glow, Transparenz)
- **Nihilus** (Glow-Augen, Aura): LoRA für Basis-Charakter, Aura/Glow per SkiaSharp-Shader
- **3 Protagonist-Varianten**: 3 separate LoRAs (Sword, Mage, Assassin) mit gleichen Gesichtszügen aber unterschiedlichen Outfits/Waffen

### Aufwand
- 10 LoRAs (inkl. 3 Protagonist-Varianten)
- ~2-3h pro Charakter (Ref-Sheet + Training + Validierung)
- ~20-30h Gesamtaufwand
- Danach: unbegrenzt skalierbar

---

## 2. Alle 10 Charaktere

| # | ID | Name | Haare | Augen | Kern-Outfit | Spezial |
|---|---|---|---|---|---|---|
| 1 | protag_sword | Protagonist Schwert | Dunkelblau, wild/stachelig | System-Blau | Dunkelblau-Grau + rote Akzente | - |
| 2 | protag_mage | Protagonist Magier | Lila, lang-glatt | Mystisch-Lila | Dunkelblau + Lila-Akzente | - |
| 3 | protag_assassin | Protagonist Assassine | Schwarz, kurz | Gift-Grün | Sehr dunkel + Grün-Akzente | - |
| 4 | aria | Aria | Feuerrot, lang | Grasgrün | Braunes Leder + Gold | Referenz: Seed 3141 |
| 5 | luna | Luna | Hellblau, sehr lang/Zopf | Lavendel | Weiß + Hellblau | Heilerin |
| 6 | kael | Kael | Braun, mittellang/wild | Amber | Dunkelbraun + Gold | Rivale |
| 7 | aldric | Aldric | Weiß/Silber, lang | Leuchtendes Blau | Dunkles Lila + Gold | Erzmagier |
| 8 | system_aria | System ARIA | Blau (transparent) | Leuchtendes Blau + Glow | Blau-transparent | Holografisch (SkiaSharp) |
| 9 | vex | Vex | Schwarz, sehr kurz | Rot | Dunkelbraun + Gold | Dieb/Händler |
| 10 | nihilus | Nihilus | Schwarz, wild | Dunkelrot + Glow | Dunkle Robe + Blutrot | Aura (SkiaSharp) |

---

## 3. Story-getriebene Asset-Generierung

### Prinzip
Keine fixe Pose-Anzahl pro Charakter. Jede Story-Szene definiert was gebraucht wird. Das LoRA liefert den konsistenten Charakter, der Prompt den Rest.

### Charakter-Sprites: Komplette Bilder (kein Head-Body-Compositing)
Pro Emotion wird ein **komplettes Bild** generiert (ganzer Charakter mit Gesichtsausdruck). Kein separates Head-Cropping auf Body - das verhindert Naht-Probleme und Beleuchtungs-Inkonsistenzen.

Emotions-Wechsel = anderes Bild laden (mit Crossfade-Überblendung).

### Asset-Struktur pro Charakter

```
Assets/Characters/{charId}/
├── full/                           # Komplette Bilder (Pose x Emotion)
│   ├── standing_neutral.webp
│   ├── standing_happy.webp
│   ├── standing_angry.webp
│   ├── standing_sad.webp
│   ├── standing_surprised.webp
│   ├── standing_determined.webp
│   ├── battle_neutral.webp
│   ├── battle_determined.webp
│   ├── sitting_happy.webp
│   ├── sitting_neutral.webp
│   └── ...                         # Story-spezifische Kombinationen
├── overlays/
│   ├── blink.webp                  # Geschlossene Augen (Overlay auf aktuelles Bild)
│   ├── mouth_open.webp             # Sprech-Frame 1
│   └── mouth_wide.webp             # Sprech-Frame 2
└── meta.json                       # Overlay-Positionen (Augen-Region, Mund-Region)
```

### Geschätzte Sprite-Mengen (Prolog + Arc 1)

| Charakter | Posen | Emotionen/Pose | Outfits | Total Bilder |
|---|---|---|---|---|
| Protagonist (x3) | 3-4 | 4-6 | 2 | ~60-70 |
| Aria | 3-4 | 6 | 2-3 | ~25-30 |
| Luna | 2-3 | 5 | 1-2 | ~15-20 |
| Kael | 3 | 5 | 2 | ~15-20 |
| Aldric | 2 | 4 | 1 | ~8-10 |
| System_ARIA | 2 | 3 | 1 | ~6-8 |
| Vex | 2 | 4 | 1 | ~8-10 |
| Nihilus | 2 | 3 | 1-2 | ~8-10 |
| Xaroth | 1-2 | 3 | 1 | ~5-6 |
| **Gesamt** | | | | **~150-185** |

### Szenen-Beispiele aus der Story

| Kapitel | Szene | Charakter | Pose | Emotion | Outfit |
|---|---|---|---|---|---|
| P1 | Erwachen | Protagonist | Liegend → Stehend | Verwirrt → Neutral | Isekai-Start |
| P1 | ARIA-Treffen | System_ARIA | Schwebend | Neutral | Digital |
| P1 | Klassenwahl | Protagonist x3 | Stehend, Waffe zeigend | Determined | Klassen-Rüstung |
| K2 | Aria-Begegnung | Aria | Kampfbereit | Misstrauisch/Angry | Lederrüstung |
| K2 | Taverne | Aria, Kael | Sitzend | Happy/Neutral | Casual |
| K2 | Banditen-Kampf | Aria, Protagonist | Kampfpose | Determined | Kampfrüstung |
| P2 | Kaels Opfer | Kael | Zusammenbrechend | Sad/Determined | Beschädigt |
| P2 | Luna-Zusammenbruch | Luna | Kniend | Sad/Crying | Heiler-Gewand |
| P3 | Nihilus-Konfrontation | Nihilus | Schwebend, bedrohlich | Angry/Determined | Dunkle Robe |

---

## 4. Animierte Szenen (AnimateDiff → Animated WebP)

### Typen

1. **CG-Szenen** (15) - Emotionale Höhepunkte, 3-5s, dramatisch animiert
2. **Cutscene-Knoten** (8-10) - Story-Übergänge aus Kapitel-JSONs, 2-3s
3. **Kapitel-Transitions** (5-6) - Überblendungen mit Title-Cards

### Format: Animated WebP
- AnimateDiff generiert Video → konvertiert zu Animated WebP
- 15-24 fps, optimierte Dateigröße durch Inter-Frame-Kompression
- Eine .webp Datei pro Szene (~0.5-1.5 MB)
- Character-LoRAs funktionieren auch in AnimateDiff

### Rendering in der App
- SKCodec liest Animated WebP Frame für Frame
- SKCanvas zeichnet Frames + Overlays (Untertitel, Partikel, Überblendungen)
- Volle Kontrolle, keine Codec-Abhängigkeiten, kein MediaPlayer nötig
- Nahtlose Überblendung: Letzter Frame → nächste Dialogue-Scene

### Asset-Struktur

```
Assets/Scenes/
├── cg_001_awakening.webp           # Animated WebP
├── cg_002_team_assembled.webp
├── cg_003_kael_sacrifice.webp
├── cutscene_k2_bandits.webp
├── transition_prolog_arc1.webp
└── ...
```

### CG-Szenen-Liste (Prolog + Arc 1)

| # | Moment | Kapitel | Charaktere | Dauer |
|---|---|---|---|---|
| 1 | Erwachen in fremder Welt | P1 | Protagonist, ARIA | 4s |
| 2 | Team versammelt sich | P1 | Alle 5 Alliierte | 5s |
| 3 | Kael opfert sich | P2 | Kael, Protagonist | 4s |
| 4 | Luna bricht zusammen | P2 | Luna, Aria | 3s |
| 5 | Nihilus-Konfrontation | P3 | Nihilus, Protagonist | 5s |
| 6 | Aldric-Zeitmagie | P3 | Aldric, Protagonist | 4s |
| 7 | Erwachen im Wald | K1 | Protagonist allein | 3s |
| 8 | Klassenwahl-Zeremonie | K1 | 3 Waffen-Symbole | 4s |
| 9 | Aria-Begegnung | K2 | Aria, Protagonist | 3s |
| 10 | Banditen-Kampf | K2 | Aria, Protagonist, Garak | 5s |
| 11 | Kael im Wald | K3 | Kael, Protagonist | 3s |
| 12 | Alptraum-Nihilus | K3 | Nihilus-Echo | 4s |
| 13 | Kristall-Entdeckung | K3 | Protagonist, ARIA | 3s |
| 14 | Good Ending | K10 | Team, warmes Licht | 5s |
| 15 | Dark Ending | K10 | Protagonist allein | 4s |

---

## 5. Hintergründe (Hybrid)

### Ansatz
AI-generiertes Basis-Hintergrundbild + SkiaSharp dynamische Overlays.

### AI-Basis
- 14 Szenen-Hintergründe als einzelne WebP-Bilder
- Generiert mit Animagine XL 4.0 (Landscape-Format, 1216x832)
- Atmosphärisch, ohne Charaktere, passend zur Szenen-Definition
- Neutral beleuchtet (LightingRenderer übernimmt Stimmung)

### SkiaSharp-Overlays (bleiben prozedural)
- **LightingRenderer** - Ambient-Tönung, PointLights (Fackeln, Kerzen, Lagerfeuer)
- **SceneParticleRenderer** - 12 Partikel-Typen (Blätter, Funken, Glühwürmchen, Staub, etc.)
- **ForegroundRenderer** - 5 Typen über Charakteren (Gras, Nebel, Äste, Spinnweben, Lichtstrahlen)

### Vorteil
Ein Hintergrundbild + verschiedene Beleuchtungs-Presets = Tag/Nacht/Stimmungswechsel ohne neue Bilder.

---

## 6. Gegner-Sprites

### Format
- Regular Enemies: Half-Body Portraits (WebP)
- Bosse: Full Portraits
- Multi-Phase Bosse: Separate Varianten (z.B. `nihilus.webp` + `nihilus_phase2.webp`)

### Generierung
- Ohne LoRA (jeder Gegner ist einzigartig, keine Konsistenz über mehrere Bilder nötig)
- txt2img mit detaillierten Prompts
- ~21 Regular + ~5 Bosse + ~3 Multi-Phase = ~29 Bilder

---

## 7. Items & Skills Icons

- 65 Item-Icons (Waffen, Rüstung, Tränke, Materialien)
- 15 Skill-Icons (Aktive + Passive Skills)
- Generiert als 512x512 mit transparentem Hintergrund
- Komprimiert als WebP, ~20-40 KB pro Icon

---

## 7a. Equipment-Konsistenz (Waffen, Rüstung, Inventar)

### Problem
AI-Generierung erzeugt bei jedem Prompt ein leicht anderes Waffen-Design. Wenn Arias Schwert im Charakter-Sprite anders aussieht als das Icon im Inventar, bricht die Immersion.

### Lösung: Equipment-Referenz-Prompts

Pro Waffe/Rüstungsteil wird eine **feste Prompt-Beschreibung** definiert. Diese wird identisch verwendet in:
1. **Inventar-Icons** (512x512, transparenter Hintergrund)
2. **Charakter-Sprites** (Charakter hält/trägt das Equipment)
3. **CG-Szenen / Cutscenes** (Nahaufnahmen, Kampfszenen)

### Equipment-Vokabular (Beispiele)

| Equipment-ID | Prompt-Beschreibung (immer identisch verwenden) |
|---|---|
| `sword_starter` | `simple iron longsword, straight blade, brown leather grip, round pommel` |
| `sword_flame` | `ornate flame longsword, red-orange blade with fire engravings, golden crossguard, red gemstone pommel` |
| `staff_arcane` | `tall wooden staff, twisted dark wood, glowing purple crystal orb at top, silver rings` |
| `dagger_shadow` | `curved black dagger, serrated edge, dark leather wrapped handle, green poison glow` |
| `armor_leather` | `brown leather armor, golden buckles and accents, layered shoulder pads` |
| `armor_mage_robe` | `dark blue mage robe, purple trim, silver star embroidery, high collar` |
| `armor_assassin` | `dark hooded leather outfit, many hidden pockets, dark green accents, light and sleek` |
| `bow_hunter` | `recurve wooden bow, dark wood with carved runes, green string` |
| `shield_knight` | `round metal shield, blue crest with golden lion emblem, dented edges` |
| `ring_system` | `glowing blue digital ring, holographic runes orbiting, translucent` |

### Workflow

**Phase 1: Equipment-Referenz-Sheets generieren**
1. Pro Equipment-ID ein 512x512 Icon generieren (isoliertes Item, transparenter Hintergrund)
2. Icon kuratieren und als Referenz speichern
3. Prompt-Beschreibung ggf. verfeinern bis das Design passt

**Phase 2: Equipment in Charakter-Sprites verwenden**
1. Beim Charakter-Sprite den Equipment-Prompt exakt übernehmen:
   `aria_char, standing, battle pose, holding {sword_flame Prompt}, leather armor`
2. LoRA sorgt für konsistenten Charakter, Equipment-Prompt für konsistente Waffe

**Phase 3: Konsistenz-Check**
1. Inventar-Icon und Charakter-Sprite nebeneinander vergleichen
2. Bei Abweichungen: Prompt verfeinern oder Seed optimieren
3. CG-Szenen mit denselben Equipment-Prompts generieren

### Asset-Struktur

```
Assets/Items/
├── weapons/
│   ├── sword_starter.webp          # 512x512 Icon
│   ├── sword_flame.webp
│   ├── staff_arcane.webp
│   ├── dagger_shadow.webp
│   └── ...
├── armor/
│   ├── armor_leather.webp
│   ├── armor_mage_robe.webp
│   └── ...
├── consumables/
│   ├── potion_health.webp
│   ├── potion_mana.webp
│   └── ...
└── materials/
    ├── crystal_dark.webp
    └── ...
```

### Equipment-Referenz-Datei

Eine zentrale JSON-Datei definiert alle Equipment-Prompts, damit sie überall konsistent verwendbar sind:

```json
{
  "equipment_prompts": {
    "sword_starter": "simple iron longsword, straight blade, brown leather grip, round pommel",
    "sword_flame": "ornate flame longsword, red-orange blade with fire engravings, golden crossguard, red gemstone pommel",
    "staff_arcane": "tall wooden staff, twisted dark wood, glowing purple crystal orb at top, silver rings",
    "armor_leather": "brown leather armor, golden buckles and accents, layered shoulder pads"
  }
}
```

Wird von den ComfyUI-Workflow-Scripts geladen und automatisch in die Prompts eingesetzt.

---

## 8. App-Integration

### SpriteCharacterRenderer (überarbeitet)

**Kein Head-Body-Compositing mehr.** Komplette Bilder pro Pose+Emotion.

```
Rendering-Ablauf:
1. Story-Node: character="aria", pose="sitting", emotion="happy"
2. Lade: Assets/Characters/aria/full/sitting_happy.webp
3. Zeichne komplettes Bild
4. Blink-Overlay drüber (wenn Blink-Timer feuert)
5. Mouth-Overlay drüber (wenn Charakter spricht)
6. LightingRenderer tönt Szene-Stimmung
```

**Emotions-Wechsel:** Crossfade-Überblendung zum neuen Bild (150ms).

### Animations-State pro Charakter (Fix)

```csharp
// Statt globaler statischer Felder:
Dictionary<string, CharacterAnimState> _animStates;

struct CharacterAnimState {
    float NextBlinkTime;    // Per charId-Hash versetzt
    bool IsBlinking;
    bool IsSpeaking;
    int MouthFrame;
    float MouthTimer;
}
```
- Kein synchrones Blinzeln mehr
- Mund-State pro Charakter unabhängig

### Animated WebP Player

```csharp
// Neuer Renderer für Cutscenes
class AnimatedWebPRenderer {
    SKCodec _codec;
    int _currentFrame;
    float _frameTimer;

    void Update(float deltaTime);     // Frame-Timing
    void Draw(SKCanvas canvas);        // Aktuellen Frame zeichnen
    bool IsFinished { get; }           // Animation abgeschlossen?
}
```
- Wird von CutsceneScene verwendet
- SKCodec dekodiert Animated WebP nativ
- Overlay-fähig (Untertitel, Partikel)
- Nahtlose Überblendung zum nächsten Scene-Typ

### SpriteCache (angepasst)

- Einzelbilder statt Atlanten
- LRU mit höherem Limit
- Prefetch: Story-Engine meldet kommende Szenen → Cache lädt vor
- Animated WebP werden bei Bedarf geladen (nicht vorgehalten)

### Kein Fallback

- Asset-Download ist Pflicht beim ersten Start
- Loading-Screen: Progress-Bar, WiFi-Empfehlung, Resume bei Abbruch
- Nach Download: 100% offline-fähig
- CharacterParts-Rendering wird entfernt (oder nur für Dev/Preview behalten)

---

## 9. Asset Delivery

### Firebase Storage REST API
- Bucket: `rebornsaga-assets`
- Öffentlich lesbar (später: Firebase App Check für Bot-Schutz)
- Direkte Download-URLs ohne SDK

### Download beim ersten Start
- Gesamtgröße: ~50-75 MB (alle Assets für Prolog + Arc 1)
- Loading-Screen mit:
  - Progress-Bar (pro Datei + Gesamt)
  - WiFi-Check und Empfehlung
  - Resume-Fähigkeit bei Abbruch (SHA256-basiert)
  - Stream-basierter Download (kein byte[] im RAM)
  - Exponentieller Retry (3 Versuche pro Datei)

### Manifest-System
```json
{
  "version": "1.0.0",
  "minAppVersion": "1.0.0",
  "totalSize": 68000000,
  "files": [
    {
      "path": "characters/aria/full/standing_neutral.webp",
      "hash": "sha256:abc123...",
      "size": 185000
    }
  ]
}
```
- Delta-Updates: Nur geänderte Dateien nachladen
- Lokal gespeichert unter: `LocalApplicationData/RebornSaga/assets/`

### Spätere Arcs
- Arc 2+ als separate Manifest-Erweiterungen
- Beim Story-Fortschritt: "Neue Inhalte verfügbar" → Download

---

## 10. Download-Größe (Gesamtschätzung)

| Asset-Typ | Anzahl | Format | Größe |
|---|---|---|---|
| Charakter-Sprites | ~150-185 Bilder | WebP 832x1216 | ~20-30 MB |
| Gegner-Sprites | ~29 Bilder | WebP 832x1216 | ~5-8 MB |
| Hintergründe | 14 Bilder | WebP 1216x832 | ~5-8 MB |
| Animierte Szenen | 25-30 Clips | Animated WebP | ~15-25 MB |
| Equipment-Icons (Waffen/Rüstung) | ~40 Icons | WebP 512x512 | ~1-2 MB |
| Verbrauchsgüter/Material/Skill Icons | ~40 Icons | WebP 512x512 | ~1-2 MB |
| Title Key Visual | 1 Bild | WebP | ~0.5 MB |
| **Gesamt** | | | **~50-75 MB** |

---

## 11. Phasen-Plan

### Phase 1: Aria PoC (Proof of Concept)
- Aria Referenz-Sheet erstellen (Seed 3141 als Basis)
- Aria LoRA trainieren (kohya_ss)
- 5-6 Sprite-Varianten generieren (verschiedene Posen + Emotionen)
- 1 Animated WebP Cutscene generieren
- SpriteCharacterRenderer anpassen (komplette Bilder, per-Character AnimState)
- AnimatedWebPRenderer implementieren
- End-to-End Test: Aria in einer Dialogue-Scene mit Emotionswechsel

### Phase 2: Alle Charaktere + Equipment-Referenzen
- 9 weitere LoRAs trainieren
- **Equipment-Referenz-Prompts definieren** (alle Waffen, Rüstungen, Items)
- **Equipment-Icons generieren** (512x512, als visuelle Referenz für Charakter-Sprites)
- Story-Szenen analysieren → benötigte Sprites pro Charakter generieren
  - Equipment-Prompts exakt übernehmen für Waffen/Rüstung auf Sprites
  - Konsistenz-Check: Icon vs. Sprite nebeneinander prüfen
- Alle 29 Gegner-Sprites generieren
- Alle 15 CG-Szenen + 10-15 Cutscenes als Animated WebP

### Phase 3: Welt & UI
- 14 Szenen-Hintergründe generieren
- Verbleibende Item-Icons (Tränke, Materialien, Skill-Icons)
- Title Key Visual
- AssetDeliveryService fertigstellen (Firebase, Download, Retry)
- Loading-Pipeline integrieren

### Phase 4: Polish
- Alle Assets auf Konsistenz prüfen
- Download-Größe optimieren (WebP-Qualität tunen)
- Überblendungen und Transitions feintunen
- Firebase App Check einrichten
- Play Store Screenshots mit AI-Assets

---

## 12. Technische Voraussetzungen

### Lokal (Generierung)
- ComfyUI v0.16.3+ mit Animagine XL 4.0 Opt
- kohya_ss für LoRA-Training
- AnimateDiff Custom Nodes für ComfyUI
- IPAdapter Plus (bereits installiert, als Backup-Option)
- GPU: RTX 4080 (ausreichend für alles)

### App-seitig
- SkiaSharp 3.119.2 (SKCodec für Animated WebP)
- Firebase Storage REST API (kein SDK)
- ~50-75 MB lokaler Speicher nach Download

### Erkenntnisse aus bisherigen Tests
- **IPAdapter**: Degradiert Qualität massiv, fehlender Mund → nicht als Hauptmethode
- **Inpainting (SetLatentNoiseMask)**: Greift nicht → VAEEncodeForInpaint oder LoRA stattdessen
- **txt2img verschiedene Prompts**: Inkonsistente Charaktere → LoRA löst das Problem
- **Gleicher Seed + anderer Prompt**: Ändert gesamte Komposition → nicht nutzbar für Varianten
