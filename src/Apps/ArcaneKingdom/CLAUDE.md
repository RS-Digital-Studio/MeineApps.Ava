# ArcaneKingdom: Mobile TCG + RPG (Arbeitstitel)

Vollstaendig **Unity-basiertes** Sammelkartenspiel — die **einzige App in dieser Codebase, die nicht
Avalonia/.NET** nutzt. Bitte vor jeder Arbeit am Projekt die Tech-Stack-Unterschiede zu allen
anderen Apps bewusst machen.

| Aspekt | Wert |
|--------|------|
| Status | Konzept-Phase abgeschlossen — Pre-MVP (Stand 2026-05-25, Designplan v4 eingearbeitet) |
| Tech | Unity 6 (6000.4.8f1) + C# (.NET Standard 2.1) |
| Plattform | Android (Phase 1), iOS (Phase 2 ab Monat 26+) |
| Render-Pipeline | URP 17.x (optional aktivieren, siehe SETUP.md) |
| Backend | Firebase + Photon |
| Genre | TCG + RPG, Free-to-Play |
| Karten-Pool (Launch) | 131 Standard + 27 Oekosystem (Event/Premium/Sternkarten/Prestige) = 158 Karten |
| Welten | 10 Welten (Elderwald → Drachenfeste) mit Story-Mythologie |
| Farbpalette | Royal-Purple #6B46C1 + Gold #F59E0B (Brand-Referenz, finalisiert v5.2) |

