# HandwerkerImperium (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Idle-Game: Baue dein Handwerker-Imperium auf, stelle Mitarbeiter ein, kaufe Werkzeuge, erforsche Upgrades, schalte neue Workshop-Typen frei. Verdiene Geld durch automatische Auftraege oder spiele Mini-Games.

**Version:** 2.0.3 | **Package-ID:** com.meineapps.handwerkerimperium | **Status:** Geschlossener Test

## Haupt-Features

- **8 Workshop-Typen** (Tischlerei, Malerei, Sanitär, Elektrik, Landschaftsbau, Renovierung, Architekt, Generalunternehmer)
- **8 Mini-Games** (Sawing, Pipe Puzzle, Wiring, Painting, RoofTiling, Blueprint, DesignPuzzle, Inspection)
- **Worker-System** mit 10 Tiers (F/E/D/C/B/A/S/SS/SSS/Legendary)
- **Goldschrauben-Economy** (Premium-Währung für Boosts/Unlock)
- **Research Tree** (45 Upgrades in 3 Branches: Tools, Management, Marketing)
- **7 Gebäude** (Canteen, Storage, Office, Showroom, TrainingCenter, VehicleFleet, WorkshopExtension)
- **Daily Challenges** (3 pro Tag)
- **Daily Login Rewards** (30-Tage-Zyklus mit steigenden Belohnungen)
- **Achievements** (33 Erfolge)
- **Prestige-System** (3 Stufen: Bronze ab Lv.30, Silver ab Lv.100, Gold ab Lv.250)
- **Events** (8 zufällige Events + saisonaler Multiplikator)
- **Bulk Buy** (x1/x10/x100/Max für Workshop-Upgrades)
- **Milestone-Celebrations** (Level 10/25/50/100/250/500/1000 mit Goldschrauben-Bonus)
- **Statistiken** (Gesamt-Verdienst, Workshop-Verteilung)
- **Feierabend-Rush** (2h 2x-Boost, 1x täglich gratis, danach 10 Goldschrauben)
- **Meisterwerkzeuge** (12 sammelbare Artefakte mit passiven Einkommens-Boni, 5 Seltenheitsstufen)
- **Lieferant-System** (Variable Rewards alle 2-5 Minuten: Geld, Schrauben, XP, Mood, Speed)
- **Prestige-Shop** (12 Items: Income, XP, Mood, Kosten, Rush, Lieferant, Goldschrauben)

## Premium & Ads

### Premium-Modell
- **Preis**: 4,99 EUR (Lifetime)
- **Vorteile**: +50% Einkommen, +100% Goldschrauben aus Mini-Games, keine Werbung

### Rewarded (9 Placements)
1. `golden_screws` → 5 Goldschrauben (Dashboard)
2. `score_double` → Mini-Game Score verdoppeln
3. `market_refresh` → Arbeitermarkt-Pool neu wuerfeln
4. `workshop_speedup` → 2h Produktionsertrag sofort (WorkshopView)
5. `workshop_unlock` → Workshop ohne Level freischalten (Dashboard)
6. `worker_hire_bonus` → +1 Worker-Slot persistent (WorkerMarket)
7. `research_speedup` → Forschung sofort fertig gratis (ResearchView)
8. `daily_challenge_retry` → Challenge-Fortschritt zuruecksetzen (Dashboard)
9. `achievement_boost` → Achievement Progress +20% (AchievementsView)

## Architektur-Besonderheiten

### Game Loop
- **GameLoopService** (1-Sekunden-Takt via DispatcherTimer) → Idle-Einkommen, Kosten, Worker-States, Research-Timer, Event-Check
- **AutoSave** im GameLoop (alle 30 Sekunden) → GameState → JSON via SaveGameService
- **Research-/Gebäude-Effekte** werden pro Tick angewendet (EfficiencyBonus, CostReduction, WageReduction, ExtraWorkerSlots)

### Workshop-Typen
Enum: `Carpenter`, `Plumber`, `Electrician`, `Painter`, `Roofer`, `Contractor`, `Architect`, `GeneralContractor`
Jeder Typ hat: `BaseIncomeMultiplier`, `UnlockLevel`, `UnlockCost`, `RequiredPrestige`

