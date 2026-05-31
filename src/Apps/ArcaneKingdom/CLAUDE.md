# ArcaneKingdom: Mobile TCG + RPG (Arbeitstitel)

Vollstaendig **Unity-basiertes** Sammelkartenspiel — die **einzige App in dieser Codebase, die nicht
Avalonia/.NET** nutzt. Bitte vor jeder Arbeit am Projekt die Tech-Stack-Unterschiede zu allen
anderen Apps bewusst machen.

| Aspekt | Wert |
|--------|------|
| Status | Pre-MVP (Konzept-Phase abgeschlossen) |
| Tech | Unity 6 (6000.4.8f1) + C# (.NET Standard 2.1) |
| Plattform | Android (Phase 1), iOS (Phase 2 ab Monat 26+) |
| Render-Pipeline | URP 17.x (optional aktivieren, siehe SETUP.md) |
| Backend | Firebase + Photon |
| Genre | TCG + RPG, Free-to-Play |
| Karten-Pool (Launch) | 131 Standard + 27 Oekosystem (Event/Premium/Sternkarten/Prestige) + 4 Sammelset-Belohnungen = 162 Karten |
| Welten | 10 Welten (Elderwald → Drachenfeste) mit Story-Mythologie |
| Farbpalette | Gold #f5c842 auf #0a0a18 (UI-Leitfarbe) · Royal-Purple #6B46C1 (Sekundär-Akzent/Brand) |

