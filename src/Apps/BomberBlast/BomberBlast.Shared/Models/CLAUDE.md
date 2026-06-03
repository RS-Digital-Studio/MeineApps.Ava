# Models — Domain-Modelle

Reine Daten-Klassen: Entities, Grid, Levels, Dungeon, Cards, BattlePass, Cosmetics,
Cloud-Save. Keine Business-Logik — die gehört in Services/GameEngine.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## Unterordner

### `Entities/`

| Datei | Zweck |
|-------|-------|
| `Entity.cs` | Basis: GridX/GridY, PixelX/PixelY, Velocity |
| `Player.cs` | PowerUps, Lives, Skulls, `SquashScaleX/Y`, `DiscoveredPowerUps`, Pre-Turn-Buffer |
| `Enemy.cs` | EnemyType, Speed, `IsElite` (1.2× Speed, 2× HP, 3× Points, lila Glow) |
| `BossEnemy.cs` | Multi-Cell-BoundingBox, HP, Phase, `Modifier` (BossModifier-Enum), `AnticipationScale` |
| `Bomb.cs` | BombType, FireRange, PlantedAt, IsDetonator, Slide-State |
| `Explosion.cs` | Zellen-Liste, Schaden-Radius, OwnerType |
| `PowerUp.cs` | PowerUpType, Unlock-Level |
| `PowerUpType.cs` | 13 Typen: BombUp/Fire/Speed/Wallpass/Detonator/Bombpass/Flamepass/Mystery/Kick/LineBomb/PowerBomb/Skull/Cure. **Cure muss als LETZTER Wert bleiben** — Skull-Persistenz darf nicht durch Enum-Verschiebung brechen. |
| `EnemyType.cs` | 12 Typen: 8 Basis + Tanker/Ghost/Splitter/Mimic |
| `Direction.cs` | Up/Down/Left/Right/None |
| `BossModifier.cs` | 8 Modifier-Typen (Shielded/Healing/Summoner/…) + `RollForWorld(world, rng)` |
| `CollisionHelper.cs` | Statisch: Grid-Kollisionsberechnungen |

### `Grid/`

| Datei | Zweck |
|-------|-------|
| `GameGrid.cs` | 15×10 Grid, Cell-Array, Accessor-Methoden |
| `Cell.cs` | CellType, `IsDestructible`, enthält Entities-Referenz |
| `CellType.cs` | 9 Werte: Empty/Wall/Block/Exit + Ice/Conveyor/Teleporter/LavaCrack/PlatformGap (5 Welt-Mechanik-Zellen für Welt 2/3/4/5/9) |

### `Levels/`

| Datei | Zweck |
|-------|-------|
| `Level.cs` | LevelNumber, WorldIndex, Layout-Typ, aktive Mutator |
| `LevelLayoutGenerator.cs` | **Static**. 12 Layout-Typen (`LevelLayout`-Enum in Level.cs), Pool 8 Layouts pro Welt, `GenerateLevel(levelNumber)`, `GenerateDailyChallengeLevel(seed)`. KEINE DI. |

### `Dungeon/`

| Datei | Zweck |
|-------|-------|
| `DungeonRunState.cs` | Aktiver Run: Floor, HP, Buffs, Modifikatoren. Keine eigene Versionierung — Migration in `CloudSaveSchemaMigrator`. |
| `DungeonBuff.cs` | 16 Buffs (5 Common/5 Rare/2 Epic/4 Legendary), Rarity, Effekt |
| `DungeonMapNode.cs` | Node-Map-Knoten (10×3): RoomType, gewählter Pfad |
| `DungeonRoomType.cs` | Normal/Elite/Treasure/Challenge/Rest mit Gewichtung |
| `DungeonFloorModifier.cs` | 8 Modifikatoren ab Floor 3 (30% Chance) |
| `DungeonUpgrade.cs` | Permanente Dungeon-Upgrades (DungeonCoins, 8 Typen) |

### `Cards/`

| Datei | Zweck |
|-------|-------|
| `BombCard.cs` | Karten-Definition: BombType, Rarity, Uses-pro-Level, Upgrade-/Direktkauf-Kosten |
| `OwnedCard.cs` | Besessene Karte: CardId + Level + Count |
| `CardCatalog.cs` | 13 Bomben-Karten (3 Common + 4 Rare + 4 Epic + 2 Legendary), `MaxDeckSlots = 5`, `DefaultDeckSlots = 4` |

### `BattlePass/`

