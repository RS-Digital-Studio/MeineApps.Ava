# Views — AXAML-Views & UI-Patterns

Alle Views folgen ViewModel-First: DataContext wird vom ViewLocator gesetzt, nie im Code-Behind.
`x:CompileBindings="True"` + `x:DataType` auf jeder View-Root ist Pflicht.
Generische AXAML-Patterns → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## View-Struktur

```
Views/
├── MainView.axaml          # 5-Tab-Navigation, Dialoge als UserControls
├── DashboardView.axaml     # 423 Z., Header+City+Workshop-Grid (Code-Behind für IUiEffectBus)
├── ImperiumView.axaml      # 171 Z., Sub-Tab-Router (kein Code-Behind)
├── MissionenView.axaml     # Daily/Weekly/QuickJobs/LuckySpin
├── GuildView.axaml         # 5-Tab-Hub (Übersicht/Kampf/Forschung/Chat/Mitglieder)
├── ShopView.axaml          # IAP, GS-Pakete, Whale-Bundles
├── SettingsView.axaml      # Grafik, Audio, Sprache, Premium, CrossPromo, Referral
├── OrderView.axaml         # Auftrags-Details, MaterialOffer
├── PrestigeView.axaml      # Tier-Auswahl, Challenges, Heirloom-Selection
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
├── Dashboard/              # AutomationPanel, BannerStrip, OrdersQuickJobsSection,
│                           #   DailyChallengeSection, WeeklyMissionSection
├── Imperium/               # WorkshopsSection, WarehouseSection, WorkersSection,
│                           #   ResearchSection, EquipmentSection, AscensionSection
├── Dialogs/                # AchievementDialog, StoryDialog, HintDialog, LevelUpDialog,
│                           #   AlertDialog, ConfirmDialog, PrestigeConfirmationDialog,
│                           #   NotificationCenterView, WhatsNewDialog
├── Guild/                  # GuildView-Sub-Views (Research, Boss, Hall, War, Chat, Members, ...)
│                           #   GuildBuildSiteView (Mega-Projekt)
└── MiniGames/              # 10 MiniGame-Views (eine pro WorkshopType)
```

---

## MainView-Layout-Pattern

```axaml
<!-- Row 0: Content — 4 Direct-Bound + 1 ContentControl für Sub-Pages -->
<!-- Row 1: Ad-Spacer 64dp (kein Banner bei BomberBlast, aber HI hat Banner) -->
<!-- Row 2: GameTabBarRenderer (SkiaSharp, kein XAML Tab-Bar) -->
<Grid RowDefinitions="*,Auto,Auto">
```

**Lazy-Loading via ContentControl**: Statt 25+ `IsVisible`-Views ein einzelnes
`ContentControl Content="{Binding ActivePageContent}"`. Der ViewLocator rendert nur
die aktive Sub-Page. Die 4 Haupt-Tabs (Dashboard/Imperium/Missionen/GuildView) bleiben
Direct-Bound mit `IsVisible` für schnelle Tab-Wechsel ohne ViewLocator-Overhead.

---

## DashboardView Code-Behind

`DashboardView.axaml.cs` ist die einzige View mit substantiellem Code-Behind —
**erlaubt** weil es ausschließlich Bus-Subscription für UI-Effekte ist:

```csharp
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);
    _uiEffectBus.FloatingTextRequested += OnFloatingText;
    _uiEffectBus.CelebrationRequested  += OnCelebration;
}

protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    _uiEffectBus.FloatingTextRequested -= OnFloatingText;
    _uiEffectBus.CelebrationRequested  -= OnCelebration;
    base.OnDetachedFromVisualTree(e);
}
```

Kein ViewModel-Code im Code-Behind — nur Bus-Subscription und Canvas-Weiterleitung.

---

## Scroll-Pattern

`ScrollViewer`-Kind-Elemente brauchen mindestens 60dp `Margin` (NICHT `Padding`) am unteren
Ende für das Ad-Banner. Avalonia `Padding` auf `ScrollViewer` verhindert Scrollen.

---

## Imperium-Sub-Tabs

`ImperiumSubTab`-Enum (V7): Workshops / **Warehouse** / Workers / Research / Equipment / Ascension.

- Warehouse-Tab: Immer sichtbar, gesperrt via Lock-Icon-Overlay bis Spielerlevel 50
- Ascension-Tab: Immer sichtbar, gesperrt via Lock-Icon-Overlay bis `LegendeCount >= 3`
- Beide IMMER sichtbar (Layout-Stabilität) — KEIN `IsVisible=false`

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
