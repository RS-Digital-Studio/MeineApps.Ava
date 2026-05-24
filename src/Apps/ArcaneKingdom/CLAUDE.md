# ArcaneKingdom: Mobile TCG + RPG (Arbeitstitel)

Vollstaendig **Unity-basiertes** Sammelkartenspiel — die **einzige App in dieser Codebase, die nicht
Avalonia/.NET** nutzt. Bitte vor jeder Arbeit am Projekt die Tech-Stack-Unterschiede zu allen
anderen Apps bewusst machen.

| Aspekt | Wert |
|--------|------|
| Status | Konzept-Phase abgeschlossen — Pre-MVP (Stand 2026-05-24) |
| Tech | Unity 2022.3 LTS + C# (.NET Standard 2.1) |
| Plattform | Android (Phase 1), iOS (Phase 2 ab Monat 26+) |
| Render-Pipeline | URP |
| Backend | Firebase + Photon |
| Genre | TCG + RPG, Free-to-Play |
| Farbpalette | Royal-Purple #6B46C1 + Gold #F59E0B (Brand-Referenz, finalisiert v5.2) |

> Pflichtlektuere VOR Aenderungen: [DESIGN.md](DESIGN.md), [ARCHITECTURE.md](ARCHITECTURE.md).
> Generische Repo-Konventionen siehe [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Wichtige Dateien

| Datei | Zweck |
|-------|-------|
| [README.md](README.md) | Quickstart fuer Entwickler |
| [DESIGN.md](DESIGN.md) | Konsolidiertes GDD v5.3 (19 Sektionen, TBDs geschlossen) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Folder-Layout, DI, Networking, Conventions |
| [Unity/](Unity/) | Das eigentliche Unity-Projekt |

---

## Tech-Stack Schnellueberblick

| Bereich | Wahl | Anders als die anderen Apps |
|---------|------|----------------------------|
| Engine | Unity 2022.3 LTS | Andere: Avalonia 12 |
| Sprache | C# (.NET Standard 2.1) | Andere: .NET 10 |
| UI | UI Toolkit + UGUI | Andere: AXAML |
| DI | VContainer | Andere: Microsoft.Extensions.DI |
| Async | UniTask | Andere: Task<T> |
| Localization | com.unity.localization | Andere: RESX + ILocalizationService |
| Persistenz | PlayerPrefs + JSON-File + Firebase Realtime DB | Andere: sqlite-net + PreferencesService |
| Networking | Firebase + Photon | Andere: SignalR (BingXBot) oder keines |
| IAP | Unity IAP + Google Play Billing v6 | Andere: Xamarin.Android.Google.BillingClient v8 |
| Build | Unity Cloud Build / GitHub Actions | Andere: dotnet publish |

---

## Build & Run

### Voraussetzungen

1. **Unity Hub** installieren ([unity.com/download](https://unity.com/download))
2. **Unity 2022.3.50f1** via Unity Hub installieren
3. **JDK 17** (mit Unity Android Build Support oft mitgeliefert)
4. **Android SDK / NDK** (ueber Unity Hub installiert)

### Projekt oeffnen

```
Unity Hub → Add project → F:\Meine_Apps_Ava\src\Apps\ArcaneKingdom\Unity\
```

Erstes Oeffnen dauert mehrere Minuten (Packages werden aufgeloest, Assets importiert).

### Im Editor starten

1. Boot-Scene oeffnen: `Assets/_Project/Scenes/Boot/Boot.unity`
2. `Play` druecken

### Android Build (Debug)

Unity Editor:
1. File → Build Settings → Android → Switch Platform
2. Player Settings → Other Settings → Scripting Backend = IL2CPP, Target Architecture = ARM64
3. Build oder Build & Run

### Android Release (AAB fuer Play Store)

```
# Via Unity CLI (Beispiel, exakte Pfade anpassen):
"C:\Program Files\Unity\Hub\Editor\2022.3.50f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "F:\Meine_Apps_Ava\src\Apps\ArcaneKingdom\Unity" `
  -executeMethod BuildScripts.BuildAndroidRelease `
  -logFile build.log
```

`BuildAndroidRelease`-Methode wird in `_Project/Scripts/Editor/BuildScripts.cs` definiert.

---

## Wichtige Konventionen (Auszug)

> Vollstaendig in [ARCHITECTURE.md Kapitel 11](ARCHITECTURE.md#11-conventions).

### Code

- **Namespaces:** `ArcaneKingdom.{Module}` (z.B. `ArcaneKingdom.Battle`)
- **Kommentare auf Deutsch** (siehe globale Conventions)
- **UniTask statt Task<T>**, `_camelCase` fuer private Fields, `PascalCase` fuer Properties/Methoden
- **Nullable Reference Types aktiv** (`#nullable enable`)
- **Keine Unicode-Umlaute in v5.x Doku-Dateien** ist die globale Regel — Code-Kommentare in Deutsch koennen Umlaute nutzen (Visual-Studio-konform)

### Assets

- ScriptableObjects in `Assets/_Project/ScriptableObjects/`
- Karten-Assets als `Card_{Name}.asset`
- Sprites in 4096x4096 Atlases gruppieren (Rarity-basiert)
- Texturen: max. 2048 Karten, BC7-Compression auf Android

### Scenes

- Boot bleibt **immer geladen** (DontDestroyOnLoad), andere Scenes additive
- Scene-Wechsel ueber `SceneLoaderService` (kein direktes `SceneManager.LoadScene`)

---

## DI-Pattern (VContainer)

```csharp
// Boot-Scene RootLifetimeScope
public class RootLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<ILogger, UnityLogger>(Lifetime.Singleton);
        builder.Register<IAuthService, FirebaseAuthService>(Lifetime.Singleton);
        builder.Register<ISaveService, FirebaseSaveService>(Lifetime.Singleton);
        builder.Register<INetworkService, PhotonNetworkService>(Lifetime.Singleton);
        builder.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton);
        builder.Register<IAudioService, UnityAudioService>(Lifetime.Singleton);
        builder.Register<ISceneLoaderService, AdditiveSceneLoaderService>(Lifetime.Singleton);

        // ScriptableObject-Configs
        builder.RegisterInstance(_balancingConfig);
        builder.RegisterInstance(_cardDatabase);
    }
}
```

```csharp
// Service via Constructor Injection
public class BattleController
{
    private readonly IBattleEngine _engine;
    private readonly ISaveService _save;

    public BattleController(IBattleEngine engine, ISaveService save)
    {
        _engine = engine;
        _save = save;
    }
}
```

---

## Save-System (Firebase als Source-of-Truth)

```csharp
public interface ISaveService
{
    UniTask<PlayerSave> LoadAsync(CancellationToken ct = default);
    UniTask SaveAsync(PlayerSave save, CancellationToken ct = default);
    UniTask<Result> ApplyMutationAsync(Func<PlayerSave, PlayerSave> mutation, CancellationToken ct = default);
}
```

- **Optimistic Update:** lokale Mutation sofort, dann Server-Sync
- **Conflict-Resolution:** Server gewinnt, lokales Backup wird verworfen
- **Trigger:** Nach Kampf-Ende, Karten-Drop, Deck-Aenderung, Stunden-Tick

---

## Testing

```
Unity Test Runner (Window → General → Test Runner)
- EditMode-Tests: Domain (NUnit, ohne Unity-API)
- PlayMode-Tests: Game-Logik (mit MonoBehaviours)
```

Domain-Tests sollen ohne Unity laufen — `BattleEngine` etc. sind reines C#.

---

## Bekannte Stolperfallen

| Problem | Loesung |
|---------|---------|
| Scene-YAML-Skelette: Components zeigen "missing script" beim ersten Open | Erwartet. Per Hand `RootLifetimeScope` / `UnityAudioService` an die `[Bootstrapper]` / `[Audio]`-GameObjects ziehen. Anleitung in `Assets/_Project/Scenes/README.md`. |
| `Resources.LoadAll<T>("")` liefert leere Liste im Editor vor Erstimport | Erst `ArcaneKingdom -> Data -> Import All` ausfuehren — danach existieren die SO-Assets unter `Assets/_Project/ScriptableObjects/`. |
| Newtonsoft.Json fehlt im Build | Bereits via `com.unity.nuget.newtonsoft-json` 3.2.1 im `Packages/manifest.json`. Falls Package-Resolve fehlschlaegt: `Window -> Package Manager -> Refresh`. |
| Domain-Tests laufen nicht im EditMode | Tests-asmdef hat `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — in den Test Runner Settings das Define aktivieren. |
| VContainer-Registrierungen werden nicht aufgeloest | Wurde Service im `GameInstaller.RegisterServices` vergessen? Datei pflegen, sobald ein neuer Service hinzukommt. |

---

## Status & Roadmap

Vollstaendige Roadmap siehe [DESIGN.md Kapitel 19](DESIGN.md#19-entwicklungs-zeitplan-24-monate).

**Aktuell (Stand 2026-05-24, Iteration 5):**

- [x] App-Ordner angelegt
- [x] GDD v5.3 (alle 15 v5.1-TBDs geschlossen)
- [x] Architektur-Plan (ARCHITECTURE.md)
- [x] Unity-Projekt-Skelett (6 asmdefs + Tests, Boot/Hub/Battle/Arena/Guild/GuildWorld Scenes)
- [x] Domain (22 Module — Cards/Runes/Player/Battle/World/Economy/Config + Guild/Quest/Achievement/Thief/Chat/Shop + Progression/Hero/Replay/Collection/Tutorial/Notification/Season)
- [x] Game (21 Services/Controller — Hub/Battle/Arena/Login + Guild/Thief/Chat/Shop/Quest/DailyReward + Progression/Hero/Replay/IAP/DeckBuilder/Collection/Tutorial/Notification/SeasonReset/Codex)
- [x] 30 Karten + 32 Faehigkeiten + 18 Runen + 6 Helden + 9 Welten/90 Nodes + 4 Sammelsets + 8 Tutorial-Schritte + 5 Notifications als JSON
- [x] 23 Domain-Test-Klassen (~120 Test-Cases, alle pure C#)
- [x] CI-Pipeline (GitHub Actions, EditMode-Tests + Android-AAB)
- [x] Editor-Tools (DataImporter + CardPreview + LocalizationCheck + BalancingDashboard)
- [ ] MVP: Kampf-UI (Drag&Drop, Mana-Orbs, Damage-Numbers) — Monat 4-6
- [ ] MVP: Hub-UI (Tabs, Energie-Bar, Navigation) — Monat 4-5
- [ ] MVP: Welt-1-UI (Elderwald-Karte mit 10 Nodes) — Monat 6-8
- [ ] Firebase Unity SDK installieren + Auth/RTDB/Analytics verdrahten — Monat 10-14

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