| Datei | Zweck |
|-------|-------|
| `BattlePassData.cs` | Saison-Daten: Tier, XP, Theme (deterministisch aus SeasonNumber), `XpBoostStartTicks` (Anti-Cheat-Anchor), Legacy-Rewards + Premium-Veteran-Bonus |
| `BattlePassTier.cs` | Tier-Definition: Level, Free/Premium-Reward, XP-Schwelle |
| `BattlePassReward.cs` | Reward-Payload: Coins/Gems/Cards/Cosmetics |
| `BattlePassTheme.cs` | 10 Themes (Classic + 9 Saison-Themes) + `BattlePassThemeExtensions` (Farben, Icon-Hints, RESX-Keys, `GetThemeForSeason`) |

### `Cosmetics/`

| Datei | Zweck |
|-------|-------|
| `TrailDefinition.cs` | 32 Trail-Cosmetics (`TrailDefinitions.All`) |
| `FrameDefinition.cs` | 33 Frame-Cosmetics (`FrameDefinitions.All`) |
| `VictoryDefinition.cs` | 33 Victory-Cosmetics (`VictoryDefinitions.All`) |

### `Collection/`

| Datei | Zweck |
|-------|-------|
| `CollectionEntry.cs` | Sammlung-Eintrag: Gegner/Boss/PowerUp-Tracking (Kills, Erstentdeckung) |

### `League/`

| Datei | Zweck |
|-------|-------|
| `LeagueData.cs` | Spieler-Liga-State: Tier, SubTier, Points, UID |
| `LeagueTier.cs` | Enum + `LeagueSubTier` (I/II/III für Bronze–Platinum, Diamond single) |

### `Firebase/`

| Datei | Zweck |
|-------|-------|
| `FirebaseAuthResponse.cs` | Anonymous-Auth-Response (idToken, localId, expiresIn) |
| `FirebaseLeagueEntry.cs` | Firebase-RTDB-Eintrag für Liga-Leaderboard |
| `FirebaseTokenResponse.cs` | Token-Refresh-Response |

---

## Wurzel-Dateien

| Datei | Zweck |
|-------|-------|
| `CloudSaveData.cs` | Persistenz-Keys als `Dictionary<string, string>`, `ChooseBest()` (TotalStars→Wealth→Cards→Keys.Count→Timestamp→Cloud-Default), `MergeBest()` (Per-Field-Max, verhindert Data-Loss bei minimal divergierten Ständen) |
| `CloudSaveSchemaMigrator.cs` | `CurrentSchemaVersion = 3`, `TryMigrateAndValidate()`. V1→V2→V3-Migrationen. Läuft VOR `ApplyCloudData`. **DeleteCloudSaveAsync muss `Version = CurrentSchemaVersion` setzen** (nie hardcoded 1). |
| `BomberBlastIapSkus.cs` | IAP-SKU-Konstanten (`remove_ads`, Gem-Pakete, BattlePass-Plus, VIP) |
| `SkinDefinition.cs` | Spieler-Skin: ID, Name-Key, UnlockCondition, PreviewColor |
| `HeroDefinition.cs` | 5 Heroes (Default/SpeedySam/BrickBoris/TwinTina/LuckyLola): Stats + Multiplier + HeroTrait |
| `TutorialStep.cs` | Tutorial-Schritt: Phase, Text-Key, `IsFirstOfPhase`-Flag |
| `Achievement.cs` | Achievement: ID, Kategorie, Ziel-Count, Icon-Key |
| `WeeklyMission.cs` | Wöchentliche Mission: Typ, Ziel, XP-Belohnung |
| `DailyReward.cs` | 7-Tage-Login-Bonus-Definition: Tag, Coin/Gem-Belohnung |
| `DailyRewardDisplayItem.cs` | UI-Hilfsklasse für DailyReward-Anzeige |
| `RotatingDeal.cs` | Tägliches/wöchentliches Angebot: Original- + Rabattpreis, Typ |
| `ShopDisplayItem.cs` | Shop-Upgrade-Anzeige-Hilfsobjekt |
| `SkinDisplayItem.cs` | Skin-Galerie-Anzeige-Hilfsobjekt |
| `PowerUpDisplayItem.cs` | PowerUp-Discovery-Anzeige-Hilfsobjekt |
| `PlayerUpgrades.cs` | Persistierte Shop-Upgrade-Level pro UpgradeType |
| `UpgradeType.cs` | Enum: 12 permanente Shop-Upgrades (9 Stat-Upgrades + 3 Bomb-Unlocks IceBomb/FireBomb/StickyBomb) |
| `Rarity.cs` | Enum: Common/Rare/Epic/Legendary mit Drop-Gewichtung |
