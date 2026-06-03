# BomberBlast 3D — Tech-Architektur

> Vollständige technische Spezifikation. Komplementär zu [PLAN.md](PLAN.md) (Übersicht),
> [DESIGN.md](DESIGN.md) (Game-Design) und [ROADMAP.md](ROADMAP.md) (Produktion).
> Stand 2026-05-26.

---

## Inhaltsverzeichnis

1. [Tech-Stack (Versionen + Begründungen)](#1-tech-stack-versionen--begründungen)
2. [Folder-Layout](#2-folder-layout)
3. [Assembly Definitions (asmdef)](#3-assembly-definitions-asmdef)
4. [Dependency Injection mit VContainer](#4-dependency-injection-mit-vcontainer)
5. [Scene-Architektur](#5-scene-architektur)
6. [Daten-Architektur (ScriptableObjects + Save)](#6-daten-architektur-scriptableobjects--save)
7. [Cloud-Save & Cross-Save](#7-cloud-save--cross-save)
8. [Photon Fusion Netcode (PvP)](#8-photon-fusion-netcode-pvp)
9. [Photon Realtime Netcode (Co-op)](#9-photon-realtime-netcode-co-op)
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
| Min-iOS | iOS 13 | – | Metal-Renderer, 95 %+ Markt |

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
| **Timeline** | **Unity Timeline** | 1.8+ | Welt-Cutscenes, Cinematic-Sequenzen |
| **VFX** | **VFX Graph** | 17.0+ | GPU-Compute-Shader-Particles |
| **Particle System** | **Built-in** | Unity 6 | Backup für simple Effekte (Trail, Pickup) |
| **Shader Graph** | **URP-Built-in** | – | Custom Shaders (Glow, Dissolve, Hologramm, Outline, Liquid) |

### 1.4 Networking

| Bereich | Wahl | Version | Begründung |
|---------|------|---------|------------|
| **Real-time PvP** | **Photon Fusion 2** | 2.x | Tick-based Server-Authoritative + Rollback (alt: Lockstep). Mobile-erprobt |
| **Real-time Co-op** | **Photon Realtime** | 5.x | Host-Authoritative, einfacher als Fusion für PvE |
| **Chat** | **Photon Chat** | 4.x | Channels, Friends, DMs |
| **Voice-Chat** (Phase 2) | **Photon Voice 2** | – | Push-to-Talk + Spatial-Audio |
| **Async-Backend** | **Firebase Realtime DB** | Unity SDK 12.x | Liga + Cloud-Save + Daily-Race |
| **Auth** | **Firebase Auth** | Unity SDK 12.x | Anonymous + Email + Google + Apple SignIn |
| **Notifications** | **Firebase Cloud Messaging** | Unity SDK 12.x | Push + Local |
| **Remote Config** | **Firebase Remote Config** | Unity SDK 12.x | Live-Tuning, Event-Toggles, A/B-Tests |
| **Analytics** | **Firebase Analytics** + **Unity Analytics** | – | Funnel + Behavior |
| **Crashlytics** | **Firebase Crashlytics** | Unity SDK 12.x | Crash-Reporting (wieder rein nach alt-Wartungsproblem) |
| **Cloud Storage** | **Firebase Storage** | – | Addressables-CDN, Replay-Files |
| **Cloud Functions** | **Firebase Functions** | Node.js 20, TypeScript 5.x | Anti-Cheat-Worker, Saison-Reset, IAP-Validation |

### 1.5 Persistenz

| Bereich | Wahl | Begründung |
|---------|------|------------|
| **Settings** | PlayerPrefs (Unity-built-in) | Plattform-native, klein, schnell |
| **Game-Save** | Firebase RTDB (Source-of-Truth) + JSON-File (Last-Known-Good) | Cross-Save Mobile↔PC |
| **Replay-Files** | Lokal JSON + Firebase Storage (für PvP-Validation) | 5-30 KB pro Match |
| **Lokal-Cache** | Application.persistentDataPath/cache/ | Custom JSON-Files |

### 1.6 Monetization

| Bereich | Wahl | Version |
|---------|------|---------|
| **IAP** | **Unity IAP** | 4.13+ |
| **Google Play Billing** | (via Unity IAP) | v6 |
| **Apple StoreKit** | (via Unity IAP) | StoreKit 2 |
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
- Photon Fusion 2 + Photon Realtime + Photon Chat
- Firebase Unity SDK (Auth, RTDB, Functions, Messaging, Analytics, Crashlytics, Storage, Remote Config)
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
│   │   │   ├── clanInvite.ts
│   │   │   ├── friendRequest.ts
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
│   │   ├── DailyRaceMode.cs
│   │   ├── PvpMode.cs
│   │   └── CoopMode.cs
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
├── Multiplayer/             (BomberBlast.Multiplayer)
│   ├── PhotonFusion/
│   │   ├── PvpNetworkRunner.cs (NetworkRunner-Wrapper)
│   │   ├── PvpNetworkPlayer.cs (NetworkBehaviour)
│   │   ├── PvpNetworkBomb.cs
│   │   ├── PvpMatchmaker.cs
│   │   └── PvpAuthority.cs
│   ├── PhotonRealtime/
│   │   ├── CoopRoomManager.cs
│   │   ├── CoopHost.cs
│   │   ├── CoopGuest.cs
│   │   └── CoopSync.cs
│   ├── PhotonChat/
│   │   ├── ChatService.cs
│   │   └── ChatChannel.cs
│   ├── PhotonVoice/ (Phase 2)
│   │   └── VoiceController.cs
│   ├── Lobby/
│   │   ├── LobbyManager.cs
│   │   ├── HeroPickPhase.cs
│   │   └── MatchReadyCheck.cs
│   ├── Anti-Cheat/
│   │   ├── ClientReplayUploader.cs
│   │   └── SuspiciousPatternDetector.cs
│   └── Replay/
│       ├── ReplayService.cs
│       ├── ReplayBlobSerializer.cs
│       └── ReplayPlayback.cs
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
│   │   ├── ClanTabView.cs
│   │   └── ProfileTabView.cs
│   ├── HUD/
│   │   ├── BattleHUDView.cs
│   │   ├── JoystickUI.cs
│   │   ├── BombButton.cs
│   │   ├── HeroSkillBar.cs
│   │   ├── ComboDisplay.cs
│   │   └── MiniMap.cs
│   ├── Lobbies/
│   │   ├── PvpLobbyView.cs
│   │   ├── CoopLobbyView.cs
│   │   ├── HeroPickModal.cs
│   │   └── MatchReadyModal.cs
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

BomberBlast.Multiplayer (depends on Game, Domain, Core)
   ├── References: BomberBlast.Core, BomberBlast.Domain, BomberBlast.Game
   ├── Photon Fusion + Realtime + Chat
   └── Define Constraints: UNITY_INCLUDE_NETWORK

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
- BomberBlast.Multiplayer.Tests      (NUnit, EditMode, Photon-Mock)
- BomberBlast.LiveOps.Tests          (NUnit, EditMode)
```

### 3.1 Asmdef-Constraints

- **Domain darf NICHT Unity-API** verwenden (Compile-Constraint via Reflection-Check + CI-Gate)
- **Multiplayer ist Define-konditional** (UNITY_INCLUDE_NETWORK) → ermöglicht PvP-freie Demo-Builds
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
│  ├─ INetworkService → PhotonNetworkService
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
├─ Online-Services (Singleton)
│  ├─ ILeagueService → LeagueService
│  ├─ IClanService → ClanService
│  ├─ IPvpMatchmakingService → PvpMatchmakingService
│  ├─ ICoopLobbyService → CoopLobbyService
│  ├─ IChatService → ChatService
│  ├─ IFriendsService → FriendsService
│  ├─ IReplayService → ReplayService
│  └─ IAntiCheatClientService → AntiCheatClientService
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
   ├─ EconomyConfig
   └─ NetworkConfig          (nur für optionalen Multiplayer)
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

PvpLifetimeScope (Pvp-Scene)
├─ PvpController (Scoped)
├─ PvpNetworkRunner (Scoped)
├─ PvpMatchmaker (Scoped)
└─ HeroPickPhase (Scoped)

CoopLifetimeScope (Coop-Scene)
├─ CoopController (Scoped)
├─ CoopRoomManager (Scoped)
└─ CoopSync (Scoped)
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
        builder.Register<INetworkService, PhotonNetworkService>(Lifetime.Singleton);
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

        // ── Online-Services ──
        builder.Register<ILeagueService, LeagueService>(Lifetime.Singleton);
        builder.Register<IClanService, ClanService>(Lifetime.Singleton);
        builder.Register<IPvpMatchmakingService, PvpMatchmakingService>(Lifetime.Singleton);
        builder.Register<ICoopLobbyService, CoopLobbyService>(Lifetime.Singleton);
        builder.Register<IChatService, ChatService>(Lifetime.Singleton);
        builder.Register<IReplayService, ReplayService>(Lifetime.Singleton);

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
MainMenu.unity (additive, immer aktiv neben Battle/Pvp/Coop)
   ├── MainMenuLifetimeScope
   ├── 3D-Skybox + Animated-Hub-Background
   ├── 5-Tab-Navigation (Home/Play/Shop/Clan/Profile)
   ├── Daily-Reward-Modal
   ├── Battle-Pass-Modal
   ├── WhatsNew-Modal
   └── Push-Modal-Queue (Feature-Unlocks)
        │ Tap "Play → Story" → Game.unity
        │ Tap "Play → PvP"   → Pvp.unity (Lobby first, dann Match)
        │ Tap "Play → Co-op" → Coop.unity (Lobby first, dann Match)
        ▼
Game.unity / Pvp.unity / Coop.unity (additive)
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
| Pvp | PvP-Lobby + Match | Pro Session | Additive |
| Coop | Co-op-Lobby + Match | Pro Session | Additive |
| Cinematic | Welt-Cutscenes | Pro Cutscene | Additive |
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
- `worlds.json` (10 Welten + 100 Level, 12 Layouts, 4 Mutatoren) — *(folgt im Content-Sprint)*
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
- `world_story.json` (10 Welt-Intros + 9 Welt-Outros)

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

/clans/{clanId}/
   ├── info { name, tag, country, language, level, motto, leaderId, createdAt }
   ├── members {
   │     [uid]: { role: "Leader"|"Officer"|"Member", joinedAt, contribution, lastSeen }
   │   }
   ├── chat {
   │     [msgId]: { uid, displayName, text, time: ServerValue.TIMESTAMP }
   │   }
   ├── war_history { [seasonId]: { score, won, opponent } }
   └── treasury { coins, gems }

/leagues/s{season}/{tier}/{uid}
   ├── displayName, country, points, last_updated: ServerValue.TIMESTAMP

/daily_race/{date_YYYYMMDD}/{tier}/{uid}
   ├── score, replay_hash, completed_at: ServerValue.TIMESTAMP

/matches/{matchId}/
   ├── players: [{uid, hero, slot}], mode, seed, region, timestamp
   ├── replay_blob: base64-encoded ReplayCapture
   ├── result: { winner, scores, stats }
   ├── validated_at: ServerValue.TIMESTAMP
   ├── validation_status: "pending"|"valid"|"invalid"
   └── ttl_at: ServerValue.TIMESTAMP    // 30 Tage Aufbewahrung

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

## 7. Cloud-Save & Cross-Save

### 7.1 Cross-Platform-Login

Spieler-Account ist plattform-übergreifend:
- **Anonymous-Auth** beim ersten Start
- **Mit-Account-verknüpfen** Option: Email/Google/Apple SignIn
- Beim Login auf neuem Gerät: Auth-Provider-Wahl → identischer UID → identischer Save

### 7.2 Cross-Save Mobile↔PC

- Firebase RTDB ist Single-Source-of-Truth
- Lokal-Cache (JSON-Datei) als Last-Known-Good
- Pull bei App-Start
- Push-Debounce 5 s (aus alt portiert)
- Konflikt-Resolution: TotalStars → Wealth → Cards → Timestamp → Cloud-Default

### 7.3 Separate PvP-Pools (trotz Cross-Save)

```
/players/{uid}/league/{platform}/
   ├── mobile: { rank, tier, season }
   ├── pc: { rank, tier, season }
```

Mobile-User spielt nur gegen Mobile-User in Match-Pool. PC-User analog. Aber:
- BP-Progress geteilt
- Cosmetics geteilt
- Coins/Gems geteilt
- Saison-Daily-Race-Leaderboards getrennt

---

## 8. Photon Fusion Netcode (PvP)

### 8.1 Konfiguration

| Parameter | Wert | Begründung |
|-----------|------|------------|
| **Tick-Rate (Server)** | 30 Hz | Mobile-tauglich, niedrige Bandwidth |
| **Tick-Rate (Client Prediction)** | 60 Hz | Smooth-Feel, Re-Sim on Server-Update |
| **Snapshot-Frequenz** | 30 Hz | Equal Tick-Rate |
| **Input-Frequenz** | 60 Hz | Doppelt Server-Rate, Server interpoliert |
| **Max Rollback** | 250 ms | Mobile-Latency-Toleranz |
| **Region-Routing** | EU/NA/SEA-Region | Auto-Detect via GeoIP |
| **Authoritative Model** | Server-Authoritative | Anti-Cheat-Pflicht |
| **Lag-Compensation** | Snapshot-Interpolation für Remote-Players | Smooth-Movement bei 80-150ms-Latency |

### 8.2 NetworkBehaviour-Beispiel

```csharp
public class PvpNetworkPlayer : NetworkBehaviour
{
    [Networked] public int HeroId { get; set; }
    [Networked] public Vector2Int GridPosition { get; set; }
    [Networked] public int HP { get; set; }
    [Networked] public int Lives { get; set; }
    [Networked] public int Coins { get; set; }
    [Networked] public NetworkBool IsAlive { get; set; }
    [Networked] public TickTimer InvulnerableUntil { get; set; }
    
    [Networked, Capacity(8)] public NetworkArray<BombNetState> ActiveBombs => default;
    
    public override void Spawned()
    {
        // Initialize visual representation
        var hero = HeroDatabase.Instance.GetById(HeroId);
        InstantiateHeroModel(hero.HeroModelPrefab);
    }
    
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerInput input))
        {
            ProcessInput(input);
        }
    }
    
    private void ProcessInput(PlayerInput input)
    {
        if (input.MoveDirection != Vector2Int.zero)
        {
            TryMove(input.MoveDirection);
        }
        if (input.PlaceBomb && CanPlaceBomb())
        {
            SpawnBomb();
        }
        // ...
    }
}

// Netz-Repräsentation der Bombe: blittable INetworkStruct (kein managed Reference-Typ).
public struct BombNetState : INetworkStruct
{
    public Vector2Int Cell;   // Grid-Position
    public int Timer;         // Rest-Ticks bis Zündung
    public byte Type;         // BombType-Index
    // ... weitere unmanaged Felder (Range, OwnerSlot)
}
```

> Fusion synchronisiert nur unmanaged- bzw. `INetworkStruct`-Typen — **keine** managed
> Reference-Klassen. `BombInstance` ist managed und bleibt der Render-/Domain-Typ; `BombNetState`
> ist die schlanke, blittable Netz-Repräsentation, die in der `NetworkArray<>` liegt. Pro Tick
> wird zwischen beiden gemappt.

### 8.3 Snapshot-Größen (Schätzung)

Pro Player-Snapshot:
- HeroId (1 Byte), GridPos (2 Bytes), HP+Lives (1 Byte), Coins (4 Bytes), Bomb-Array-Index (8×4 = 32 Bytes) → ~40 Bytes
- Bei 4 Players: 160 Bytes pro Tick

Pro Tick zusätzlich:
- Grid-State (Blöcke zerstört) → 150 Bits = 19 Bytes
- Particle-Spawn-Events → < 50 Bytes

**Total pro Snapshot: ~250 Bytes (Brutto-Obergrenze, voller State)**
**Pro Sekunde bei 30 Hz: ~7.5 KB (Brutto-Obergrenze)**
**Match (10 Min) ≈ 4.5 MB Bandwidth pro Spieler — Brutto-Obergrenze, akzeptabel für 4G**

> Diese Zahlen sind eine **Brutto-Obergrenze** (voller State pro Tick). Fusion überträgt
> tatsächlich **delta-komprimiert** (nur geänderte `[Networked]`-Properties, plus Range-/
> Bit-Packing) — der reale Traffic liegt im Match deutlich darunter, weil sich pro Tick nur
> wenige Felder ändern.

### 8.4 Rollback-Mechanik

1. Client predicted Input → Spieler bewegt sich sofort
2. Server bestätigt Input via Snapshot (~150ms später)
3. Bei Diskrepanz (z.B. Server hatte anderes Input gesehen) → Client Re-simuliert
4. Bei kleinen Differenzen: Smooth-Correction via Interpolation
5. Bei großen Differenzen (>250ms Rollback): Hard-Snap + visueller Glitch-Effekt (Stilmittel)

### 8.5 Anti-Cheat-Hooks in PvP

- **Reaction-Time-Detection**: Wenn Spieler reagiert in <50 ms auf Boss-Telegraph → Verdacht
- **Bomb-Spawn-Frequency**: Maximal 4 Bomben/sec, Server kapt zusätzliche
- **Damage-Output**: Max Damage-Rate plausibel? Server berechnet alle Damage-Events
- **Replay-Hash**: Client sendet Hash am Match-Ende, Server vergleicht mit eigenem

---

## 9. Photon Realtime Netcode (Co-op)

### 9.1 Host-Authoritative-Modell

- Spieler 1 ist Host (autoritativ über Game-State)
- Spieler 2-4 schicken Inputs → Host simuliert → broadcast State-Updates
- Tick-Rate: 20 Hz (Co-op braucht weniger Präzision als PvP)
- Reconnect: Bei Host-Disconnect → Auto-Promotion auf Spieler 2

### 9.2 Co-op-Sync-Strategie

```csharp
public class CoopSync : MonoBehaviourPunCallbacks
{
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Senden aktuellen Game-State an neuen Spieler
            SyncGameStateToPlayer(newPlayer);
        }
    }
    
    public override void OnMasterClientSwitched(Player newMaster)
    {
        if (newMaster.IsLocal)
        {
            // Übernahme der Host-Rolle
            BecomeHost();
        }
    }
}
```

### 9.3 Loot-Sharing

Loot-Drops (Coins, Karten) werden via RPC an alle Players geschickt:

```csharp
photonView.RPC(nameof(OnLootDropped), RpcTarget.All, lootType, lootId);
```

Karten-Drops werden Round-Robin verteilt (deterministisch via Seed).

---

## 10. Anti-Cheat-Pipeline

### 10.1 Mehrstufige Anti-Cheat-Strategie

```
Client → Cloud Function `submitMatchResult(matchId, result, replay)`
   ↓
[Stufe 1: Schnell-Validation, <100ms]
   ├── Format-Check (Replay-Schema valide?)
   ├── Hash-Validation (result_hash, final_state_hash)
   ├── Rate-Limit (Spieler darf nicht alle 30s submitten)
   └── Anti-Replay (matchId schon vorhanden?)
   ↓
[Stufe 2: Async-Worker, <30s]
   ├── Replay-Re-Simulation auf Server-Worker
   ├── Hash-Vergleich (Server-Output vs Client-Hash)
   ├── Suspicious-Pattern-Detection (Reaction-Times, Bomb-Spam)
   ├── ML-Modell (Phase 3+, anomaly detection)
   └── Decision: VALID | SUSPICIOUS | INVALID
   ↓
[Stufe 3: Action]
   ├── VALID: Score schreiben, Belohnungen
   ├── SUSPICIOUS: Score unter Quarantäne, Manual-Review-Queue
   ├── INVALID: Score verwerfen, Cheat-Flag erhöhen
   └── 3+ INVALID in 24h: 7-Tage-Ban (Appeal-Queue)
```

### 10.2 Replay-Re-Simulation auf Server-Worker

> **Voraussetzung (siehe §13.0):** Die Re-Simulation setzt eine **bit-stabile** Sim zwischen IL2CPP-Client
> und .NET-10-Worker voraus. Mit float-basierter Physik ist das **nicht** garantiert. Daher ist dieser
> Anti-Cheat-Pfad an das Float-Determinismus-Mandat (Fixed-Point/Quantisierung) gebunden **und** nur für
> den optionalen **Online-Versus** relevant — kein Launch-Blocker. Solange dieser nicht aktiv ist, greifen
> die übrigen Stufen (Format/Hash/Rate-Limit/Heuristiken) plus die bestehenden Firebase-Server-Rules.

**Tech**: Eigener C#-Worker (.NET 10) der **denselben (quantisierten) Domain-Code** wie der Client ausführt.

```
Server/DomainReplay/Program.cs
├── Liest Replay-Blob aus Firebase Storage
├── Lädt initial game state (Seed, Heroes, Loadout)
├── Simuliert Tick-für-Tick durch Replay-Inputs
├── Berechnet final state + hash
└── Vergleicht mit Client-gemeldetem Hash
```

**Cloud Function-Trigger**: Bei `matchId.validation_status === "pending"` → Pub/Sub-Trigger → Spawn Worker → Update `validation_status`.

### 10.3 Suspicious-Pattern-Detection (Phase 2+)

Heuristiken:

| Pattern | Schwellwert | Aktion |
|---------|-------------|--------|
| Reaction-Time auf Boss-Telegraph | <50 ms im Schnitt über 10 Matches | Flag |
| Move-Direction-Wechsel | >12 pro Sekunde im Schnitt | Flag |
| Bomb-Place-Frequency | >5 pro Sekunde | Auto-Lock |
| Win-Rate gegen Top-Spieler | >85 % bei <30 Matches | Flag |
| Score höher als physikalisch möglich | (Berechnet via Domain-Modell) | Auto-Lock |
| Replay-Hash-Mismatch >3× in 24h | – | Auto-Lock 7 Tage |

### 10.4 Account-Lock-Pipeline

- **Soft-Lock**: Match-Belohnungen suspendiert (Score wird nicht in Liga-Tabelle eingetragen)
- **Hard-Lock**: 7-Tage-Ban, App zeigt "Account suspendiert"-Screen
- **Appeal-Queue**: Spieler kann via UI Appeal einreichen → Manual-Review (Customer-Support-Tool)
- **Perma-Ban**: Bei 3+ Hard-Locks → permanent (mit Appeal-Möglichkeit nach 30 Tagen)

### 10.5 Tools für Cheat-Resistance

- **Determinismus-Replay-Suite** (Test-Sprint): 1000+ Replays als CI-Check, MUST be identisch zwischen Client + Server-Worker
- **Code-Obfuscation** (Phase 3): Beggar-Logic für IL2CPP-Build mit BeBy- oder Custom-Obfuscation
- **Native-Layer-Anti-Tampering** (Phase 3): Anti-Memory-Edit-Schutz via Native-Plugin

---

## 11. Cloud Functions (TypeScript)

### 11.1 Function-Übersicht

| Function | Trigger | Zweck |
|----------|---------|-------|
| `submitMatchResult` | HTTP-Callable (Auth-pflichtig) | Empfängt Match-Ergebnis, triggert Validation |
| `validateMatch` | Pub/Sub | Spawn Server-Worker für Replay-Re-Sim |
| `validateDailyRace` | HTTP-Callable | Daily-Race-Replay-Validation |
| `leagueReset` | Scheduled (alle **14 Tage**) | Liga-Saison-Reset (Perzentil-Promotion Top 30 % / Bottom 20 %), Liga-Belohnungen |
| `battlePassReset` | Scheduled (alle **30 Tage**) | Battle-Pass-Saison-Reset + Theme-Rotation (getrennt von der Liga-Saison!) |
| `dailyMissionReset` | Scheduled (00:00 UTC) | Daily-Quests neu würfeln |
| `weeklyMissionReset` | Scheduled (Mo 00:00 UTC) | Weekly-Missions neu würfeln |
| `clanWarResult` | Scheduled (Sa 20:00 UTC) | Clan-War-Auswertung |
| `accountDelete` | HTTP-Callable (Auth) | DSGVO Art-17 Cascade-Delete |
| `dataExport` | HTTP-Callable (Auth) | DSGVO Art-20 JSON-Export |
| `purchaseValidate` | HTTP-Callable (Auth) | Google/Apple-Receipt-Validation |
| `iapEntitlementsCheck` | Scheduled (täglich) | Subscription-Status-Refresh |
| `friendRequest` | HTTP-Callable (Auth) | Friend-Code-Lookup, Anti-Spam |
| `reportPlayer` | HTTP-Callable (Auth) | Report-Queue, Auto-Action bei 5+ Reports/24h |
| `notificationSend` | Pub/Sub | Server-getriggerte Pushes |
| `importLegacySave` | HTTP-Callable (Auth) | **Legacy-Save-Import alt → neu** (Cross-Projekt `bomberblast-league` → `bomberblast-arena`, 35-Key-Mapping, UID-Bridging — siehe §6.5) |
| `migrateSchema` | HTTP-Callable (Auth) | Intra-Schema-Version-Upgrade (nur arena, siehe §6.4) |
| `clanInvite` | HTTP-Callable (Auth) | Clan-Invite-Code generieren + Annahme |
| `leaderboardSnapshot` | Scheduled (stündlich) | Liga-Tabellen-Snapshot für Display |

### 11.2 Beispiel: submitMatchResult.ts

```typescript
import { onCall, HttpsError } from 'firebase-functions/https';
import * as admin from 'firebase-admin';

admin.initializeApp();
const db = admin.database();

export const submitMatchResult = onCall(async (request) => {
  if (!request.auth) {
    throw new HttpsError('unauthenticated', 'Login required');
  }
  const uid = request.auth.uid;
  const { matchId, mode, players, seed, result, replayBase64 } = request.data;

  // Rate-Limit: Max 1 Submit pro 30s pro UID
  const lastSubmit = await db.ref(`/players/${uid}/lastMatchSubmit`).once('value');
  const lastSubmitMs = lastSubmit.val() || 0;
  if (Date.now() - lastSubmitMs < 30_000) {
    throw new HttpsError('resource-exhausted', 'Rate limit');
  }

  // Schema-Validation (Zod oder Joi)
  if (!validateMatchSubmission(request.data)) {
    throw new HttpsError('invalid-argument', 'Invalid schema');
  }

  // Anti-Replay: matchId noch nicht vorhanden?
  const existing = await db.ref(`/matches/${matchId}`).once('value');
  if (existing.exists()) {
    throw new HttpsError('already-exists', 'Match already submitted');
  }

  // Speichern Match + Replay-Blob
  await db.ref(`/matches/${matchId}`).set({
    players, mode, seed,
    result,
    submitted_by: uid,
    submitted_at: admin.database.ServerValue.TIMESTAMP,
    validation_status: 'pending',
    ttl_at: Date.now() + 30 * 24 * 60 * 60 * 1000, // 30 Tage
  });
  
  // Replay-Blob in Cloud Storage (cheaper als RTDB)
  await admin.storage().bucket().file(`replays/${matchId}.bin`).save(Buffer.from(replayBase64, 'base64'));

  // Update last-submit
  await db.ref(`/players/${uid}/lastMatchSubmit`).set(Date.now());

  // Async Worker triggern via Pub/Sub
  await admin.pubsub().topic('validate-match').publishMessage({
    json: { matchId, uid },
  });

  return { status: 'pending', estimatedValidationTime: 30 };
});
```

### 11.3 Beispiel: validateMatch.ts (Pub/Sub-Trigger)

```typescript
import { onMessagePublished } from 'firebase-functions/pubsub';

export const validateMatch = onMessagePublished('validate-match', async (event) => {
  const { matchId, uid } = event.data.message.json;

  // Lade Match aus DB
  const matchSnap = await db.ref(`/matches/${matchId}`).once('value');
  if (!matchSnap.exists()) return;

  // Lade Replay-Blob aus Storage
  const replayBuffer = await admin.storage().bucket().file(`replays/${matchId}.bin`).download();

  // Spawn Worker (Cloud Run Container) für Replay-Re-Sim
  const result = await callReplayWorker({
    matchId, replayBlob: replayBuffer[0].toString('base64'),
    seed: matchSnap.val().seed,
    initialState: matchSnap.val().initialState,
  });

  if (result.serverHash === matchSnap.val().result.clientHash) {
    // VALID
    await db.ref(`/matches/${matchId}/validation_status`).set('valid');
    await applyRewards(uid, matchSnap.val());
  } else {
    // INVALID
    await db.ref(`/matches/${matchId}/validation_status`).set('invalid');
    await incrementCheatFlag(uid);
  }
});
```

### 11.4 Beispiel: accountDelete.ts (DSGVO Art-17)

```typescript
import { onCall, HttpsError } from 'firebase-functions/https';

export const accountDelete = onCall(async (request) => {
  if (!request.auth) throw new HttpsError('unauthenticated', 'Login required');
  const uid = request.auth.uid;

  // 1. Liga-Einträge löschen (alle Saisons)
  await db.ref(`/leagues`).once('value').then(async (snap) => {
    const seasons = snap.val() || {};
    for (const season in seasons) {
      for (const tier in seasons[season]) {
        await db.ref(`/leagues/${season}/${tier}/${uid}`).remove();
      }
    }
  });

  // 2. Daily-Race-Einträge löschen
  // ... ähnlich

  // 3. Clan-Mitgliedschaften löschen
  const clansSnap = await db.ref('/clans').once('value');
  // ... iterate, remove from members

  // 4. Friends/Blocked löschen
  await db.ref(`/players/${uid}/friends`).remove();
  // ... mirror auf andere uids

  // 5. Match-Replays löschen
  const matchesSnap = await db.ref(`/matches`).orderByChild('submitted_by').equalTo(uid).once('value');
  // ... delete

  // 6. Auth-User löschen
  await admin.auth().deleteUser(uid);

  // 7. Player-Daten löschen
  await db.ref(`/players/${uid}`).remove();

  return { status: 'deleted', timestamp: Date.now() };
});
```

---

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

    "matches": {
      "$matchId": {
        ".read": "auth != null && (data.child('players').hasChild(auth.uid) || data.child('submitted_by').val() === auth.uid)",
        ".write": "auth != null && !data.exists()",  // Nur einmal schreibbar (anti-replay)
        ".validate": "newData.hasChildren(['players', 'mode', 'seed', 'result'])"
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

    "clans": {
      "$clanId": {
        ".read": "auth != null",
        "info": { ".write": "auth != null && data.child('leaderId').val() === auth.uid" },
        "chat": {
          "$msgId": {
            ".write": "auth != null && data.parent().parent().child('members').hasChild(auth.uid)",
            ".validate": "newData.child('text').isString() && newData.child('text').val().length <= 200",
            "time": { ".validate": "newData.val() === now" },
            "uid": { ".validate": "newData.val() === auth.uid" }
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
    match /replays/{matchId}.bin {
      allow read: if false;  // Nur Server-Worker via Admin-SDK
      allow write: if false;  // Nur Cloud Functions via Admin-SDK
    }
    
    match /avatars/{uid}/{file} {
      allow read: if true;  // Public
      allow write: if request.auth != null && request.auth.uid == uid && request.resource.size < 1 * 1024 * 1024;  // Max 1 MB
    }
  }
}
```

### 12.3 Photon-Webhooks für Anti-Cheat

`Photon-Dashboard → Webhooks`:

- `PathBeforeRoomClose`: Validiert Match-End vor Room-Close
- `PathCreate`: Validiert Auth-Token vor Room-Create
- `PathLeave`: Cleanup bei Spieler-Leave

```typescript
// CloudFunctions/src/photonWebhook.ts
import { onRequest } from 'firebase-functions/https';

export const photonWebhook = onRequest(async (req, res) => {
  const { Type, GameId, ActorNr, UserId, AuthCookie } = req.body;
  
  switch (Type) {
    case 'Create':
      // Validate User-Auth, Room-Configuration
      if (!await isValidAuth(UserId, AuthCookie)) {
        return res.status(403).send({ ResultCode: 1, Message: 'Invalid Auth' });
      }
      break;
    case 'Close':
      // Match beendet, trigger Server-Worker
      await triggerMatchValidation(GameId);
      break;
  }
  
  return res.status(200).send({ ResultCode: 0 });
});
```

---

## 13. Determinismus-First-Design

### 13.0 Status & Mandat (WICHTIG)

> **Im Original ist Determinismus nur Foundation, NICHT integriert.** Der Live-Game-Loop nutzt
> `System.Random` (`GameEngine.cs`); `DeterministicRandom`/`ReplayCapture`/`FixedTimestepRunner`/
> `IRngProvider` existieren als isolierte Bausteine, sind aber **nicht** in `GameEngine.Update`
> verdrahtet (EnemyAI ~16 + LevelGenerator ~13 Random-Stellen laufen noch über `System.Random`).
> **Die Determinismus-Integration ist daher Neu-Arbeit (mehrwöchiger Sprint), kein reiner Port.**

> **Float-Determinismus-Mandat:** `DeterministicRandom` (xoshiro256+) ist nur **integer**-bit-stabil.
> Die gesamte Gameplay-Physik nutzt `float` (Positionen, Timer, `1/60f`). IEEE-754-Operationen
> (FMA/SIMD/transzendente Funktionen) divergieren zwischen **IL2CPP/ARM64-Client** und **x64-.NET-10-
> Server-Worker** — eine "isomorphe" bit-identische Re-Simulation ist damit **nicht garantiert**.
> Konsequenz für hash-relevante Zustände (State-Hash / Anti-Cheat / Online-Versus):
> - **Entweder** Fixed-Point/Integer-Sim für alle hash-relevanten Felder (Grid, HP, quantisierte Positionen),
> - **oder** dokumentierte Quantisierung der Felder **vor** dem FNV-1a-State-Hash.
>
> Online-Versus (das diese bit-stabile Sim braucht) ist ein **optionales Post-Launch-Feature** (siehe
> DESIGN §24) — daher **kein Launch-Blocker**. Für Single-Player/Replay genügt Client-seitiger
> Determinismus; Lockstep statt Rollback ist der wahrscheinliche Online-Default.

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
> separaten `PlayerInputSnapshot` (Multiplayer-Wire-Format). Das `0x20`-Bit unten ist eine **Erweiterung**
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
| **Low** | iPhone 8, Galaxy S8, Pixel 3a | 4 Cores @ 1.8 GHz | 2-3 GB | Adreno 530, A11 | 30 FPS stabil |
| **Mid** | iPhone 11, Pixel 5, Galaxy S20 | 6-8 Cores @ 2.4 GHz | 4-6 GB | Adreno 650, A13 | 60 FPS stabil bei Standard-Modi, 30 FPS bei PvP |
| **High** | iPhone 13+, Pixel 7+, Galaxy S22+ | 8 Cores @ 3.0 GHz | 6-8 GB | Adreno 730, A15+ | 60 FPS PvP, optional 120 FPS |
| **Ultra** | iPhone 15 Pro, ROG Phone 7, S24-Ultra | 8 Cores @ 3.3+ GHz | 8-12 GB | Adreno 750, A17+ | 120 FPS PvP, alle VFX |

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
| iOS IPA | < 300 MB | < 900 MB |
| Steam | < 1.5 GB | < 1.5 GB (PC kann mehr) |

**Strategie:**
- Initial nur Helden 1-3 + Welt 1-3 im AAB
- Restliche Assets via Addressables nach Welt-Freischaltung lazy geladen

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
| **Cyber-Floor.shadergraph** | Welt-1-Boden (Wet + Holo) |
| **Glitch.shadergraph** | Glitch-Effekte als Stilmittel (Welt 10) |

### 15.4 Lighting-Strategy

- **Static-Lights**: Backed (Lightmap) für Map-Decoration
- **Dynamic-Lights**: Each Bomb = Point-Light (Tier-skaliert)
- **Hero-Aura-Lights**: 1 Spotlight pro Hero (Tier-aware)
- **Welt-Atmosphäre**: Volumetric-Lighting via URP-Custom-Pass (Ultra only)

---

## 16. Asset-Pipeline (Addressables)

### 16.1 Addressable Groups

| Group | Inhalt | Loading-Strategy | Size-Target |
|-------|--------|------------------|-------------|
| `Bootstrap` | Splash, Default-Font, Loading-Tipps | Sync, im AAB | 5 MB |
| `Heroes.Launch` | 3 MVP-Helden (Default/SpeedySam/BrickBoris) | Sync, im AAB | 30 MB |
| `Heroes.Remaining` | Restliche 2 Helden (TwinTina/LuckyLola) | Lazy, nach Unlock | 20 MB |
| `Heroes.Seasonal` | Saisonale Helden | Remote CDN, post-Saison-Start | 10 MB pro Saison |
| `Worlds.1-3` | Welt 1-3 Assets | Sync, im AAB | 80 MB |
| `Worlds.4-10` | Welt 4-10 Assets | Lazy, nach World-Unlock | 200 MB |
| `VFX.Bombs` | VFX-Graph für 22 Bomben | Pre-Load mit Game-Scene | 50 MB |
| `VFX.HeroTraits` | Hero-Trait-/Skin-VFX (passive Traits, keine Ultimates) | Pre-Load mit Hero-Pick | 30 MB |
| `Audio.BGM` | Welt-Music-Loops | Streaming | 100 MB (alle Welten) |
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
| iOS | ASTC 6×6 | Medium |
| Steam (Windows) | DXT5 / BC7 | High |
| Steam (Linux/macOS) | BC7 / ETC2 | High |

Texture-Max-Size:
- Hero-/Charakter-Modelle: 1024×1024 (Mid/High), 512×512 (Low)
- UI: 2048×2048 Atlas, 512×512 individual
- Welt-Hintergründe: 2048×1024 (High), 1024×512 (Low)

---

## 17. Audio-Architektur

### 17.1 AudioMixer-Setup

```
Master (Mixer-Root)
   ├── Music (Welt-Themes, Hub-Music)
   ├── SFX
   │   ├── Bombs (Tick + Explosion)
   │   ├── PowerUps (Pickup-Sounds)
   │   ├── UI (Button-Clicks, Modal-Open)
   │   └── Combat (Hit-Sounds, Damage-Vocal)
   ├── Voice
   │   ├── Heroes (VoiceLines)
   │   ├── Announcer (Match-Events: "Match Start!", "Combo!")
   │   └── Story (Cutscene-Voices)
   ├── Ambient (Welt-Ambient-Sounds: Regen, Wind, Industrie)
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
- 1 Hauptloop pro Welt mit 5 Layern (Base / Standard / Combat / Boss / Victory)
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
├── ReplayCorpus/                     (1000+ Match-Replays als Test-Fixtures)
│   ├── match_001_pvp_1v1.bin
│   ├── match_002_coop_dungeon.bin
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
            $"Replay {replay.MatchId} produced different hash!");
    }
}
```

**CI-Gate:** Pflicht-Check in jedem PR. Failure blockt Merge.

### 18.5 Load-Tests

**Tool:** Custom Photon-Stress-Test mit Bot-Clients

**Szenarien:**
- 100 simultane PvP-Matches in EU-Region
- 500 simultane Co-op-Lobbys
- Liga-Submission-Burst: 1000 Spieler beenden Match in 60 s

**Ziel-Metriken:**
- Server-Latency P95 < 80 ms
- Photon-Server-CPU < 60 %
- Firebase-RTDB-Throughput-Limit nicht erreicht

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

  build-ios:
    needs: test
    runs-on: macos-latest
    if: github.event_name == 'push' && (github.ref == 'refs/heads/main' || startsWith(github.ref, 'refs/tags/v'))
    steps:
      # ... ähnlich Android, mit Xcode-Build-Step
      
  build-steam:
    needs: test
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      # ... Steam-Build mit SteamCMD-Upload
```

### 19.2 Build-Targets

| Target | Build-Zeit (Cold-Cache) | Build-Zeit (Warm-Cache) |
|--------|-------------------------|--------------------------|
| Android (AAB Release) | 25-35 min | 8-12 min |
| iOS (Xcode-Project) | 20-30 min | 7-10 min |
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
- **MAJOR**: Breaking-Change im Save-Schema oder Multiplayer-Protokoll
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
    
    public void StartMatch(int matchId)
    {
        _logger.LogInformation("Starting match {MatchId}", matchId);
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
| `pvp_match_start` | mode, region | PvP-Match-Start |
| `pvp_match_end` | mode, result, durationSec, leaguePointsChange | PvP-Match-End |
| `coop_match_start` | mode, partyCount | Co-op-Match-Start |
| `coop_match_end` | mode, result, durationSec | Co-op-Match-End |
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
- [ ] `Core/Multiplayer/PlayerInputSnapshot.cs` → `BomberBlast.Domain/Multiplayer/`
- [ ] `Core/Multiplayer/InputBuffer.cs` → `BomberBlast.Domain/Multiplayer/`
- [ ] `Core/Multiplayer/GameStateSnapshot.cs` → `BomberBlast.Domain/Multiplayer/`

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
- [ ] `Services/FirebaseClanService.cs` (Clan voll integriert: Create/Join/Chat/Leaderboard) → `BomberBlast.LiveOps/Clan/`
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
| Interface | `I`-Prefix + PascalCase | `IBombService`, `INetworkService` |
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
    
    public IObservable<Unit> OnBombButtonPressed { get; }
    
    // ... Commands, Events
}

public class BattleHUDBinder : MonoBehaviour
{
    [Inject] private BattleHUDViewModel _vm;
    [SerializeField] private TextMeshProUGUI _coinsLabel;
    
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

---

> **Status:** Tech-Architektur finalisiert für Phase 0 Setup.
> **Nächste Schritte:** Unity-Projekt anlegen, asmdefs erstellen, Firebase-Projekt einrichten, Domain-Code-Port starten.
