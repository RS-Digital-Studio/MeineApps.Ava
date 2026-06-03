# Models — Datenmodelle

Reine Datenmodelle ohne Spiellogik. Persistiert von `SaveGameService` via SQLite
(eigene `*Entity`-Klassen) und JSON-Deserialisierung (Story/Map/Assets).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Identität, Klassen-Wachstum, Element-Kreislauf, Economy → [../../../CLAUDE.md](../../../CLAUDE.md).

---

## Dateien und Klassen

### Laufzeit-Modelle (reine Daten, kein SQLite)

| Datei | Klasse(n) | Beschreibung |
|-------|-----------|-------------|
| `Player.cs` | `Player` | Spieler-Stats, Level, Inventar, Karma, Story-Fortschritt. `Create(className)` + `CreatePrologHero()`. `AddExp()` berechnet Level-Ups intern. |
| `PlayerClass.cs` | `PlayerClass` | Klassen-Definitionen mit Basis-Stats und Wachstums-Werten pro Level. Statische Instanzen `Swordmaster`, `Arcanist`, `Shadowblade`. `Get(ClassName)` / `Get(int)` als Lookup. |
| `Enemy.cs` | `Enemy`, `EnemyDrop` | Gegner-Daten aus JSON (HP, ATK, DEF, Element, Drops, Phases, Scripted-Flags). `Element`/`Weakness` lazy-gecacht aus String. |
| `Item.cs` | `Item` | Item-Daten aus JSON (ID, NameKey, Typ, Stat-Boni, Heal-Werte, Preis, Effect-String). `IsEquippable`, `IsUsable`, `Slot` als berechnete Properties. |
| `Skill.cs` | `Skill`, `PlayerSkill` | Skill-Definition aus JSON (Klasse, MP-Kosten, Multiplikator, Tier 1-5, MasteryRequired, NextTierId). `PlayerSkill` hält die Instanz mit Mastery-Fortschritt und `CanEvolve`. |
| `Chapter.cs` | `Chapter` | Kapitel-Struktur (ID, TitleKey, IsProlog, GoldCost, Nodes). Aus `chapter_{id}.json` geladen. |
| `StoryNode.cs` | `StoryNode`, `SpeakerLine`, `ChoiceOption`, `StoryEffects` | Knoten-Daten (Typ, Condition, Speakers, Options, Effects, MangaPanel, VisualEffects). |
| `MapNode.cs` | `MapNode`, `ChapterMap` | Overworld-Map-Knoten (Position 0–1 normalisiert, Typ, Connections, RuntimeState). `ChapterMap` enthält alle Knoten eines Kapitels. |
| `AffinityData.cs` | `AffinityData`, `FateDecision`, `CodexEntry` | Bond-Wert (0–100) für einen NPC mit 5 Stufen; Entscheidungs-Log-Eintrag; Kodex-Eintrag (Bestiary, Lore). |
| `AssetManifest.cs` | `AssetManifest`, `AssetPack`, `AssetFile` | Beschreibt alle Asset-Packs für `AssetDeliveryService` (Dateiname, SHA256-Hash, Größe, Required-Flag). |

### Enums

| Datei | Enum(s) | Werte |
|-------|---------|-------|
| `Enums/ClassName.cs` | `ClassName` | `Swordmaster`, `Arcanist`, `Shadowblade` |
| `Enums/Element.cs` | `Element` | `Fire`, `Ice`, `Lightning`, `Wind`, `Light`, `Dark` |
| `Enums/ItemType.cs` | `ItemType`, `EquipSlot` | `Weapon`, `Armor`, `Accessory`, `Consumable`, `KeyItem` · `Weapon`, `Armor`, `Accessory` |
| `Enums/NodeType.cs` | `NodeType`, `MapNodeType` | Siehe Abschnitt unten |

### SQLite-Entitäten (`SaveData.cs`)

Kein `SaveData`-Monolith — das Savegame ist auf mehrere normalisierte Tabellen aufgeteilt:

| Klasse | SQLite-Tabelle | Beschreibung |
|--------|---------------|-------------|
| `SaveSlotEntity` | `SaveSlots` | Slot-Metadaten (1–3 + Auto-Save-Slot 0) |
| `PlayerDataEntity` | `PlayerData` | Stats, Gold, Karma, FlagsJson, CompletedChaptersJson |
| `InventoryEntity` | `Inventory` | Item-ID, Quantity, IsEquipped, EquipSlot |
| `SkillDataEntity` | `SkillData` | SkillId, MasteryCount, IsUnlocked |
| `AffinityEntity` | `AffinityData` | NpcId, Points, BondLevel, SeenScenesStr |
| `StoryProgressEntity` | `StoryProgress` | ChapterId, NodeId, ChoiceIndex, KarmaChange |
| `CodexEntity` | `CodexEntries` | EntryId, IsDiscovered |
| `ChapterUnlockEntity` | `ChapterUnlocks` | ChapterId, UnlockMethod, UnlockedAt (global für alle Slots) |

---

## Player — Wichtige Details

```csharp
// EXP-Schwelle: 40 * Level^1.35
public int ExpToNextLevel => (int)(40 * MathF.Pow(Level, 1.35f));

// AddExp() gibt Anzahl Level-Ups zurück (für LevelUpOverlay), vergibt 3 FreeStatPoints/Level
public int AddExp(int amount) { ... }

// Inventar: HashSet<string> (Item-IDs, Duplikate ausgeschlossen)
// Equipment: Dictionary<string, string> (Slot → Item-ID)
// Affinities: Dictionary<string, int> (NPC-ID → Punkte)
// Flags: HashSet<string> (Story-Conditions, z.B. "betrayed_aldric")
```

---

## Knoten-Typen

### NodeType (StoryNode)

| Wert | Bedeutung |
|------|-----------|
| `Dialogue` | Dialogsequenz mit Portrait + Sprecher-Lines + Choices |
| `Choice` | Verzweigung mit optionalen Karma-Tags |
| `Battle` | Gegner-Kampf |
| `ClassSelect` | Klassen-Auswahl (Prolog) |
| `Shop` | Kaufen/Verkaufen |
| `Cutscene` | Nicht-interaktive Zwischensequenz |
| `Overworld` | Kapitel-Karte einblenden |
| `BondScene` | NPC-Affinitäts-Szene |
| `FateChange` | `FateChangedOverlay` + `FateChangeTriggered`-Event |
| `SystemMessage` | ARIA-System-Nachricht via `SystemMessageOverlay` |
| `ChapterEnd` | Kapitel abschließen, nächstes freischalten |

### MapNodeType (MapNode)

| Wert | Farbe | Bedeutung |
|------|-------|-----------|
| `Story` | Gold | Hauptquest, Pflicht |
| `SideQuest` | Silber | Optional, Bonus-EXP/Items |
| `Boss` | Rot | Kampf-Knoten |
| `Npc` | Blau | NPC/Shop, Bond-Szenen |
| `Dungeon` | Lila | Mehrere Kämpfe hintereinander |
| `Rest` | Grün | HP/MP regenerieren, Speichern |
| `Locked` | Grau | Kapitel nicht freigeschaltet |
