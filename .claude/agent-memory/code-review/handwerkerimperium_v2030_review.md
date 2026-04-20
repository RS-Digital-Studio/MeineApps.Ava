---
name: HandwerkerImperium v2.0.30 Review Apr 2026
description: 5 Findings (2 Bugs 3 Verbesserungen) - Premium +50% fehlt auf Crafting-Sellpreis, RebirthService Lambda ohne Unsubscribe, state.Money kann negativ werden, decimal-Truncation-Risiko in DoRebirth
type: project
---

# HandwerkerImperium v2.0.30 (Apr 2026) - Code Review

## Was gut gemacht ist
- v2.0.29-Findings KOMPLETT gefixt: AssignedWorkshop jetzt in CreateForTier(WorkerTier, WorkshopType?) ueberall gesetzt (PrestigeService.cs:730/744/754, AscensionService.cs:133, GameStateService.Workshop.cs:163). SaveGameService.ImportSaveAsync haelt ioLock fuer Initialize+Save atomar. Alle 10 MiniGame-VMs nutzen HandleTimerTick-Wrapper mit try/catch + Timer-Stop.
- DisposeServices in App.axaml.cs:197-227 ist jetzt selbstreparierend: GameLoopService+GameJuiceEngine explizit zuerst, dann ServiceProvider.Dispose() kaskadiert alle IDisposable. Verhindert Silent-Leaks bei neuen Services.
- MainViewModel.Dispose (Z.2065-2146): Alle Events unsubscribed in identischer Reihenfolge, benannte Delegate-Felder statt Lambdas.
- GuildViewModel.Dispose: Saubere Unsubscription aller 8 Sub-VM-Events.

## Findings

### BUG-1 [KRITISCH] Premium +50% fehlt auf Crafting-Verkaufspreis
Datei: Services/IncomeCalculatorService.cs:282-284 + Services/CraftingService.cs:196-220
Problem: Kommentar in CalculateCraftingSellMultiplier behauptet "Premium-Bonus wird jetzt zentral in CalculateGrossIncome() angewendet" - aber CraftingService.GetSellPrice() ruft ausschliesslich CalculateCraftingSellMultiplier auf, NICHT CalculateGrossIncome. Premium-Spieler bekommen +50% auf passives Einkommen UND Offline-Earnings, aber NULL auf Crafting-Items. Balancing-Inkonsistenz (dokumentierter Fix 06.04.2026 "Doppel-Bonus entfernt" hat jetzt einen Komplett-Verlust hinterlassen).
Fix: In CalculateCraftingSellMultiplier vor dem return ergaenzen:
```csharp
if (state.IsPremium) mult *= 1.5m;
```

### BUG-2 [HOCH] RebirthService PrestigeCompleted/AscensionCompleted - Lambdas ohne Unsubscribe + Doppelzaehlung
Datei: Services/RebirthService.cs:53-58
Problem: Drei Lambda-Handler ohne benannte Felder, keine Dispose-Methode. RebirthService implementiert NICHT IDisposable und steht nicht in App.DisposeServices-Chain explizit drin. Lifetime Singleton->Singleton ist zwar OK, aber: AchievementService.cs:44 subscribed ZUSAETZLICH auf `_rebirthService.RebirthCompleted` - und RebirthService wird bei jedem Ascension noch einmal neu via AscensionService.AscensionCompleted ausgeloest (`ApplyStarsToWorkshops()` wird dann doppelt gerufen: erst von RebirthService-Subscriber, dann vom PrestigeService-Reset). Aktuelles ApplyStarsToWorkshops ist idempotent, aber fragil.
Fix: Mindestens Dispose-Pattern mit benannten Delegates, oder den Lambda ersetzen durch eine explizite OnStateLoaded/OnPrestigeCompleted/OnAscensionCompleted-Methode.

### BUG-3 [MITTEL] DoRebirth kann state.Money negativ werden lassen
Datei: Services/RebirthService.cs:100-101
Problem: `state.Money -= moneyCost` ohne Check `state.Money >= moneyCost`. Szenario: Goldschrauben-Check besteht, Spieler hat 1000 EUR, Rebirth-Kosten = 30% = sollte 300 EUR sein — aber wenn GameLoop zwischen Read und Write zuschlaegt (negatives Income bei Verlust-Run), koennte `state.Money` schon < 300 EUR sein. `_gameStateService.TrySpendMoney(moneyCost)` stattdessen nutzen (atomar unter _stateLock mit Check).
Fix:
```csharp
decimal moneyCost = state.Money * cost.moneyPercent;
if (!_gameStateService.TrySpendMoney(moneyCost)) {
    // Refund Goldschrauben (bereits abgezogen) - Rollback
    _gameStateService.AddGoldenScrews(cost.goldenScrews);
    return false;
}
```

### VERBESSERUNG-1 LuckySpin async void Timer-Tick - inkonsistent zu MiniGame-Base
Datei: ViewModels/LuckySpinViewModel.cs:210
Problem: `async void OnSpinTick` wurde nicht auf das neue BaseMiniGameViewModel.HandleTimerTick-Pattern umgestellt (try/catch + Timer-Stop bei Fehler). Hat zwar try/catch, aber kein Timer.Stop bei Exception -> Animation laeuft endlos weiter bei Fehler. `_spinTimer!.Tick -= OnSpinTick` im Erfolgspfad (Z.228) fehlt im catch-Pfad.
Fix: Im catch-Block `_spinTimer?.Stop(); _spinTimer = null; IsSpinning = false;` setzen.

### VERBESSERUNG-2 RebirthCosts-Tabelle hat inkonsistenten Kommentar
Datei: Services/RebirthService.cs:30-37
Problem: Kommentar im Header sagt "Gesamt-GS halbiert von 2350 auf 1175" - aber das Array summiert zu 50+125+250+250+500 = 1175 GS. Stimmt. Stern 3 und Stern 4 kosten beide 250 GS (aber 20% vs 25% Geld) - das ist bewusste Progression aber wirkt ungluecklich identisch nebeneinander. Nicht kritisch.

## Zusammenfassung
- Verifizierte Findings: 5 (Bugs: 3 | Quality: 2)
- Commit-ready: Nein (BUG-1 ist Balancing-Verlust fuer Premium-Kunden)
- Top-3 Prioritaeten: BUG-1 Premium-Crafting, BUG-3 DoRebirth TrySpendMoney, BUG-2 RebirthService Dispose-Pattern
