# Views — AXAML-Views & UI-Patterns

26 Views + 5 Components. ViewModel-First, Compiled Bindings Pflicht.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
App-Überblick → [../../CLAUDE.md](../../CLAUDE.md).

---

## Haupt-Views

| Datei | VM | Besonderheit |
|-------|----|-------------|
| `MainView.axaml(.cs)` | `MainViewModel` | Root-ContentControl, `IsHitTestVisible="{Binding !IsAnyDialogOpen}"`, Tab-Routing |
| `GameView.axaml(.cs)` | `GameViewModel` | SKCanvasView, 3-stufige VM-Subscription, `IsHitTestVisible="{Binding !IsAnyOverlayOpen}"` |
| `MainMenuView.axaml(.cs)` | `MainMenuViewModel` | Dashboard + Season-Banner |
| `LevelSelectView.axaml(.cs)` | `LevelSelectViewModel` | Welt-Grid via SkiaSharp-Visualization |
| `ShopView.axaml(.cs)` | `ShopViewModel` | Torn-Metal-Buttons, scrollbare Sektions-Liste (Upgrades + 8 Sektionen: PowerUps, Mechanics, Skins, BombSkins, ExplosionSkins, Trails, Victories, Frames) |
| `BattlePassView.axaml(.cs)` | `BattlePassViewModel` | **Einzige echte Virtualisierung**: `ListBox + VirtualizingStackPanel Horizontal` (60+ Tiers) |
| `DungeonView.axaml(.cs)` | `DungeonViewModel` | Node-Map, Buff-Selection-Overlay |
| `LeagueView.axaml(.cs)` | `LeagueViewModel` | Firebase-Leaderboard-Anzeige |
| `ProfileView.axaml(.cs)` | `ProfileViewModel` | Cosmetics-Galerie, DSGVO-Sektion |
| `DeckView.axaml(.cs)` | `DeckViewModel` | Karten-Grid, Crafting-Panel |
| `PlayHubView.axaml(.cs)` | `PlayHubViewModel` | Bottom-Tab "Spielen" (Modi-Auswahl) |
| `GemShopView.axaml(.cs)` | `GemShopViewModel` | IAP-Gem-Pakete |
| `SettingsView.axaml(.cs)` | `SettingsViewModel` | Audio/Visual/Accessibility/Privacy |
| `AchievementsView.axaml(.cs)` | `AchievementsViewModel` | 72 Achievements in 5 Kategorien |
| `CollectionView.axaml(.cs)` | `CollectionViewModel` | Gegner/Bosse/PowerUps-Album |
| `StatisticsView.axaml(.cs)` | `StatisticsViewModel` | Session-Statistiken |
| `HighScoresView.axaml(.cs)` | `HighScoresViewModel` | Top-10 local + Liga-Link |
| `HelpView.axaml(.cs)` | `HelpViewModel` | Tutorial/Hilfe-Texte |
| `GameOverView.axaml(.cs)` | `GameOverViewModel` | Rewarded-Ad-Buttons (Continue/LevelSkip/Revival) |
| `VictoryView.axaml(.cs)` | `VictoryViewModel` | Sterne-Anzeige + Score-Double-Rewarded |
| `BossRushView.axaml(.cs)` | `BossRushViewModel` | Boss-Rush-Modus-Auswahl |
| `LuckySpinView.axaml(.cs)` | `LuckySpinViewModel` | Glücksrad-Animation |
| `WeeklyChallengeView.axaml(.cs)` | `WeeklyChallengeViewModel` | 5 wöchentliche Missionen |
| `DailyChallengeView.axaml(.cs)` | `DailyChallengeViewModel` | Tages-Level-Vorschau |
| `QuickPlayView.axaml(.cs)` | `QuickPlayViewModel` | Schnell-Spiel-Konfiguration |
| `MainWindow.axaml(.cs)` | — | Desktop-only Fenster-Wrapper |

---

## Components (`Views/Components/`)

| Datei | Zweck |
|-------|-------|
| `BottomTabBar.axaml(.cs)` | **Singleton BottomTabBarViewModel** — DataContext per `{Binding BottomTabVm}` aus MainViewModel gesetzt. 4 Tabs (Home, Play, Shop, Profile) mit GameIcon. |
| `WhatsNewOverlay.axaml(.cs)` | **Transient WhatsNewViewModel** — Modal-Overlay. Getrennte DataContext-/x:DataType-Ebenen (AXAML-Compiled-Binding-Pattern, korrekt in Avalonia 12). |
| `DailyRewardOverlay.axaml(.cs)` | Daily-Login-Bonus-Anzeige, animiert. |
| `OnboardingOverlay.axaml(.cs)` | D0-Modal-Gate — verhindert Neulings-Überforderung am ersten Start. |
| `SeasonBanner.axaml(.cs)` | Saison-/Event-Banner im MainMenu. |

---

## Compiled-Bindings-Pflicht

```axaml
<UserControl x:CompileBindings="True" x:DataType="vm:ShopViewModel">
    <!-- DataTemplates in ItemsControl MÜSSEN x:DataType angeben: -->
    <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="models:ShopDisplayItem">
            <!-- Parent-Command in DataTemplate: -->
            <Button Command="{Binding $parent[ItemsControl].((vm:ShopViewModel)DataContext).PurchaseCommand}"
                    CommandParameter="{Binding UpgradeType}" />
        </DataTemplate>
    </ItemsControl.ItemTemplate>
```

---

## GameView — 3-stufige VM-Subscription

`ContentControl + ViewLocator` setzt DataContext verzögert → `InvalidateCanvasRequested`
kann beim `StartGameLoop()` keinen Subscriber haben → Render-Timer startet nie.

```csharp
// TrySubscribeToViewModel() als zentrale idempotente Methode:
// (1) OnDataContextChanged
// (2) OnLoaded als Backup
// (3) OnPaintSurface Safety-Net — startet Timer wenn noch kein Subscriber
```

---

## Overlay-Sichtbarkeit und ZIndex

Keine `ZIndex`-basierten interaktiven Overlays — auf Android geht Touch durch ZIndex-Overlays.
Stattdessen Content-Swap: `IsVisible=false` für normalen Content, Overlay-Content als Ersatz.
`IsHitTestVisible`-Aggregate via Compiled-Bindings (nie Code-Behind-Setter).
Details → App-Root-CLAUDE.md (Gotchas: ZIndex-Overlay-Touch, Overlay-Hit-Test-Aggregate).