### Worker-System
10 Tiers via Enum: `F` (0.5x), `E` (0.75x), `D` (1.0x), `C` (1.3x), `B` (1.7x), `A` (2.2x), `S` (3.0x), `SS` (4.5x), `SSS` (7.0x), `Legendary` (12.0x)
`HireWorker()` → kostet Geld, erhoeht Workshop-Effizienz
Tier-Farben: F=Grau, E=Gruen, D=#0E7490(Teal), C=#B45309(DarkOrange), B=Amber, A=Rot, S=Gold, SS=Lila, SSS=Cyan, Legendary=Rainbow-Gradient
**3 Training-Typen**: Efficiency (XP→Level→+Effizienz), Endurance (senkt FatiguePerHour, max -50%), Morale (senkt MoodDecayPerHour, max -50%). TrainingType Enum + Worker.EnduranceBonus/MoraleBonus (persistiert). Training-Typ-Auswahl + Echtzeit-Fortschrittsbalken in WorkerProfileView.

### Goldschrauben-Quellen
1. Mini-Games (3-10 Schrauben pro Win)
2. Daily Challenges (20 Schrauben)
3. Achievements (5-50 Schrauben)
4. Rewarded Ad (5 Schrauben)
5. IAP-Paket (100/500/2000 Schrauben)
6. Daily Login Rewards (1-25 Schrauben je nach Tag im 30-Tage-Zyklus)
7. Milestone-Level (3-200 Schrauben bei Level 10/25/50/100/250/500/1000)

### Research Tree
45 Upgrades in 3 Branches à 15 Level: Tools (Effizienz + MiniGame-Zone), Management (Löhne + Worker-Slots), Marketing (Belohnungen + Order-Slots)
Kosten: Geld (500 bis 1B). Dauer: 10min bis 72h (Echtzeit). Effekte werden im GameLoop auf Einkommen/Kosten angewendet.

### Mini-Games
- **Sawing**: Holz im richtigen Bereich sägen (Timing + Präzision)
- **Pipe Puzzle**: Rohre drehen um Durchfluss zu schaffen
- **Wiring**: Farbige Kabel links-rechts verbinden
- **Painting**: Zielzellen anmalen ohne Fehler
- **Blueprint**: Bauschritte in korrekter Reihenfolge antippen (Sequenz-Memory)
- **RoofTiling**: Dachziegel im korrekten Farbmuster platzieren (Pattern-Match, Dachdecker)
- **Blueprint**: Bauschritte in korrekter Reihenfolge antippen (Sequenz-Memory, Bauunternehmer)
- **DesignPuzzle**: Räume im Grundriss zuordnen (Drag&Drop, Architekt)
- **Inspection**: Mängel auf der Baustelle finden (Suchbild, Grid-basiert, Generalunternehmer)

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| `GameLoopService` | 1s-Takt Loop: Einkommen, Kosten, Worker-States, AutoSave (30s) |
| `GameStateService` | Zentraler State mit Thread-Safety (lock) |
| `SaveGameService` | JSON-Persistenz (Load/Save/Import/Export/Reset) |
| `WorkerService` | Worker-Lifecycle: Mood, Fatigue, Training, Ruhe, Kündigung |
| `PrestigeService` | 3-Tier Prestige (Bronze/Silver/Gold) + Shop-Effekte |
| `ResearchService` | 45 Research-Nodes, Timer, Effekt-Berechnung |
| `EventService` | Zufällige Events (8 Typen) + saisonaler Multiplikator |
| `DailyChallengeService` | 3 Challenges/Tag (00:00 Reset) |
| `DailyRewardService` | 30-Tage Login-Zyklus mit steigenden Belohnungen |
| `QuickJobService` | Schnelle MiniGame-Jobs (4min Rotation, 20/Tag Limit) |
| `AchievementService` | 33 Erfolge tracken + Goldschrauben-Rewards |
| `OfflineProgressService` | Offline-Einnahmen (Brutto mit allen Modifikatoren - Kosten, * Saison-Multiplikator) |
| `OrderGeneratorService` | Aufträge generieren (pro freigeschaltetem Workshop-Typ) |

## Game Juice

