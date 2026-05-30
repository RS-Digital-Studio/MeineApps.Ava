# Services — Domänen-Services

37 Services (alle Singleton), vollständig als Interface abstrahiert.
Business-Logik (Economy, Persistenz, Firebase, Live-Ops, Audio) gehört ausschließlich hierher —
nie in ViewModels oder GameEngine (außer Render-/Loop-nahe Aufrufe via Interface).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## Service-Kategorien

### Kern-Economy

| Service | Schlüsselmethoden | Besonderheit |
|---------|-------------------|--------------|
| `ICoinService` / `CoinService` | `Add(amount)`, `Spend(amount)`, `Balance` | Overflow-Guard via `(long)Balance + amount` + Clamp `int.MaxValue`. `Load()` clampt < 0 auf 0 + Corruption-Flag |
| `IGemService` / `GemService` | analog CoinService | Zweite Währung (NUR Gameplay, kein direkter IAP-Kauf) |
| `IShopService` / `ShopService` | `GetUpgradeLevel(type)`, `Purchase(type)` | 9 permanente Upgrades, Preise 700-17.000 Coins |

### Fortschritt & Persistenz

| Service | Beschreibung |
|---------|-------------|
| `IProgressService` / `ProgressService` | Level-Sterne, Fail-Counter, World-Gating (3+ Sterne → nächste Welt), `ClearProgress()` |
| `IHighScoreService` / `HighScoreService` | Top-10 Scores (sqlite-net-pcl), `GetRank(score)` |
| `ICloudSaveService` / `NullCloudSaveService` | 35 Persistenz-Keys, Local-First, Pull bei Start, Push-Debounce 5s. **Android-Override** via `App.CloudSaveServiceFactory`. Dispose flusht pending Writes |
| `IMasterModeService` / `MasterModeService` | Level-Status (Normal-Stars bleiben unberührt), `IsActive`-Toggle. Dispose nötig. |
| `IDeckTelemetryService` / `DeckTelemetryService` | Used/Plays/Wins pro BombType (Balance-Telemetrie). Dispose flusht letzten pending Save. |

### Audio

| Service | Beschreibung |
|---------|-------------|
| `ISoundService` / `NullSoundService` (Desktop) | `PlaySound(key, pitch?, pan?)`, `PlayMusic(key)`, `StopMusic()`. Android-Override via Factory. |
| `IVibrationService` / `NullVibrationService` | 10 Pattern-Methoden (`VibrateBombPlant`, `VibrateDeath`, …). Android: `VibrationEffect.CreateWaveform`. |

### Live-Ops & Events

| Service | Beschreibung |
|---------|-------------|
| `IEventCalendarService` / `EventCalendarService` | Wöchentlicher Calendar via ISO-Week-Seed. Pool 8 Event-Typen. 12-Wochen-Vorschau-API. |
| `IEventService` / `EventService` | Saisonale Events (Halloween/Christmas/NewYear/Summer). Aktuellen Event ermitteln. |
| `IRotatingDealsService` / `RotatingDealsService` | 3 täglich + 1 wöchentlich, 20-50% Rabatt. Mitternacht-UTC-Reset. |
| `IRemoteConfigService` / `DefaultsRemoteConfigService` | Laedt `Resources/remote_config_defaults.json`. Android-Override via `FirebaseRemoteConfigService`. 23 Keys via `RemoteConfigKeys`. |
| `IWeeklyContentService` / `WeeklyContentService` | 8 WeeklyModifier + 4 WeeklyReward + 3 Boss-Modifier pro Woche (ISO-Week-deterministisch). |

### Firebase & Cloud

| Service | Beschreibung |
|---------|-------------|
| `IFirebaseService` / `FirebaseService` | Anonymous-Auth, Realtime-DB (Liga/Cloud-Save). Bearer-Header statt URL-Query-Token. `IDisposable`. |
| `ILeagueService` / `LeagueService` | 5 Tiers, 14-Tage-Saisons, NPC-Backfill bei < 20 echten Spielern, Profanity-Filter (Unicode-NFKD). |
| `IClanService` / `FirebaseClanService` | Create/Join/Leave, 30s-Pull-Chat. Firebase-RTDB via `IFirebaseService`. |
| `IPlayGamesService` / `NullPlayGamesService` | Google Play Games v2 (Leaderboards, Online-Achievements). Android-Override via Factory. |

### Belohnungen & Daily-Content

