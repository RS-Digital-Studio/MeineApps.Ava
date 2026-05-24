# ArcaneKingdom: Mobile TCG + RPG (Arbeitstitel)

Vollstaendig **Unity-basiertes** Sammelkartenspiel — die **einzige App in dieser Codebase, die nicht
Avalonia/.NET** nutzt. Bitte vor jeder Arbeit am Projekt die Tech-Stack-Unterschiede zu allen
anderen Apps bewusst machen.

| Aspekt | Wert |
|--------|------|
| Status | Konzept-Phase (Stand 2026-05-24) |
| Tech | Unity 2022.3 LTS + C# (.NET Standard 2.1) |
| Plattform | Android (Phase 1), iOS (Phase 2) |
| Render-Pipeline | URP |
| Backend | Firebase + Photon |
| Genre | TCG + RPG, Free-to-Play |
| Farbpalette | Wird im Konzept-Phase festgelegt [TBD: Vermutlich Royal-Purple #6B46C1 + Gold #F59E0B] |

> Pflichtlektuere VOR Aenderungen: [DESIGN.md](DESIGN.md), [ARCHITECTURE.md](ARCHITECTURE.md).
> Generische Repo-Konventionen siehe [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Wichtige Dateien

| Datei | Zweck |
|-------|-------|
| [README.md](README.md) | Quickstart fuer Entwickler |
| [DESIGN.md](DESIGN.md) | Konsolidiertes GDD v5.1 (19 Sektionen + TBDs) |
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

## Bekannte Stolperfallen (wird mit Implementierung ergaenzt)

| Problem | Loesung |
|---------|---------|
| (noch nichts implementiert) | — |

---

## Status & Roadmap

Vollstaendige Roadmap siehe [DESIGN.md Kapitel 19](DESIGN.md#19-entwicklungs-zeitplan-24-monate).

**Aktuell (Stand 2026-05-24):**

- [x] App-Ordner angelegt
- [x] GDD v5.1 konsolidiert (DESIGN.md)
- [x] Architektur-Plan (ARCHITECTURE.md)
- [x] Unity-Projekt-Skelett
- [x] Initiale C#-Scripts (Models, Enums, ScriptableObjects)
- [ ] Konzept-Phase (TBDs schliessen, Karten-Set v1 entwerfen, BalancingConfig erstellen)
- [ ] MVP: Kampfsystem (Monat 4-6)
- [ ] MVP: Welt 1 (Elderwald, Monat 6-8)
- [ ] Firebase-Integration (Monat 10-14)

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