| Feature | Implementierung |
|---------|-----------------|
| Workshop Cards | Farbiges BorderBrush nach Typ |
| Worker Avatars | 10 Tier-Farben (F=Grau bis Legendary=Rainbow-Gradient) |
| Golden Screw Icon | Gold-Shimmer Animation (CSS scale+rotate Loop) |
| Level-Up | CelebrationOverlay mit Confetti (100 Partikel, 2s) |
| Income | FloatingTextOverlay (gruen, +100px, 1.5s) |
| Button Hover | Pulse Effect (scale 1.05) |
| Combo Badge | Gold-Badge "#E8AA00" mit Fire-Icon, ScaleUpDown bei Combo >= 3 (PaintingGame) |
| Bottom Sheets | CSS translateY(800px→0px) mit CubicEaseOut (WorkerProfile, Confirm-Dialoge) |
| Parallax Header | Dashboard-Header verschiebt sich beim Scrollen (translateY = -offset * 0.3, max 20px) |
| Tab-Wechsel | FadeIn (150ms, CubicEaseOut) auf ContentPanel |

## Farbkonsistenz (Craft-Palette)

- **Alle Buttons** (Primary/Secondary/Outlined) ueberschrieben via App.axaml Style-Overrides → immer Craft-Orange/Braun
- **Keine `{DynamicResource PrimaryBrush}`** in Views → alles durch `{StaticResource CraftPrimaryBrush/LightBrush}` ersetzt
- **Workshop-Farben**: Carpenter=#A0522D, Plumber=#0E7490(Teal), Electrician=#F97316(Orange), Painter=#EC4899, Roofer=#DC2626, Contractor=#EA580C, Architect=#78716C(Stone), GeneralContractor=#FFD700
- **Tier-Farben**: F=Grau, E=Gruen, D=#0E7490(Teal), C=#B45309(DarkOrange), B=Amber, A=Rot, S=Gold
- **Branch-Farben**: Tools=#EA580C, Management=#92400E(Braun), Marketing=#65A30D(Lime)

## Daily Challenge Tracking

- `MiniGameResultRecorded` Event auf `IGameStateService` → `DailyChallengeService` subscribt automatisch
- Jedes MiniGame-Ergebnis trackt `PlayMiniGames` + `AchieveMinigameScore` Challenges
- Score-Mapping: Perfect=100%, Good=75%, Ok=50%, Miss=0%

## Reputation-System

- **CustomerReputation** (0-100 Score, startet bei 50): Beeinflusst Auftragsbelohnungen (0.7x bis 1.5x)
- **AddRating()** wird automatisch bei Auftragsabschluss aufgerufen (MiniGame-Rating → 1-5 Sterne)
- **Showroom-Gebäude**: Passive tägliche Reputation-Steigerung (0.5-2.5/Tag je nach Level)
- **DecayReputation()**: Langsamer Abbau >50 wenn keine Aufträge abgeschlossen werden (1/Tag)
- **Event-ReputationChange**: CelebrityEndorsement (+5), EconomicDownturn (+2) wirken einmalig bei Event-Start
- **Prestige-Reset**: Setzt Reputation auf Startwert (50) zurück

## Event-SpecialEffects

- **TaxAudit** ("tax_10_percent"): 10% Steuer auf Brutto-Einkommen (dauerhaft während Event)
- **WorkerStrike** ("mood_drop_all_20"): Alle Worker-Stimmungen -20 (einmalig bei Event-Start)
- Event-ID-Tracking verhindert doppelte Anwendung einmaliger Effekte

## Feierabend-Rush

- **Täglicher 2x-Boost** für 2 Stunden (Einkommens-Verdopplung)
- 1x pro Tag gratis, weitere Aktivierungen kosten 10 Goldschrauben
- Stackt mit SpeedBoost (2x * 2x = 4x möglich)
- Prestige-Shop "Rush-Verstärker" erhöht Rush auf 3x statt 2x
- GameState: `RushBoostEndTime`, `LastFreeRushUsed`, `IsRushBoostActive`, `IsFreeRushAvailable`
- Button im Dashboard zeigt Timer oder "Gratis-Rush!"

## Meisterwerkzeuge (Sammelbare Artefakte)

- **12 Werkzeuge** in 5 Seltenheitsstufen (Common/Uncommon/Rare/Epic/Legendary)
- Jedes gibt permanenten Einkommens-Bonus (+2% bis +15%)
- Maximaler Gesamt-Bonus: +74% wenn alle 12 gesammelt
- Freischaltung durch Meilensteine: Workshop-Level, Aufträge, Minispiele, Prestige
- Prüfung alle 2 Minuten im GameLoop (`MasterToolCheckIntervalTicks`)
- `MasterToolUnlocked` Event → FloatingText + Celebration in MainViewModel
- GameState: `CollectedMasterTools` (List<string> der IDs)
- Statische Definitionen in `MasterTool.GetAllDefinitions()`, Eligibility in `MasterTool.CheckEligibility()`