| Service | Beschreibung |
|---------|-------------|
| `IDailyRewardService` / `DailyRewardService` | 7-Tage Login-Bonus + Comeback-Bonus (> 3 Tage inaktiv). Anti-Cheat-Hybridtimer (Tick64 + UTC). |
| `IDailyChallengeService` / `DailyChallengeService` | Tägliches deterministisches Level (Seed: `yyyy*10000+MM*100+dd`), Streak-Tracking. |
| `IDailyMissionService` / `DailyMissionService` | 3 tägliche Missionen aus 14er-Pool, Mitternacht-UTC-Reset. |
| `IWeeklyChallengeService` / `WeeklyChallengeService` | 5 wöchentliche Missionen aus 14er-Pool, Montag-Reset. |
| `ILuckySpinService` / `LuckySpinService` | 8 Segmente gewichtet, 1× gratis/Tag. Pity-Counter: nach 50 Spins garantierter Jackpot (`SpinsSinceLastJackpot` persistiert). `GetDropRates()` für Compliance. |
| `IBattlePassService` / `BattlePassService` | 30-Tier Saison, XP-basiert, Free/Premium-Track. XP-Boost via Hybridtimer. |

### Spieler-Progression

| Service | Beschreibung |
|---------|-------------|
| `IAchievementService` / `AchievementService` | 72 Achievements in 5 Kategorien, JSON-Persistenz, Dirty-Flag, `FlushIfDirty()` |
| `ICardService` / `CardService` | 14 Bomben-Karten, Deck (4+1 Slots), Upgrade, Crafting (5 Common→1 Rare, 5 Rare→1 Epic, 5 Epic→1 Legendary) |
| `IDungeonService` / `DungeonService` | Roguelike-Run-State, 16 Buffs, Eintritt-Logic (1×/Tag gratis, 500 Coins, 3 Gems, Rewarded) |
| `IDungeonUpgradeService` / `DungeonUpgradeService` | 8 permanente Dungeon-Upgrades (DungeonCoins) |
| `ICustomizationService` / `CustomizationService` | Spieler-Skins (98 Cosmetics: Trails/Frames/Victories) |
| `ICollectionService` / `CollectionService` | Sammlungs-Album: Gegner/Bosse/PowerUps, `FlushIfDirty()` |
| `IHeroService` / `HeroService` | 5 spielbare Heroes, `ActiveHero`, `Unlock(id)`, persistiert |
| `IDiscoveryService` / `DiscoveryService` | Erstentdeckungs-Tracking, `HasDiscovered(powerUpType)` |
| `ITutorialService` / `TutorialService` | 6-Schritt-Tutorial (T1/T2/T3-Phasen), `PhaseChanged`-Event |

### Platform & Premium

| Service | Beschreibung |
|---------|-------------|
| `IReviewService` / `ReviewService` | Google In-App Review API (Trigger nach Meilenstein) |
| `IStarterPackService` / `StarterPackService` | Einmal-Kauf-Angebot im ersten Start-Fenster |
| `IFirstPurchaseService` / `FirstPurchaseService` | ×2 Multiplier auf ersten IAP-Kauf, Anti-Reinstall. |
| `IGameAssetService` / `GameAssetService` | AI-generierte WebP-Bitmaps, LRU-Cache 30 MB. `Current`-Static-Accessor. `PlatformAssetLoader` (Android: `Assets.Open`). `Evict()` draint `_pendingDispose` auf UI-Thread. |
| `IGameTrackingService` / `GameTrackingService` | Session-Tracking, Spielzeit, Game-Events. `FlushIfDirty()`. |
| `IRetentionService` / `RetentionService` | `RegisterFirstWin()`, D1/D7-Detection, `ComebackEligible`. `TouchSession()` in LoadingPipeline. |
| `IAccessibilityService` / `AccessibilityService` | ColorblindMode/HighContrast/UiScale/SubtitlesEnabled. Persistiert. |
| `IAccountDeletionService` / `AccountDeletionService` | DSGVO Art. 17: Local→Firebase-Liga→CloudSave-Cascade. Best-Effort-Netz. |
| `IDataExportService` / `DataExportService` | DSGVO Art. 20: JSON + Human-Readable-Export. |
| `IHardwareProfileService` / `HardwareProfileService` | Low/Medium/High/Ultra-Tier via ProcessorCount + GC-Heuristik. Battery-Toggle, Thermal-Hook, `OnMemoryTrimRequested`. |
| `IPrivacyCenter` / `PrivacyCenter` | DSGVO/COPPA-Consent-Toggles, `AnalyticsConsent`. |
| `IBattlePassPlusService` + `IVipSubscriptionService` | Premium-Pass-Plus + VIP (Foundation) |
| `IMultiplayerSessionService` / `MultiplayerSessionService` | `MultiplayerMode`, `IsCoopEnabled/IsVersusEnabled`. Foundation. |

