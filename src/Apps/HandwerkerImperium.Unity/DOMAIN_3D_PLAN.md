# HandwerkerImperium.Unity — Domain-Port-Roadmap + 3D-Remake-Leitlinie

Dependency-geordnete Rest-Roadmap des Domain-Ports (netstandard2.1 / C# 9, Unity 6000.4.8f1),
bewusst Zurückgestelltes, durchgehender 3D-Präsentationsplan und empfohlener nächster Schritt.
Erstellt aus einer Klassifikations-Analyse aller Rest-Typen (6 Slices) + zwei 3D-Plan-Streams.

Bereits portierte Schichten (verifiziert im Repo unter `Domain/`): `Economy`, `Orders`, `Crafting`,
`Progression`, `Research`, `Reputation`, `Buildings`, `Guild` (Enums + Kataloge). = Schicht 1–9.
Roadmap nummeriert ab **Schicht 10**.

---

## Verbleibende Domain-Port-Roadmap

Reihenfolge ist strikt **dependency-geordnet**. Verifikation pro Schicht (bestehende Harness, `D:\AI\_tmp\hwi_domain_*`):
- **Compat** = netstandard2.1-Compile (kein file-scoped ns, kein `[]`-Collection-Expr, kein `Random.Shared`,
  kein generisches `Enum.GetValues<T>()`, kein `init` ohne IsExternalInit, Newtonsoft statt System.Text.Json).
- **Werte** = Werte-Run gegen das Avalonia-Original (Formeln/Tabellen identisch).
- **Source-Diff** = Quelltext-Diff der Logik gegen Original (nur Hazard-Stellen dürfen abweichen).

Übergreifend: jedes `[JsonPropertyName]`/`[JsonIgnore]` → Newtonsoft `[JsonProperty]`/`[JsonIgnore]`.

### Schicht 10 — Katalog-/Wert-Enums (keine Abhängigkeiten)
Reine Wert-Kataloge, sofort portierbar, Fundament für alle Daten-Typen darüber. Enums:
DailyChallengeType, WeeklyMissionType, ToolType, DeliveryType, Season, BattlePassRewardType,
LuckySpinPrizeType, TournamentRewardTier, ManagerAbility, EquipmentType, EquipmentRarity,
WelcomeBackOfferType, LiveEventTemplate, VipTier (+Ext), MasterToolRarity (+Ext), MiniGameMasteryTier
(+Thresholds), GameEventType (+Ext), DailyBonusType, HintPosition, FtueExpectedAction, NotificationKind,
AchievementCategory, GuildMegaProjectType, WorkerAuctionStatus, CoopOrderStatus.
- **Hazard:** Enum-Integer-Reihenfolge MUSS identisch bleiben (Save-Kompatibilität). Extension-Farb-Methoden
  (z.B. `MasterToolRarity.GetColor()`) sind UI → weglassen; nicht-farbliche Mapping-/Schwellen-Logik bleibt.
- **Ziel:** je Feature-Unterordner `…/Enums/`.

### Schicht 11 — Effekt-/Reward-Structs & blattlose Daten
GameEventEffect, SeasonalItemEffect, BattlePassReward, TournamentLeaderboardEntry, GuildMegaProjectReward,
GuildMegaProjectDonation, AutoSellRule, CraftingJob, LiveEvent, ReferralProgress, WelcomeBackOffer, Friend,
CloudSaveMetadata, MiniGameStats, IProgressProvider.
- **Hazard:** `Friend.CreateSimulatedFriends()` nutzt `Random.Shared` → übergebene `System.Random`-Instanz
  (deterministischer Seed, reproduzierbarer Werte-Diff). `LiveEng` Collection-Expr → `new List<>()`.

### Schicht 12 — Tool/MasterTool/Manager/Achievement-Kataloge
Tool (+CreateDefaults), MasterToolDefinition + MasterTool, ManagerDefinition + Manager, Achievement +
Achievements.GetAll() (95+ Defs), LuckySpinPrize + LuckySpinState.
- **Hazard:** Collection-Expr in Katalogen → Liste. `ManagerDefinition`/`MasterToolDefinition` = positional
  records mit `init` → `{ set; }` / Ctor. `GetDisplayName()` zieht ILocalizationService → nur NameKey ausgeben.
  `MasterTool.CheckEligibility(state)` → Inputs als schmales DTO statt GameState.

### Schicht 13 — Event- & Seasonal-Engine
GameEvent (+Create), SeasonalEvent (+CheckSeason), SeasonalShopItem, BattlePass (+Generate*/AddXp).
- **Hazard:** `GameEvent.Create()` `Random.Shared` + generisches `Enum.GetValues<WorkshopType>()` →
  Instanz bzw. `(WorkshopType[])Enum.GetValues(typeof(WorkshopType))`. Collection-Expr → Liste.

### Schicht 14 — Daily/Weekly/Tournament-Live-Ops-State
DailyReward (+GetScaledMoney/30-Tage), DailyChallenge + DailyChallengeState, WeeklyMission +
WeeklyMissionState, Tournament, ShopOffer.
- **Hazard:** `Tournament.GenerateSimulatedOpponents()` + `ShopOffer.GenerateDaily()` `Random.Shared` →
  Instanz (Seed fixieren). Collection-Expr → Liste.

### Schicht 15 — Sammel-Daten-States & FTUE/Story-Daten
StatisticsData, SettingsData, AutomationSettings, BoostData, CosmeticData, GuildMembership (Offline-Cache),
FtueState, StoryChapter, SeasonStoryline, ContextualHint + ContextualHints-Katalog (30+).
- **Hazard:** Collection-Expr → Liste; `init`-sealed-classes → `{ set; }`.

### Schicht 16 — GameState-Root (Persistenz-Wurzel, SaveGame v7) — ZULETZT
`GameState` integriert ~20 Sub-States; erst portierbar wenn alle obigen stehen. Zusätzlich vorzuziehen:
`SupplierDelivery` + `WorkerMarketPool` (beide `Random.Shared` → Instanz).
- **Hazard:** System.Text.Json → Newtonsoft durchgängig; `Random.Shared` → Instanz; Collection-Expr-Init → Liste;
  Income/Cost-Caching-Verhalten exakt erhalten.
- **Verifikation (höchste Stufe):** Compat + **Save-Roundtrip** (v7-JSON laden→serialisieren→Feld-Diff leer) +
  voller Income-Pipeline-Werte-Run auf realem Save.

---

## Bewusst zurückgestellt

| Typ | Klasse | Wohin später | Begründung |
|-----|--------|--------------|------------|
| ActivePage, ImperiumSubTab | presentation | UI-Navigation | Reine View-Navigation, kein Domain-Wert. |
| GraphicsQuality | presentation | UI/Settings | Render-Setting; nur der Wert reist in SettingsData mit, Enum in UI. |
| GameGoal, PrestigeCinematicData | presentation | UI (Banner/Cinematic) | Lok-Keys + Routes + Render-Eingang, nicht persistiert. |
| NotificationItem, NotificationKind | presentation | UI (Bell) — Datenteil in GameState | RESX-Keys/Icons UI-gekoppelt; Persistenz-Teil reist als schlanker Record mit. |
| TutorialState, FtueStep, FtueExpectedAction (Step-Def) | presentation | UI (FTUE-Spotlight) | SpotlightAutomationId + Overlay-Logik; reiner Fortschritt (FtueState) ist portiert. |
| DailyProgressData (Container) | presentation/state | GameState-Feld | v7-Save-Feld; Welcome-Back-UI bleibt draußen. |
| Guild-Display-DTOs (GuildListItem, GuildDetailData, …, GuildWarDisplayData) | presentation | UI (Guild-Hub) | Reine Anzeige-DTOs mit berechneten UI-Props. |
| Firebase-DTOs (ChatMessage, CoopOrderState, FirebaseGuildData/-Member, Gift, GuildWar(+Season/Score/Log), GuildMegaProject, WorkerAuctionState, AvailablePlayerInfo, …) | networking | Firebase-Adapter | An Firebase-Pfade + HMAC gebunden; gehören nicht ins Offline-Core. Zugehörige Enums (CoopOrderStatus, WorkerAuctionStatus, GuildMegaProjectType) + Reward-Arithmetik → Schicht 10/11. |
| RemoteConfigKeys, AnalyticsEvents/-UserProperties | infra-catalog | Networking/Telemetrie | Lookup-Pfade/Telemetrie-Namen; an noch fehlende Infra gebunden. |
| DailyBundleOffer, CrossPromoApp | data (Feature offen) | Services/UI | Keine Service-Impl im Original; erst bei Feature-Verdrahtung. |
| *EventArgs (Models/Events/) | event-transport | Domain-Event-Bus/Services | C#9-kompatibel, kommen mit den feuernden Services. |

### Aus Services nur teil-extrahierbare Formeln (Service bleibt zurückgestellt, Formel-Kern → `Domain/.../*Formulas.cs`)

| Service | Extrahierbare Formel(n) → Zielklasse | Hazard |
|---------|--------------------------------------|--------|
| IncomeCalculatorService | GetPrestigeIncomeBonus, GetTotalHeirloomBonus, ApplySoftCap (Log2), CalculateCraftingSellMultiplier, CalculateGrossIncome (Effekte als Parameter-DTO) → `Domain/Economy/IncomeFormulas.cs` | DI-Inputs als Parameter |
| OfflineProgressService | Staffelung 0.80/0.35/0.15/0.05, ApplyBoostsProRata, Simulate*, GetMaxOfflineDuration → `Domain/Offline/OfflineProgressFormulas.cs` | Random.Shared → Instanz |
| WorkerService | Mood-Decay, Fatigue, Training ±0.05/h, Level-Up, Efficiency → `Domain/Economy/WorkerFormulas.cs` | file-scoped ns/Range |
| OrderGeneratorService | DetermineOrderType, GetDifficulty, GenerateCustomerName(seed) → `Domain/Orders/OrderGenerationFormulas.cs` | Collection-Expr |
| MarketService | ComputeDailyFactor (Seed+Sinus ±50%), Buy/SellPrice (Spread 5%), 24h-Serie → `Domain/Warehouse/MarketFormulas.cs` | eigener stabiler Hash |
| CraftingService | CraftingSpeedBonus (Cap 50%), MaterialAffinity (+20%), CanStoreOutput → `Domain/Crafting/CraftingFormulas.cs` | — |
| AutoProductionService | GetProductionInterval, CalculateItemsProducedOffline, IsAutoProductionUnlocked → `Domain/Production/AutoProductionFormulas.cs` | — |
| DailyChallengeService | GetTier, GetAllCompletedBonusGS + Reward-Tabellen → `Domain/LiveOps/Daily/DailyChallengeFormulas.cs` | `Enum.GetValues<T>` |
| EquipmentService | CalculateDropChance (0.05 + diff×0.05 + perfect×0.05) → `Domain/Economy/EquipmentFormulas.cs` | Random.Shared → Instanz |
| GuildBossService | CalculateBossHp (×max(0.5, members/5)), ApplyDamageMultiplier, GetRewardByRank → `Domain/Guild/GuildBossFormulas.cs` | System.Text.Json→Newtonsoft, Range |

---

## 3D-Präsentationsplan (durchgehend)

Leitlinie: **Mechanik/Werte bleiben 1:1 zum Avalonia-Original** — nur die PRÄSENTATION wird 3D. Jedes der
~59 2D-SkiaSharp-Systeme bekommt ein 3D-Pendant. Stil-Lock: URP/Toon-Shader + Outlines, Amber #D97706 Primary,
Tier-Farbkodierung konsistent. Asset-Ökonomie: **1 Basis-Mesh + 5 Decal-Material-Sets** statt 5 Modelle
(~65% Zeitersparnis); aggressives Recycling über Szenen (Worker im Hub + Workshop + Guild-Hall; Crafting-Produkte
in Lager + Markt + Shop).

| # | System | Was wird 3D + wie | Asset-Bedarf | Aufwand |
|---|--------|-------------------|--------------|---------|
| 1 | Dashboard/City-Hub | 10 Werkstätten im Kreis, Cinemachine Orbit+Pinch-Zoom, Tag/Nacht+saisonal Light, URP Bloom/Grading/Vignette, Upgrade via 5 Decal-Sets, Idle-Partikel, Game-Juice (Münz-Fly/Shake/Floating-Text/Burst) | 10 Workshop-Basis (TRELLIS 2) + 5 Decals; 4 Skyboxen; 3 LODs | M (4–6 Wo) |
| 2 | Workshop-Interior | Additive Innen-Szene, Orbit-Cam, Fenster-Shader, sichtbare Worker in Arbeits-Pose, Equipment auf Bones, 3D-Auftragstafel, Crafting-Regale ab Lv50 | Interior-Mesh + 16 Equipment-Props + 5 Affinity-Props + Tafel | L (8–12 Wo) |
| 3 | Arbeiter (Character) | 20 Basis-Chars (10 Tiers × m/w), Tier-Recolor (Legendary Gold-Emissive), Mixamo-Rig, 4 Mood-Face-Swaps, Equipment/Affinity an Hand-Bone, Animator an Mood-Schwellen, 3D-Audio | 20 Modelle + 80 Mood-Tex + 160 Anim-Slots (80% Mixamo) | L (6–8 Wo) — Anim-Bottleneck |
| 4 | Mini-Games (10) | Additive Vollbild-Szene je Game, fixed Cam, 10 distinkte 3D-Props, UI bleibt 2D (UXML) mit Floating-Combo+Burst, Rating 1:1 | 10 Props + 3 Spezial-Shader (Wasser/Emissive/Hologramm) + 10 Partikel | M (3–4 Wo) |
| 5 | Aufträge | 3D-Tafel mit Live-Countdown, VIP-Gold-Shimmer, Material-Glow-Badge+Stacks, Risk/Safe-Farbkodierung, Spawn-Blink, Complete-Konfetti | Tafel + 5 Material-Stacks + TMP/DOTween + Stinger | S (1–2 Wo) |
| 6 | Crafting-Produkte & Lager | 33 Produkt-Modelle (T1→T4 mit 5 Bauphasen), additive Lager-Szene mit Regalen + instanzierte Stacks, Stack-Warnung, Auto-Sell-Partikel, Markt-Heatmap+LineRenderer | 33 Produkte + 3 T4-Heroes + 4 Regale + LODs | M (3–4 Wo) |
| 7 | Master-Tools (12) | 12 Artefakt-Modelle, Rarity-Recolor+Emissive, auf Altar im Hub, Unlock-Materialize+Voice | 12 Tools + 5 Rarity-Mats + Altar + Stinger/Voice | S (1–2 Wo) |
| 8 | Forschung (Lab) | 3D-Lab-Interior, aktive Forschung visualisiert (Mikroskop/Reagenzglas-Shader/Progress-Röhrchen), Completed-Burst, Tree bleibt 2D-Canvas | Lab + Mikroskop + 5 Reagenzgläser + Flüssig-Shader | M (2–3 Wo) |
| 9 | Gilde/Guild-Hub | 3D-Halle progressiv, Mitglieder instanziert mit Tabard, Boss-Arena (6 Boss-Modelle + Attack-VFX), 10 Hall-Gebäude (1+5 Decals), 2 Mega-Projekte (5 Bauphasen), Liga-Podium | Halle + 6 Bosse+Rig+Anims + 10 Gebäude + 10 Mega-Module | L (6–8 Wo) — Boss-Anims |
| 10 | Prestige-Cinematic | 4-Phasen-3D (Gold-Regen→Badge→Multiplier-Text→Reward-Burst), Tier-Roadmap (7 Medals), Heirloom-Drehplattform, Orchestral+Voice | Badge + 7 Medals + Heirlooms + Theme | M (2–3 Wo) |
| 11 | Imperium-Tab | Workshop-Karussell (3D-Thumbs+Spec-Aura), 7 Building-Previews (1+5 Decals), Equipment-Showcase-Drehplattform | 7 Buildings + Decals; Equipment recycelt | M (2–3 Wo) |
| 12 | City-Tiles/Environments | 80 Bodenelemente als 10–15 modulare Variationen + Instanzierung, 8 World-Tier-Themes (rein ästhetisch), saisonale Overrides, 1 Atlas pro World-Tier | 80 Tiles (TripoSG-Batch) + Atlas + 8 Skyboxen | L (4–6 Wo) — parallelisierbar |
| 13 | Audio | 10+ Musik-Loops, 150–200 SFX, 3D-Positional, Meister-Hans-Voice 6 Sprachen (~900 Files), Worker-Reactions | Stable Audio 3 + ElevenLabs; OGG q0.6; Addressables | M (2–3 Wo) |
| 14 | Shop | Optionale 3D-Marktstand-Szene, IAP-3D-Thumbs (recycelt), Gold-Glow Premium | Vitrine + Gold-Partikel | S (1 Wo) |
| 15 | Settings/Grafik | Quality-Tiers steuern Post-FX/Particle-Cap/LOD/Compression; Ziel 60 FPS Mobile / 90+ Desktop, <500 MB | keine neuen Assets; 2–4 Shader-Varianten | S (1–2 Wo) |

**Gesamtaufwand:** 14–18 Wo Alpha-MVP (4–6 GPU-Workstations), 20–24 Wo Beta. Kritischer Pfad = Worker- + Boss-Animation (Cascadeur).

### Asset-Pipeline — nächste Schritte
Lokale Pipeline (SDXL→TRELLIS 2/SPAR3D/TripoSG→Blender→Substance→Mixamo/Cascadeur→Unity URP/Addressables→
Stable Audio 3/ElevenLabs) deckt ~90% ab; ~5% Hero-Assets (6 Bosse, 2 Mega-Projekte, opt. Prestige-Hero) via
Cloud-Fallback (Rodin Gen-2.5). Worker-Rig-Fallback AccuRIG 2/Tripo.
**Pilot (Woche 1–2, Go/No-Go):** Toon-LoRA trainieren → 6 Test-Konzepte → TRELLIS 2 → Blender-Cleanup (<15 min/Asset)
→ Worker-F-Tier nach Mixamo → Unity-Import+Addressables+LOD+ASTC → Test-Hub >30 FPS Low-End-Android.
**Go-Kriterien:** LoRA konsistent · Topologie riggbar · Cleanup <15 min · Mixamo akzeptiert Worker · >30 FPS.
**Batch (Woche 3–8):** 10 Werkstätten → 20 Worker+Mixamo → 48 Props. Parallel Scripts: `hwi_unity_batch_cleanup.py`,
`hwi_unity_workshop_modular.py`, ComfyUI-Nacht-Queue. Voice-Batch = Phase 2.

---

## Empfohlener nächster Schritt
**Schicht 10 (Wert-Enums) zuerst** — kleinste unabhängige Einheit, Fundament für Schicht 11–16 (fast jeder
Daten-/State-Typ referenziert ein Enum), persistenz-kritisch aber risikoarm (Integer-Reihenfolge per Compat+Diff
trivial verifizierbar). Danach Schicht 11 → 12 → … → GameState (16) strikt zuletzt mit v7-Save-Roundtrip als Gesamt-Gate.
Der 3D-Pipeline-Pilot kann parallel und ressourcen-unabhängig sofort starten.
