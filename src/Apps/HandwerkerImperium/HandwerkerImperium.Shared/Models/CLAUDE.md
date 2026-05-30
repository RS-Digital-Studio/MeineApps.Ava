# Models — Domain-Entities & Persistenz

Unveränderliche Datenstrukturen, Persistenz-Objekte und Konfigurations-Kataloge.
Keine Avalonia-Abhängigkeiten — reine C#-Klassen.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Schlüsseldateien

| Datei | Zweck |
|-------|-------|
| `GameState.cs` | Root-Persistenz-Objekt (Version 7). Enthält alle Sub-Objekte: Workshops, Workers, Orders, Research, Prestige, Ascension, Buildings, Boosts, DailyProgress, Settings, Statistics, Tutorial, Automation, CraftingInventory, ReservedInventory, HeirloomItems, Warehouse |
| `GameBalanceConstants.cs` | **Alle** Balancing-Zahlen an einem Ort — Prestige-PP-Formel, Income-Multiplikatoren, Timings, Caps. NIEMALS hardcoded Zahlen außerhalb dieser Datei |
| `LevelThresholds.cs` | Alle Level-Gates: Feature-Unlocks, Tab-Visibilities, Automation-Unlock |
| `GameState.cs` → `CurrentStateVersion = 7` | SaveGame-Version. Cloud-Save mit höherer Version → Alert statt Download |

---

## SaveGame-Versionen

| Version | Wesentliche Änderung |
|---------|---------------------|
| 1 | Legacy (altes Worker-System) |
| 2 | Neues Worker-System, Buildings, Research, Events, Prestige, Reputation |
| 3 | Workshop Rebirth Stars (WorkshopStars Dictionary) |
| 4 | Settings, Statistics, Tutorial in Sub-Objekte extrahiert |
| 5 | Boosts, DailyProgress, Cosmetics in Sub-Objekte extrahiert |
| 6 | ParallelOrdersByWorkshop, PausedAt/AccumulatedPauseDuration |
| 7 | Warehouse (SlotCount 20, StackLimit 50), ReservedInventory, AutoSellRules, HeirloomItems. Migration kürzt überlaufende Stacks + zahlt BaseValue als Geld aus |

---

## Domain-Entities (Auswahl)

| Datei | Inhalt |
|-------|--------|
| `Worker.cs` | 10 Tiers (F–Legendary), EffectiveEfficiency-Formel, MaterialAffinity-Enum. `HiringCost` persistiert (`[JsonPropertyName("hiringCost")]`) — Marktpreise bleiben nach Neustart korrekt |
| `Order.cs` | Alle Order-Typen, MaterialOffer-Felder, PausedAt/AccumulatedPauseDuration, ExpiresAt |
| `Workshop.cs` | WorkshopType-Enum, Spezialisierung, Rebirth-Sterne |
| `CraftingRecipe.cs` | 30 Rezepte (10 Workshops × 3 Tiers). `GetEffectiveInputs()` filtert Cross-Workshop-Inputs unter Spielerlevel 100 |
| `Research.cs` | 45 Forschungs-Nodes, Branches (Management/Craft/Logistics + Guild-spezifisch) |
| `ResearchTree.cs` | Node-Graph, Abhängigkeits-Checks |
| `MasterTool.cs` | 12 Artefakte, 5 Seltenheiten, Freischalt-Bedingungen |
| `GuildResearch.cs` | 18 Gilde-Forschungen, 6 Kategorien, Effekte |
| `PrestigeData.cs` | Prestige-Verlauf, Meilensteine, Challenges, PrestigesSinceLastWeeklyReward |
| `AscensionData.cs` | 6 Perks × MaxLevel 3, AP-Punkte |
| `AscensionPerk.cs` | Perk-Definition + Effekt-Beschreibung |

---

## Enums (Models/Enums/)

Wichtige Enums: `ActivePage`, `WorkshopType`, `WorkerTier`, `PrestigeTier`, `OrderType`,
`WorkshopSpecializationType`, `ResearchBranch`, `GraphicsQuality`, `ImperiumSubTab`,
`CustomerReputationTier`, `MaterialAffinity`.

---

## Firebase-DTOs (Models/Firebase/)

DTOs für Firebase Realtime Database: `FirebaseGuildData`, `FirebaseGuildMember`,
`FirebaseCoopOrder`, `FirebaseWorkerAuction`, `FirebaseGuildMegaProject`.
HMAC-Signierung über stabile Felder (kein `updatedAt` in der Hash-Basis — ändert sich bei jedem Write).

---

## Konfigurations-Kataloge

| Datei | Zweck |
|-------|-------|
| `WorkshopFormulas.cs` | Upgrade-Kosten-Kurven, Einkommens-Formeln pro Workshop-Typ |
| `RemoteConfigKeys.cs` | Vordefinierte Schlüssel für `IRemoteConfigService` |
| `AnalyticsEvents.cs` | Alle Telemetrie-Event-Namen + Property-Keys (Single-Source-of-Truth) |
| `FtueStep.cs` | FTUE-Schritt-Definitionen (Enum + Metadaten) |
| `ContextualHint.cs` | Hint-Typ-Definitionen + Auslöse-Bedingungen |
| `DailyBundleOffer.cs` | Bundle-Template-Struktur |

---

## Gotcha — sqlite-net InsertAsync gibt keine ID zurück

`InsertAsync()` gibt Zeilen-Count zurück (immer 1), NICHT Auto-Increment-ID.
sqlite-net setzt die ID direkt auf dem Objekt-Property.

```csharp
// FALSCH:
entity.Id = await db.InsertAsync(entity);
// RICHTIG:
await db.InsertAsync(entity);
// entity.Id ist bereits korrekt gesetzt
```

## Gotcha — Worker.AssignedWorkshop null bei CreateNew()

`GameState.CreateNew()` und `Worker.CreateForTier()` MÜSSEN `AssignedWorkshop` explizit setzen.
Enum-Default `0` (= `Carpenter`) ist zufällig korrekt, aber niemals darauf verlassen.