> Pflichtlektuere VOR Aenderungen: [DESIGN.md](DESIGN.md) (v6, Quelle der Wahrheit) und [ARCHITECTURE.md](ARCHITECTURE.md).
> Generische Repo-Konventionen siehe [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Designplan-Quelle

Die aktuelle Designspec basiert auf **Designplan v4** (Maerz 2026):

| Quelldokument | Inhalt |
|---------------|--------|
| `Arcane_Legends_Designplan_v4.docx` | 5 Rassen, 6 Elemente Doppel-Dreieck, 131 Karten Pyramide, Fusions-Crafting, Auto-Battle |
| `Arcane_Legends_Kartenliste_v4.docx` | Vollstaendige Liste aller 131 Karten + Stats |
| `Arcane_Legends_Skills_v4.docx` | Skills 1 + 2 + 3 + Letzter Wille fuer alle 3*+ Karten |
| `Arcane_Legends_Oekosystem_v4.docx` | Karten-Besitz, Event/Premium/Saison-Pass/Sternkarten/Prestige |
| `Arcane_Legends_Story_v4_1.docx` | Welt-Mythologie, 10 Welten Story, Erinnerungs-Fragmente, Twist, NPCs |
| `Arcane_Legends_Art_Style_Guide_v4.docx` | Visueller Leitfaden, AI-Prompts pro Rasse |
| `Arcane_Legends_Implementierungsplan_v4.docx` | 7 Phasen Entwicklungs-Roadmap, MVP-Definition |

Quelldokumente liegen unter `F:\AI\ComfyUI_windows_portable\ComfyUI\output\eva\Spiele Ideen Ordner\Ideen\`.

---

## Wichtige Dateien

| Datei | Zweck |
|-------|-------|
| [README.md](README.md) | Quickstart fuer Entwickler |
| [SETUP.md](SETUP.md) | Schritt-fuer-Schritt-Anleitung fuer Unity 6 (Pflichtlektuere beim ersten Open) |
| [DESIGN.md](DESIGN.md) | **GDD v6.0** (autoritativ, basiert auf Designplan v4) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Folder-Layout, DI, Networking, Conventions |
| [Server/SERVEROPS.md](Server/SERVEROPS.md) | Server-side Cloud-Functions (Anti-Cheat, Saison-Rewards) |
| [Server/CloudFunctions/](Server/CloudFunctions/) | TypeScript-Cloud-Functions (8 Endpoints) |
| [Unity/](Unity/) | Das eigentliche Unity-Projekt |

---

## Tech-Stack Schnellueberblick

| Bereich | Wahl | Anders als die anderen Apps |
|---------|------|----------------------------|
| Engine | Unity 6 (6000.4.x) | Andere: Avalonia 12 |
| Sprache | C# (.NET Standard 2.1) | Andere: .NET 10 |
| UI | UI Toolkit + UGUI | Andere: AXAML |
| DI | VContainer | Andere: Microsoft.Extensions.DI |
| Async | UniTask | Andere: Task<T> |
| Localization | com.unity.localization + strings.csv | Andere: RESX + ILocalizationService |
| Persistenz | PlayerPrefs + JSON-File + Firebase Realtime DB | Andere: sqlite-net + PreferencesService |
| Networking | Firebase + Photon | Andere: SignalR (BingXBot) oder keines |
| IAP | Unity IAP + Google Play Billing v6 | Andere: Xamarin.Android.Google.BillingClient v8 |
| Build | Unity Cloud Build / GitHub Actions | Andere: dotnet publish |

---

## Domain-Modelle (v4-Spec)

### Rassen (5)

```csharp
public enum Race { Ritter, Goetter, Elfen, Tiergeister, Daemonen }
```

`Goetter` sind eine **Premium-Rasse** — nur als 4*+ und ausschliesslich ueber Fusion erhaeltlich.

### Elemente (6, Doppel-Dreieck)

```csharp
public enum Element { Feuer, Wasser, Natur, Erde, Dunkel, Licht }
```

Physisches Dreieck (Feuer → Natur → Wasser → Feuer) + Magisches Dreieck (Licht → Dunkel → Erde → Licht).
Matchup-Matrix: `Domain/Battle/ElementMatchup.cs` (Stark = 1.10x, Schwach = 0.90x, Cross-Dreieck = 1.00x).

### Seltenheitsstufen (6)

```csharp
public enum Rarity { Gewoehnlich, Ungewoehnlich, Selten, Epic, Legendaer, Mythisch }
```

6* Mythisch sind die einzigartigen Endgame-Karten (5 Stueck im Spiel, eine pro Rasse).

### Helden-Passivs (1 pro Rasse)

```csharp
public enum HeroFaehigkeitsTyp {
    KoeniglicheAura,   // Ritter: +5% HP fuer eigene Karten
    GoettlicherSegen,  // Goetter: 1x pro Kampf Tod verhindern
    Waldlaeufer,       // Elfen: erste Karte jeder Runde kostet 0
    Rudelbund,         // Tiergeister: +3% ATK pro Tiergeist im Deck
    LebensraubAura     // Daemonen: 20% Schaden heilt Helden-HP
}
```

Helden sind PASSIV in v4. Kein Cooldown, kein manuelles Auslösen.

---

## Daten-Files (Resources/Data)

| Datei | Inhalt |
|-------|--------|
| `cards.json` | 158 Karten (131 Standard + 9 Event + 6 Premium + 2 Sternkarten + 10 Prestige) |
| `worlds.json` | 10 Welten mit Saeulen, Bossen, Erinnerungs-Fragmenten, Mentoren |
| `heroes.json` | 5 Rassen-Helden mit Passiv-Skills |
| `abilities.json` | Skill-Definitionen (Awakening, Skill 2, Skill 3, Letzter Wille) |
| `runes.json` | 18 Runen-Definitionen |
| `fusion_recipes.json` | Feste Rezepte (inkl. Goetter-Crafting Rezepte) |
| `login_rewards.json` | 30-Tage-Login-Zyklus mit Sternkarten |
| `star_temple.json` | Sternkarten-Tempel-Eintausch (30/80/150/350/100/500 Sternpunkte) |
| `premium_shop.json` | 6 Premium-Karten (3 permanent + 3 rotierend) |
| `events.json` | 5 Saison-Events (Yule, Bluetenfest, Sonnenwende, Erntemond, Schattenerwachen) |
| `prestige_balancing.json` | Prestige I-IV Balancing (Stats, Drops, Daily-Income, Boss-Phasen) |
| `story_fragments.json` | Welt-Mythologie, 10 Erinnerungs-Fragmente, Schluessel-NPCs, sechs Saeulen |
| `collections.json` | Material-Karten-Sets (Weisses Herz, Dunkles Herz, Drachen, Maschinen) |
| `material_drops.json` | Material-Drops pro Welt-Boss / Mini-Boss |
| `tutorial.json` | 8 Tutorial-Schritte |
| `quests.json` | Daily/Weekly/Achievement-Quests |
| `notifications.json` | Push-Notification-Templates |
| `saison_pass.json` | Saison 1 (Aetherius), 30 Stufen, Free + Premium |
| `packs.json` | Karten-Pack-Definitionen |
| `achievements.json` | Erfolge mit Trigger-Hooks |

---

## Build & Run

### Voraussetzungen

> Vollstaendige Anleitung in [SETUP.md](SETUP.md).

1. **Unity Hub** installieren ([unity.com/download](https://unity.com/download))
2. **Unity 6000.4.8f1** via Unity Hub installieren (mit Android Build Support + JDK + Android SDK/NDK)
3. Projekt oeffnen, Setup-Wizard erscheint automatisch ("ArcaneKingdom → Setup → First-Time Setup Wizard")

### Projekt oeffnen

```
Unity Hub → Add project → F:\Meine_Apps_Ava\src\Apps\ArcaneKingdom\Unity\
```

### Daten importieren (ScriptableObjects)

```
Unity-Menue → ArcaneKingdom → Data → Import All
```

Erzeugt alle ScriptableObject-Assets aus den JSON-Dateien unter `Resources/Data/`.

### Im Editor starten

1. Boot-Scene oeffnen: `Assets/_Project/Scenes/Boot/Boot.unity`
2. `Play` druecken

### Android Build (Debug)

1. File → Build Settings → Android → Switch Platform
2. Player Settings → Other Settings → Scripting Backend = IL2CPP, Target Architecture = ARM64
3. Build oder Build & Run

### Android Release (AAB fuer Play Store)

```
"C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "F:\Meine_Apps_Ava\src\Apps\ArcaneKingdom\Unity" `
  -executeMethod BuildScripts.BuildAndroidRelease `
  -logFile build.log
```

---

## Wichtige Konventionen (Auszug)

> Vollstaendig in [ARCHITECTURE.md Kapitel 11](ARCHITECTURE.md#11-conventions).

### Code

- **Namespaces:** `ArcaneKingdom.{Module}` (z.B. `ArcaneKingdom.Battle`)
- **Kommentare auf Deutsch** (siehe globale Conventions, Umlaute erlaubt im Code)
- **UniTask statt Task<T>**, `_camelCase` fuer private Fields, `PascalCase` fuer Properties/Methoden
- **Nullable Reference Types aktiv** (`#nullable enable`)

### Lokalisierungs-Keys (Pattern)

| Pattern | Beispiel |
|---------|----------|
| `card.<id>.name` / `flavor` | `card.aetherius_allschoepfer.name` |
| `card.<id>.play` / `victory` / `death` | `card.fenrir_urdrachenwolf.play` |
| `world.<id>.name` / `story` / `memory` / `saeule` | `world.elderwald.memory` |
| `fragment.<n>.title` / `content` / `reveal` | `fragment.8.content` (Der Twist) |
| `hero.<rasse>.name` / `flavor` / `skill.name` / `skill.desc` | `hero.ritter.skill.desc` |
| `npc.<id>.name` / `desc` | `npc.lumis.desc` |
| `saeule.<name>` / `.state` | `saeule.lebensbaum.state` |

---

## DI-Pattern (VContainer)

```csharp
public class RootLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<ILogger, UnityLogger>(Lifetime.Singleton);
        builder.Register<IAuthService, FirebaseAuthService>(Lifetime.Singleton);
        builder.Register<ISaveService, FirebaseSaveService>(Lifetime.Singleton);
        builder.Register<INetworkService, PhotonNetworkService>(Lifetime.Singleton);
        builder.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton);

        builder.RegisterInstance(_balancingConfig);
        builder.RegisterInstance(_cardDatabase);
    }
}
```

---

## Save-System (Firebase als Source-of-Truth)

Save-Schema **v3** (mit v4-Erweiterungen):

| Slice | Inhalt |
|-------|--------|
| Basis (v1) | Profile, Currencies, Cards, Decks, World-Progress |
| v2-Erweiterungen | Achievements, Friends, Chat, Saison-Pass v2 |
| **v3-Erweiterungen (Designplan v4)** | Prestige-Slice (Map<worldId, Stufe>), Sternkarten-Inventar, Memory-Fragmente, Hero-Passiv-Wahl, Karten-Persoenlichkeit-Gesehen-Tracking, Event-Punkte |

`SaveMigrator.CurrentSchemaVersion = 3` bei Implementierung.

---

## Bekannte Stolperfallen

| Problem | Loesung |
|---------|---------|
| Scene-YAML-Skelette: Components zeigen "missing script" beim ersten Open | Erwartet. Per Hand `RootLifetimeScope` / `UnityAudioService` an die `[Bootstrapper]` / `[Audio]`-GameObjects ziehen. Anleitung in `Assets/_Project/Scenes/README.md`. |
| `Resources.LoadAll<T>("")` liefert leere Liste im Editor vor Erstimport | Erst `ArcaneKingdom -> Data -> Import All` ausfuehren — danach existieren die SO-Assets unter `Assets/_Project/ScriptableObjects/`. |
| Newtonsoft.Json fehlt im Build | Bereits via `com.unity.nuget.newtonsoft-json` 3.2.1 im `Packages/manifest.json`. Falls Package-Resolve fehlschlaegt: `Window -> Package Manager -> Refresh`. |
| Domain-Tests laufen nicht im EditMode | Tests-asmdef hat `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — in den Test Runner Settings das Define aktivieren. |
| VContainer-Registrierungen werden nicht aufgeloest | Wurde Service im `GameInstaller.RegisterServices` vergessen? Datei pflegen, sobald ein neuer Service hinzukommt. |
| Karten-Skills-IDs in cards.json existieren nicht in abilities.json | Nach v4-Migration: viele Karten-Skills sind noch Platzhalter (`skill_<card_id>_1/2/3`). DataImporter loggt Warnung, setzt Ability auf null. abilities.json muss nachgepflegt werden. |
| 6* Karten ohne `lastWillAbilityId` | Validierungs-Fehler im DataImporter. Jede Mythische Karte braucht einen Letzten Willen (LV 15 freigeschaltet). |
| Goetter-Karten als Drop konfiguriert | Validierungs-Fehler. Goetter sind ausschliesslich ueber Fusion erhaeltlich (siehe `fusion_recipes.json`). |
| Premium-Karte in Fusion verwendet | Crafting-Service muss `CardDefinition.CanBeUsedInFusion` (= `!IsPremiumCard`) pruefen, sonst Verlust einer gekauften Karte! |

---

## Roadmap-Status

**Aktuell (Stand 2026-05-25, Iteration 7 — v4-Designplan eingearbeitet):**

- [x] App-Ordner angelegt + 6 Unity-Scenes (Boot/Hub/Battle/Arena/Guild/GuildWorld)
- [x] **GDD v6.0** (basiert auf Designplan v4, ersetzt v5.4)
- [x] Architektur-Plan + komplette Service-Liste in ARCHITECTURE.md
- [x] Unity-Projekt-Skelett (6 asmdefs + Tests-asmdefs)
- [x] **Domain-Modelle v4-Spec**: Race (5), Element (6 Doppel-Dreieck), Rarity (6), HeroFaehigkeitsTyp (5 Passivs), CardDefinition mit Personality+Last-Will, WorldDefinition mit Story-Mythologie
- [x] **158 Karten in cards.json**: 131 Standard (Ritter 31, Elfen 31, Tiergeister 31, Daemonen 31, Goetter 7) + 9 Event + 6 Premium + 2 Sternkarten + 10 Prestige
- [x] **10 Welten in worlds.json**: Elderwald → Drachenfeste, alle mit Element/Boss/Saeule/Erinnerungs-Fragment/Mentor
- [x] **Story-Daten in story_fragments.json**: Welt-Mythologie, 10 Erinnerungs-Fragmente, 8 Schluessel-NPCs, 6 Saeulen
- [x] **Fusions-Crafting**: CategoryFusionRules (Typ A) + FusionRecipe (Typ B), 10 feste Rezepte (inkl. Goetter)
- [x] **Auto-Battle-Progression**: LV 10/20/30/50 Speed-Stufen, Boss-Erster-Versuch-Manual-Rule
- [x] **Prestige-System**: PrestigeStufe-Enum + Balancing-JSON (I-IV mit Stats/Drops/Income)
- [x] **Sternkarten-System**: Sternkarte-Enum + Login-Belohnungen 30-Tage + Tempel-Eintausch
- [x] **Premium-Shop-Daten**: 6 Karten mit Rotation
- [x] **Event-Kalender**: 5 Saison-Events
- [x] **Lokalisierung**: Hero-Passivs, 10 Welt-Stories, 10 Erinnerungs-Fragmente, 8 NPCs, 6 Saeulen, Mythologie (DE + EN)
- [x] **DataImporter angepasst**: Neue Felder, neue Validierung, ResolveAbility statt Hard-Fail
- [x] **30 Domain-Test-Klassen** (~165 Test-Cases — muessen bei Race/Element/Rarity-Aenderungen ggf. nachgezogen werden)
- [x] CI-Pipeline (GitHub Actions, EditMode-Tests + Android-AAB)
- [x] Editor-Tools (DataImporter + CardPreview + LocalizationCheck + BalancingDashboard)
- [x] **Cloud-Functions-Skelett** (8 TypeScript-Endpoints unter `Server/CloudFunctions/`)
- [x] BattleEngine vollstaendig + BattleStateSerializer (deterministisch, replay-faehig)
- [x] BattleEngine erweitert um Helden-Passivs (KoeniglicheAura/GoettlicherSegen/Waldlaeufer/Rudelbund/LebensraubAura)
- [x] BattleEngine erweitert um Karten-Persoenlichkeit-Events (OnPlay/OnVictory/OnDeath/Synergy/Rivalry/HeroPassivTriggered)
- [x] FusionService implementiert (Kategorie-Fusion + feste Rezepte + Premium-Sperre + Favoriten-Schutz + Letzte-Kopie-Warnung)
- [x] PrestigeService implementiert (I-IV, Sterne-Reset, Stat-Multiplier, Daily-Income, Boss-Phasen-Skalierung)
- [x] SternkartenService implementiert (Inventar, Sternpunkte, Tempel-Eintausch, Mythic-Fragment-Sammlung)
- [x] LoginTracker implementiert (30-Tage-Zyklus, CanClaimToday-Logik)
- [x] 1*/2* Skill-Mechaniken nach Rasse getunt (Ritter=Defense, Elfen=Control, Tiergeister=Synergy/Buff, Daemonen=Damage/Debuff, Goetter=Buff)
- [x] PlayerSave Schema v3: PrestigeSaveSlice + SternkartenSaveSlice + StorySaveSlice + EventSaveSlice + FavoritedCardInstanceIds
- [x] SaveMigrator.MigrateToV3 implementiert
- [x] Domain-Tests: FusionServiceTests + PrestigeServiceTests + SternkartenServiceTests + HeroPassivBattleTests + BattlePersonalityTests
- [x] DI-Wiring: PrestigeService + SternkartenService in GameInstaller (FusionService bleibt Application-Wrapper-Job)
- [ ] MVP: Kampf-UI (Drag&Drop, Mana-Orbs, Damage-Numbers, Personality-Line-Anzeige) — Monat 4-6
- [ ] MVP: Hub-UI (Tabs, Energie-Bar, Navigation) — Monat 4-5
- [ ] MVP: Welt-1-UI (Elderwald-Karte mit 10 Nodes) — Monat 6-8
- [ ] FusionAppService in Game-Assembly (Wrapper der CardCatalog + Rezepte injiziert)
- [ ] PrestigeAppService in Game-Assembly (Wrapper der PlayerSave-Mutation + Karten-Drop orchestriert)
- [ ] LoginRewardController in Game-Assembly (claimt taeglich, ruft SternkartenService.AddSternkarte)
- [ ] Firebase Unity SDK installieren + Auth/RTDB/Analytics verdrahten — Monat 10-14
- [ ] Cloud-Functions deployen (Staging) — Monat 11+
- [ ] Karten-Artworks (Mid-Journey/Stable-Diffusion) + Sound-Assets — laufend

---

## Wichtige Hinweise

- **Dieses Projekt ist NICHT Teil von `MeineApps.Ava.sln`** und wird von `dotnet build` ignoriert.
- **Kein gemeinsamer Code** mit den Avalonia-Apps. Wenn etwas geteilt werden soll (z.B. Telemetrie-Server-Endpunkte), dann ueber **HTTP-API** / Firebase, nicht ueber Code-Sharing.
- **Keystore-Reuse moeglich** (gleicher `meineapps.keystore`), aber Package-ID-Namespace separat.
- **CLAUDE-Agents:** Die fuer Avalonia geschriebenen Agents (`mvvm-auditor`, `skiasharp`, `code-review` mit MVVM-Bias) sind hier **nicht direkt nutzbar**. Stattdessen: Klassische Code-Review-Prinzipien, Unity-spezifische Audits per Hand.

---

## Wenn du Aenderungen machst, denk daran

1. **CLAUDE.md aktualisieren** wenn sich Konventionen oder Architektur aendern
2. **DESIGN.md aktualisieren** wenn sich Game-Design-Entscheidungen aendern (mit Aenderungslog am Ende)
3. **ARCHITECTURE.md aktualisieren** wenn sich Tech-Entscheidungen oder Folder-Layout aendern
4. **Unit-Tests schreiben** fuer alle nicht-trivialen Logik-Aenderungen (Domain-Assembly)
5. **PR-Description verlinkt** auf relevante DESIGN/ARCHITECTURE-Sektion