| ID | Name | Seltenheit | Bonus | Bedingung |
|----|------|-----------|-------|-----------|
| mt_golden_hammer | Goldener Hammer | Common | +2% | Workshop Lv.25 |
| mt_diamond_saw | Diamant-Säge | Common | +2% | Workshop Lv.50 |
| mt_titanium_pliers | Titanium-Zange | Common | +3% | 50 Aufträge |
| mt_brass_level | Messing-Wasserwaage | Common | +3% | 100 Minispiele |
| mt_silver_wrench | Silber-Schraubenschlüssel | Uncommon | +5% | Workshop Lv.100 |
| mt_jade_brush | Jade-Pinsel | Uncommon | +5% | 25 Perfect Ratings |
| mt_crystal_chisel | Kristall-Meißel | Uncommon | +5% | Bronze Prestige |
| mt_obsidian_drill | Obsidian-Bohrmaschine | Rare | +7% | Workshop Lv.250 |
| mt_ruby_blade | Rubinsägeblatt | Rare | +7% | Silver Prestige |
| mt_emerald_toolbox | Smaragd-Werkzeugkasten | Epic | +10% | Workshop Lv.500 |
| mt_dragon_anvil | Drachenschmiede-Amboss | Epic | +10% | Gold Prestige |
| mt_master_crown | Meisterkrone | Legendary | +15% | Alle 11 Tools |

## Lieferant-System (Variable Rewards)

- Zufällige Bonus-Lieferungen alle **2-5 Minuten** (Prestige-Bonus reduziert Intervall)
- 5 Lieferungstypen: Geld (35%), Goldschrauben (20%), XP (20%), Mood-Boost (15%), Speed-Boost (10%)
- Lieferung muss innerhalb von **2 Minuten** abgeholt werden, sonst verfällt sie
- `DeliveryArrived` Event → FloatingText-Benachrichtigung in MainViewModel
- GameState: `NextDeliveryTime`, `PendingDelivery`, `TotalDeliveriesClaimed`
- Model: `SupplierDelivery` (Type, Amount, CreatedAt, ExpiresAt)

## Changelog Highlights

