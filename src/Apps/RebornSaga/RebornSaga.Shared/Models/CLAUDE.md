# Models — Datenmodelle

Reine Datenmodelle ohne Spiellogik. Serialisiert von `SaveGameService` (SQLite + JSON).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Klasse | Beschreibung |
|-------|--------|-------------|
| `Player.cs` | `Player` | Spieler-Stats, Level, Inventar, Karma, Story-Fortschritt. `Create(className)` + `CreatePrologHero()`. `AddExp()` berechnet Level-Ups intern. |
| `PlayerClass.cs` | `PlayerClass` | Klassen-Definitionen mit Basis-Stats und Wachstums-Werten pro Level. |
| `Enemy.cs` | `Enemy` | Gegner-Daten (HP, ATK, DEF, Element, Drops). `EnemyLoader.GetById()` gibt geklonte Kopie zurück. |
| `Item.cs` | `Item` | Item-Daten (ID, Name, Typ, Wert, Effekt). |
| `Skill.cs` | `Skill` | Skill-Definition (Klasse, Level-Anforderung, MP-Kosten, Effekt). |
| `Chapter.cs` | `Chapter` | Kapitel-Struktur (ID, Nodes, Verbindungen). Aus `chapter_{id}.json` geladen. |
| `StoryNode.cs` | `StoryNode` | Knoten-Daten (Typ, Condition, Effekte, Dialogue-Key, Choices). |
| `MapNode.cs` | `MapNode` | Overworld-Kartenknoten (Position, Typ, Verbindungen, Freischaltstatus). |
| `SaveData.cs` | `SaveData` | SQLite-Entität für einen Speicherplatz (enthält serialisierten Player-JSON). |
| `AffinityData.cs` | `AffinityData` | Bond-Wert (0–100) für einen NPC. |
| `AssetManifest.cs` | `AssetManifest` | Beschreibt alle Asset-Packs für `AssetDeliveryService` (Dateiname, SHA256, Größe). |
| `Enums/ClassName.cs` | `ClassName` | Swordmaster, Arcanist, Shadowblade |
| `Enums/Element.cs` | `Element` | Fire, Ice, Lightning, Wind, Light, Dark |
| `Enums/ItemType.cs` | `ItemType` | Weapon, Armor, Consumable, KeyItem |
| `Enums/NodeType.cs` | `NodeType` | Story, Boss, SideQuest, Npc, Dungeon, Rest, Locked |

## Player — Wichtige Details

```csharp
// EXP-Schwelle: 40 * Level^1.35 (flacht ab Level 15 ab — weniger Grind)
public int ExpToNextLevel => (int)(40 * MathF.Pow(Level, 1.35f));

// AddExp() gibt Anzahl Level-Ups zurück (für LevelUpOverlay)
public int AddExp(int amount) { ... }

// Player.Flags: HashSet<string> für Story-Conditions ("betrayed_aldric", "alliance_aria")
// Player.Affinities: Dictionary<string, int> NPC-ID → Bond-Wert
// Player.Equipment: Dictionary<string, string> Slot → Item-ID
```

**Klassen-Wachstum:**

| Klasse | Stärke | Schwäche | Auto-Bonus/Level |
|--------|--------|----------|------------------|
| Schwertmeister | STR, DEF | MAG | +2 ATK |
| Arkanist | MAG, MP | DEF | +3 MP |
| Schattenklinke | AGI, LUK | HP | +5% Crit |

**Element-System:**
Feuer > Eis > Blitz > Wind > Licht > Dunkel > Feuer
(Schwäche: 1,5× Schaden, Resistenz: 0,5× Schaden)

## Knoten-Typen (StoryNode)

| Typ | Bedeutung |
|-----|-----------|
| Dialogue | Dialogsequenz mit Portrait + Text + Choices |
| Choice | Verzweigung (mit optionalen Karma-Tags) |
| Battle | Gegner-Kampf |
| ClassSelect | Klassen-Auswahl des Spielers |
| Shop | Kaufen/Verkaufen |
| Cutscene | Animated-WebP-Cutscene |
| Overworld | Kapitel-Karte einblenden |
| BondScene | NPC-Affinitäts-Szene |
| FateChange | `FateChangedOverlay` + `FateChangeTriggered`-Event |
| SystemMessage | ARIA-System-Nachricht via `SystemMessageOverlay` |
| ChapterEnd | Kapitel abschließen, nächstes freischalten |
