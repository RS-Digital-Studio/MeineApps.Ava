# HandwerkerImperium Balancing-Redesign

> Basierend auf Idle-Game-Industrie-Recherche (Cookie Clicker, AdVenture Capitalist, Egg Inc, Idle Miner Tycoon, Clicker Heroes, Realm Grinder)

## Ziel

AAA-Niveau Idle-Game-Balancing: Befriedigende Progression, spuerbarer Prestige-Fortschritt, "Bumpy Progression" durch Multiplikator-Meilensteine, kein frustrierender Hard-Cap, dynamisches Ziel-System.

## Aenderungen

### 1. Prestige-Anforderungen senken (PrestigeTier.cs)

**Problem**: 3x vorheriger Tier fuer jeden Aufstieg = 729 Prestiges auf unteren Stufen fuer Legende. Kein Spieler macht das.

**Loesung**: Gestaffelt 0/1/1/2/2/2/3

```
Bronze  => 0  (unveraendert)
Silver  => 1  (war 3)
Gold    => 1  (war 3)
Platin  => 2  (war 3)
Diamant => 2  (war 3)
Meister => 2  (war 3)
Legende => 3  (bleibt - Endgame-Ziel)
```

**Gesamtprestiges fuer Legende**: Alt ~729, Neu ~48. Herausfordernd aber erreichbar.

### 2. Hard-Cap 3.0x → Soft-Cap mit Diminishing Returns (GameLoopService.cs, OfflineProgressService.cs)

**Problem**: Harter 3.0x-Cap auf Income-Multiplikator. Kein erfolgreiches Idle Game hat einen globalen Hard-Cap. Spieler spueren keinen Fortschritt mehr nach Cap.

**Loesung**: Logarithmischer Soft-Cap ab 2.0x

```csharp
if (effectiveMultiplier > 2.0m)
{
    decimal excess = effectiveMultiplier - 2.0m;
    decimal softened = 2.0m + (decimal)Math.Log(1.0 + (double)excess, 2.0);
    grossIncome = state.TotalIncomePerSecond * softened;
}
```

**Beispielwerte**:
| Roh-Multiplikator | Effektiver Multiplikator |
|-------------------|--------------------------|
| 2.0x | 2.0x (kein Cap) |
| 3.0x | 2.58x |
| 5.0x | 3.17x |
| 10.0x | 4.17x |
| 20.0x | 5.25x |
| 50.0x | 6.64x |

Jeder Bonus bringt etwas, aber mit Diminishing Returns.

### 3. Workshop-Level Multiplikator-Meilensteine (Workshop.cs)

**Problem**: Fehlende "Bumpy Progression". Einkommen waechst gleichmaessig → kein Belohnungsgefuehl bei Leveln.

**Loesung**: Automatische Multiplikatoren bei bestimmten Workshop-Leveln (AdVenture-Capitalist-Pattern)

```
Level  25 → x1.5
Level  50 → x2
Level 100 → x2
Level 250 → x3
Level 500 → x5
Level 1000 → x10
```

**Kumulativ**: Lv1000 = 1.5 * 2 * 2 * 3 * 5 * 10 = **900x**

In `BaseIncomePerWorker` eingebaut. FloatingText + CelebrationOverlay bei jedem Meilenstein. SkiaSharp-Banner "x2 EINKOMMENS-BOOST!" mit Gold-Shimmer.

### 4. Offline-Earnings Staffelung (OfflineProgressService.cs)

**Problem**: 100% Rate fuer volle Offline-Dauer. Kein Anreiz regelmaessig reinzuschauen.

**Loesung**: Degressive Staffelung

```
Erste 2h:   100% der Rate
2h - 6h:    50% der Rate
6h+:        25% der Rate
```

**Rewarded-Ad-Integration**: "Werbung schauen um Offline-Einnahmen zu verdoppeln" als prominenter Button im OfflineEarningsDialog. Bestes Rewarded-Ad-Placement laut Industrie-Daten.

### 5. Naechstes-Ziel-System (NEU: GoalService.cs)

**Problem**: Spieler wissen nicht was sie als naechstes tun sollen. Kein klarer Main-Loop.

**Loesung**: Dynamischer "Naechstes Ziel"-Banner im Dashboard

**Ziel-Typen** (priorisiert):
1. **Workshop-Meilenstein**: "Tischlerei auf Level 25 → x1.5 Einkommen!" (hoechste Prio wenn <5 Level entfernt)
2. **Prestige verfuegbar**: "Prestige jetzt → +15 Punkte!" (wenn CanPrestige == true)
3. **Neuer Workshop**: "Noch 50K bis Sanitaer-Werkstatt!" (wenn naechster Workshop erschwinglich)
4. **Gebaeude-Upgrade**: "Kantine auf Level 3 → +3% Stimmung/h!"
5. **Daily Challenge**: "Noch 1 Auftrag bis Challenge-Bonus!"
6. **Forschung**: "Forschung starten → +10% Effizienz!"
7. **Worker**: "Stelle einen B-Tier Worker ein!"

