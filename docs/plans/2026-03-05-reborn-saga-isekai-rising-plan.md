# Reborn Saga: Isekai Rising - Implementierungsplan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Volle SkiaSharp Anime-Isekai-RPG App mit Prolog (3 Kap) + Arc 1 (10 Kap), Solo-Leveling-System-UI, aktionsbasierten Kämpfen, Overworld-Map, Gold-Economy und JSON-basierter Story-Engine.

**Architecture:** Szenen-basierte SkiaSharp-Engine (Scene-Stack mit Transitions). Alles prozedural gerendert. Story-Daten in JSON. SQLite für Spielstände. Gold als einzige Währung (kein Echtgeld für Kapitel).

**Tech Stack:** Avalonia 11.3, SkiaSharp 3.119.2, CommunityToolkit.Mvvm, sqlite-net-pcl, MeineApps.Core.Ava, MeineApps.Core.Premium.Ava

**Design-Dokument:** `docs/plans/2026-03-05-reborn-saga-isekai-rising-design.md`

**Geschätzter Aufwand:** ~40 Arbeitstage - ALLE PHASEN ERLEDIGT (2026-03-06)

---

## Phasen-Übersicht

| Phase | Inhalt | Status | Aufwand |
|-------|--------|--------|---------|
| 1 | Projekt-Scaffolding | ERLEDIGT | 1 Tag |
| 2 | Engine-Kern | ERLEDIGT | 1 Tag |
| 3 | Basis-Rendering | ERLEDIGT | 1 Tag |
| 4 | Title + Save-Slots | ERLEDIGT | 1 Tag |
| 5 | Charakter-Rendering | ERLEDIGT | 3 Tage |
| 6 | Dialogue-System | ERLEDIGT | 2 Tage |
| 7 | Story-Engine | ERLEDIGT | 2 Tage |
| 8 | Klassenwahl | ERLEDIGT | 1 Tag |
| 9 | System-UI (Solo Leveling) | ERLEDIGT | 2 Tage |
| 10 | Kampf-System | ERLEDIGT | 3 Tage |
| 11 | Overworld-Map | ERLEDIGT | 2 Tage |
| 12 | RPG-Systeme | ERLEDIGT | 3 Tage |
| 13 | Save-System | ERLEDIGT | 1 Tag |
| 14 | Gold-Economy + Monetarisierung | ERLEDIGT | 2 Tage |
| 15 | Visuelle Polish | ERLEDIGT | 2 Tage |
| 16 | Audio | ERLEDIGT | 1 Tag |
| 17 | Tutorial + Settings | ERLEDIGT | 1 Tag |
| 18 | Content: Prolog P1-P3 | ERLEDIGT | 3 Tage |
| 19 | Content: Arc 1 K1-K5 (gratis) | ERLEDIGT | 4 Tage |
| 20 | Content: Arc 1 K6-K10 (Gold) | ERLEDIGT | 4 Tage |
| 21 | Android-Integration + Final | ERLEDIGT | 2 Tage |

---

## Phase 1-3: ERLEDIGT

Bereits implementiert:
- 3 csproj (Shared/Android/Desktop), Solution erweitert, AppPalette
- Engine: Scene, SceneManager, InputManager, Camera, Transitions (Fade/Slide)
- Rendering: UIRenderer, ParticleSystem (Struct-basiert), BackgroundRenderer (8 Typen), ParallaxRenderer
- Code-Review Fixes: SKColor-Bug, DPI-Touch-Skalierung, Camera Guard, Shader-Caching, Memory Leak Prevention, Solution-Folder

---

## Phase 4: Title + Save-Slots (~1 Tag)

### Task 4.1: TitleScene

**Erstellen:** `Scenes/TitleScene.cs`

