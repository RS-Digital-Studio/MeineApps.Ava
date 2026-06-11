# BomberBlast 3D — Tech-Architektur

> Vollständige technische Spezifikation. Komplementär zu [PLAN.md](PLAN.md) (Übersicht),
> [DESIGN.md](DESIGN.md) (Game-Design) und [ROADMAP.md](ROADMAP.md) (Produktion).
> Stand 2026-05-26 (Tech-Stack), Richtung v0.5.
>
> **Richtung v0.5 (2026-06-08):** Das Spiel ist ein **modernes, aktiv gespieltes 3D-Bomberman** mit neuer
> Story — **kein Idle/AFK, kein Offline-Income**. Der hier beschriebene Tech-Stack (Unity 6, URP, VContainer,
> UniTask, R3, Firebase, Determinismus-First, Addressables) bleibt davon **unberührt und gültig**. Multiplayer/Netcode/Photon gibt es **nicht**
> (§8 = expliziter Ausschluss). Determinismus-First gilt weiter für
> Replay / Daily-Race / Anti-Cheat (es gibt keinen Idle-Loop, der zu modellieren wäre).

---

## Inhaltsverzeichnis

1. [Tech-Stack (Versionen + Begründungen)](#1-tech-stack-versionen--begründungen)
2. [Folder-Layout](#2-folder-layout)
3. [Assembly Definitions (asmdef)](#3-assembly-definitions-asmdef)
4. [Dependency Injection mit VContainer](#4-dependency-injection-mit-vcontainer)
5. [Scene-Architektur](#5-scene-architektur)
6. [Daten-Architektur (ScriptableObjects + Save)](#6-daten-architektur-scriptableobjects--save)
7. [Cloud-Save](#7-cloud-save)
8. [Multiplayer/Netcode — nicht Teil von v0.5](#8-multiplayer--netcode--nicht-teil-von-v05)
9. [(entfällt — siehe §8)](#9-entfällt--multiplayer-siehe-8)
10. [Anti-Cheat-Pipeline](#10-anti-cheat-pipeline)
11. [Cloud Functions (TypeScript)](#11-cloud-functions-typescript)
12. [Firebase-Security-Rules](#12-firebase-security-rules)
13. [Determinismus-First-Design](#13-determinismus-first-design)
14. [Performance-Targets + Hardware-Profile](#14-performance-targets--hardware-profile)
15. [URP-Rendering-Setup](#15-urp-rendering-setup)
16. [Asset-Pipeline (Addressables)](#16-asset-pipeline-addressables)
17. [Audio-Architektur](#17-audio-architektur)
18. [Test-Strategie](#18-test-strategie)
19. [Build & DevOps](#19-build--devops)
20. [Versionierung + Hotfix-Strategie](#20-versionierung--hotfix-strategie)
21. [Logging + Telemetrie](#21-logging--telemetrie)
22. [Domain-Code-Port aus altem BomberBlast](#22-domain-code-port-aus-altem-bomberblast)
23. [Code-Conventions](#23-code-conventions)

---

## 1. Tech-Stack (Versionen + Begründungen)

### 1.1 Engine & Core

| Bereich | Wahl | Version | Begründung |
|---------|------|---------|------------|
| Engine | **Unity 6** | 6000.4.x LTS | Aktueller Stand, URP 17 default, identisch ArcaneKingdom (Skill-Reuse) |
| Sprache | C# | .NET Standard 2.1 | Standard für Unity, IL2CPP-kompatibel |
| Render-Pipeline | **URP** | 17.0.4+ | 2D/3D-Mix, Shader Graph, gute Mobile-Performance |
| Build-Backend | **IL2CPP** | inkl. Unity | ARM64-Pflicht für Play-Store seit 2019 |
| Min-Android | API 24 (Android 7) | – | 98 % Marktabdeckung, NEON-SIMD verfügbar |

### 1.2 DI + Async + Reactive

| Bereich | Wahl | Version | Begründung |
|---------|------|---------|------------|
| **DI** | **VContainer** | 1.16.9+ | AOT-kompatibel, schnell, identisch ArcaneKingdom |
| **Async** | **UniTask** | 2.5.10+ | Allokationsarm, frame-deterministisch |
| **Reactive** | **R3** (preferred) oder **UniRx** | R3 1.x oder UniRx 7.1+ | R3 ist Nachfolger, kompatibel mit UniTask |

### 1.3 Input & UI

| Bereich | Wahl | Version | Begründung |
|---------|------|---------|------------|
| **Input** | **New Input System** | 1.19+ | Action-basiert, Multi-Touch + Gamepad + Keyboard in einer Abstraktion |
| **UI (statisch)** | **UI Toolkit** | (Unity-built-in) | Deklarativ, USS-stylebar, schneller iterierbar |
| **UI (Animation)** | **UGUI** | 2.0+ | DOTween + RectTransform-Animation flexibler |
| **TMP** | **TextMeshPro** | (in com.unity.ugui 2.0.0 integriert) | Standard, SDF-Font-Rendering — in Unity 6 Teil von UGUI, kein separates Paket |
| **Animation** | **DOTween** | Pro v1.2.7+ | Tweens für UI + Camera + Custom-Animations |
| **Cinemachine** | **Cinemachine** | 3.x | Procedural Camera, Damping, CinemachineConfiner2D/3D, CinemachineImpulseSource/-Listener |
| **Timeline** | **Unity Timeline** | 1.8+ | Sektor-Cutscenes, Cinematic-Sequenzen |
| **VFX** | **VFX Graph** | 17.0+ | GPU-Compute-Shader-Particles |
| **Particle System** | **Built-in** | Unity 6 | Backup für simple Effekte (Trail, Pickup) |
| **Shader Graph** | **URP-Built-in** | – | Custom Shaders (Glow, Dissolve, Hologramm, Outline, Liquid) |

### 1.4 Backend (asynchron, kein Echtzeit-MP)

**Kein Echtzeit-Netcode — reiner Single-Player.** Nur asynchrone Firebase-Dienste:

| Bereich | Wahl | Begründung |
|---------|------|------------|
| **Async-Backend** | Firebase Realtime DB | Cloud-Save + Grid-Rankings + Daily-Race (Score-Submit) |
| **Auth** | Firebase Auth | Anonymous (+ optional Google-Link für Geräte-Wechsel) |
| **Notifications** | Firebase Cloud Messaging | Push + Local (Daily-/Event-Reminder) |
| **Remote Config** | Firebase Remote Config | Live-Tuning, Event-Toggles |
| **Cloud Storage** | Firebase Storage | optional: Addressables-CDN |

> **Entfernt (alte Logik):** Photon Fusion/Realtime/Chat/Voice, Real-time PvP/Co-op, Match-Cloud-Functions.
> Analytics/Crashlytics optional/später — kein Launch-Blocker.

### 1.5 Persistenz

| Bereich | Wahl | Begründung |
|---------|------|------------|
| **Settings** | PlayerPrefs (Unity-built-in) | Plattform-native, klein, schnell |
| **Game-Save** | Firebase RTDB (Source-of-Truth) + JSON-File (Last-Known-Good) | Cloud-Save (Android-fokussiert) |
| **Replay-Files** | Lokal JSON (Daily-Race-Replay) | 5-30 KB pro Run |
| **Lokal-Cache** | Application.persistentDataPath/cache/ | Custom JSON-Files |

### 1.6 Monetization

| Bereich | Wahl | Version |
|---------|------|---------|
| **IAP** | **Unity IAP** | 4.13+ |
| **Google Play Billing** | (via Unity IAP) | v6 |
| **Ads** | **Google AdMob** | v9+ |
| **Ads-Mediation** | **Unity LevelPlay** | (Backup) |
| **Subscription** | Unity IAP + Server-Worker | Anti-Refund-Validation |

### 1.7 Tooling

| Bereich | Wahl |
|---------|------|
| **CI/CD** | GitHub Actions + game-ci/unity-builder (Linux) |
| **Build-Cache** | GitHub-Cache für Library/ |
| **Asset-CDN** | Firebase Storage (Addressables Remote Catalog) |
| **Voice-Synthesis** | **ElevenLabs Enterprise** + Backup-Plan Mensch-Sprecher für Schlüssel-Lines |
| **Audio-Engine** (Optional) | **FMOD Studio** für adaptive Music (Lizenz 5k/Jahr Indie) |
| **3D-Tools** | **Blender 4.x** (Standard), Maya/3ds Max (optional für komplexe Skins) |
| **Texturen** | **Substance Painter** + Photoshop |
| **Mockups** | **Figma** (UI), **Penpot** (Open-Source-Alternative) |
| **PM** | **Linear** oder **GitHub Projects** |
| **Communication** | **Slack** + **Discord** (Community-Channel) |
| **Code-Review** | GitHub PRs + ggf. Claude-Code-Review-Agent |

### 1.8 Dependencies via Unity Package Manager

`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.cysharp.unitask": "2.5.10",
    "com.cysharp.r3": "1.x",
    "com.unity.addressables": "2.9.1",
    "com.unity.cinemachine": "3.x",
    "com.unity.collab-proxy": "2.12.4",
    "com.unity.ide.rider": "3.0.40",
    "com.unity.ide.visualstudio": "2.0.23",
    "com.unity.inputsystem": "1.19.0",
    "com.unity.localization": "1.5.11",
    "com.unity.mobile.notifications": "2.4.3",
    "com.unity.nuget.newtonsoft-json": "3.2.2",
    "com.unity.render-pipelines.universal": "17.0.4",
    "com.unity.test-framework": "1.5.1",
    "com.unity.timeline": "1.8.12",
    "com.unity.ugui": "2.0.0",
    "com.unity.visualeffectgraph": "17.0.x",
    "com.unity.purchasing": "4.13.x",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "jp.hadashikick.vcontainer": "1.16.9"
  },
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.cysharp.unitask", "com.cysharp.r3", "jp.hadashikick.vcontainer"]
    }
  ]
}
```

**Manuelle Installation (Asset Store / DLL):**
- DOTween Pro
- Firebase Unity SDK (Auth, RTDB, Messaging, Storage, Remote Config)
- FMOD Studio Unity Integration (optional)

---

## 2. Folder-Layout

```
src/Apps/BomberBlast.Unity/
├── PLAN.md
├── DESIGN.md
├── ARCHITECTURE.md
├── ROADMAP.md
├── CLAUDE.md
├── SETUP.md                   (folgt nach Projekt-Anlage)
├── Server/
│   ├── SERVEROPS.md
│   ├── CloudFunctions/        (TypeScript)
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── src/
│   │   │   ├── index.ts
│   │   │   ├── matchValidation.ts
│   │   │   ├── seasonRewards.ts
│   │   │   ├── leagueRank.ts
│   │   │   ├── reportPlayer.ts
│   │   │   ├── accountDelete.ts
│   │   │   ├── dataExport.ts
│   │   │   ├── purchaseValidate.ts
│   │   │   ├── notificationSend.ts
│   │   │   └── migrateOldBomberBlast.ts
│   │   └── tests/
│   ├── DomainReplay/          (C# .NET 10 Worker für Replay-Re-Sim)
│   │   ├── BomberBlast.ReplayWorker.csproj
│   │   ├── Program.cs
│   │   └── tests/
│   └── firebase.rules.json
└── Unity/
    ├── Assets/
    │   ├── _Project/
    │   │   ├── Scripts/        (siehe Asmdef-Sektion)
    │   │   ├── ScriptableObjects/
    │   │   ├── Scenes/
    │   │   ├── Prefabs/
    │   │   ├── Art/
    │   │   ├── Audio/
    │   │   ├── Addressables/
    │   │   └── Resources/
    │   ├── ThirdParty/
    │   └── StreamingAssets/
    ├── Packages/manifest.json
    └── ProjectSettings/
```

### 2.1 Scripts-Folder-Struktur

```
Assets/_Project/Scripts/
├── Bootstrap/               (BomberBlast.Bootstrap)
│   ├── RootLifetimeScope.cs
│   ├── AppInstaller.cs
│   ├── BootController.cs
│   └── SafeMode.cs
│
├── Core/                    (BomberBlast.Core)
│   ├── Logging/
│   │   ├── ILogger.cs
│   │   ├── UnityLogger.cs
│   │   └── FileLogger.cs
│   ├── Result/
│   │   ├── Result.cs
│   │   └── ResultExtensions.cs
│   ├── Random/
│   │   ├── IRngProvider.cs
│   │   ├── DeterministicRngProvider.cs
│   │   └── SystemRngProvider.cs
│   ├── GameClock/
│   │   ├── IGameClock.cs
│   │   └── UnityGameClock.cs
│   ├── FixedTimestep/
│   │   └── FixedTimestepRunner.cs
│   ├── ReplayCapture/
│   │   ├── ReplayCapture.cs
│   │   └── ReplayEntry.cs
│   └── Extensions/
│
├── Domain/                  (BomberBlast.Domain — keine Unity-API)
│   ├── Grid/
│   │   ├── GameGrid.cs
│   │   ├── CellType.cs
│   │   └── GridUtils.cs
│   ├── Bombs/
│   │   ├── BombDefinition.cs (ScriptableObject)
│   │   ├── BombInstance.cs (runtime)
│   │   ├── CardCatalog.cs   (13 Karten + Standard = 14 BombTypes)
│   │   ├── BombEffects/   (pro BombType: Frost/Lava/Sticky/Smoke/Lightning/Gravity/Poison/TimeWarp/Mirror/Vortex/Phantom/Nova/BlackHole)
│   │   └── BombFactory.cs
│   ├── PowerUps/
│   │   ├── PowerUpDefinition.cs (ScriptableObject)
│   │   ├── PowerUpEffects/  (12 PowerUps + Cure)
│   │   └── PowerUpFactory.cs
│   ├── Heroes/
│   │   ├── HeroDefinition.cs (ScriptableObject — 5 Helden)
│   │   ├── HeroTrait.cs      (None/DoubleDetonation/LuckyDrops/DemolitionExpert/QuickPocket)
│   │   └── HeroState.cs
│   ├── Enemies/
│   │   ├── EnemyDefinition.cs (ScriptableObject)
│   │   ├── EnemyType.cs
│   │   ├── EnemyStats.cs
│   │   └── AI/
│   │       ├── AStar.cs
│   │       ├── BFSSafeCellFinder.cs
│   │       ├── DangerZone.cs
│   │       └── EnemyAI.cs
│   ├── Bosses/
│   │   ├── BossDefinition.cs (ScriptableObject)
│   │   ├── BossPhase.cs
│   │   └── BossModifier.cs
│   ├── Worlds/
│   │   ├── WorldDefinition.cs (ScriptableObject)
│   │   ├── LevelDefinition.cs
│   │   └── LevelLayoutGenerator.cs (port aus alt)
│   ├── Modes/
│   │   ├── IGameMode.cs
│   │   ├── GameModeBase.cs
│   │   ├── StoryMode.cs
│   │   ├── MasterMode.cs
│   │   ├── DungeonMode.cs
│   │   ├── DailyChallengeMode.cs
│   │   ├── QuickPlayMode.cs
│   │   ├── SurvivalMode.cs
│   │   ├── BossRushMode.cs
│   │   └── DailyRaceMode.cs
│   ├── Combat/
│   │   ├── ComboSystem.cs (port aus alt)
│   │   ├── SpecialExplosionEffects.cs (port aus alt)
│   │   ├── EnemyPositionIndex.cs (port aus alt)
│   │   └── DamageEvent.cs
│   ├── Dungeon/
│   │   ├── DungeonRun.cs
│   │   ├── DungeonSynergyResolver.cs (port aus alt)
│   │   ├── Buffs/
│   │   ├── NodeMap.cs
│   │   ├── RoomType.cs
│   │   └── FloorModifier.cs
│   ├── Economy/
│   │   ├── Coin.cs
│   │   ├── Gem.cs
│   │   ├── DungeonCoin.cs
│   │   ├── OverflowGuard.cs
│   │   └── Shop/
│   │       ├── UpgradeType.cs        (12 permanente Upgrades: 9 Stat + 3 Bomb-Unlocks — Haupt-Coin-Sink)
│   │       ├── PlayerUpgrades.cs
│   │       └── ShopUpgrade.cs
│   ├── League/
│   │   ├── LeagueTier.cs
│   │   ├── SubTier.cs
│   │   ├── LeaguePoints.cs           // Async-Score (kein MMR)
│   │   └── PercentilePromotion.cs    // Top 30% auf / Bottom 20% ab (Saisonende)
│   ├── BattlePass/
│   │   ├── BattlePassData.cs
│   │   ├── BattlePassRewardDefinition.cs
│   │   └── XpToTier.cs
│   ├── Achievements/
│   │   ├── AchievementDefinition.cs (ScriptableObject)
│   │   ├── Achievement.cs
│   │   └── AchievementTrigger.cs
│   ├── Cards/
│   │   ├── OwnedCard.cs              (CardId + Level + Count)
│   │   ├── Deck.cs                   (4 Basis-Slots + 1 freischaltbar)
│   │   └── CardCrafter.cs            (Coin-Sink: 5+2000C->Rare, 5+8000C->Epic, 5+25000C->Legendary)
│   └── Chat/
│       ├── ProfanityFilter.cs (port aus alt)
│       └── ChatMessage.cs
│
├── Game/                    (BomberBlast.Game — mit Unity-API)
│   ├── Bootstrap/
│   │   ├── GameLifetimeScope.cs
│   │   └── GameController.cs
│   ├── Grid/
│   │   ├── GridView.cs (MonoBehaviour, renders Grid)
│   │   └── BlockPrefab.cs
│   ├── Player/
│   │   ├── Player.cs (MonoBehaviour)
│   │   ├── PlayerController.cs (Input)
│   │   ├── PlayerAnimator.cs
│   │   └── PlayerVFX.cs
│   ├── Bombs/
│   │   ├── BombView.cs (MonoBehaviour)
│   │   ├── BombController.cs
│   │   └── BombVFX.cs
│   ├── Enemies/
│   │   ├── EnemyView.cs
│   │   ├── EnemyAnimator.cs
│   │   └── EnemySpawner.cs
│   ├── Bosses/
│   │   ├── BossView.cs
│   │   ├── BossController.cs
│   │   └── BossAttackPattern.cs
│   ├── Heroes/
│   │   ├── HeroController.cs
│   │   ├── HeroSkillExecutor.cs
│   │   └── HeroAbilityVFX.cs
│   ├── Cards/
│   │   ├── CardSlotController.cs
│   │   └── CardSlotVFX.cs
│   ├── Battle/
│   │   ├── BattleController.cs (orchestriert Match)
│   │   ├── BattleState.cs
│   │   ├── BattleReplay.cs
│   │   └── BattleHUD.cs
│   ├── Camera/
│   │   ├── BattleCamera.cs (Cinemachine-Wrapper)
│   │   └── CameraShake.cs
│   ├── PowerUps/
│   │   └── PowerUpPickup.cs
│   ├── World/
│   │   ├── WorldRenderer.cs
│   │   ├── WorldTheme.cs
│   │   └── AmbientEffects.cs
│   └── FX/
│       ├── ExplosionVFX.cs
│       ├── ComboFloatingText.cs
│       └── DamageNumber.cs
│
├── UI/                      (BomberBlast.UI)
│   ├── Bootstrap/
│   │   └── UIInstaller.cs
│   ├── Modals/
│   │   ├── ModalService.cs
│   │   ├── ConfirmModal.cs
│   │   ├── AlertModal.cs
│   │   └── BaseModal.cs
│   ├── Hub/
│   │   ├── MainHubView.cs
│   │   ├── PlayTabView.cs
│   │   ├── ShopTabView.cs
│   │   └── ProfileTabView.cs
│   ├── HUD/
│   │   ├── BattleHUDView.cs
│   │   ├── JoystickUI.cs
│   │   ├── BombButton.cs
│   │   ├── HeroSkillBar.cs
│   │   ├── ComboDisplay.cs
│   │   └── MiniMap.cs
│   ├── Settlement/
│   │   ├── PostMatchView.cs
│   │   ├── RewardReveal.cs
│   │   └── BPProgressBar.cs
│   ├── Settings/
│   │   ├── SettingsView.cs
│   │   ├── AudioPanel.cs
│   │   ├── GraphicsPanel.cs
│   │   └── AccessibilityPanel.cs
│   ├── Cinematic/
│   │   ├── CinematicView.cs
│   │   └── SubtitleSystem.cs
│   └── ViewModels/
│       └── (alle MVVM-VMs hier)
│
└── LiveOps/                 (BomberBlast.LiveOps)
    ├── BattlePass/
    │   ├── BattlePassService.cs
    │   └── SaisonManager.cs
    ├── Quests/
    │   ├── DailyQuestService.cs
    │   ├── WeeklyMissionService.cs
    │   └── QuestProgress.cs
    ├── Events/
    │   ├── EventCalendar.cs (port aus alt, ISO-Wochen)
    │   ├── LiveEvent.cs
    │   └── EventTrigger.cs
    ├── Achievements/
    │   └── AchievementService.cs
    ├── RemoteConfig/
    │   ├── RemoteConfigService.cs
    │   └── ConfigKeys.cs
    ├── Cosmetics/
    │   ├── CosmeticInventory.cs
    │   ├── CosmeticUnlock.cs
    │   └── CosmeticShop.cs
    ├── Onboarding/
    │   ├── TutorialController.cs
    │   ├── TutorialStep.cs
    │   └── FeatureUnlockChoreographer.cs
    ├── Retention/
    │   ├── RetentionService.cs (port aus alt)
    │   └── DailyRewardService.cs
    └── Analytics/
        ├── AnalyticsService.cs
        ├── FunnelEvent.cs
        └── ConsentManager.cs
```

---

## 3. Assembly Definitions (asmdef)

```
BomberBlast.Core
   ├── Dependencies: Newtonsoft.Json, UniTask, R3, ZLogger (optional)
   └── References: kein
   
BomberBlast.Domain (depends on Core)
   ├── References: BomberBlast.Core
   └── Constraint: NUR Unity-API-frei (testbar ohne Unity)

BomberBlast.Game (depends on Domain, Core)
   ├── References: BomberBlast.Core, BomberBlast.Domain
   ├── Unity-API allowed
   └── MonoBehaviours, Coroutines

BomberBlast.UI (depends on Game, Domain, Core)
   ├── References: BomberBlast.Core, BomberBlast.Domain, BomberBlast.Game
   └── UI Toolkit + UGUI + DOTween

BomberBlast.LiveOps (depends on Game, Domain, Core)
   ├── References: BomberBlast.Core, BomberBlast.Domain, BomberBlast.Game
   └── Firebase Remote Config + Analytics

BomberBlast.Bootstrap (depends on all)
   ├── References: alle anderen
   └── VContainer-Composition-Root

Test-Assemblies:
- BomberBlast.Core.Tests             (NUnit, EditMode)
- BomberBlast.Domain.Tests           (NUnit, EditMode, KEINE Unity-API)
- BomberBlast.Game.PlayModeTests     (Unity Test Framework, PlayMode)
- BomberBlast.LiveOps.Tests          (NUnit, EditMode)
```

### 3.1 Asmdef-Constraints

- **Domain darf NICHT Unity-API** verwenden (Compile-Constraint via Reflection-Check + CI-Gate)
- **Test-Assemblies haben** `defineConstraints: ["UNITY_INCLUDE_TESTS"]`

---

## 4. Dependency Injection mit VContainer

### 4.1 LifetimeScope-Hierarchie

```
RootLifetimeScope (Boot-Scene, DontDestroyOnLoad)
│
├─ Core
│  ├─ ILogger → UnityLogger (Singleton)
│  ├─ ILoggerFactory → ZLoggerFactory mit File-Sink (Singleton)
│  ├─ IRngProvider → DeterministicRngProvider (Singleton)
│  ├─ IGameClock → UnityGameClock (Singleton)
│  ├─ FixedTimestepRunner (Singleton)
│  └─ ReplayCapture (Scoped pro Match)
│
├─ Cross-Cutting Services (alle Singleton)
│  ├─ IAuthService → FirebaseAuthService
│  ├─ ISaveService → FirebaseSaveService
│  ├─ IAnalyticsService → FirebaseAnalyticsService
│  ├─ IRemoteConfigService → FirebaseRemoteConfigService
│  ├─ IAudioService → UnityAudioService (oder FMODAudioService)
│  ├─ INotificationService → MobileNotificationService
│  ├─ IIapService → UnityIapService
│  ├─ ISceneLoaderService → AdditiveSceneLoaderService
│  └─ ILocalizationService → UnityLocalizationService
│
├─ Domain-Services (Singleton)
│  ├─ IHeroService → HeroService
│  ├─ ICardService → CardService
│  ├─ IDeckService → DeckService
│  ├─ IPowerUpService → PowerUpService
│  └─ IBombService → BombService
│
├─ Meta-Services (Singleton)
│  ├─ IProgressService → ProgressService
│  ├─ IEconomyService → EconomyService
│  ├─ IShopService → ShopService
│  ├─ IBattlePassService → BattlePassService
│  ├─ IQuestService → QuestService
│  ├─ IAchievementService → AchievementService
│  ├─ ICosmeticService → CosmeticService
│  ├─ IDailyRewardService → DailyRewardService
│  ├─ IRetentionService → RetentionService
│  └─ IEventCalendarService → EventCalendarService
│
├─ Async-Services (Singleton, kein Multiplayer)
│  ├─ ILeagueService → LeagueService (Grid-Rankings)
│  └─ IReplayService → ReplayService (Daily-Race)
│
└─ Konfiguration (ScriptableObject-Instances)
   ├─ HeroDatabase
   ├─ BombDatabase
   ├─ PowerUpDatabase
   ├─ EnemyDatabase
   ├─ BossDatabase
   ├─ WorldDatabase
   ├─ CardCatalog            (13 Karten)
   ├─ DungeonBuffDatabase    (16 Buffs)
   ├─ AchievementDatabase    (72 Achievements)
   ├─ ShopUpgradeConfig      (12 Upgrades: 9 Stat + 3 Bomb-Unlocks)
   ├─ BalancingConfig
   └─ EconomyConfig
```

### 4.2 Sub-Scopes pro Scene

```
GameLifetimeScope (Game-Scene)
├─ GameController (Scoped)
├─ BattleController (Scoped)
├─ BattleState (Scoped)
├─ GridView (Scoped)
├─ GameHUD (Scoped)
└─ Mode-spezifisches Service (Scoped: StoryMode oder DungeonMode oder...)

```

### 4.3 RootLifetimeScope.cs (vollständig)

```csharp
namespace BomberBlast.Bootstrap;

using VContainer;
using VContainer.Unity;
using BomberBlast.Core;
using BomberBlast.Domain;
using BomberBlast.Services;

public class RootLifetimeScope : LifetimeScope
{
    [SerializeField] private HeroDatabase _heroDatabase;
    [SerializeField] private BombDatabase _bombDatabase;
    [SerializeField] private WorldDatabase _worldDatabase;
    [SerializeField] private BalancingConfig _balancingConfig;
    // ... weitere ScriptableObjects

    protected override void Configure(IContainerBuilder builder)
    {
        // ── Core ──
        builder.Register<ILogger, UnityLogger>(Lifetime.Singleton);
        builder.Register<IRngProvider, DeterministicRngProvider>(Lifetime.Singleton);
        builder.Register<IGameClock, UnityGameClock>(Lifetime.Singleton);
        builder.Register<FixedTimestepRunner>(Lifetime.Singleton);

        // ── Services ──
        builder.Register<IAuthService, FirebaseAuthService>(Lifetime.Singleton);
        builder.Register<ISaveService, FirebaseSaveService>(Lifetime.Singleton);
        builder.Register<IAudioService, UnityAudioService>(Lifetime.Singleton);
        builder.Register<ISceneLoaderService, AdditiveSceneLoaderService>(Lifetime.Singleton);
        builder.Register<ILocalizationService, UnityLocalizationService>(Lifetime.Singleton);
        builder.Register<IAnalyticsService, FirebaseAnalyticsService>(Lifetime.Singleton);
        builder.Register<IRemoteConfigService, FirebaseRemoteConfigService>(Lifetime.Singleton);
        builder.Register<INotificationService, MobileNotificationService>(Lifetime.Singleton);
        builder.Register<IIapService, UnityIapService>(Lifetime.Singleton);

        // ── Domain-Services ──
        builder.Register<IHeroService, HeroService>(Lifetime.Singleton);
        builder.Register<ICardService, CardService>(Lifetime.Singleton);
        builder.Register<IDeckService, DeckService>(Lifetime.Singleton);

        // ── Meta-Services ──
        builder.Register<IProgressService, ProgressService>(Lifetime.Singleton);
        builder.Register<IEconomyService, EconomyService>(Lifetime.Singleton);
        builder.Register<IShopService, ShopService>(Lifetime.Singleton);
        builder.Register<IBattlePassService, BattlePassService>(Lifetime.Singleton);
        builder.Register<IQuestService, QuestService>(Lifetime.Singleton);
        builder.Register<IAchievementService, AchievementService>(Lifetime.Singleton);
        builder.Register<ICosmeticService, CosmeticService>(Lifetime.Singleton);
        builder.Register<IRetentionService, RetentionService>(Lifetime.Singleton);

        // ── Async-Services (kein Multiplayer) ──
        builder.Register<ILeagueService, LeagueService>(Lifetime.Singleton);   // Grid-Rankings (async)
        builder.Register<IReplayService, ReplayService>(Lifetime.Singleton);   // Daily-Race-Replay

        // ── ScriptableObject-Instances ──
        builder.RegisterInstance(_heroDatabase);
        builder.RegisterInstance(_bombDatabase);
        builder.RegisterInstance(_worldDatabase);
        builder.RegisterInstance(_balancingConfig);

        // ── EntryPoints (VContainer's IInitializable / ITickable) ──
        builder.RegisterEntryPoint<BootController>();
    }
}
```

---

## 5. Scene-Architektur

### 5.1 Scene-Lifecycle

```
Boot.unity (Dauer-Scene, DontDestroyOnLoad)
   ├── RootLifetimeScope
   ├── SplashScreen
   ├── AnonymousAuth (mit Migrate-Old-Account-Flow)
   └── SceneLoaderService → MainMenu.unity additive
        │
        ▼
MainMenu.unity (additive, immer aktiv neben Game)
   ├── MainMenuLifetimeScope
   ├── 3D-Skybox + Animated-Hub-Background
   ├── Tab-Navigation (Home/Play/Shop/Rankings/Profile)
   ├── Daily-Reward-Modal
   ├── Battle-Pass-Modal
   ├── WhatsNew-Modal
   └── Push-Modal-Queue (Feature-Unlocks)
        │ Tap "Play → Story" → Game.unity
        ▼
Game.unity (additive)
   ├── XxxLifetimeScope
   ├── BattleController (Match-Logic)
   ├── HUD
   └── Pause-Overlay
        │ Match-Ende
        ▼
Settlement-Modal (in MainMenu, kein Scene-Wechsel)
   - Reward-Reveal mit DOTween-Animation
   - BP-XP-Bar
   - Quest-Progress-Updates
```

### 5.2 Scene-Übersicht

| Scene | Zweck | Lifetime | Lade-Strategie |
|-------|-------|----------|----------------|
| Boot | Bootstrap, DI, Splash, Auth | Dauer (DontDestroyOnLoad) | Auto-Start |
| MainMenu | Hub mit Tabs | Dauer nach Boot | Auto nach Auth |
| Game | Single-Player-Match | Pro Match | Additive von MainMenu |
| Cinematic | Sektor-Cutscenes | Pro Cutscene | Additive |
| Tutorial | Initial-Tutorial Onboarding | Einmal | Additive nach erstem Boot |

### 5.3 Scene-Transitions

- 200 ms Fade-Out → Scene-Load → 200 ms Fade-In
- Loading-Tipps während Load (33 globale + 10 welt-spezifische — alt portiert)
- Persistenter Loading-Canvas in Boot-Scene

---

## 6. Daten-Architektur (ScriptableObjects + Save)

### 6.1 ScriptableObject-Datenbanken

```csharp
// Beispiel: HeroDatabase
[CreateAssetMenu(fileName = "HeroDatabase", menuName = "BomberBlast/HeroDatabase")]
public class HeroDatabase : ScriptableObject
{
    public List<HeroDefinition> Heroes = new();
    
    public HeroDefinition GetById(string id) => Heroes.Find(h => h.Id == id);
}

// 1:1 aus dem Original (HeroDefinition.cs) — 5 Helden, NUR Stat-Variation + Trait + Skin-Farben.
// KEINE Skills/Ultimates/Talent-Baeume (das war die verworfene Sci-Fi-Reinvention).
[CreateAssetMenu(fileName = "Hero_Default", menuName = "BomberBlast/Hero")]
public class HeroDefinition : ScriptableObject
{
    public string Id;                       // "hero_default", "hero_speedy_sam", ...
    public string NameKey;                  // Localization-Key (z.B. "HeroSpeedySamName")
    public string DescriptionKey;
    public int StartMaxBombs = 1;
    public int StartFireRange = 2;
    public int StartSpeedLevel = 0;         // 0-3, BASE_SPEED 100 + Level*20
    public int StartLives = 3;
    public float CoinPickupMultiplier = 1f;
    public float PowerUpDropMultiplier = 1f;
    public float BlockDropChanceBonus = 0f;
    public HeroTrait Trait;                 // None / DoubleDetonation / LuckyDrops / DemolitionExpert / QuickPocket
    public string UnlockCondition;          // "default" / "ach_speed_demon" / "gems_500" ...
    public Color BodyColor;                 // Skin-Hauptfarbe
    public Color AccentColor;
    public GameObject HeroModelPrefab;      // 3D-Modell (im Neon-Arcade-Stil)
}
```

> **5 Helden** (siehe DESIGN §5): Default, SpeedySam (QuickPocket), BrickBoris (DemolitionExpert),
> TwinTina (DoubleDetonation), LuckyLola (LuckyDrops). Stats/Trait/Unlock 1:1 aus `HeroDefinition.cs`.

### 6.2 Daten-Importer (Editor-Tool)

Analog ArcaneKingdom: JSON-Sources in `Resources/Data/` → ScriptableObject-Assets via Menü `BomberBlast → Data → Import All`.

JSON-Files in `Resources/Data/` (Inhalte 1:1 aus dem Original-Code, verifiziert 2026-05-30):
- `heroes.json` (**5 Helden**)
- `bombs.json` (**14 Bomben-Typen / 13 Karten** im CardCatalog)
- `power_ups.json` (**12 PowerUps + Cure**)
- `enemies.json` (**12 Enemies** + Elite-Flag)
- `bosses.json` (**5 Bosse** + 8 Modifier)
- `worlds.json` (10 Sektoren + 100 Level, 12 Layouts, 4 Mutatoren; Dateiname bleibt `worlds.json`) — *(folgt im Content-Sprint)*
- `achievements.json` (**72 Achievements** in 5 Kategorien)
- `dungeon.json` (16 Buffs, 5 Synergien, 5 Raum-Typen, 8 Floor-Modifier, 8 Dungeon-Upgrades)
- `daily_missions.json` (17er-Pool), `weekly_missions.json` (17er-Pool)
- `events.json` (**8 Wochen-Events** + 4 saisonale)
- `battle_pass_s1.json` (30 Tiers, Saison-1-Rewards)
- `cosmetics.json` (**98 Items**: 32 Trails / 33 Frames / 33 Victories + Skins)
- `shop_upgrades.json` (12 permanente Upgrades: 9 Stat-Upgrades + 3 Bomb-Unlocks IceBomb/FireBomb/StickyBomb)
- `balancing.json` (Stat-Curves, Drop-Gewichte, Combo-Boni)
- `localization_de.json`, `localization_en.json`, ... (6 Sprachen initial)
- `tutorial.json` (3 Tutorial-Phasen: Movement/Bombs/PowerUps)
- `loading_tips.json` (33 globale + 10 welt-spezifische)
- `world_story.json` (10 Sektor-Intros + 9 Outros, neue Neo-Grid-Story; Dateiname bleibt `world_story.json`)

> Keine `talents.json` / `affixes.json` — Talent-Baeume und Affix-System gehoeren zur verworfenen
> Sci-Fi-Reinvention. Progression laeuft ueber die 12 permanenten Shop-Upgrades (9 Stat + 3 Bomb-Unlocks, siehe DESIGN §16.2).

### 6.3 Save-Schema (Firebase RTDB)

```
/players/{uid}/
   ├── profile {
   │     displayName: string,
   │     country: string,
   │     language: string,
   │     level: number,             // Player-Level (XP-basiert)
   │     exp: number,
   │     currentHeroId: string,
   │     bannerSkinId: string,
   │     frameId: string,
   │     trailId: string,
   │     victoryId: string,
   │     createdAt: ServerValue.TIMESTAMP,
   │     lastSeenAt: ServerValue.TIMESTAMP
   │   }
   ├── currencies {
   │     coins: number,
   │     gems: number,
   │     dungeon_coins: number      // nur 3 Waehrungen (wie Original)
   │   }
   ├── shop_upgrades {              // 12 permanente Upgrades (PlayerUpgrades / UpgradeType)
   │     startBombs, startFire, startSpeed, extraLives, scoreMultiplier,
   │     timeBonus, shieldStart, coinBonus, powerUpLuck,    // 9 Stat-Upgrades
   │     iceBomb, fireBomb, stickyBomb   // 3 Bomb-Unlocks — jeweils Level-Wert
   │   }
   ├── inventory
   │   ├── heroes {
   │   │     [heroId]: { unlocked: bool, selectedSkin: skinId }   // KEINE Talente/Level (nur Unlock + Skin)
   │   │   }
   │   ├── active_hero: string,
   │   ├── cards {
   │   │     [cardId]: { level, count }       // OwnedCard: CardId + Level + Count (keine Affixe)
   │   │   }
   │   ├── deck { active_slot, slots: [cardId], extra_slot_unlocked: bool }
   │   └── cosmetics {
   │         trails: [id],        // 32
   │         frames: [id],        // 33
   │         victories: [id],     // 33
   │         skins: [id]          // CustomizationService
   │       }
   ├── progress {
   │     story: { [worldId]: { [levelId]: stars } },
   │     master: { [worldId]: { [levelId]: stars } },
   │     dungeon: { lastRunState, stats: { total_runs, best_floor, ... } },
   │     achievements: { [achId]: { unlockedAt: ServerValue.TIMESTAMP } },
   │     discoveries: { [powerUpId]: bool, [bombId]: bool }
   │   }
   ├── league {
   │     points: number,          // Liga-Punkte (Async-Score, KEIN MMR/Glicko)
   │     tier: string,            // "Bronze.I" / "Silver.II" / ... / "Diamond"
   │     season: number           // 14-Tage-Liga-Saison
   │   }
   ├── battle_pass {
   │     season: number,          // 30-Tage-BP-Saison (von Liga-Saison getrennt!)
   │     tier: number,            // 1..30
   │     xp: number,
   │     premium: bool,           // BattlePass-Plus = separates IAP-Flag (vip/plus)
   │     claimedTiers: [number],
   │     xpBoostStartTicks: number  // Hybridtimer-Anti-Cheat
   │   }
   ├── quests {
   │     daily: [{ id, progress, completed, rerollUsed }],
   │     weekly: [{ id, progress, completed }]
   │   }
   ├── friends {
   │     [friendUid]: { friendCode, accepted, since: ServerValue.TIMESTAMP }
   │   }
   ├── blocked: { [uid]: ServerValue.TIMESTAMP }
   ├── settings {
   │     language, audio: {music, sfx, voice, ambient},
   │     graphics: {tier, frameLimit, particles},
   │     controls: {joystickType, joystickSize, ...},
   │     accessibility: {colorblind, highContrast, uiScale, subtitles, reducedMotion}
   │   }
   ├── consent {
   │     analytics: bool, marketing: bool, voiceChat: bool,
   │     dsgvoSignedAt: ServerValue.TIMESTAMP
   │   }
   ├── crashCount: number,
   ├── version: number,            // Save-Schema-Version
   └── schemaVersion: number       // Current = 1 (neues arena-Schema; die Legacy-Import-Bruecke
                                    // liest weiterhin das Original-V3-Schema ein — kein Konflikt, getrennte Projekte, siehe §6.5)

/leagues/s{season}/{tier}/{uid}
   ├── displayName, country, points, last_updated: ServerValue.TIMESTAMP

/daily_race/{date_YYYYMMDD}/{tier}/{uid}
   ├── score, replay_hash, completed_at: ServerValue.TIMESTAMP

/reports/{reportedUid}/{reporterUid}
   ├── reason, comment, time: ServerValue.TIMESTAMP
```

### 6.4 Save-Schema-Migration (Intra-Schema)

Schema-Version-Feld auf `players/{uid}/schemaVersion` (neues Projekt startet bei **1**). Cloud-Function
`migrateSchema(uid, fromVersion, toVersion)` läuft beim ersten Login einer neuen App-Version (Forward-Migration-Pflicht).

```typescript
// CloudFunctions/src/migrateSchema.ts — betrifft NUR das neue arena-Schema (intra)
export async function migrateSchema(uid: string, fromV: number, toV: number) {
  if (fromV === 1 && toV === 2) { /* neue Felder mit Defaults auffuellen */ }
  // ... weitere Forward-Migrations, danach schemaVersion setzen
}
```

### 6.5 Legacy-Save-Import (KRITISCH — alt → neu, Cross-Projekt)

> Das produktive BomberBlast speichert in einem **anderen Firebase-Projekt** und einem **anderen Format**
> als das Unity-Remake. Bestands-Spieler dürfen ihren Fortschritt **nicht** verlieren.

| Aspekt | Original (alt) | Unity-Remake (neu) |
|--------|----------------|---------------------|
| Firebase-Projekt | **`bomberblast-league`** | **`bomberblast-arena`** |
| Save-Format | 35-Key-Preferences-Blob (`CloudSaveData`, `CloudSaveSchemaMigrator` V1→V3) | strukturiertes `/players/{uid}/` (oben) |
| Identität | Anonymous-UID (im league-Projekt) | Anonymous-UID (im arena-Projekt) |

**Cloud-Function `importLegacySave` (HTTP-Callable, Auth):**
1. **UID-Bridging:** Alt-UID (league-Projekt) ↔ Neu-UID (arena-Projekt) verknüpfen (Mapping-Tabelle
   `/legacy_links/{newUid} = oldUid`; idempotent, einmalig pro Account).
2. **Lesen:** 35-Key-Blob aus `bomberblast-league` über Admin-SDK (Cross-Projekt-Service-Account).
3. **Mapping (Feld-für-Feld):** TotalStars/Story-Sterne → `progress.story`; Coins/Gems → `currencies`;
   Shop-Upgrade-Level → `shop_upgrades`; OwnedCards (CSV) → `inventory.cards`; Cosmetics/Skins; Achievements;
   Liga-Punkte/Tier/Saison; BattlePass-Tier/XP; DungeonStats/DungeonCoins; Hero-Unlocks + aktiver Hero;
   Accessibility/Consent-Flags; DailyReward/Streak. (Quelle: `CloudSaveData.cs`-SyncKeys.)
4. **Konflikt-Resolution** wie Original `ChooseBest`: TotalStars → Wealth → Cards → Keys.Count → Timestamp.
5. **Validierung + Logging**, kein Datenverlust bei Teilfehler (Best-Effort, Corruption-Flag wie `PersistenceHealth`).

**Pflicht-Test vor Launch:** Import mit echten Alt-Accounts (alle 35 Keys, CSV-Karten, leere/teilweise Saves).
Erfolgsrate-Ziel ≥ 99 % (PLAN §4.4).

---

## 7. Cloud-Save

### 7.1 Login
- **Anonymous-Auth** beim ersten Start; optional **Google-Link** für Geräte-Wechsel (gleiche UID → gleicher Save).

### 7.2 Save-Sync
- Firebase RTDB als Source-of-Truth, lokaler JSON-Cache als Last-Known-Good.
- Pull bei App-Start, Push-Debounce 5 s. Konflikt-Resolution: TotalStars → Wealth → Cards → Timestamp → Cloud-Default.
- `PersistenceHealth`-Corruption-Schutz: bei erkannter Korruption Pull statt Push (kein Data-Loss).

> **Entfernt (alte Logik):** Cross-Save Mobile↔PC, separate PvP-Pools — reiner Single-Player, Android-fokussiert.

## 8. Multiplayer / Netcode — nicht Teil von v0.5

**Reiner Single-Player. Kein Photon (Fusion/Realtime/Chat/Voice), kein Echtzeit-PvP/Co-op, keine
`[Networked]`-Properties, kein Rollback-/Host-Authoritative-Modell.** Die früheren Netcode-Kapitel
(Fusion-PvP, Realtime-Co-op, Snapshot-/Rollback-/Loot-Sharing-Mechanik) sind **entfernt** — sie gehörten
zur abgelösten Online-MP-Richtung.

**Grid-Rankings** und **Daily-Race** sind **asynchron**: der Client submittet einen Score an Firebase RTDB
(rule-validiert, Server-Timestamp, Rate-Limit), kein Live-Match. Ein etwaiger künftiger Multiplayer wäre
ein **separates Projekt** und ist hier nicht vorausgesetzt.

---

## 9. (entfällt — Multiplayer, siehe §8)

---

## 10. Anti-Cheat (Single-Player)

Kein Online-Match → **kein server-autoritatives Anti-Cheat, keine Replay-Re-Simulation auf Server-Workern.**
Fokus auf lokale Integrität + Plausibilität der asynchron eingereichten Scores:

- **Zeit-Manipulation:** Hybrid-Timer (`Environment.TickCount64` **+** persistierte `DateTime.UtcNow`, OR-verknüpft)
  für Daily-Bonus/Cooldowns/Comeback.
- **Save-Integrität:** Overflow-Guards (`(long)+amount`-Clamp), `PersistenceHealth`-Corruption-Flag, Pull-statt-Push bei Korruption.
- **Grid-Rankings/Daily-Race (async):** Plausibilität über Firebase-RTDB-Rules (Wertebereiche, Server-Timestamp,
  Write-Rate-Limit) + Report-Button + Profanity-Filter. Bewusst leichtgewichtig — kein Live-Validierungs-Backend.

## 11. Cloud Functions (minimal, TypeScript)

**Kein Match-Validation/Submit** (kein Multiplayer). Nur das Nötigste — vieles läuft über RTDB-Rules ohne Functions:

- **`accountDelete`** (DSGVO Art. 17): kaskadiertes Löschen Local→RTDB.
- **`seasonReset`** (Scheduled): Liga-/BP-Saison-Reset (sofern nicht rule-seitig gelöst).
- **`validateIap`** (optional): serverseitige Kaufbeleg-Prüfung (Play Billing).

> **Entfernt (alte Logik):** `submitMatchResult`, `validateMatch` (Pub/Sub), Match-Anti-Cheat-Worker.

## 12. Firebase-Security-Rules

### 12.1 RTDB Security-Rules (Auszug)

`firebase.rules.json`:

```json
{
  "rules": {
    ".read": false,
    ".write": false,

    "players": {
      "$uid": {
        ".read": "auth != null && auth.uid === $uid",
        ".write": "auth != null && auth.uid === $uid",
        
        "currencies": {
          ".validate": "newData.hasChildren(['coins', 'gems'])",
          "coins": { ".validate": "newData.isNumber() && newData.val() >= 0 && newData.val() <= 99999999" },
          "gems": { ".validate": "newData.isNumber() && newData.val() >= 0 && newData.val() <= 999999" }
        },
        
        "consent": {
          ".write": "auth != null && auth.uid === $uid",
          "analytics": { ".validate": "newData.isBoolean()" }
        }
      }
    },

    "leagues": {
      "s$season": {
        "$tier": {
          ".read": "auth != null",
          "$uid": {
            ".write": "false",  // Nur Cloud Functions dürfen schreiben
            ".validate": "newData.hasChildren(['displayName', 'points', 'last_updated'])"
          }
        }
      }
    },

    "daily_race": {
      "$date": {
        "$tier": {
          ".read": "auth != null",
          "$uid": {
            ".write": "false"  // Nur Cloud Functions
          }
        }
      }
    },

    "reports": {
      "$reportedUid": {
        "$reporterUid": {
          ".read": "false",
          ".write": "auth != null && auth.uid === $reporterUid && !data.exists()"  // 1× pro Paar
        }
      }
    }
  }
}
```

### 12.2 Cloud Storage Security-Rules

`firebase.storage.rules`:

```
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {
    match /avatars/{uid}/{file} {
      allow read: if true;  // Public
      allow write: if request.auth != null && request.auth.uid == uid && request.resource.size < 1 * 1024 * 1024;  // Max 1 MB
    }
  }
}
```

### 12.3 (entfällt — kein Multiplayer)

*Photon-Webhooks für Match-Anti-Cheat entfernt — reiner Single-Player.*

## 13. Determinismus-First-Design

### 13.0 Status & Mandat (WICHTIG)

Determinismus dient **Single-Player-Zwecken**: **Daily-Race** (weltweit identisches Tages-Level) und
**Replay-Verifikation**. Mandat: alle gameplay-relevanten Random-Calls über `IRngProvider`, Sim-Updates über
`FixedTimestepRunner` (60 Hz Fixed-Step, nie `Time.deltaTime`). Ein Replay muss denselben State-Hash
reproduzieren (CI-Gate).

> **Kein** Online-/Server-Re-Sim, **keine** Float-Determinismus-Anforderung für Online-Versus (kein Multiplayer).
> `DeterministicRandom` (xoshiro256+) ist integer-bit-stabil — für die Daily-Race-Replay-Verifikation ausreichend.

### 13.1 Pflicht-Konstanten

> Nach der Integration gilt: Alle gameplay-relevanten Random-Calls gehen über `IRngProvider`.
> Alle Tick-Updates laufen über `FixedTimestepRunner` bei 60 Hz. Alle Inputs werden in `ReplayCapture` aufgezeichnet.

### 13.2 IRngProvider-Implementation

```csharp
public interface IRngProvider
{
    int NextInt(int min, int max);
    float NextFloat();
    bool NextBool(float probability);
    void Seed(ulong seed);
    (ulong, ulong, ulong, ulong) GetState();
    void SetState(ulong s0, ulong s1, ulong s2, ulong s3);
}

public class DeterministicRngProvider : IRngProvider
{
    private DeterministicRandom _rng;
    
    public DeterministicRngProvider(ulong seed = 0)
    {
        _rng = new DeterministicRandom(seed);
    }
    
    public int NextInt(int min, int max) => _rng.Next(min, max);
    public float NextFloat() => _rng.NextSingle();
    public bool NextBool(float p) => _rng.NextSingle() < p;
    public void Seed(ulong s) => _rng = new DeterministicRandom(s);
    public (ulong, ulong, ulong, ulong) GetState() => _rng.GetState();
    public void SetState(ulong s0, ulong s1, ulong s2, ulong s3) => _rng.SetState(s0, s1, s2, s3);
}
```

`DeterministicRandom` = xoshiro256+ aus altem Code (Public Domain). Die `DeterministicRandom`-Klasse
selbst (256-Bit-State, `GetState`/`SetState` als Vierer-Tupel, `Next(int)`/`Next(int, int)`/
`NextDouble()`/`NextSingle()`/`NextBool()`) wird 1:1 übernommen. Das `IRngProvider`-Interface
darüber ist eine **bewusste Erweiterung** (`NextInt(min, max)`, `NextFloat()`,
`NextBool(probability)`, `Seed(ulong)`) — kein 1:1-Port der Original-API, sondern ein Adapter,
der die Provider-Semantik vereinheitlicht und das xoshiro-Backend kapselt.

### 13.3 SystemRngProvider (für Visual-Random)

Particle-Jitter, Screen-Shake-Offset, Camera-Tremor → **NICHT** deterministisch (würde künstlich wirken).

```csharp
public class SystemRngProvider : IRngProvider
{
    private Random _sys = new();
    // ... default Behavior
}
```

DI-Konfiguration:

```csharp
builder.Register<IRngProvider, DeterministicRngProvider>(Lifetime.Singleton)
       .WithParameter("seed", 0UL);  // Default seed
       
// Separater Inject für Visual-Code:
builder.Register<IRngProvider, SystemRngProvider>(Lifetime.Singleton)
       .Keyed("visual");
```

### 13.4 FixedTimestepRunner

```csharp
public class FixedTimestepRunner
{
    private const float FixedDeltaTime = 1f / 60f;  // 60 Hz
    private float _accumulator = 0f;
    
    public void Tick(float realDeltaTime, Action<float> simulateFunc)
    {
        _accumulator += realDeltaTime;
        while (_accumulator >= FixedDeltaTime)
        {
            simulateFunc(FixedDeltaTime);
            _accumulator -= FixedDeltaTime;
        }
    }
}
```

### 13.5 ReplayCapture

> Original-Layout (`ReplayCapture.cs`, Schema-V1): 1 Byte/Tick = **Direction (Bits 0-2) + Bomb (Bit 3) +
> Detonate (Bit 4)**, Bits 5-7 reserviert. **`ToggleSpecial` ist NICHT im ReplayCapture-Byte** — es lebt im
> separaten `PlayerInputSnapshot` (kompaktes Input-Wire-Format). Das `0x20`-Bit unten ist eine **Erweiterung**
> für das Remake und erfordert einen Schema-Versions-Bump (V1 → V2), wenn Alt-Replays gelesen werden sollen.

Input pro Tick (Remake-Erweiterung V2): 1 Byte (Direction 3 Bits + Bomb 1 Bit + Detonate 1 Bit + ToggleSpecial 1 Bit + Reserved 2 Bits).

```csharp
public class ReplayCapture
{
    private List<byte> _inputs = new();
    public ulong InitialSeed { get; private set; }
    
    public void Initialize(ulong seed)
    {
        InitialSeed = seed;
        _inputs.Clear();
    }
    
    public void RecordTick(PlayerInput input)
    {
        byte encoded = (byte)(
            (int)input.MoveDirection & 0x07 |
            (input.PlaceBomb ? 0x08 : 0) |
            (input.Detonate ? 0x10 : 0) |
            (input.ToggleSpecial ? 0x20 : 0)
        );
        _inputs.Add(encoded);
    }
    
    public byte[] Serialize() => _inputs.ToArray();
    public void Deserialize(byte[] data) => _inputs = new(data);
}
```

Bei 60 Hz und 10 Min Match: 60 × 600 = 36.000 Bytes raw. RLE-komprimiert: ~5-10 KB.

---

## 14. Performance-Targets + Hardware-Profile

### 14.1 Hardware-Tier-Definition

| Tier | Beispiel-Geräte | CPU | RAM | GPU | Target FPS |
|------|----------------|-----|-----|-----|------------|
| **Low** | Galaxy A50, Galaxy S8, Pixel 3a | 4 Cores @ 1.8 GHz | 2-3 GB | Adreno 530 | 30 FPS stabil |
| **Mid** | Pixel 5, Galaxy S20, Redmi Note 11 | 6-8 Cores @ 2.4 GHz | 4-6 GB | Adreno 650 | 60 FPS stabil |
| **High** | Pixel 7+, Galaxy S22+ | 8 Cores @ 3.0 GHz | 6-8 GB | Adreno 730 | 60 FPS, optional 120 FPS |
| **Ultra** | ROG Phone 7, Galaxy S24 Ultra, Pixel 8 Pro | 8 Cores @ 3.3+ GHz | 8-12 GB | Adreno 750 | 120 FPS, alle VFX |

### 14.2 Tier-Auto-Detection

```csharp
public class HardwareProfiler
{
    public HardwareTier Detect()
    {
        var ramGB = SystemInfo.systemMemorySize / 1024f;
        var cpuCores = SystemInfo.processorCount;
        var gpuTier = SystemInfo.graphicsDeviceName; // Heuristik

        if (ramGB >= 8 && cpuCores >= 8) return HardwareTier.Ultra;
        if (ramGB >= 6 && cpuCores >= 6) return HardwareTier.High;
        if (ramGB >= 4 && cpuCores >= 4) return HardwareTier.Mid;
        return HardwareTier.Low;
    }
}
```

User kann manuell überschreiben in Settings.

### 14.3 Frame-Budget pro Tier

| Tier | Frame-Budget (ms) | Davon: Update | Render | Particles | UI |
|------|-------------------|---------------|--------|-----------|----|
| Low (30 FPS) | 33.3 | 12 | 14 | 4 | 3 |
| Mid (60 FPS) | 16.6 | 6 | 7 | 2 | 1.5 |
| High (60 FPS) | 16.6 | 6 | 7 | 2 | 1.5 |
| Ultra (120 FPS) | 8.3 | 3 | 3.5 | 1 | 0.8 |

### 14.4 Tier-Auswirkungen

| Setting | Low | Mid | High | Ultra |
|---------|-----|-----|------|-------|
| Particle-Cap | 300 | 800 | 1.200 | 1.500 |
| Shadow-Quality | Off | Low | Med | High |
| Dynamic-Lights | 1 | 4 | 16 | 32+ |
| Bloom | Off | Off | Off | On |
| Post-Processing | Minimal | Basic | Standard | Full |
| Texture-Quality | Half | Standard | Standard | High |
| Anti-Aliasing | Off | FXAA | SMAA | SMAA + TAA |
| Resolution-Scale | 0.75 | 1.0 | 1.0 | 1.0 |

### 14.5 Memory-Targets

| Tier | RAM-Budget App | Texture-Budget | Audio-Budget |
|------|---------------|----------------|--------------|
| Low | 600 MB | 200 MB | 80 MB |
| Mid | 800 MB | 350 MB | 120 MB |
| High | 1200 MB | 500 MB | 160 MB |
| Ultra | 1600 MB | 800 MB | 200 MB |

### 14.6 Download-Größe-Targets

| Plattform | App-Größe (Initial) | After Install + Addressables |
|-----------|---------------------|------------------------------|
| Android AAB | < 250 MB | < 800 MB |
| Steam | < 1.5 GB | < 1.5 GB (PC kann mehr) |

**Strategie:**
- Initial nur Helden 1-3 + Sektor 1-3 im AAB
- Restliche Assets via Addressables nach Sektor-Freischaltung lazy geladen

### 14.7 Battery-Saver-Mode

Auto-Aktivierung bei <20 % Akku:
- Tier um 1 Stufe senken
- Bloom aus
- Particle-Cap halbieren
- Frame-Cap bei 30 FPS (auch wenn High-End)

---

## 15. URP-Rendering-Setup

### 15.1 Render-Pipeline-Asset-Konfiguration

| Setting | Wert |
|---------|------|
| Render-Path | Forward+ |
| MSAA | 4× (High/Ultra), Off (Mid/Low) |
| HDR | Enabled (High/Ultra) |
| Shadow-Distance | 30 (High/Ultra), 15 (Mid), 0 (Low) |
| Shadow-Resolution | 2048 (Ultra), 1024 (High), 512 (Mid) |
| Tier-Switch-Mode | Quality-Settings per Tier |

### 15.2 Renderer-Features

- **Outline-Renderer-Feature** (Custom, für stilisierte Charakter-/Entity-Outlines — wie Original `OutlineRenderHelper`)
- **Decal-Renderer-Feature** (für Bomb-Brandflecken)
- **PostProcessing** (Bloom, Vignette, ChromaticAberration, FilmGrain)

### 15.3 Shader-Graph-Shader

| Shader | Verwendung |
|--------|-----------|
| **Glow.shadergraph** | Bomben-Pulsing, Hero-Aura |
| **Dissolve.shadergraph** | Block-Zerstörung |
| **Hologramm.shadergraph** | Phantom/Ghost-Bombs, Decoy |
| **Outline.shadergraph** | Charakter-/Entity-Outline (Stylized-Toon) |
| **Liquid.shadergraph** | Slime-/Poison-Felder |
| **ForceField.shadergraph** | Shields, Invuln-Indication |
| **Cyber-Floor.shadergraph** | Sektor-1-Boden (Wet + Holo) |
| **Glitch.shadergraph** | Glitch-Effekte als Stilmittel (Sektor 10) |

### 15.4 Lighting-Strategy

- **Static-Lights**: Backed (Lightmap) für Map-Decoration
- **Dynamic-Lights**: Each Bomb = Point-Light (Tier-skaliert)
- **Hero-Aura-Lights**: 1 Spotlight pro Hero (Tier-aware)
- **Sektor-Atmosphäre**: Volumetric-Lighting via URP-Custom-Pass (Ultra only)

---

## 16. Asset-Pipeline (Addressables)

### 16.1 Addressable Groups

| Group | Inhalt | Loading-Strategy | Size-Target |
|-------|--------|------------------|-------------|
| `Bootstrap` | Splash, Default-Font, Loading-Tipps | Sync, im AAB | 5 MB |
| `Heroes.Launch` | 3 MVP-Helden (Default/SpeedySam/BrickBoris) | Sync, im AAB | 30 MB |
| `Heroes.Remaining` | Restliche 2 Helden (TwinTina/LuckyLola) | Lazy, nach Unlock | 20 MB |
| `Heroes.Seasonal` | Saisonale Helden | Remote CDN, post-Saison-Start | 10 MB pro Saison |
| `Worlds.1-3` | Sektor 1-3 Assets | Sync, im AAB | 80 MB |
| `Worlds.4-10` | Sektor 4-10 Assets | Lazy, nach Sektor-Unlock | 200 MB |
| `VFX.Bombs` | VFX-Graph für 22 Bomben | Pre-Load mit Game-Scene | 50 MB |
| `VFX.HeroTraits` | Hero-Trait-/Skin-VFX (passive Traits, keine Ultimates) | Pre-Load mit Hero-Pick | 30 MB |
| `Audio.BGM` | Sektor-Music-Loops | Streaming | 100 MB (alle Sektoren) |
| `Audio.SFX` | Sound-Effects | Pre-Load | 50 MB |
| `Audio.Voice` | Hero-VoiceLines (alle Sprachen) | Lazy nach Sprach-Wahl | 150 MB pro Sprache |
| `Cosmetics.Launch` | Cosmetic-Pool Launch | Lazy, Cache-LRU | 80 MB |
| `Cosmetics.Saison` | Saisonale Cosmetics | Remote CDN, lazy | 30 MB pro Saison |

### 16.2 Addressables Profile-Setup

**Local-Profile** (Editor + Dev-Build):
- Local-Path

**Remote-Profile** (Production):
- Remote-Load-Path: `https://firebasestorage.googleapis.com/v0/b/bomberblast-arena.appspot.com/o/addressables/`
- Catalog-CDN-Cache: 24h
- Version-Strategy: Semantic (1.0.0 → 1.0.1 patches)

### 16.3 Asset-Production-Workflow

```
3D-Artist erstellt FBX in Blender
    ↓
Substance Painter: PBR-Texturen
    ↓
Export: FBX + Materials + Textures (UE-Format optional)
    ↓
Unity-Import: Custom-Import-Settings (Compression: ASTC, max-texture-size 1024)
    ↓
Sprite-Atlas-Builder packt UI-Sprites
    ↓
Addressables-Group-Assignment
    ↓
CI: Build Addressables → Upload zu Firebase Storage
```

### 16.4 Texture-Compression

| Platform | Format | Quality |
|----------|--------|---------|
| Android | ASTC 6×6 | Medium |
| Steam (Windows) | DXT5 / BC7 | High |
| Steam (Linux/macOS) | BC7 / ETC2 | High |

Texture-Max-Size:
- Hero-/Charakter-Modelle: 1024×1024 (Mid/High), 512×512 (Low)
- UI: 2048×2048 Atlas, 512×512 individual
- Sektor-Hintergründe: 2048×1024 (High), 1024×512 (Low)

---

## 17. Audio-Architektur

### 17.1 AudioMixer-Setup

```
Master (Mixer-Root)
   ├── Music (Sektor-Themes, Hub-Music)
   ├── SFX
   │   ├── Bombs (Tick + Explosion)
   │   ├── PowerUps (Pickup-Sounds)
   │   ├── UI (Button-Clicks, Modal-Open)
   │   └── Combat (Hit-Sounds, Damage-Vocal)
   ├── Voice
   │   ├── Heroes (VoiceLines)
   │   ├── Announcer (Match-Events: "Match Start!", "Combo!")
   │   └── Story (Cutscene-Voices)
   ├── Ambient (Sektor-Ambient-Sounds: Regen, Wind, Industrie)
   └── Cinematic (Stinger, Boss-Reveal, Victory)
```

### 17.2 AudioMixer-Snapshots

| Snapshot | Music | SFX | Voice | Ambient | Cinematic |
|----------|-------|-----|-------|---------|-----------|
| **Menu** | -3 dB | -6 dB | -3 dB | -6 dB | -inf |
| **Game** | -6 dB | 0 dB | 0 dB | -3 dB | -inf |
| **Pause** | -12 dB | -12 dB | -6 dB | -12 dB | -inf |
| **Boss-Battle** | -3 dB (mit Layer 4) | 0 dB | 0 dB | -6 dB | -inf |
| **Cutscene** | -6 dB | -12 dB | 0 dB | -6 dB | 0 dB |

### 17.3 Sidechain-Ducking

- Voice → SFX -6 dB (für 2s nach Voice-Start)
- Cinematic-Stinger → Music -12 dB (für 1.5s)
- UI-Sounds → keine Ducking

### 17.4 FMOD-Studio-Integration (Optional)

Falls Budget: FMOD-Studio mit Layer-Music + Crossfade-Markers
- 1 Hauptloop pro Sektor mit 5 Layern (Base / Standard / Combat / Boss / Victory)
- Trigger via Code: `FMODUnity.RuntimeManager.PlayOneShot()`
- Saves manche Implementierungs-Aufwand für adaptive Music

Falls kein FMOD: Manueller AudioMixer mit Crossfade-Animation via DOTween.

### 17.5 AI-Voice-Pipeline (ElevenLabs)

```
Skript-Files in Resources/Voices/Heroes/ (Markdown)
├── nova_en.md
├── nova_de.md
├── cryo_en.md
└── ...

Build-Step (Editor-Tool):
1. Lade Skript-Files
2. ElevenLabs-API mit Voice-ID pro Hero+Sprache
3. Generiere WAV-Files
4. Speichere unter Audio/Voice/Generated/
5. Mark for Re-Render: bei Skript-Änderung

Manual-QA-Step:
- Mensch hört alle Lines via Editor-Tool "VoiceQA-Window"
- Bei Issues: Re-Render mit anderer Voice-Params (Stability, Similarity)
- Final-Approval-Flag in JSON

Mastering-Step (Build-Pipeline):
- ffmpeg-Pipeline:
  ffmpeg -i input.wav -af loudnorm=I=-16:TP=-1.5:LRA=11 output.wav
- Resample auf 44.1 kHz, Vorbis Q5 für Mobile
```

---

## 18. Test-Strategie

### 18.1 Test-Pyramide

```
                  ▲
                  │   E2E-Tests (Playmode)
                  │   ~50 Tests, 5-10 min Suite
              ────┼────
                  │   Integration-Tests
                  │   ~200 Tests, 2-3 min Suite
              ────┼────
                  │   Unit-Tests (EditMode)
                  │   ~1000-1500 Tests, < 30 s Suite
                  ▼
```

### 18.2 Unit-Tests (EditMode)

**Pflicht für:**
- Alle Domain-Klassen (BomberBlast.Domain.Tests)
- Pure-Funktionen, Algorithmen (Combo-System, DungeonSynergyResolver, LevelLayoutGenerator)
- Math-Funktionen (Overflow-Guard, Liga-Punkt-/Sub-Tier-Berechnung, Combo-Score)

**Coverage-Ziel:** 90 % für BomberBlast.Domain, 75 % für BomberBlast.Core

**Framework:** NUnit (via Unity Test Framework)

```csharp
[Test]
public void ComboSystem_FiveKillsInTwoSeconds_TriggersUltraCombo()
{
    var combo = new ComboSystem(rngProvider: new DeterministicRngProvider(42));
    var time = 0f;
    for (int i = 0; i < 5; i++)
    {
        combo.RegisterKill(time);
        time += 0.3f; // 5 Kills in 1.5s
    }
    Assert.AreEqual(5, combo.ComboCount);
    Assert.IsTrue(combo.IsUltraCombo);
}
```

### 18.3 PlayMode-Tests

**Pflicht für:**
- Scene-Loading
- Player-Bewegung + Bomb-Place
- Match-End-to-End-Flow (Story-Level)

**Tool:** Unity Test Framework PlayMode

### 18.4 Determinismus-Suite (KRITISCH)

**Zweck:** Sicherstellen, dass Client + Server-Worker bei gleichen Inputs identische States produzieren.

**Setup:**
```
Tests/DeterminismSuite/
├── ReplayCorpus/                     (Daily-Race + Story-Run-Replays als Test-Fixtures)
│   ├── dailyrace_2026-06-08.bin
│   ├── story_sektor3_l07.bin
│   └── ...
├── DeterminismTest.cs
└── DeterminismRunner.cs
```

**Test-Logik:**
```csharp
[Test]
public void Determinism_AllReplays_ProduceIdenticalHash()
{
    var replays = LoadAllReplays("ReplayCorpus/");
    foreach (var replay in replays)
    {
        var simulator = new BattleSimulator(seed: replay.Seed);
        var finalState = simulator.RunReplay(replay.Inputs);
        var hash = ComputeStateHash(finalState);
        Assert.AreEqual(replay.ExpectedHash, hash, 
            $"Replay {replay.RunId} produced different hash!");
    }
}
```

**CI-Gate:** Pflicht-Check in jedem PR. Failure blockt Merge.

### 18.5 Last-/Stress-Tests (Single-Player)

**Kein Server-/Photon-Load-Test** (kein Multiplayer). Relevante Stress-Tests:
- **Gameplay-Stress:** viele Gegner + Ketten-Explosionen + VFX gleichzeitig auf Min-Spec (Frame-Budget halten).
- **Save-/Offline-Robustheit:** große Save-States, lange Sessions, App-Kill/Restart, Cloud-Sync-Konflikte.
- **Grid-Rankings-Submit:** Burst von Score-Submits gegen RTDB-Rules (Rate-Limit greift).

### 18.6 Beta-Test-Programme

**Phase 1: Internal Alpha (Monat 8-10)**
- 10 Studio-interne Tester
- Vollständiger Feature-Loop
- Crashlytics + Manual-Bug-Reports

**Phase 2: Closed Beta DACH (Monat 10-11)**
- 100-500 externe Tester (Discord/Twitter-Recruit)
- NDA-pflicht
- Wochen-Build-Schedule
- Discord-Feedback-Channel

**Phase 3: Open Beta DACH (Monat 11-12)**
- Unbegrenzt Tester via Play-Store-Beta-Track
- Public-Marketing
- Stress-Tests live

---

## 19. Build & DevOps

### 19.1 GitHub Actions Workflow

`.github/workflows/unity-build.yml`:

```yaml
name: Unity Build

on:
  pull_request:
    branches: [main, develop]
  push:
    branches: [main, develop]
    tags: ['v*']

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: actions/cache@v4
        with:
          path: src/Apps/BomberBlast.Unity/Unity/Library
          key: Library-${{ hashFiles('src/Apps/BomberBlast.Unity/Unity/Assets/**', 'src/Apps/BomberBlast.Unity/Unity/Packages/**') }}
      - uses: game-ci/unity-test-runner@v4
        with:
          unityVersion: 6000.4.8f1
          projectPath: src/Apps/BomberBlast.Unity/Unity
          testMode: EditMode
          githubToken: ${{ secrets.GITHUB_TOKEN }}

  build-android:
    needs: test
    runs-on: ubuntu-latest
    if: github.event_name == 'push' && (github.ref == 'refs/heads/main' || startsWith(github.ref, 'refs/tags/v'))
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
          ANDROID_KEYSTORE_BASE64: ${{ secrets.ANDROID_KEYSTORE_BASE64 }}
          ANDROID_KEYSTORE_PASS: ${{ secrets.ANDROID_KEYSTORE_PASS }}
          ANDROID_KEYALIAS_PASS: ${{ secrets.ANDROID_KEYALIAS_PASS }}
        with:
          targetPlatform: Android
          unityVersion: 6000.4.8f1
          projectPath: src/Apps/BomberBlast.Unity/Unity
      - uses: actions/upload-artifact@v4
        with:
          name: BomberBlast-Arena-Android
          path: build/Android

```

### 19.2 Build-Targets

| Target | Build-Zeit (Cold-Cache) | Build-Zeit (Warm-Cache) |
|--------|-------------------------|--------------------------|
| Android (AAB Release) | 25-35 min | 8-12 min |
| Steam (Windows x64) | 15-25 min | 5-8 min |
| Steam (Linux x64) | 15-25 min | 5-8 min |
| Steam (macOS) | 20-30 min | 7-10 min |

### 19.3 Cloud Functions Deploy

```yaml
# .github/workflows/cloudfunctions-deploy.yml
name: Deploy Cloud Functions

on:
  push:
    branches: [main]
    paths: ['src/Apps/BomberBlast.Unity/Server/CloudFunctions/**']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: |
          cd src/Apps/BomberBlast.Unity/Server/CloudFunctions
          npm install
          npm run build
          npm test
      - uses: w9jds/firebase-action@master
        with:
          args: deploy --only functions
        env:
          FIREBASE_TOKEN: ${{ secrets.FIREBASE_TOKEN }}
```

---

## 20. Versionierung + Hotfix-Strategie

### 20.1 Versions-Schema

`MAJOR.MINOR.PATCH` (z.B. 1.0.0):
- **MAJOR**: Breaking-Change im Save-Schema
- **MINOR**: Saison-Release mit neuem Inhalt
- **PATCH**: Bugfixes, Balancing-Tweaks, Hotfixes

**Build-Number**: Auto-inkrementiert via CI (z.B. `1.0.0+123`)

### 20.2 Hotfix-Pipeline

Bei Production-Issues:

1. **Hot-Fix via Remote Config** (sofort, kein Build): Balancing-Tweaks, Event-Toggle, Drop-Rate-Anpassung
2. **Hot-Fix via Addressables** (gleicher Tag, kein Play-Store-Update): Asset-Fix (z.B. fehlerhafte Texture)
3. **Hot-Fix via App-Update** (1-3 Tage, Play-Store-Review): Code-Fix

### 20.3 Saison-Release-Schedule

- **Saison-Cut**: alle 8 Wochen, Mittwoch 10:00 UTC
- **Pre-Saison-Beta**: 1 Woche vorher, Closed-Beta-Channel
- **Saison-Post-Mortem**: 2 Wochen nach Saison-End

---

## 21. Logging + Telemetrie

### 21.1 Logging-Setup

Alle Services/MonoBehaviours nutzen `ILogger<T>` per Constructor Injection.

```csharp
public class BattleController
{
    private readonly ILogger<BattleController> _logger;
    
    public BattleController(ILogger<BattleController> logger) => _logger = logger;
    
    public void StartLevel(int levelId)
    {
        _logger.LogInformation("Starting level {LevelId}", levelId);
    }
}
```

**Sinks:**
- `UnityLogger` → Unity-Console
- `FileLogger` → `persistentDataPath/logs/app.log` (512 KB Cap, 1 Backup)
- `FirebaseAnalyticsLogger` → Firebase-Analytics-Event (LogLevel.Information+)

**Build-Filtering:**
- DEBUG: LogLevel.Trace
- RELEASE: LogLevel.Information

### 21.2 Telemetrie-Events (Firebase-Analytics)

Funnel-Events:

| Event-Name | Properties | Trigger |
|-----------|------------|---------|
| `app_open` | version, deviceTier | App-Start |
| `tutorial_complete` | phase (T1/T2/T3) | Tutorial-Phasen-End |
| `level_start` | worldId, levelId, heroId | Story-Match-Start |
| `level_complete` | worldId, levelId, stars, durationSec | Story-Match-End |
| `purchase_start` | productId | IAP-Initiated |
| `purchase_complete` | productId, priceEur | IAP-Success |
| `bp_tier_up` | tier, premium | BP-Tier-Reached |
| `hero_unlock` | heroId, source | Hero-Unlock |
| `achievement_unlock` | achievementId | Achievement-Unlock |
| `crash` | reason, stack | Auto-via-Crashlytics |

**DSGVO-Compliance:**
- Analytics opt-in (default off in DACH, opt-in in EU)
- Marketing-Tracking separat opt-in
- Anonymized device-id

---

## 22. Domain-Code-Port aus altem BomberBlast

### 22.1 Port-Checkliste (Priority-1, Sprint 1-2)

- [ ] `Core/DeterministicRandom.cs` (xoshiro256+) → `BomberBlast.Core/Random/DeterministicRandom.cs`
- [ ] `Core/ReplayCapture.cs` → `BomberBlast.Core/ReplayCapture/`
- [ ] `Core/FixedTimestepRunner.cs` → `BomberBlast.Core/FixedTimestep/`
- [ ] `Core/Combat/ComboSystem.cs` → `BomberBlast.Domain/Combat/`
- [ ] `Core/Combat/SpecialExplosionEffects.cs` → `BomberBlast.Domain/Bombs/Effects/`
- [ ] `Core/Combat/EnemyPositionIndex.cs` → `BomberBlast.Domain/Combat/`
- [ ] `Core/Dungeon/DungeonSynergyResolver.cs` → `BomberBlast.Domain/Dungeon/`
- [ ] `GameStateSnapshot.cs` (FNV-1a State-Hash für Daily-Race-Replay-Verifikation) → `BomberBlast.Domain/Determinism/`; Input-Recording via `ReplayCapture`. *(MP-Wire-Bits PlayerInputSnapshot/InputBuffer entfallen — Single-Player.)*

### 22.2 Port-Checkliste (Priority-2, Sprint 3-4)

- [ ] `Core/LevelGeneration/LevelGenerator.cs` + `MutatorEffects.cs` → `BomberBlast.Domain/Worlds/`
- [ ] `Models/Levels/LevelLayoutGenerator.cs` (**12 Layouts** + 4 Mutatoren) → `BomberBlast.Domain/Worlds/Layouts/`
- [ ] `Models/Cards/CardCatalog.cs` (**13 Karten** + Standard-Bombe = 14 BombTypes) → `BomberBlast.Domain/Bombs/`
- [ ] `AI/PathFinding/AStar.cs` → `BomberBlast.Domain/Enemies/AI/`
- [ ] BFS + DangerZone-Code → `BomberBlast.Domain/Enemies/AI/`

### 22.3 Port-Checkliste (Priority-3, Sprint 5-6)

- [ ] `Services/LeagueService.cs` Logik (5-Tier × 3-Sub, **Perzentil-Promotion Top30/Bottom20**, NPC-Backfill, Daily-Race) → `BomberBlast.Domain/League/`
- [ ] `Services/BattlePassService.cs` (**30 Tiers**, XP-Tier, 10 Themes) → `BomberBlast.Domain/BattlePass/`
- [ ] `Services/AchievementService.cs` (**72 Definitionen**, 5 Kategorien) → `BomberBlast.Domain/Achievements/`
- [ ] `Services/CoinService.cs` + `GemService.cs` + DungeonCoins (Overflow-Guard) → `BomberBlast.Domain/Economy/`
- [ ] `Services/ShopService.cs` + `Models/UpgradeType.cs` + `PlayerUpgrades.cs` (**12 permanente Upgrades**: 9 Stat + 3 Bomb-Unlocks — Haupt-Coin-Sink!) → `BomberBlast.Domain/Economy/Shop/`
- [ ] `Services/CardService.cs` (Deck, Crafting) + `DungeonService.cs` + `DungeonUpgradeService.cs` (16 Buffs, 5 Synergien, 8 Upgrades) → `BomberBlast.Domain/`
- [ ] `Services/DailyChallengeService.cs` (Tages-Seed), `DailyMissionService.cs`, `WeeklyChallengeService.cs`, `WeeklyContentService.cs` → `BomberBlast.Domain/Modes/` bzw. `LiveOps/`
- [ ] `Services/EventCalendarService.cs` (**8 Wochen-Events**, ISO-Wochen-Seed) + `EventService.cs` (saisonal) → `BomberBlast.LiveOps/Events/`
- [ ] `Services/LuckySpinService.cs` (**9 Segmente**, Pity-Counter, Drop-Rate-Disclosure) → `BomberBlast.LiveOps/`
- [ ] `Services/DailyRewardService.cs` (7-Tage + Comeback) → `BomberBlast.LiveOps/`
- [ ] `Services/FirstPurchaseService.cs` (×2) + `StarterPackService.cs` + `VipSubscriptionService.cs` + `RotatingDealsService.cs` (Monetarisierungs-Glue) → `BomberBlast.LiveOps/`
- [ ] `Services/RetentionService.cs` (D1/D3/D7) + `ReEngagementScheduler.cs` + `ReviewService.cs` + `DiscoveryService.cs` → `BomberBlast.LiveOps/Retention/`
- [ ] **`Services/GameTrackingService.cs`** (zentraler Engine↔Meta-Event-Dispatcher, 30+ Hooks — kritisch für die Verkabelung aller Meta-Systeme) → `BomberBlast.LiveOps/`
- [ ] `Services/HighScoreService.cs` (Top-10) + `SurvivalSpawner` (Survival-Modus) + `QuickPlay`-Logik → `BomberBlast.Domain/Modes/`
- [ ] `Services/MasterModeService.cs`, `BossRushService.cs`, `WorldStoryService.cs`, `WhatsNewService.cs`, `FeatureUnlockChoreographer.cs`, `TutorialService.cs`, `HeroService.cs`, `CustomizationService.cs`, `CollectionService.cs`, `AccessibilityService.cs` → jeweiliges Modul
- [ ] `Services/RemoteConfigService` (+ `RemoteConfigKeys`) + `PushNotificationService` (FCM + Local) → Plattform-Layer
- [ ] Hybridtimer-Pattern (TickCount64 + persistierte UTC) → `BomberBlast.Core/HybridTimer.cs`
- [ ] Profanity-Filter (NFKD, im Original **inline** in `LeagueService.cs`) → als `BomberBlast.Domain/Chat/ProfanityFilter.cs` **extrahieren**

### 22.4 Geschätzte Port-Zeit

> Das Original umfasst ~117 Services / ~86k LOC. Der Pure-Domain-Anteil (keine Avalonia/Android-API)
> ist 1:1 portierbar; die Engine-/UI-Verkabelung ist Neu-Arbeit in Unity.

- Pure-Domain-Port (Combo/Dungeon/Liga/Cards/Economy/Determinismus-Bausteine): **3-4 Wochen** (1 Senior).
- Vollständige Feature-Parität inkl. Live-Service-Glue + UI: **mehrere Monate** (siehe ROADMAP-Phasen).
- Mit Tests + Coverage: zusätzlich +50 %.

> **Pflicht:** Die vollständige **Parity-Matrix** → [PARITY.md](PARITY.md) (jedes Original-System →
> Unity-Äquivalent + Status) als lebende Checkliste führen — das Original ist umfangreich; ohne Matrix
> gehen Systeme verloren. Beim Port jeden Eintrag-Status dort aktualisieren.

---

## 23. Code-Conventions

### 23.1 Naming

| Element | Convention | Beispiel |
|---------|-----------|----------|
| Namespace | `BomberBlast.{Module}` | `BomberBlast.Domain.Bombs` |
| Class | PascalCase | `BattleController`, `BombDefinition` |
| Interface | `I`-Prefix + PascalCase | `IBombService`, `IShopService` |
| Method | PascalCase | `PlaceBomb()`, `ProcessInput()` |
| Field (private) | `_camelCase` | `private int _fireRange` |
| Property | PascalCase | `public int FireRange { get; }` |
| Constant | `UPPER_SNAKE` | `private const int MAX_BOMBS = 8` |
| Event | `On{Verb}{Past}` (Unity-Standard) | `OnBombPlaced`, `OnPlayerDied` |
| ScriptableObject Asset | PascalCase + `_` für ID | `Hero_Default.asset`, `Bomb_Frost.asset` |
| Scene | PascalCase | `Boot.unity`, `MainMenu.unity` |
| Prefab | PascalCase + `_Prefab` Suffix | `BombPrefab`, `HeroViewPrefab` |
| asmdef | `BomberBlast.{Module}` | `BomberBlast.Game.asmdef` |
| Test-Class | `{Subject}Tests` | `ComboSystemTests`, `LeagueServiceTests` |
| Test-Method | `{Method}_{Scenario}_{Expected}` | `ProcessInput_ValidMove_UpdatesPosition` |

### 23.2 Code-Style

- **Modernes C#** (Primary Constructors, Records, Pattern-Matching, Switch-Expressions)
- **File-scoped Namespaces** (`namespace BomberBlast.Game;`)
- **Nullable Reference Types** aktiv (`<Nullable>enable</Nullable>`)
- **var** wenn Typ aus Kontext klar
- **Async-Konventionen:** `Async`-Suffix, `CancellationToken` als letzter Parameter
- **UniTask** statt Task<T>, außer bei Library-Calls
- **Kommentare auf Deutsch** (siehe globale Project-Conventions, Umlaute erlaubt)
- **Keine TODO ohne Issue-Verweis** (`// TODO(#42): ...`)
- **Keine emojis im Code** (außer auf explizite User-Anforderung)

### 23.3 Defensive Programming

- `UnityEngine.Assertions.Assert` für Editor/Development-Builds
- Production: `Result<T>`-Type für fehlbare Operationen, kein Silent-Fail
- Logging mit Strukturierten Templates: `_logger.LogError(ex, "Failed at {Route}", route)`
- Niemals `String.Format` für Log-Messages (kein Strukturieren)

### 23.4 MVVM-Pattern

Wie in altem BomberBlast (übertragen):
- View (UXML / UGUI) → Binder (MonoBehaviour) → ViewModel (POCO)
- ViewModel ist Unity-API-frei (testbar)
- Binder ist Adapter zur Unity-UI

```csharp
public class BattleHUDViewModel
{
    public ReactiveProperty<int> Coins { get; } = new();
    public ReactiveProperty<int> Lives { get; } = new();
    public ReactiveProperty<int> ComboCount { get; } = new();

    public Observable<Unit> OnBombButtonPressed { get; }   // R3

    // ... Commands, Events
}

public class BattleHUDBinder : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _coinsLabel;

    private BattleHUDViewModel _vm;

    // Method-Injection via Construct (CLAUDE.md-DI-Regel — keine Field-Injection)
    [Inject]
    public void Construct(BattleHUDViewModel vm) => _vm = vm;

    private void Start()
    {
        _vm.Coins.Subscribe(c => _coinsLabel.text = c.ToString()).AddTo(this);
    }
}
```

---

## Änderungslog (ARCHITECTURE)

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-ARCHITECTURE.md mit Tech-Stack, DI, Netcode, Anti-Cheat, Performance-Targets | Robert Schneider + Claude |
| 2026-06-08 | v0.5 | Neuausrichtung: Single-Player, kein Idle, Neo-Grid-Story; Netcode-Kapitel entfernt | Robert Schneider + Claude |

---

> **Status:** Tech-Architektur finalisiert für Phase 0 Setup. Unity-Projekt-Skelett unter `Unity/` angelegt (6000.4.8f1) — Editor-Open, Asmdef-Kompilierung und CI stehen aus.
> **Nächste Schritte:** Projekt im Editor öffnen und verifizieren, Firebase-Projekt einrichten, Domain-Code-Port starten.