**UI**: Gold-umrandeter Banner im DashboardView, oberhalb der Workshop-Karten. Tap → Navigation zum relevanten Feature. Pulsierender Pfeil-Icon. Wechselt automatisch wenn Ziel erreicht.

### 6. Erweiterte Prestige-Preview (ImperiumView.axaml, BuildingsViewModel.cs)

**Problem**: Spieler sehen nicht was sie bei Prestige gewinnen/verlieren.

**Loesung**: Ausfuehrliche Preview-Karte

```
┌─────────────────────────────────┐
│ PRESTIGE: BRONZE → SILVER       │
│                                  │
│ DU BEKOMMST:                     │
│ ✦ 23 Prestige-Punkte (x2.0)    │
│ ✦ +25% permanenter Income-Bonus │
│ ✦ Forschung bleibt erhalten!    │
│                                  │
│ DU VERLIERST:                    │
│ ✗ Workshop-Level → 1            │
│ ✗ Geld → 0                      │
│ ✗ Worker → entlassen            │
│                                  │
│ GESCHAETZTER SPEED-UP: ~40%     │
│                                  │
│ [PRESTIGE DURCHFUEHREN]          │
└─────────────────────────────────┘
```

### 7. Celebration-Effekte bei Meilensteinen (GameJuiceEngine, MainViewModel)

**Problem**: Grosse Momente (Workshop-Meilenstein, Prestige) haben zu wenig visuelles Feedback.

**Loesung**: Volle RewardCeremony fuer Multiplikator-Meilensteine

- **Workshop Lv25**: FloatingText "x1.5 BOOST!" + CoinFlyAnimation + Sound
- **Workshop Lv50**: RewardCeremony (WorkshopMilestone) + Confetti + Sound
- **Workshop Lv100**: RewardCeremony + Fireworks + ScreenShake + Sound
- **Workshop Lv250+**: Volle Zeremonie mit speziellem Gold-Overlay

### 8. Prestige-Zeremonie aufwerten (PrestigeService, MainViewModel)

**Problem**: Prestige ist der wichtigste Moment im Spiel, aber die Zeremonie ist unterdimensioniert.

**Loesung**:
- **Pre-Prestige**: 3-2-1 Countdown mit Screen-Darkening
- **Reset-Moment**: FlashOverlay (weiss) + ShockwaveRing + alle Zahlen rollen auf 0
- **Post-Prestige**: RewardCeremony (Prestige-Typ) + Confetti + Fireworks + permanenter Bonus fliegt als Gold-Zahl ins HUD
- **Neuer Tier**: Spezielle Tier-Farbe als Vignette + Kronen-/Medaillen-Icon + Sound

## Savegame-Kompatibilitaet

- Alle Aenderungen rueckwaertskompatibel
- Bestehende Spielstaende profitieren sofort
- Kein Datenverlust, keine Schema-Aenderung
- GameState braucht nur neues Feld: `CurrentGoal` (optional, transient)

## Betroffene Dateien

| Datei | Aenderung |
|-------|-----------|
| `Models/Enums/PrestigeTier.cs` | GetRequiredPreviousTierCount anpassen |
| `Models/Workshop.cs` | GetMilestoneMultiplier() + Einbau in BaseIncomePerWorker |
| `Services/GameLoopService.cs` | Hard-Cap → Soft-Cap |
| `Services/OfflineProgressService.cs` | Hard-Cap → Soft-Cap + Staffelung |
| `Services/Interfaces/IGoalService.cs` | NEU: Interface |
| `Services/GoalService.cs` | NEU: Naechstes-Ziel-Logik |
| `ViewModels/MainViewModel.cs` | GoalService integrieren, Milestone-Celebrations |
| `ViewModels/BuildingsViewModel.cs` | Erweiterte Prestige-Preview |
| `Views/DashboardView.axaml` | Ziel-Banner UI |
| `Views/ImperiumView.axaml` | Erweiterte Prestige-Preview UI |
| `App.axaml.cs` | DI: GoalService registrieren |
| `Resources/Strings/AppStrings*.resx` | RESX-Keys (6 Sprachen) |

## Risiken

| Risiko | Mitigation |
|--------|------------|
| Bestehende Spieler haben bereits viele Bronze-Prestiges → ploetzlich Silver/Gold verfuegbar | Feature, kein Bug - Spieler werden belohnt |
| Soft-Cap koennte Economy brechen bei extremen Multiplikatoren | Log2 waechst sehr langsam, selbst bei 100x nur ~8.6x effektiv |
| Meilenstein-Multiplikatoren erhoehen Einkommen massiv (900x bei Lv1000) | Kosten skalieren mit 1.035^n, uebersteigen 900x ab ~Lv200 Vorsprung |
| GoalService muss performant sein (laeuft im GameLoop) | Nur alle 60 Ticks pruefen, Ergebnis cachen |