Animierter Titelbildschirm:
- BackgroundRenderer.Title als Hintergrund (dunkler Gradient + Partikel-Ringe)
- "REBORN SAGA" Titel mit Oswald-Font, Glow-Effekt (#4A90D9), leichtes Pulsieren
- "Isekai Rising" Untertitel mit Rajdhani, Fade-In nach 1s
- Schwebende Partikel (ParticleSystem.MagicSparkle, kontinuierlich)
- 3 Buttons: "Neues Spiel" / "Fortsetzen" / "Einstellungen"
- InputAction.Tap → Button-Hit-Test → Scene-Wechsel

```csharp
public class TitleScene : Scene
{
    private float _time;
    private readonly ParticleSystem _particles = new(100);
    private readonly SKRect[] _buttonRects = new SKRect[3];
    private int _hoveredButton = -1;

    public override void Update(float dt)
    {
        _time += dt;
        _particles.EmitContinuous(bounds.MidX, bounds.MidY, 5f, dt,
            ParticleSystem.MagicSparkle, bounds.Width, bounds.Height * 0.3f);
        _particles.Update(dt);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        BackgroundRenderer.Render(canvas, bounds, SceneBackground.Title, _time);
        _particles.Render(canvas);

        // Titel mit Glow
        UIRenderer.DrawTextWithShadow(canvas, "REBORN SAGA",
            bounds.MidX, bounds.Height * 0.25f, bounds.Width * 0.1f, UIRenderer.PrimaryGlow);

        // Buttons
        var btnW = bounds.Width * 0.5f;
        var btnH = bounds.Height * 0.06f;
        var startY = bounds.Height * 0.55f;
        string[] labels = { "Neues Spiel", "Fortsetzen", "Einstellungen" };
        for (int i = 0; i < 3; i++)
        {
            _buttonRects[i] = new SKRect(
                bounds.MidX - btnW/2, startY + i * (btnH + 15),
                bounds.MidX + btnW/2, startY + i * (btnH + 15) + btnH);
            UIRenderer.DrawButton(canvas, _buttonRects[i], labels[i], i == _hoveredButton);
        }
    }

    public override void HandleInput(InputAction action, SKPoint pos)
    {
        if (action == InputAction.Tap)
        {
            for (int i = 0; i < 3; i++)
            {
                if (UIRenderer.HitTest(_buttonRects[i], pos))
                {
                    switch (i)
                    {
                        case 0: SceneManager.ChangeScene<ClassSelectScene>(new FadeTransition()); break;
                        case 1: SceneManager.ChangeScene<SaveSlotScene>(new FadeTransition()); break;
                        case 2: SceneManager.PushScene<SettingsScene>(new SlideTransition()); break;
                    }
                }
            }
        }
    }
}
```

### Task 4.2: SaveSlotScene

**Erstellen:** `Scenes/SaveSlotScene.cs`

3 Save-Slot-Karten:
- Pro belegtem Slot: Klasse-Icon, Level, Kapitel, Spielzeit
- Leere Slots: "Leer - Tap zum Starten"
- Long-Press auf belegten Slot → Löschen-Bestätigung
- Back → TitleScene

---

## Phase 5: Charakter-Rendering (~3 Tage)

### Task 5.1: CharacterRenderer + CharacterParts + EmotionSet

**Erstellen:**
- `Rendering/Characters/CharacterRenderer.cs`
- `Rendering/Characters/CharacterParts.cs`
- `Rendering/Characters/EmotionSet.cs`

Modulares System - jedes Teil als Methode:

```csharp
public static class CharacterParts
{
    // Alle SKPaint/SKPath gepooled
    private static readonly SKPaint _skinPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _outlinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPath _hairPath = new();
    private static readonly SKPath _bodyPath = new();

    public static void DrawHead(SKCanvas canvas, CharacterDefinition def, float cx, float cy, float scale) { ... }
    public static void DrawEyes(SKCanvas canvas, CharacterDefinition def, Emotion emotion, float cx, float cy, float scale, float time) { ... }
    public static void DrawHair(SKCanvas canvas, CharacterDefinition def, float cx, float cy, float scale, float time) { ... }
    public static void DrawMouth(SKCanvas canvas, CharacterDefinition def, Emotion emotion, float cx, float cy, float scale) { ... }
    public static void DrawBody(SKCanvas canvas, CharacterDefinition def, float cx, float cy, float scale) { ... }
    public static void DrawAccessories(SKCanvas canvas, CharacterDefinition def, float cx, float cy, float scale) { ... }
}
```

EmotionSet - 6 Emotionen als Bezier-Kontrollpunkte für Augen/Mund/Brauen:
```csharp
public enum Emotion { Neutral, Happy, Angry, Sad, Surprised, Determined }
```

### Task 5.2: CharacterDefinitions

**Erstellen:** `Rendering/Characters/CharacterDefinitions.cs`

Statische Definitionen für alle NPCs:

```csharp
public class CharacterDefinition
{
    public string Id { get; init; } = "";
    public SKColor SkinColor { get; init; }
    public SKColor HairColor { get; init; }
    public SKColor EyeColor { get; init; }
    public SKColor OutfitColor { get; init; }
    public SKColor OutfitAccent { get; init; }
    public float HairLength { get; init; }    // 0-1
    public int HairStyle { get; init; }       // 0=kurz, 1=lang, 2=Zopf, 3=wild
    public int BodyType { get; init; }        // 0=schlank, 1=muskulös, 2=Robe
    public int AccessoryType { get; init; }   // 0=Schwert, 1=Stab, 2=Dolche, 3=keine
    public bool HasGlowingEyes { get; init; }
    public bool IsHolographic { get; init; }  // Für ARIA
}

public static class CharacterDefinitions
{
    public static readonly CharacterDefinition Protagonist_Sword = new() { ... };
    public static readonly CharacterDefinition Protagonist_Mage = new() { ... };
    public static readonly CharacterDefinition Protagonist_Assassin = new() { ... };
    public static readonly CharacterDefinition Aria = new()
    {
        Id = "aria", SkinColor = new(0xFF, 0xDB, 0xAC),
        HairColor = new(0xCC, 0x33, 0x33), EyeColor = new(0x2E, 0xCC, 0x71),
        OutfitColor = new(0x8B, 0x45, 0x13), HairLength = 0.7f,
        HairStyle = 1, BodyType = 1, AccessoryType = 0
    };
    public static readonly CharacterDefinition Aldric = new() { ... };
    public static readonly CharacterDefinition Kael = new() { ... };
    public static readonly CharacterDefinition Luna = new() { ... };
    public static readonly CharacterDefinition Vex = new() { ... };
    public static readonly CharacterDefinition SystemAria = new() { IsHolographic = true, ... };
}
```

---

## Phase 6: Dialogue-System (~2 Tage)

### Task 6.1: DialogBoxRenderer + TypewriterRenderer

**Erstellen:**
- `Rendering/UI/DialogBoxRenderer.cs` - Halbtransparente Box, Sprecher-Name, Weiter-Indikator
- `Rendering/UI/TypewriterRenderer.cs` - Buchstabe-für-Buchstabe, einstellbare Geschwindigkeit, Markup-Support

```csharp
public class TypewriterRenderer
{
    private string _fullText = "";
    private float _charIndex;
    private float _speed = 30f; // Zeichen/Sekunde

    public bool IsComplete => _charIndex >= _fullText.Length;
    public string VisibleText => _fullText[..(int)Math.Min(_charIndex, _fullText.Length)];

    public void SetText(string text) { _fullText = text; _charIndex = 0; }
    public void ShowAll() { _charIndex = _fullText.Length; }
    public void Update(float dt) { if (!IsComplete) _charIndex += _speed * dt; }

    public void SetSpeed(TypewriterSpeed speed) => _speed = speed switch
    {
        TypewriterSpeed.Slow => 15f,
        TypewriterSpeed.Medium => 30f,
        TypewriterSpeed.Fast => 60f,
        TypewriterSpeed.Instant => 9999f,
        _ => 30f
    };
}
```

### Task 6.2: ChoiceButtonRenderer

**Erstellen:** `Rendering/UI/ChoiceButtonRenderer.cs`

2-4 Buttons vertikal, Hover/Tap-Feedback, optionale Tags ([Karma+], [STR Check]).

### Task 6.3: DialogueScene

**Erstellen:** `Scenes/DialogueScene.cs`

Hintergrund + Portrait + Typewriter + Choices + Skip/Auto/Log Buttons.

### Task 6.4: BacklogOverlay

**Erstellen:** `Overlays/BacklogOverlay.cs`

Scrollbare Dialog-Historie (max 200 Einträge).

---

## Phase 7: Story-Engine (~2 Tage)

### Task 7.1: Daten-Modelle

**Erstellen:**
- `Models/StoryNode.cs`
- `Models/Chapter.cs`
- `Models/Enums/NodeType.cs`

```csharp
public enum NodeType
{
    Dialogue, Choice, Battle, ClassSelect, Shop, Cutscene,
    Overworld, BondScene, FateChange, SystemMessage, ChapterEnd
}

public class StoryNode
{
    public string Id { get; set; } = "";
    public NodeType Type { get; set; }
    public string? BackgroundKey { get; set; }
    public string? MusicKey { get; set; }
    public List<SpeakerLine>? Speakers { get; set; }
    public List<ChoiceOption>? Options { get; set; }
    public string? Next { get; set; }
    public List<string>? Enemies { get; set; }
    public StoryEffects? Effects { get; set; }
    public string? Condition { get; set; }
}

public class SpeakerLine
{
    public string Character { get; set; } = "";
    public string Emotion { get; set; } = "neutral";
    public string TextKey { get; set; } = "";
    public float TypewriterSpeed { get; set; } = 30f;
    public string? Position { get; set; } // "left", "right", "center"
}

public class ChoiceOption
{
    public string TextKey { get; set; } = "";
    public string Next { get; set; } = "";
    public StoryEffects? Effects { get; set; }
    public string? Condition { get; set; }
    public string? Tag { get; set; } // "[Karma+]", "[Aria Affinität]"
}

public class StoryEffects
{
    public int Karma { get; set; }
    public int Exp { get; set; }
    public int Gold { get; set; }
    public Dictionary<string, int>? Affinity { get; set; }
    public List<string>? AddItems { get; set; }
    public List<string>? RemoveItems { get; set; }
    public bool? FateChanged { get; set; }
}
```

### Task 7.2: StoryEngine Service

**Erstellen:** `Services/StoryEngine.cs`

```csharp
public class StoryEngine
{
    public Chapter? CurrentChapter { get; private set; }
    public StoryNode? CurrentNode { get; private set; }

    public async Task LoadChapter(int chapterId) { ... }     // JSON aus EmbeddedResource
    public StoryNode? GetNode(string nodeId) { ... }
    public void AdvanceToNode(string nodeId) { ... }
    public void MakeChoice(int optionIndex) { ... }          // Effects anwenden
    public string GetLocalizedText(string key) { ... }       // Sprach-abhängig
    public bool EvaluateCondition(string condition) { ... }   // "karma > 50" etc.
}
```

### Task 7.3: JSON-Strukturen und Beispiel-Daten

**Chapter JSON** (`Data/Chapters/chapter_p1.json`):
```json
{
  "id": "p1",
  "titleKey": "chapter_p1_title",
  "isProlog": true,
  "goldCost": 0,
  "nodes": [
    {
      "id": "p1_intro",
      "type": "dialogue",
      "backgroundKey": "battlefield",
      "musicKey": "prolog_march",
      "speakers": [
        { "character": "system_aria", "emotion": "neutral", "textKey": "p1_aria_init", "typewriterSpeed": 20 }
      ],
      "next": "p1_campfire"
    },
    {
      "id": "p1_campfire",
      "type": "dialogue",
      "backgroundKey": "village",
      "musicKey": "emotional_piano",
      "speakers": [
        { "character": "aria", "emotion": "sad", "textKey": "p1_aria_memory", "position": "left" },
        { "character": "protagonist", "emotion": "determined", "textKey": "p1_protag_response", "position": "right" }
      ],
      "next": "p1_aldric_secret"
    }
  ]
}
```

**Dialog-Texte** (`Data/Dialogue/de/chapter_p1.json`):
```json
{
  "chapter_p1_title": "Der letzte Marsch",
  "p1_aria_init": "Held von Aethermoor. Level 50. Alle Systeme bereit für den finalen Kampf.",
  "p1_aria_memory": "Weißt du noch, unser erstes Abenteuer? Du konntest nicht mal ein Schwert richtig halten.",
  "p1_protag_response": "Und du hast mich trotzdem nicht aufgegeben. Das werde ich nie vergessen."
}
```

**Map JSON** (`Data/Maps/overworld_k01.json`):
```json
{
  "chapterId": "k1",
  "nodes": [
    { "id": "k1_start", "type": "story", "x": 0.5, "y": 0.8, "label": "Waldlichtung", "next": ["k1_wolf"] },
    { "id": "k1_wolf", "type": "boss", "x": 0.5, "y": 0.6, "label": "Wolf-Rudel", "next": ["k1_path_split"] },
    { "id": "k1_path_split", "type": "story", "x": 0.5, "y": 0.4, "label": "Wegkreuzung", "next": ["k1_village_road"] },
    { "id": "k1_side1", "type": "sidequest", "x": 0.3, "y": 0.5, "label": "Verlassene Hütte", "next": [] },
    { "id": "k1_village_road", "type": "rest", "x": 0.5, "y": 0.2, "label": "Weg nach Eldenrath", "next": [] }
  ],
  "paths": [
    { "from": "k1_start", "to": "k1_wolf" },
    { "from": "k1_wolf", "to": "k1_path_split" },
    { "from": "k1_path_split", "to": "k1_side1" },
    { "from": "k1_path_split", "to": "k1_village_road" }
  ]
}
```

**Enemies JSON** (`Data/Enemies/enemies.json`):
```json
{
  "enemies": [
    {
      "id": "E001",
      "nameKey": "enemy_shadow_wolf",
      "level": 2, "hp": 35, "atk": 8, "def": 3,
      "element": "dark", "weakness": "light",
      "exp": 15, "gold": 10,
      "drops": [{ "itemId": "M001", "chance": 0.3 }]
    }
  ],
  "bosses": [
    {
      "id": "B005",
      "nameKey": "boss_wolf_alpha",
      "level": 3, "hp": 80, "atk": 12, "def": 5,
      "element": null, "weakness": "fire",
      "exp": 50, "gold": 30, "phases": 1,
      "drops": [{ "itemId": "C001", "chance": 1.0 }]
    }
  ]
}
```

---

## Phase 8: Klassenwahl (~1 Tag)

### Task 8.1: Player + PlayerClass Models

**Erstellen:**
- `Models/Player.cs` - Stats, Level, EXP, Gold, Karma, Klasse, Inventar
- `Models/PlayerClass.cs` - Basis-Stats, Auto-Bonus, Evolutions
- `Models/Enums/ClassName.cs`

### Task 8.2: ClassSelectScene

**Erstellen:** `Scenes/ClassSelectScene.cs`

3 Portraits nebeneinander, Stats-Vergleich als Balken, Skill-Preview, Bestätigen-Button.

Im Prolog: Klasse ist vorgewählt (Schwertmeister), zeigt Level 50 Stats.
In K1: Freie Wahl nach Klassenwahl-Tutorial.

---

## Phase 9: System-UI / Solo Leveling (~2 Tage)

### Task 9.1: StatusWindowRenderer + Overlay

**Erstellen:**
- `Rendering/UI/StatusWindowRenderer.cs`
- `Overlays/StatusWindowOverlay.cs`

Solo Leveling Stil:
- Dunkler Hintergrund (#0D1117, 85% Alpha)
- Blaue leuchtende Ränder mit SKMaskFilter.CreateBlur Glow
- Glitch-Effekt beim Einblenden (300ms)
- Stats CountUp-Animation
- Layout: Name → Klasse → Level + EXP-Bar → Stats-Grid → HP/MP Bars → Gold-Counter → Buffs

### Task 9.2: SystemMessage + LevelUp + FateChanged Overlays

**Erstellen:**
- `Overlays/SystemMessageOverlay.cs` - ARIA-Nachrichten, auto-dismiss 3-5s
- `Overlays/LevelUpOverlay.cs` - Fanfare, Stats hochzählen, +3 Punkte verteilen
- `Overlays/FateChangedOverlay.cs` - "Das Schicksal hat sich verändert...", Glitch, 2s

---

## Phase 10: Kampf-System (~3 Tage)

### Task 10.1: BattleEngine

**Erstellen:**
- `Models/Enemy.cs`
- `Models/Enums/Element.cs`
- `Services/BattleEngine.cs`

```csharp
public class BattleEngine
{
    public int CalculateDamage(int atk, float multi, int def, Element? atk_elem, Element? def_elem, int luk)
    {
        var base_dmg = (atk * multi) - (def * 0.5f);
        var elem_mod = GetElementModifier(atk_elem, def_elem);
        var rand_mod = 1f + (_rng.NextSingle() * 0.2f - 0.1f);
        var is_crit = _rng.NextSingle() < (luk * 0.005f);
        return Math.Max(1, (int)(base_dmg * elem_mod * rand_mod * (is_crit ? 2f : 1f)));
    }

    // 1.5x bei Schwäche, 0.5x bei Resistenz, 1.0x sonst
    // Feuer > Eis > Blitz > Wind > Licht > Dunkel > Feuer
    private float GetElementModifier(Element? atk, Element? def) { ... }
}
```

### Task 10.2: BattleScene

**Erstellen:** `Scenes/BattleScene.cs`

Kampf-Ablauf:
1. Intro (Gegner-Name + Level im System-Fenster)
2. Optionen (Angriff / Ausweichen / Skill / Item)
3. Animation + Floating Damage Numbers
4. HP-Bars (oben Gegner, unten Spieler)
5. Combo-Counter bei 3+ richtigen Entscheidungen
6. Boss: Phasen-Wechsel mit Mini-Cutscene

### Task 10.3: GameOverOverlay

**Erstellen:** `Overlays/GameOverOverlay.cs`

"Gefallen..." + Rewarded Ad Revive oder Speicherpunkt laden.

---

## Phase 11: Overworld-Map (~2 Tage)

### Task 11.1: MapNode Model + Map-JSON

**Erstellen:**
- `Models/MapNode.cs`
- `Models/Enums/NodeType.cs` (erweitern)
- JSON-Dateien pro Kapitel in `Data/Maps/`

### Task 11.2: OverworldRenderer

**Erstellen:**
- `Rendering/Map/OverworldRenderer.cs`
- `Rendering/Map/NodeRenderer.cs`
- `Rendering/Map/PathRenderer.cs`

### Task 11.3: OverworldScene + Camera

**Erstellen:** `Scenes/OverworldScene.cs`

Map-Navigation, HUD (Kapitel-Name, Level, HP, Gold), Menü-Button.

---

## Phase 12: RPG-Systeme (~3 Tage)

### Task 12.1: Skills + SkillEvolution

**Erstellen:**
- `Models/Skill.cs`, `Models/SkillEvolution.cs`
- `Services/SkillEvolutionService.cs`
- `Data/Skills/skills_swordmaster.json` (15 Einträge: 5 Skills × je 5 Stufen)
- `Data/Skills/skills_mage.json` (15 Einträge)
- `Data/Skills/skills_assassin.json` (15 Einträge)

### Task 12.2: Items + Inventar

**Erstellen:**
- `Models/Item.cs`, `Models/Enums/ItemType.cs`
- `Services/InventoryService.cs`
- `Data/Items/items.json` (Waffen W001-W015, Rüstungen A001-A010, Accessoires AC001-AC010, Consumables C001-C012, Key-Items K001-K008)

### Task 12.3: Scenes (Inventar/Shop/Status)

**Erstellen:**
- `Scenes/InventoryScene.cs` - Grid-Ansicht, Ausrüsten/Benutzen
- `Scenes/ShopScene.cs` - Kauf/Verkauf, Gold-Counter
- `Scenes/StatusScene.cs` - Detaillierte Stats, Skill-Mastery

### Task 12.4: Affinität + Fate-Tracking

**Erstellen:**
- `Models/AffinityData.cs`
- `Services/AffinityService.cs` - Bond-Stufen, Trigger
- `Services/FateTrackingService.cs` - Karma, Entscheidungs-Log, Zeitriss

### Task 12.5: Kodex + ChapterSummary

**Erstellen:**
- `Models/CodexEntry.cs`
- `Services/CodexService.cs`
- `Scenes/CodexScene.cs`
- `Scenes/ChapterSummaryScene.cs` - Kapitelende-Zusammenfassung
- `Services/ProgressionService.cs` - EXP/Level/Evolution

---

## Phase 13: Save-System (~1 Tag)

### Task 13.1: SaveGameService

**Erstellen:** `Services/SaveGameService.cs`

8 SQLite-Tabellen (siehe Design-Dokument). Auto-Save bei Knoten-Wechsel. 3 Slots.

**WICHTIG:** `InsertAsync()` gibt NICHT die ID zurück! (Bekannter Gotcha)

---

## Phase 14: Gold-Economy + Monetarisierung (~2 Tage)

### Task 14.1: GoldService + ChapterUnlockService

**Erstellen:**
- `Services/GoldService.cs` - Gold verwalten, Rewarded-Video-Cooldowns (3x/Tag)
- `Services/ChapterUnlockService.cs` - Kapitel-Freischaltung per Gold
- `Overlays/ChapterUnlockOverlay.cs` - "Kapitel 6 freischalten: 2.000 Gold" / "Gold verdienen: Video ansehen (3/3 verfügbar)"

```csharp
public class GoldService
{
    public int CurrentGold { get; private set; }
    public int DailyVideoWatchesRemaining { get; private set; } = 3;

    public void AddGold(int amount) { ... }
    public bool SpendGold(int amount) { ... }
    public async Task<bool> WatchVideoForGold() { ... } // Rewarded Ad → +500 Gold
    public void ResetDailyCounters() { ... } // Bei neuem Tag
}

public class ChapterUnlockService
{
    private readonly Dictionary<string, int> _chapterCosts = new()
    {
        ["k6"] = 2000, ["k7"] = 3000, ["k8"] = 4000,
        ["k9"] = 5000, ["k10"] = 6000
    };

    public bool IsUnlocked(string chapterId) { ... }
    public bool CanAfford(string chapterId) { ... }
    public bool UnlockWithGold(string chapterId) { ... }
}
```

### Task 14.2: Rewarded Ad Integration

6 Rewarded Ad Placements (gold_bonus, time_rift, bonus_exp, revive, daily_prophecy, kodex_hint).
Test-IDs bis AdMob-Console konfiguriert ist.

---

## Phase 15: Visuelle Polish (~2 Tage)

### Task 15.1: Erweiterte Transitions

**Erstellen:**
- `Engine/Transitions/GlitchCutTransition.cs` - Scan-Lines + Flicker (300ms)
- `Engine/Transitions/DissolveTransition.cs` - Partikel-Auflösung (800ms)
- `Engine/Transitions/MangaWipeTransition.cs` - Diagonal Panel-Wipe (600ms)
- `Engine/Transitions/IrisTransition.cs` - Kreis öffnet/schließt (700ms)

### Task 15.2: Spezial-Effekte

**Erstellen:**
- `Rendering/Effects/GlitchEffect.cs` - Horizontale Verschiebung + RGB-Split
- `Rendering/Effects/MangaPanelRenderer.cs` - Screen in Panels splitten
- `Rendering/Effects/SplashArtRenderer.cs` - Charakter-Portrait bei Ultimates
- `Rendering/Effects/ScreenShake.cs` - Canvas-Translation (3px/5px)

---

## Phase 16: Audio (~1 Tag)

### Task 16.1: AudioService

**Erstellen:** `Services/AudioService.cs`

SoundPool (Android) + Desktop-Fallback. BGM (loop), SFX (einmal), Volume-Controls.
Audio-Dateien als Assets einbinden (CC0 von OpenGameArt/Kenney/Freesound).

---

## Phase 17: Tutorial + Settings (~1 Tag)

### Task 17.1: TutorialService + TutorialOverlay

**Erstellen:**
- `Services/TutorialService.cs` - SeenHints in Preferences
- `Overlays/TutorialOverlay.cs` - Highlight + ARIA-Textbox

### Task 17.2: SettingsScene + PauseOverlay

**Erstellen:**
- `Scenes/SettingsScene.cs` - Sprache, Text-Speed, Volume
- `Overlays/PauseOverlay.cs` - Fortsetzen/Speichern/Status/Inventar/Kodex/Settings/Hauptmenü

### Task 17.3: DailyService

**Erstellen:** `Services/DailyService.cs` - Login-Bonus (Gold), Prophezeiung, Streak.

---

## Phase 18: Content Prolog P1-P3 (~3 Tage)

### Task 18.1: Prolog Kapitel 1 "Der letzte Marsch"

**Erstellen:**
- `Data/Chapters/chapter_p1.json` (~15-20 Nodes)
- `Data/Dialogue/de/chapter_p1.json` (+ en/es/fr/it/pt)
- `Data/Maps/overworld_p1.json`

Besonderheit: Spieler ist Level 50 mit max Ausrüstung. Alle Skills verfügbar. Prolog-spezifische Player-Config.

Emotionale Bond-Momente am Lagerfeuer:
- Aria: Erinnerung an erstes Abenteuer
- Aldric: Studiert heimlich verbotenen Zauber
- Kael: Scherzt, aber Hände zittern
- Luna: "Ich bringe euch alle nach Hause"

### Task 18.2: Prolog Kapitel 2 "Nihilus' Domäne"

**Erstellen:** Analog, ~20 Nodes.

Bosse: General Malachar (B001), General Vexara (B002). Kael-Opfer-Szene. Luna-Zusammenbruch.

### Task 18.3: Prolog Kapitel 3 "Das Ende aller Dinge"

**Erstellen:** Analog, ~15 Nodes.

Nihilus-Kampf (Skript, unbesiegbar nach 3 Runden). Aldrics Zeitriss. Alles verlieren. ARIA-Initialisierung. Übergang zu K1.

---

## Phase 19: Content Arc 1 K1-K5 gratis (~4 Tage)

### Task 19.1: K1 "Wiedergeburt" (~25 Nodes)

Level 1. Tutorial. ARIA erwacht. Wölfe. Klassenwahl. Erste Entscheidungen.

Node-IDs: `k1_awakening`, `k1_aria_intro`, `k1_tutorial_move`, `k1_tutorial_fight`, `k1_wolf_encounter`, `k1_wolf_choice` (Karma), `k1_class_select`, `k1_first_battle` (B005), `k1_after_battle`, `k1_direction_village`, `k1_chapter_end`

### Task 19.2: K2 "Das Dorf Eldenrath" (~25 Nodes)

Aria und Vex treffen. Banditen. Shop. Bond-Momente.

Node-IDs: `k2_village_arrival`, `k2_meet_aria`, `k2_aria_suspicion`, `k2_meet_vex`, `k2_shop_tutorial`, `k2_bandit_threat`, `k2_bandit_choice`, `k2_bandit_fight` (B006), `k2_aria_training`, `k2_bond_aria_1`, `k2_flashback_aria`, `k2_chapter_end`

### Task 19.3: K3 "Schatten im Wald" (~25 Nodes)

Kael. Dunkelwald. Albtraum (Nihilus-Hint). Wald-Golem.

Node-IDs: `k3_forest_enter`, `k3_meet_kael`, `k3_kael_rivalry`, `k3_dungeon_start`, `k3_nightmare` (Flashback zu Prolog), `k3_nightmare_choice`, `k3_golem_fight` (B007), `k3_crystal_find`, `k3_aria_reaction`, `k3_chapter_end`

### Task 19.4: K4 "Der Turm des Erzmagiers" + K5 "Zerbrochene Allianz"

Analog, je ~25 Nodes. K4: Aldric, Zeitmagie-Lore, Turm-Sentinel. K5: Luna, Story-Fork (Aria vs Kael).

---

## Phase 20: Content Arc 1 K6-K10 Gold (~4 Tage)

### Tasks 20.1-20.5: Je ein Kapitel

K6 Labyrinth (Gold: 2.000), K7 Mondschein-Offensive (3.000), K8 Verlorene Erinnerungen (4.000), K9 Fall von Eldenrath (5.000), K10 Erwachen + 6 Endings (6.000).

Besonderheiten:
- K6: Längster Dungeon, Story-Fork-Vereinigung
- K7: Alle NPCs zusammen, Nihilus-Hint (Dunkelheit am Horizont)
- K8: Traumwelt, ARIA = Aldric-Fragment Enthüllung
- K9: Dorf-Zerstörung, Affinität bestimmt NPC-Schicksal
- K10: 6 Endings (3 Klassen × 2 Karma), Nihilus-Echo Cutscene, Arc 2 Teaser

---

## Phase 21: Android-Integration + Final (~2 Tage)

### Task 21.1: MainActivity vollständig

- AdMob Initialize + Rewarded Ad Factory (6 Placements)
- PurchaseService Factory (Gold-Pakete, Zeitkristalle, remove_ads)
- BackPressHelper (Double-Back-to-Exit + Scene-Pop)
- DisposeServices in OnDestroy

### Task 21.2: CLAUDE.md + AppChecker

- `src/Apps/RebornSaga/CLAUDE.md` erstellen
- `dotnet run --project tools/AppChecker RebornSaga`

### Task 21.3: Localization

- `Resources/Strings/AppStrings.resx` (6 Sprachen)
- UI-Strings (Menüs, Buttons, System-Texte)
- Story-Texte sind in den JSON-Dateien

### Task 21.4: Build + Smoke Test

1. `dotnet build src/Apps/RebornSaga/RebornSaga.Shared`
2. `dotnet build src/Apps/RebornSaga/RebornSaga.Android`
3. Desktop durchspielen: Title → Prolog P1 → P2 → P3 → K1 → Klassenwahl → K2
4. Save/Load testen
5. Gold-System testen (verdienen + Kapitel freischalten)

---

## Abhängigkeiten

```
Phase 1-3 (erledigt) ← alles
Phase 4 (Title) ← Phase 13 (Save für echte Daten)
Phase 5 (Charakter) ← Phase 6 (Dialogue braucht Portraits)
Phase 6 (Dialogue) ← Phase 7 (StoryEngine nutzt DialogueScene)
Phase 7 (Story) ← Phase 18-20 (Content)
Phase 8 (Klassen) ← Phase 10 (Kampf braucht Stats)
Phase 10 (Kampf) ← Phase 18-20 (Content)
Phase 11 (Overworld) ← Phase 18-20 (Content)
Phase 14 (Gold) ← Phase 20 (K6-K10 kosten Gold)
```

**Kritischer Pfad:** 4 → 5 → 6 → 7 → 8 → 10 → 11 → 18 → 19 → 20 → 21

**Parallelisierbar:**
- Phase 9 (System-UI) parallel zu Phase 5-8
- Phase 12 (RPG) parallel zu Phase 10-11
- Phase 13 (Save) parallel zu Phase 10-12
- Phase 14-17 parallel zu Phase 18-20