### Notification & Re-Engagement

| Service | Beschreibung |
|---------|-------------|
| `IPushNotificationService` / `NullPushNotificationService` | FCM-Token, `ScheduleLocalNotification`. Android: `AndroidPushNotificationService`. |
| `IReEngagementScheduler` / `ReEngagementScheduler` | D1/D3/D7 lokale Notification-Trigger. `ScheduleAll()` in `OnPause`, `CancelAll()` in `OnResume`. |
| `IWhatsNewService` / `WhatsNewService` | Versions-Modal-Control. `CurrentVersion` aus Assembly. RESX-Pflicht für alle Bullets. L-Helper statt `GetString??default`. |
| `IFeatureUnlockChoreographer` / `FeatureUnlockChoreographer` | Queue-basierte Feature-Unlock-Overlays. Schwellen: L10/L20/L30/L40/L50/L100. |
| `IWorldStoryService` / `WorldStoryService` | Welt-Intro/Outro-Cutscenes, one-shot per Lebenszeit. |

### Querschnitts-Services

| Service | Beschreibung |
|---------|-------------|
| `IGameEventBus` / `GameEventBus` | Pub/Sub: `RaiseFloatingText`, `RaiseCelebration`, `RaiseExitHint`, `RaiseMessage`. VMs routen Game-Juice hierüber. |
| `IBottomTabHub` / `BottomTabHub` | Tab-State + Pref-Persistenz für `BottomTabController`. |
| `ILoadoutService` / `LoadoutService` | Pre-Run Boosts pro Story-Level (max 2 Boosts). |
| `IBossRushService` / `BossRushService` | 5-Boss-Sequenz, ISO-8601-Year-Week-Reset. |

---

## Besondere Patterns

### Rewarded-Ad-Cooldown (`RewardedAdCooldownTracker.cs`)

```csharp
// Hybrid-Cooldown: TickCount64 (monoton) PLUS persistierte UtcNow
// OR-verknüpft: aktiv wenn eine der Uhren im Fenster
// 5 Placements: continue / level_skip / power_up / score_double / revival
```

Gleiche Hybridtimer-Strategie für `CoinService` (Daily-Bonus), `RetentionService`
(Comeback-Pack), `BattlePassService` (XP-Boost).

### PersistenceHealth (`PersistenceHealth.cs`)

```csharp
// Static-Klasse: Services rufen ReportCorruption(name, ex) bei JSON-Parse-Fehlern.
// CloudSaveService prüft WasCorruptionDetected in ALLEN drei Sync-Pfaden (Pull/Push/ForceUpload).
// Ohne diesen Check würde ein einzelner Parse-Fehler den Cloud-Save mit Leer-State überschreiben.
// Logger wird in App.axaml.cs nach BuildServiceProvider() gesetzt.
```

### DialogPresenter (`DialogPresenter.cs`)

```csharp
// IDialogPresenter-Impl als Singleton registriert.
// ShowConfirmAsync(): TaskCompletionSource-Roundtrip — ViewModel wartet auf User-Bestätigung.
// IsAnyDialogOpen = IsAlertDialogVisible || IsConfirmDialogVisible || IsWhatsNewVisible
// StateChanged-Event → MainViewModel.OnPropertyChanged(nameof(IsAnyDialogOpen))
```

### Logging (`Services/Logging/BomberBlastLoggerProviders.cs`)

Beide Provider sind in `App.axaml.cs.ConfigureServices` via `LoggerFactory.Create` eingehängt:
- `TraceLoggerProvider` → LogCat auf Android / Debug-Output auf Desktop
- `FileLoggerProvider` → `{LocalAppData}/BomberBlast/logs/app.log` (512 KB Cap, 1 Backup)

Build-Filter: `LogLevel.Trace` in DEBUG, `LogLevel.Information` im Release.
Strukturierte Log-Templates statt String-Interpolation verwenden.
