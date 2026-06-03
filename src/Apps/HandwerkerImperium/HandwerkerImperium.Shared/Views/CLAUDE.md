# Views — AXAML-Views & UI-Patterns

Alle Views folgen ViewModel-First: DataContext wird vom ViewLocator gesetzt, nie im Code-Behind.
`x:CompileBindings="True"` + `x:DataType` auf jeder View-Root ist Pflicht.
Generische AXAML-Patterns → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## View-Struktur

```
Views/
├── MainView.axaml          # 5-Tab-Navigation, Dialoge als UserControls
├── DashboardView.axaml     # City-Skyline + Workshop-Karten (Code-Behind: IUiEffectBus + Render-Loop)
├── ImperiumView.axaml      # Sub-Tab-Router (kein Code-Behind)
├── MissionenView.axaml     # Daily/Weekly/QuickJobs/LuckySpin
├── GuildView.axaml         # 5-Tab-Hub (Übersicht/Kampf/Forschung/Chat/Mitglieder)
├── ShopView.axaml          # IAP, GS-Pakete, Whale-Bundles
├── SettingsView.axaml      # Grafik, Audio, Sprache, Premium, CrossPromo, Referral
├── OrderView.axaml         # Auftrags-Details, MaterialOffer
├── PrestigeView.axaml      # Tier-Auswahl, Challenges, Heirloom-Selection (Direct-Bound)
├── WorkshopView.axaml      # Workshop-Kauf/Upgrade/Spezialisierung/Rebirth
├── WorkerMarketView.axaml  # Marktpool, Hire
├── WorkerProfileView.axaml # Training, Bonus, Praktikant-Promotion
├── ResearchView.axaml      # 45-Node Forschungsbaum
├── BuildingsView.axaml     # 7 Gebäude
├── ManagerView.axaml       # Manager-Unlocks
├── CraftingView.axaml      # Crafting-Rezepte, aktive Jobs
├── MarketView.axaml        # Material-Markt, Heatmap
├── AscensionView.axaml     # 6 Perks × MaxLevel 3
├── BattlePassView.axaml    # 30-Tier BattlePass
├── SeasonalEventView.axaml # Saisonales Event, SP-Shop
├── TournamentView.axaml    # Turnier-Wettbewerb
├── LuckySpinView.axaml     # Glücksrad
├── AchievementsView.axaml  # Achievement-Liste
├── StatisticsView.axaml    # Spielstatistiken
├── ReputationShopView.axaml # Reputations-Shop
├── FtueOverlay.axaml       # FTUE-Spotlight-Overlay
├── MainWindow.axaml        # Desktop-Container (kein Android-Äquivalent)
├── Dashboard/              # DashboardHeader, AutomationPanel, BannerStrip, OrdersQuickJobsSection,
│                           #   DailyChallengeSection, WeeklyMissionSection
├── Imperium/               # WorkshopsSection, WarehouseSection, WorkersSection,
│                           #   ResearchSection, EquipmentSection, AscensionSection
├── Dialogs/                # AchievementDialog, StoryDialog, ContextualHintDialog, AlertDialog,
│                           #   ConfirmDialog, PrestigeSummaryDialog, DailyRewardDialog,
│                           #   OfflineEarningsDialog, WelcomeBackOfferDialog,
│                           #   NotificationCenterPopup, WorkerProfileDialog
├── Guild/                  # GuildHallView, GuildResearchView, GuildWarView, GuildBossView,
│                           #   GuildWarSeasonView, GuildChatView, GuildMembersView,
│                           #   GuildCoopOrderView, GuildInviteView, GuildAchievementsView,
│                           #   GuildMegaProjectView (Mega-Projekt Material-Spenden-Pipeline)
├── Auctions/               # WorkerAuctionView
├── Settings/               # CrossPromoCard, ReferralCard
└── MiniGames/              # 10 MiniGame-Views (eine pro WorkshopType)
```

---

## MainView-Layout-Pattern

```axaml
<!-- Row 0: Content (Direct-Bound + ContentControl für Sub-Pages) -->
<!-- Row 1: TabBar (SKCanvasView, 68dp) — bei Sub-Views durch Breadcrumb-Leiste ersetzt -->
<Grid RowDefinitions="*,Auto">
```