- **v2.0.3 (13.02.2026)**: 4-Punkt Bugfix: (1) Dashboard Workshop-Karten Layout: RowDefinitions von `Auto,*,Auto` auf `Auto,Auto,Auto` geändert, VerticalAlignment="Top" statt "Stretch" → Karten überlappen/schneiden nicht mehr ab. Kompakterer Header (Icon+Name+Level-Badge in einer Zeile). (2) Daily Challenges Live-Update: ChallengeProgressChanged Event in IDailyChallengeService/DailyChallengeService hinzugefügt, wird bei IncrementChallenge()/SetChallengeMax() gefeuert. MainViewModel subscribt Event → RefreshChallenges() sofort statt nur bei Tab-Wechsel. RefreshChallenges() erstellt neue Listen-Kopie (CommunityToolkit Referenz-Gleichheitsprüfung umgangen). (3) Research ProgressBar Fix: Progress (0-100) wird durch 100 geteilt für ProgressBar Maximum=1 (in UpdateTimer() und BuildBranchDisplayItems()). Vorher zeigte 1% bereits volle Bar. (4) Worker-Transfer Fix: IsVisible bindet an bool HasAvailableWorkshops statt int Count (Avalonia konvertiert int nicht zu bool). SelectedTransferWorkshop Property statt fragiler ComboBox CommandParameter-Binding. TransferWorkerCommand parameterlos. SanitizeState: AssignedWorkshop wird korrigiert wenn Worker in falschem Workshop steckt.
- **v2.0.3 (13.02.2026)**: Immersive-Mode-Fix: OnWindowFocusChanged Override hinzugefügt → EnableImmersiveMode() wird bei Fokus-Wechsel erneut aufgerufen. Vorher blieben Status-/Navigationsleiste nach Ad-Anzeige oder Alt-Tab sichtbar.
- **v2.0.3 (13.02.2026)**: UI/UX Overhaul Phase 4-6: (1) BottomSheetBehavior mit translateY-Animation (TransformOperationsTransition, px-Einheiten). (2) WorkerProfile als Bottom-Sheet Overlay (nicht mehr Full-Page, unterliegende View bleibt sichtbar), Confirm-Dialoge als Bottom-Sheet. (3) CSS-Klassen SheetBackdrop/BottomSheet/ConfirmSheet in MainView.axaml. (4) PaintingGame Combo-System: ComboCount, ComboMultiplier (1.0x + bestCombo/5 * 0.25), Combo-Badge mit ScaleUpDown-Animation, ComboIncreased Event. (5) Dashboard Parallax-Effekt: Header verschiebt sich beim Scrollen (translateY = -scrollOffset * 0.3, max 20px).
- **v2.0.3 (13.02.2026)**: 7-Punkt Bugfix & Feature Update: (1) QuickJob-Rewards werden bei jedem GetAvailableJobs() neu berechnet (skaliert mit aktuellem Einkommen statt fix bei Generierung). (2) Training-System erweitert: 3 Trainings-Typen (Efficiency/Endurance/Morale), TrainingType Enum, Worker Properties EnduranceBonus/MoraleBonus (persistent, max 50% Reduktion), Typ-Auswahl-Buttons + Echtzeit-Fortschrittsbalken in WorkerProfileView. (3) Dashboard Hire-Button navigiert zum Arbeitermarkt statt direkt zu hiren. (4+5) DailyChallenge-Refresh alle 5 Ticks im OnGameTick + WorkerProfile-Update alle 3 Ticks. (6) ShopViewModel subscribt MoneyChanged + GoldenScrewsChanged Events für Live-Balance. (7) SettingsView zeigt alle 5 Premium-Benefits (+50% Einkommen, +100% Goldschrauben). 8 neue Lokalisierungs-Keys in 6 Sprachen.
- **v2.0.3 (13.02.2026)**: Android Zurück-Taste: Double-Back-Press zum Beenden (2s Fenster, Toast-Hinweis). Zurück-Navigation stufenweise: Dialoge schließen → MiniGame/Detail-View → Sub-Tabs → Dashboard. HandleBackPressed() + ExitHintRequested Event in MainViewModel, OnBackPressed() in MainActivity. RESX-Key `PressBackAgainToExit` in 6 Sprachen.
- **v2.0.3 (13.02.2026)**: Deep-Audit #3 - 10 Fixes: (1) KRITISCH: Shop-IAP Consumables (Goldschrauben-Pakete, Instant-Cash, Booster) geben Rewards jetzt erst nach erfolgreichem PurchaseConsumableAsync() statt sofort ohne Kauf. IPurchaseService um PurchaseConsumableAsync erweitert. (2) ShopViewModel Memory Leak: IDisposable implementiert, PremiumStatusChanged wird korrekt unsubscribed. (3) Achievement "prestige_1" nutzt jetzt Prestige.TotalPrestigeCount statt Legacy-Feld PrestigeLevel. (4) Worker-Tier Achievements (SS/SSS/Legendary) prüfen jetzt tatsächlich Worker-Tiers statt Self-Reference. (5) all_workshops Achievement zählt UnlockedWorkshopTypes statt Workshop-Instanzen. (6) Reputation-Decay Timer in GameState persistiert (LastReputationDecay) statt lokales Feld in GameLoopService. (7) Training-XP Akkumulator-Pattern: TrainingXpAccumulator statt (int)xpGain Cast-Truncation. (8) Random.Shared statt new Random() in DailyChallengeService + QuickJobService. (9) Prestige-Reset: LastReputationDecay wird zurückgesetzt. (10) PermanentMultiplier 20x-Cap: Konsistent in DoPrestige() (Write), TotalIncomePerSecond (Read) und SanitizeState (Load) angewendet. GetPermanentMultiplier() vereinfacht (keine Shop-Boni mehr fälschlich addiert).
- **v2.0.3 (13.02.2026)**: 7-Punkt Bugfix-Audit: (1) Prestige-Shop Income-Items (pp_income_10/25/50) werden jetzt im GameLoop auf Brutto-Einkommen angewendet. (2) GoldenScrewBonus (pp_golden_screw_25, +25%) wird jetzt in AddGoldenScrews() angewendet. (3) Worker Working-XP Fix: Math.Max(1,...) Bug entfernt, Akkumulator-Pattern statt 1 XP/Tick (war 72x schneller als Training). (4) Prestige-Reset ergänzt: Rush, Lieferant, QuickJobs, DailyChallenges, MasterTools werden zurückgesetzt. (5) Offline-Einnahmen nutzen jetzt alle Modifikatoren (Research, MasterTools, Prestige-Shop, CostReduction, Storage). (6) BulkUpgrade Event sendet korrekte oldLevel/newLevel/totalCost statt Nullwerte. (7) MasterTool als static class (tote Instance-Properties entfernt).
- **v2.0.3 (12.02.2026)**: Neue Features: (1) Feierabend-Rush: 2h 2x-Boost, 1x täglich gratis, weitere 10 Goldschrauben. Stackt mit SpeedBoost. (2) Meisterwerkzeuge: 12 sammelbare Artefakte (5 Seltenheitsstufen) mit permanenten Einkommens-Boni (+2% bis +15%). Prüfung alle 2min im GameLoop. (3) Lieferant-System: Variable Rewards alle 2-5min (Geld/Schrauben/XP/Mood/Speed). 2min Abholzeit. (4) Prestige-Shop erweitert: 3 neue Items (Rush-Verstärker 3x, Lieferanten-Express -30% Intervall, Goldschrauben-Boost +25%). PrestigeEffect um RushMultiplierBonus, DeliverySpeedBonus, GoldenScrewBonus erweitert. 42 neue Lokalisierungs-Keys in 6 Sprachen.
- **v2.0.3 (12.02.2026)**: Deep-Audit #2 - Tote Systeme + Balancing: (1) Reputation-System aktiviert: AddRating bei Auftragsabschluss, ReputationMultiplier (0.7x-1.5x) auf Belohnungen. (2) SpecialEffect-Events funktional: TaxAudit (-10% Einkommen), WorkerStrike (Mood -20 alle Worker). (3) Event-ReputationChange: CelebrityEndorsement/EconomicDownturn wirken auf Reputation. (4) ExtraOrderSlots: Office-Gebäude + Research erhöhen Auftragsanzahl über 3. (5) Showroom-DailyReputationGain + Reputation-Decay im GameLoop. (6) Random.Shared statt new Random() in OrderGeneratorService + EventService. (7) DailyChallengeService: Netto statt Brutto für Belohnungsberechnung. (8) Saisonaler Multiplikator zentralisiert (EventService.GetSeasonalMultiplier). (9) CostReduction Cap: 75%→50%. (10) Building-Kosten: 3^Level→2^Level (sanftere Kurve). (11) Research EfficiencyBonus Cap bei +50%. (12) SanitizeState erweitert: Prestige, DailyRewardStreak, Worker-Mood/Fatigue, Reputation, Building-Levels.
- **v2.0.3 (12.02.2026)**: Deep-Audit Gameplay-Optimierungen: (1) Prestige früher verfügbar (Bronze Lv.30, Silver Lv.100, Gold Lv.250 statt 100/300/500). (2) Research-Effekte werden im GameLoop angewendet (EfficiencyBonus auf Einkommen, CostReduction+WageReduction auf Kosten). (3) Gebäude-Effekte aktiv: Canteen→Stimmungs-Erholung+Rest-Beschleunigung, Storage→Kosten-Reduktion, TrainingCenter→Training-Speed, VehicleFleet→Auftrags-Bonus, WorkshopExtension→Extra-Worker-Slots. (4) Offline-Einnahmen mit Saison-Multiplikator. (5) Milestone-Celebrations bei Level 10/25/50/100/250/500/1000 mit Goldschrauben-Bonus. (6) Bulk Buy (x1/x10/x100/Max) für Workshop-Upgrades. (7) Material-Kosten level-basiert statt zirkulär. (8) Login-Streak auf 30 Tage erweitert. (9) QuickJob-Exploit behoben (MiniGame muss gespielt werden). (10) Prestige-Shop-Effekte aktiv (CostReduction, MoodDecayReduction, XpMultiplier). (11) 8 Saison/BulkBuy-Lokalisierungskeys in 5 Sprachen.
- **v2.0.3 (12.02.2026)**: 10-Punkte Bugfix-Audit: (1) Offline-Earnings nutzen jetzt NetIncomePerSecond statt Brutto (Exploit behoben: Kosten wurden offline ignoriert). (2+3) DateTime.Now→UtcNow in DailyRewardService (3 Stellen), DailyChallengeService (2 Stellen), EventService (1 Stelle) - konsistente UTC-Zeitzone. (4+5) MoneyFormatter+MoneyDisplayConverter: Negative Werte korrekt (U+2212 Minus), T-Stufe (Trillion) ergänzt, K-Schwelle konsistent auf >=1000. (6) SaveGameService: State-Validierung bei Load/Import (PlayerLevel, Money, Workshops, Prestige etc. sanitized). (7) PrestigeService: Multiplikator-Cap bei 20x, toter RecalculatePermanentMultiplier()-Code entfernt. (8) Offline-Dialog zeigt "(Max. Xh)" wenn Dauer gekappt wurde. (9) QuickJob Tageslimit (20/Tag) gegen Reward-Farming, neuer Lokalisierungs-Key QuickJobDailyLimit in 6 Sprachen, GameState-Felder QuickJobsCompletedToday + LastQuickJobDailyReset.
- **v2.0.3 (12.02.2026)**: Statistik-Refresh beim Tab-Wechsel. Shop Instant-Cash: Belohnungen basieren auf stündlichem Einkommen statt fixem Level*Multiplikator (small=1h, large=4h, huge=12h, mega=48h). QuickJob-Belohnungen: ~5min Einkommen statt Cap bei 100€. Daily-Challenge-Belohnungen: ~10min Einkommen, 5 Level-Tiers (0-5/6-15/16-30/31-50/51+), bis zu 3 Goldschrauben. QuickJob-Timer Auto-Rotation im GameTick (UI aktualisiert bei Ablauf). Workshop-Auswahl-Dialog: Größere Buttons (MinHeight=56), sichtbarer Hintergrund+Rahmen. Research-ProgressBar: Background=SurfaceBrush (Fortschritt sichtbar).
- **v2.0.3 (12.02.2026)**: UI-Verbesserungen + Bugfixes: SawingGame IsResultShown-Reset in SetOrderId (Ergebnis-Screen blieb stehen). ToggleSwitch On/Off-Text entfernt. ConfirmDialog Buttons untereinander statt nebeneinander. Dialog-Overlay-Klick schließt Dialoge. WorkerProfile Training/Rest-Dauer-Anzeige (TrainingTimeDisplay, TrainingCostDisplay, RestTimeDisplay). Auto-Rest bei 100% Erschöpfung (WorkerService). 3 neue Lokalisierungs-Keys (TrainingDuration, TrainingCost, RestDuration) in 6 Sprachen.
- **v2.0.3 (12.02.2026)**: WorkshopView Kosten-Anzeige: Redundante TotalWorkerIncomeDisplay entfernt, stattdessen Income Stats Card mit Brutto/Netto/Kosten-Aufschlüsselung (Miete, Material, Löhne). Netto rot bei Verlust. MoneyFormatter.FormatPerHour() hinzugefügt. 6 neue Lokalisierungs-Keys (GrossIncome, NetIncome, RunningCosts, Rent, MaterialCosts, Wages) in 6 Sprachen.
- **v2.0.3 (12.02.2026)**: Achievement-XP-Rebalancing: Frühe/einfache Achievements XP um 50-75% reduziert (first_order 25→10, building_first 100→25, etc.). Level-Achievements geben 0 XP (Feedback-Loop verhindert: Level→XP→Level→XP), stattdessen höhere Geld+Goldschrauben-Belohnungen. Endgame-Achievements XP ebenfalls ~50% reduziert. Gesamtes XP-Tempo deutlich verlangsamt.
- **v2.0.3 (12.02.2026)**: Mini-Game Start-Button Fix: Panel-Wrapper mit !IsResultShown versteckt Button nach Spielende (alle 4 Games). Workshop-Gesamteinkommen: TotalWorkerIncomeDisplay in WorkshopView unter Worker-Liste. XP bei Workshop-Upgrades (5 + Level/10 pro Upgrade). Workshop-Freischaltung komplett überarbeitet: Level-Requirement muss erfüllt sein (kein Bypass per Video), Video-Ad gibt nur 50% Rabatt auf Kosten, TryPurchaseWorkshop/CanPurchaseWorkshop statt ForceUnlockWorkshop. Unlock-Kosten 5x erhöht (Plumber 25K, Electrician 250K, Painter 2.5M, etc.). Dashboard zeigt Kosten + offenes Schloss bei kaufbaren Workshops. 10 neue Lokalisierungs-Keys (6 Sprachen).
- **v2.0.3 (12.02.2026)**: WorkerProfileView Grid.Column="1" Fix (Text-Überlappung bei Persönlichkeit/Werkstatt/Effizienz). Workshop-Upgrade-Performance: RefreshWorkshops() nur für betroffenen Workshop statt alle (RefreshSingleWorkshop). Effizienz-Anzeige: EffectiveEfficiency + IncomeContribution (€/s) pro Worker in WorkshopView, WorkerMarket + WorkerProfile. Ad-Extra-Slot-Button auf WorkshopView (CanWatchSlotAd). Dashboard: Orders-Count-Badge, automatische Order-Regenerierung bei Rückkehr. OrdersCompleted Label lokalisiert (6 Sprachen).
- **v2.0.3 (11.02.2026)**: Worker-Tiers erweitert auf 10 (F/E/D/C/B/A/S/SS/SSS/Legendary). 9 neue Achievements (Workshop 100/250/500/1000, Level 100/250/500/1000, Worker SS/SSS/Legendary, 10B EUR). 2 neue Shop-Items (Huge/Mega Instant Cash). Lokalisierung in 6 Sprachen (33 neue Keys in AppStrings.resx/.de/.es/.fr/.it/.pt + AppStrings.Designer.cs)
- **v2.0.3 (11.02.2026)**: Order-Rewards: Doppel-Multiplikator-Bug gefixt (Difficulty wurde in BaseReward UND FinalReward angewendet → quadriert), Basis von 100 auf 300 erhöht für spürbar höhere Auftragsbelohnungen. Worker-Tiles in WorkshopView klickbar → navigiert zum Worker-Profil (Fire/Transfer/Train). Dashboard ScrollViewer HorizontalScrollBarVisibility=Disabled (rechter Rand Fix)
- **v2.0.3 (11.02.2026)**: Workshop-Karten im Dashboard auf 2-Spalten-Layout umgestellt (UniformGrid Columns=2). Vertikale Karten mit farbigem Header (Workshop-Farbe 10% Opacity), Icon+Name+Level-Badge, Worker/Income-Stats, Level-ProgressBar in Workshop-Farbe, Upgrade-Button über volle Breite mit abgerundeten unteren Ecken (CornerRadius=16)
- **v2.0.3 (11.02.2026)**: Bugfixes: Workshop-LevelProgress dividierte durch 10 statt MaxLevel (50), XP-Progress in OnXpGained nutzte falsche Formel (jetzt GameState.LevelProgress), DailyChallenge EarnMoney int-Cast→Math.Round, WorkshopView zeigte "/10" statt "/50", WorkshopDisplayModel hardcodierte 50.0→Workshop.MaxLevel
- **v2.0.3 (11.02.2026)**: 6 neue Rewarded-Ad-Placements implementiert (workshop_speedup, workshop_unlock, worker_hire_bonus, research_speedup, daily_challenge_retry, achievement_boost). Models erweitert (HasRetriedWithAd, HasUsedAdBoost, AdBonusWorkerSlots). Service-Methoden: RetryChallenge, ForceUnlockWorkshop, BoostAchievement. Lokalisierung in 6 Sprachen.
- **v2.0.2 (09.02.2026)**: Daily-Challenge-Bug: MiniGame-Ergebnisse werden jetzt via Event an DailyChallengeService gemeldet; 18 fehlende Lokalisierungs-Keys in 6 Sprachen ergaenzt; Farbkonsistenz-Fix: Alle Views auf warme Craft-Palette, Button-Style-Overrides, Workshop/Tier/Branch-Farben waermer
- **v2.0.2 (08.02.2026)**: Banner-Ad Overlap-Fix, WorkshopColorConverter, CelebrationOverlay + FloatingTextOverlay, Golden Screw Shimmer
- **v2.0.1 (07.02.2026)**: Rewarded Ads 7 Placements, Premium-Modell Sync (4,99)
- **v2.0.0 (05.02.2026)**: Initial Avalonia Migration, Research Tree + Mini-Games, Worker-System mit 3 Tiers