> Pflichtlektuere VOR Aenderungen: [DESIGN.md](DESIGN.md) (v6, Quelle der Wahrheit) und [ARCHITECTURE.md](ARCHITECTURE.md).
> Generische Repo-Konventionen siehe [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Designplan-Quellen

Die aktuelle Spec ist die Kombination aus **Spielplan v5 FINAL** (Master, Maerz 2026)
und **Arcane Legends Designplan v1** (Karten-Details). Der Code geht **ueber** die
Spec hinaus (5 Rassen statt 4, 6 Elemente statt 5, 6 Seltenheiten statt 5, 10 Welten
statt 9, plus Helden-Passivs/Sternkarten/Prestige-pro-Welt/Memory-Fragments) — diese
Erweiterungen sind bewusst, der Plan ist ein **Subset** des Codes.

| Quelldokument | Inhalt |
|---------------|--------|
| `Spielplan_v5_FINAL.docx` | Master-Spielplan (Login/Hub/Karten/Crafting/Welten/Kampf/Dieb/Arena/Gilde/Chat/Merit/Events) |
| `Arcane_Legends_Designplan.docx` | Karten-Spec V1 (4 Rassen × 25 Karten, Skills, Crafting, Saison) |
| `Implementierungsplan_Login_HubWelt_1.docx` | Splash/Login/Registration/Auto-Login + Hub-Stadt-UX |
| `files.zip → Implementierungsplan_KOMPLETT.docx` | 19 Sub-Screens vollstaendig spezifiziert |
| `Design_Entwurf_Login_HubWelt.html` | UI-Design-Referenz |
| `karten_vorlage.html` | Karten-Vorlage |

Quelldokumente liegen unter `F:\AI\ComfyUI_windows_portable\ComfyUI\output\eva\Spiele Ideen Ordner\Ideen\`.

---

## Wichtige Dateien

| Datei | Zweck |
|-------|-------|
| [README.md](README.md) | Quickstart fuer Entwickler |
| [SETUP.md](SETUP.md) | Schritt-fuer-Schritt-Anleitung fuer Unity 6 (Pflichtlektuere beim ersten Open) |
| [DESIGN.md](DESIGN.md) | **GDD v6.0** (autoritativ fuer das Soll-Design, basiert auf Designplan v4) |
| [SPIELABLAUF.md](SPIELABLAUF.md) | **Code-verifizierter Ist-Spielablauf** (A-Z) + Implementierungsstand je System (LIVE/DOMAIN-ONLY/SKELETT) + GDD-vs-Code-Diskrepanzen + Verdrahtungs-Luecken zum MVP |
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

### Kampf-Mechanik: Mana vs. COST (Designplan v3 Kap. 7.3 — kritisch)

**COST ist NICHT der Mana-Preis einer Karte.** Diese Verwechslung war ein kampfbrechender Bug
(Epic/Legendaer/Mythisch unspielbar). Korrekte Mechanik:

- **Mana:** Zu Rundenstart **3 Mana-Orbs** (konstant, KEIN Anstieg ueber Runden). **Jede Karte kostet
  genau 1 Mana** (`BattleEngine.ManaPerCard`) — unabhaengig von ihrem COST. Man spielt also ~3 Karten/Runde.
- **COST hat zwei andere Rollen:** (1) **Deck-Bau-Budget** — Summe aller Karten ≤ 200 (`DeckValidator.MaxDeckCost`);
  (2) **Schwere-Karten-Gate** — Karten mit **COST > 30** (`BattleEngine.HeavyCardCostThreshold`) duerfen nur
  eingesetzt werden, wenn in dieser Runde noch nichts anderes gespielt wurde.
- **Waldlaeufer** (Elfen-Passiv): erste Karte jeder Runde kostet 0 Mana.

**Determinismus/Anti-Cheat:** Die `BattleEngine` nutzt `DeterministicRng` (Mulberry32), NICHT `System.Random` —
bit-identisch zur TS-Portierung des Servers (`Server/CloudFunctions/src/battle/engine/`). Seeds muessen
reproduzierbar sein (kein `Environment.TickCount`); `BattleBootstrap.ComputeDeterministicSeed` (FNV-1a) liefert
einen stabilen Fallback. Der serverseitige Replay laeuft im Schatten-Modus, bis er gegen die C#-Engine
cross-getestet ist (`REPLAY_VALIDATION_ENABLED`).

---

## Daten-Files (Resources/Data)

| Datei | Inhalt |
|-------|--------|
| `cards.json` | 162 Karten (131 Standard + 9 Event + 6 Premium + 2 Sternkarten + 10 Prestige + 4 Sammelset-Belohnungen) |
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

## UI/Theme-Konventionen (Arcane Realm)

Design-Sprache: **Dark Fantasy / "Arcane Realm"** — helles Gold auf sehr dunklem, leicht
blau-violettem Grund. Referenz-Designs: `Design_Entwurf_Login_HubWelt.html` und
`karten_vorlage.html` (unter `F:\AI\ComfyUI_windows_portable\…\Spiele Ideen Ordner\Ideen\`).

### Farbpalette

| Token | Wert | Verwendung |
|-------|------|-----------|
| `--ak-gold` | #f5c842 | Leitfarbe: Titel, aktive Borders, CTA-Flächen |
| `--ak-bg-deep` | #0a0a18 | Haupt-Hintergrund |
| `--ak-bg-header` | #060610 | Header, Navigationsleisten |
| `--ak-purple` | #6B46C1 | Sekundär-Akzent (Brand, Primary-Buttons) |

Royal-Purple ist **nie** Hintergrundfarbe für Flächen — nur Akzent und Brand-Signal.

### Theme-Dateien (Ladereihenfolge)

```
Unity/Assets/_Project/UI/Theme/
├── ArcaneTheme.uss          # Root — importiert alle anderen via @import
├── Tokens.uss               # CSS-Custom-Properties (--ak-*)
├── Common.uss               # Reset, Basis-Typografie, Utilities
├── Components.uss           # Alle ak-* Komponenten-Klassen + ak-hub-*
└── V6Components.uss         # Unity-6-spezifische UI-Toolkit-Overrides (separat, kein @import)
```

UXML soll ausschließlich Theme-Klassen und `var(--ak-*)`-Tokens nutzen, **keine hardcodierten
Farben, Schriftgrößen oder Abstände**. Detail-Tokens → `Tokens.uss`.

### Gold-Border-Tokens

| Token | Stärke | Einsatz |
|-------|--------|---------|
| `--ak-border-gold-soft` | schwacher Gold-Rand | Standard-Surfaces, inaktive Elemente |
| `--ak-border-gold` | normaler Gold-Rand | Aktive Panels, Cards |
| `--ak-border-gold-strong` | starker Gold-Rand | CTAs, hervorgehobene Surfaces |

### Klassen-Katalog

| Klasse | Beschreibung |
|--------|-------------|
| `ak-surface` | Dunkle Fläche mit Gold-Rand (soft) |
| `ak-surface-elevated` | Wie `ak-surface`, aber mit stärkerem Rand + leicht erhöhtem Hintergrund |
| `ak-btn` | Basis-Button: transparente Fläche, gold-soft-Rand |
| `ak-btn--primary` | Button: Purple-Fläche + Gold-Rand (Haupt-Aktionen) |
| `ak-btn--accent` | Button: Gold-Fläche + dunkler Text (CTAs, Kauf-Buttons) |
| `ak-btn--ghost` | Button: transparente Fläche + Gold-Rand + Gold-Text |
| `ak-h1`, `ak-h2` | Großer Titel: Gold, gespreiztes Letter-Spacing |
| `ak-h3`, `ak-h4` | Unter-Titel: hell (nicht Gold) |
| `ak-bg-*` | Hintergrund-Token (`ak-bg-deep/base/surface/surface-2/overlay/accent/primary/danger`) |
| `ak-bd-*` / `ak-bd-b-*` | Rahmen-Token, alle Seiten bzw. nur unten (`gold-soft/gold/gold-strong/accent/danger/success`) |
| `ak-text--*` | Text-Farbe (`accent/accent-light/accent-dark/secondary/muted/danger/success/primary-light`) |
| `ak-rad-*` | Eck-Radius (`sm/md/lg/xl`) |
| `ak-hub-*` | Hub-Welt-spezifische Klassen (Gebäude-Overlays, Energie-Leiste, Top-Bar) |

### Theme-Werte NUR über Klassen — niemals inline (Pflicht)

**Unity UI Toolkit löst `var(--token)` ausschließlich in USS-Regeln auf, NICHT in Inline-Styles**
(`style="..."` im UXML). Inline `var()` crasht beim `VisualTreeAsset.Instantiate()`
(`StyleVariableResolver` → `NullReferenceException`), da der Tree zu dem Zeitpunkt detached ist —
ein `PanelSettings.themeStyleSheet`-Import behebt das **nicht** (per MCP verifiziert). Daher:

- **Theme-Farben/-Radien immer über die Utility-/Komponenten-Klassen** (`ak-bg-*`, `ak-bd-*`,
  `ak-text--*`, `ak-rad-*`, `ak-surface`, `ak-btn` …) am `class`-Attribut setzen.
- **Niemals** `style="… : var(--ak-…)"` inline (crasht) und **niemals** den Token-Wert hardcoden
  (`rgb(6,6,16)` etc. — Verschleierung, nicht zentral steuerbar).
- Inline-`style` nur für Layout/Einzelwerte (Größen, Abstände, `border-width`, `flex-*`,
  `-unity-*`) und für bewusste Alpha-Effekte (`rgba(...)`-Tints, `text-shadow`), die keine Tokens sind.
- Verifikation (MCP, panel-unabhängig): alle UXML per `Resources.LoadAll<VisualTreeAsset>("UI")` +
  `vta.Instantiate()` in try/catch instanziieren — crashende Screens haben noch inline `var()`.

### No-Emoji / No-Unicode-Symbol-Doktrin

**Keine Unicode-Symbole als UI-Text** (★ ☆ ← → ✓ ✗ und ähnliche) — Android rendert
viele davon als Tofu-Quadrate. Erlaubte Alternativen:

- **Close-Buttons:** ASCII "X" (kein ✕/×)
- **Stern-Bewertungen** (WorldMap, DifficultyPicker, BattleReport): `star_sprite`-Sprites als
  `VisualElement`, gefüllt = Gold-Tint, leer = reduzierte Opacity — **nie** ★/☆-Glyphen
- **Icons:** Material Icons / eigene SVG-Sprites via UI Toolkit `background-image`

### Hub-Welt-Pattern

Der Hub ist eine **Gebäude-Welt** (Lies-of-Astaroth-Stil), kein Tab-Layout:

- **Hintergrund:** `hub_main.png` als Vollbild-Sprite
- **Top-Bar:** Avatar/Gold-Ring + Level-Badge, Name, Gilden-Tag, LV-/Arena-Badge, Gold-/Diamant-Währungs-Pills
- **Energie-Leiste:** gold-umrandet, unterhalb der Top-Bar
- **Event-Banner:** optional, über dem Gebäude-Grid
- **Gebäude-Grid:** 8 Overlays (Karten-Turm→Codex, Zauberschmiede→Schmiede, Bibliothek→QuestCenter,
  Tempel, Gilden-Hafen→Guild, Marktplatz→Shop, Wand der Ehre→Merit, Postamt→Chat)
- **Right-Nav:** Landkarte / Arena / Runen / Profil
- **Bottom-Nav:** Settings / Shop / Hub (aktiv) / DeckBuilder / Freunde

`HubScreen` ist ein **reiner Navigations-Knoten**: Jedes Gebäude und jeder Nav-Button navigiert
per `ScreenManager` zu einem eigenständigen Screen. Beim Hub-Eintritt laufen Daily-Income-Tick,
PendingClaims-Check und Quest-Restore — die Hub-View selbst enthält keine Spiellogik.

`HubCityRenderer` (programmatischer Gebäude-Renderer) ist durch diesen UXML-basierten Hub
**ungenutzt/Legacy** und wird nicht mehr gepflegt.

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

## Implementierungsstand

### Implementiert

| Bereich | Details |
|---------|---------|
| **Projekt-Skelett** | 6 Unity-Scenes (Boot/Hub/Battle/Arena/Guild/GuildWorld), 6 asmdefs + Tests-asmdefs, GDD v6.0, CI-Pipeline (GitHub Actions, EditMode-Tests + Android-AAB) |
| **Domain-Modelle** | Race (5), Element (6, Doppel-Dreieck), Rarity (6), HeroFaehigkeitsTyp (5 Passivs), CardDefinition mit Personality + Last-Will, WorldDefinition mit Story-Mythologie |
| **Daten** | 162 Karten (cards.json), 10 Welten (worlds.json), Story-Fragmente, Fusions-Rezepte, Prestige-Balancing, Sternkarten-Tempel, Premium-Shop, Event-Kalender, alle weiteren JSON-Dateien (vollstaendige Liste → Abschnitt "Daten-Files") |
| **BattleEngine** | Deterministisch (DeterministicRng/Mulberry32), replay-faehig (BattleStateSerializer), Helden-Passivs, Karten-Persoenlichkeit-Events, Status-Effekte (8 Typen), DoT-Tick, Control/Synergy-Cases |
| **Services (Domain)** | FusionService, PrestigeService, SternkartenService, LoginTracker, DeckValidator, CardUpgradeService, CollectionExchangeService, Thief/Merit/Arena/Territory-Services |
| **Application-Layer** | FusionAppService, PrestigeAppService, LoginRewardController — DI-Wiring in GameInstaller |
| **Save-System** | Schema v3 (PrestigeSaveSlice, SternkartenSaveSlice, StorySaveSlice, EventSaveSlice, FavoritedCardInstanceIds), SaveMigrator.MigrateToV3 |
| **Lokalisierung** | DE + EN fuer Hero-Passivs, 10 Welt-Stories, 10 Erinnerungs-Fragmente, 8 NPCs, 6 Saeulen, Mythologie |
| **Tests** | 30 Domain-Test-Klassen, ~165 Test-Cases (FusionService, PrestigeService, SternkartenService, HeroPassiv-Battle, Karten-Persoenlichkeit) |
| **Editor-Tools** | DataImporter, CardPreview, LocalizationCheck, BalancingDashboard |
| **Cloud-Functions** | Skelett (8 TypeScript-Endpoints unter `Server/CloudFunctions/`) |
| **User-Flow** | BootEntryPoint → Splash → Login/Registration → Hub-Welt. WorldMap → DifficultyPicker → Battle → BattleReport. LoginController.RunLoginAsync (E-Mail-Login) |
| **UI (implementiert)** | SplashScreen, RegistrationScreen, DifficultyPickerModal, RuneScreen, PlayerProfileScreen, ShopScreen, QuestCenterScreen, MeritRankingScreen, BattleReportScreen, ThiefScreen, GuildWorldMapScreen, ChatOverlay, PvpMatchmakingScreen, CollectionTradeScreen, **HubScreen (Gebäude-Welt, Arcane-Realm-Design)** |
| **Theme** | ArcaneTheme.uss (Tokens → Common → Components via @import), V6Components.uss, Gold-Border-Tokens, alle ak-* und ak-hub-* Klassen |

### Noch ausstehend

| Bereich | Details |
|---------|---------|
| Kampf-UI | Drag&Drop, Mana-Orbs, Damage-Numbers, Personality-Line-Anzeige |
| Welt-1-UI | Elderwald-Karte mit 10 Nodes |
| Inventar-Sub-Systeme | Rune-Fragment + EXP-Potion |
| Firebase-Integration | Unity SDK, Auth/RTDB/Analytics verdrahten |
| Cloud-Functions | Staging-Deploy |
| Assets | Karten-Artworks, Sound-Assets |

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