**Kein separater Ad-Spacer als eigene Row** — HandwerkerImperium hat kein Banner-Ad (nur Rewarded).
Der 60dp `Margin` am unteren Ende von ScrollViewer-Inhalten gilt für die TabBar-Höhe (68dp).

**4 Direct-Bound-Views** (bleiben dauerhaft materialisiert, `IsVisible`-Toggle für schnelle Tab-Wechsel):
- `DashboardView` (`IsDashboardActive`)
- `ImperiumView` (`IsBuildingsActive`)
- `MissionenView` (`IsMissionenActive`)
- `PrestigeView` (`IsPrestigeActive`)

**Lazy-Loading via ContentControl**: Alle anderen Sub-Views (GuildView, ShopView, WorkshopView, …)
laufen über `ContentControl Content="{Binding ActivePageContent}"`. ViewLocator materialisiert
die Sub-View erst beim ersten Switch — kein Speicher für inaktive Views.

---

## DashboardView Code-Behind

`DashboardView.axaml.cs` hat das umfangreichste Code-Behind aller Views — erlaubt, weil es
ausschließlich UI-Rendering und UI-Effekte umfasst:

- **City-Skyline-Render-Loop** via `IFrameClock` (5/10/30fps adaptiv) — DashboardIdle bei
  ruhigem Zustand, DashboardActive bei aktiven Juice-Effekten
- **Workshop-Karten-Touch-Handling** — Tap-vs-Scroll-Erkennung (WorkshopCardHitTester),
  Hold-to-Upgrade-Timer (120ms Tick), Scroll-Abbruch via Tunnel-Events
- **`IUiEffectBus`-Subscription** für FloatingText und Partikel-Effekte:

```csharp
// Im Ctor (Bus ist Singleton — kein VM-Lifecycle-Abhängigkeit)
_uiEffectBus.FloatingTextRequested += OnFloatingTextRequested;    // Text-Overlay
_uiEffectBus.FloatingTextRequested += OnFloatingTextForParticles; // Münz-Partikel + Confetti

// Cleanup in OnDetachedFromVisualTree
_uiEffectBus.FloatingTextRequested -= OnFloatingTextRequested;
_uiEffectBus.FloatingTextRequested -= OnFloatingTextForParticles;
```

- **Parallax-Scroll**: `ScrollViewer.ScrollChanged` → `HeaderBorder.RenderTransform`
  (`translateY = -offset * 0.3`, max 20px), gecacht via `_headerBorder`-Field
- **Live-Countdown-Timer** (1Hz `DispatcherTimer`) — aktualisiert INPC-Events auf Live-Auftrags-POCOs
  (`RaiseLiveCountdownChanged()`) damit der rote LIVE-Badge synchron bleibt

Kein ViewModel-Code im Code-Behind — kein Navigieren, keine Geschäftslogik.

---

## Scroll-Pattern

`ScrollViewer`-Kind-Elemente brauchen mindestens 60dp `Margin` (NICHT `Padding`) am unteren
Ende für die TabBar. Avalonia `Padding` auf `ScrollViewer` verhindert Scrollen.

---

## Gotcha — ActivePage und IsHitTestVisible

`IsHitTestVisible`-Properties AUSSCHLIESSLICH per XAML-Binding steuern.
Ein CLR-Setter im Code-Behind verdrängt das Binding dauerhaft (LocalValue-Precedence).
Für Overlay-Hit-Tests: `IsHitTestVisible="{Binding !IsAnyOverlayOpen}"` im XAML.

## Gotcha — CommandParameter ist immer string

```axaml
<!-- FALSCH: CommandParameter="0" → RelayCommand<int> wirft ArgumentException -->
<!-- RICHTIG: -->
<sys:Int32>0</sys:Int32>
<!-- oder Methode auf string + int.TryParse() intern -->
```

## Gotcha — Prestige-View BindingPath mit ElementName

Heirloom-Auswahl-Buttons brauchen `ElementName=PrestigeRoot` + `((vm:MainViewModel)DataContext).`-Cast
für Commands die auf MainViewModel liegen (während der DataContext im inneren Scope auf
`PrestigeConfirmationViewModel` gewechselt hat). Pattern analog zur Tier-Auswahl-Implementierung.
