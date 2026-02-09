# HandwerkerImperium (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Idle-Game: Baue dein Handwerker-Imperium auf, stelle Mitarbeiter ein, kaufe Werkzeuge, erforsche Upgrades, schalte neue Workshop-Typen frei. Verdiene Geld durch automatische Auftraege oder spiele Mini-Games.

**Version:** 2.0.2 | **Package-ID:** com.meineapps.handwerkerimperium | **Status:** Geschlossener Test

## Haupt-Features

- **6 Workshop-Typen** (Tischlerei, Malerei, Sanitaer, Elektrik, Landschaftsbau, Renovierung)
- **3 Mini-Games** (Pipe Puzzle, Wire Match, Tile Swap)
- **Worker-System** mit 3 Tiers (Auszubildender, Geselle, Meister)
- **Goldschrauben-Economy** (Premium-Waehrung fuer Boosts/Unlock)
- **Research Tree** (16 Upgrades in 4 Kategorien)
- **Daily Challenges** (3 pro Tag)
- **Achievements** (24 Erfolge)
- **Statistiken** (Gesamt-Verdienst, Workshop-Verteilung)

## Premium & Ads

### Premium-Modell
- **Preis**: 4,99 EUR (Lifetime)
- **Vorteile**: +50% Einkommen, +100% Goldschrauben aus Mini-Games, keine Werbung

### Rewarded (7 Placements)
1. `workshop_speedup` → Workshop 2h ueberbruecken
2. `workshop_unlock` → Workshop ohne Level freischalten
3. `worker_hire_bonus` → Extra Worker-Slot
4. `research_speedup` → Forschung abschliessen
5. `daily_challenge_retry` → Challenge wiederholen nach Fail
6. `golden_screw_reward` → 50 Goldschrauben
7. `achievement_boost` → Achievement Progress +20%

## Architektur-Besonderheiten

### Game Loop
- **GameLoopService** (60 FPS Timer) → Workshop-Produktion, Worker-Animationen, Mini-Game-State
- **AutoSaveService** (alle 5 Min) → GameState → SQLite

### Workshop-Typen
Enum: `Carpentry`, `Painting`, `Plumbing`, `Electrical`, `Landscaping`, `Renovation`
Jeder Typ hat: `BaseIncome`, `BaseUpgradeCost`, `UnlockLevel`, `UnlockCost`

### Worker-System
3 Tiers via Enum: `Apprentice` (1x), `Journeyman` (1.5x), `Master` (2.5x)
`HireWorker()` → kostet Geld, erhoeht Workshop-Effizienz

### Goldschrauben-Quellen
1. Mini-Games (3-10 Schrauben pro Win)
2. Daily Challenges (20 Schrauben)
3. Achievements (5-50 Schrauben)
4. Rewarded Ad (50 Schrauben)
5. IAP-Paket (100/500/2000 Schrauben)

### Research Tree
16 Upgrades in 4 Kategorien (Efficiency, Automation, Workers, Special)
Jedes Research braucht: `GoldScrews` + `ResearchPoints` (verdient via Workshop-Produktion)

### Mini-Games
- **Pipe Puzzle**: Rohre drehen um Durchfluss zu schaffen (3 Schwierigkeiten)
- **Wire Match**: Kabel-Farben verbinden (Simon Says mit Timing)
- **Tile Swap**: 3x3 Tile-Puzzle (Sliding Puzzle)

## App-spezifische Services

| Service | Zweck |
|---------|-------|
| `GameLoopService` | 60 FPS Update-Loop |
| `AutoSaveService` | Alle 5 Min GameState → SQLite |
| `DailyChallengeService` | 3 Challenges/Tag generieren (00:00 Reset) |
| `AchievementService` | 24 Erfolge tracken + Goldschrauben-Rewards |
| `WorkshopColorConverter` | Enum → Brush Mapping (warme Palette, keine kalten Farben) |

## Game Juice

| Feature | Implementierung |
|---------|-----------------|
| Workshop Cards | Farbiges BorderBrush nach Typ |
| Worker Avatars | 3 Tier-Icons (Apprentice=Hat, Journeyman=Hammer, Master=Crown) |
| Golden Screw Icon | Gold-Shimmer Animation (CSS scale+rotate Loop) |
| Level-Up | CelebrationOverlay mit Confetti (100 Partikel, 2s) |
| Income | FloatingTextOverlay (gruen, +100px, 1.5s) |
| Button Hover | Pulse Effect (scale 1.05) |

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

## Changelog Highlights

- **v2.0.2 (09.02.2026)**: Daily-Challenge-Bug: MiniGame-Ergebnisse werden jetzt via Event an DailyChallengeService gemeldet; 18 fehlende Lokalisierungs-Keys in 6 Sprachen ergaenzt; Farbkonsistenz-Fix: Alle Views auf warme Craft-Palette, Button-Style-Overrides, Workshop/Tier/Branch-Farben waermer
- **v2.0.2 (08.02.2026)**: Banner-Ad Overlap-Fix, WorkshopColorConverter, CelebrationOverlay + FloatingTextOverlay, Golden Screw Shimmer
- **v2.0.1 (07.02.2026)**: Rewarded Ads 7 Placements, Premium-Modell Sync (4,99)
- **v2.0.0 (05.02.2026)**: Initial Avalonia Migration, Research Tree + Mini-Games, Worker-System mit 3 Tiers
