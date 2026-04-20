---
name: HandwerkerImperium Health 2026-04-20
description: v2.0.31 Makro-Analyse. Interface-Segregation ungenutzt (3 Micro-Interfaces), Raise*-Methoden im Interface dead, DashboardView.axaml.cs 802 Zeilen.
type: project
---

# HandwerkerImperium Health 2026-04-20

**Version:** 2.0.31 | **Score:** 87

## Neue Findings (nicht in CLAUDE.md dokumentiert)

### [ARCH-1] Interface-Segregation ohne Konsumenten (Mittel)
IGameCurrencyService (73 Zeilen), IGameOrderService (78), IGameWorkshopService (62) werden von IGameStateService geerbt. Grep: NIEMAND injiziert eines der 3 Sub-Interfaces allein — nur das zusammengesetzte IGameStateService. 213 Zeilen toter Abstraktion. Entweder ersatzlos entfernen oder wirklich aufteilen.

### [DEAD-1] Raise*-Public-API wird nie extern aufgerufen (Mittel)
IGameStateService.RaiseMoneyChanged/RaiseWorkshopUpgraded/RaiseWorkerHired/RaiseOrderCompleted/RaiseMiniGameResultRecorded (Zeile 86-98). Interface-Kommentar "fuer extrahierte Services" — aber seit Einfuehrung keine externen Aufrufer. Sollten `internal` werden oder geloescht. YAGNI-Violation.

### [ARCH-2] DashboardView Code-Behind 802 Zeilen (Mittel)
Views/DashboardView.axaml.cs bundelt CityRenderer, AnimationManager, CoinFlyAnimation, RenderTimer, Hold-to-Upgrade-Timer, Tap-vs-Scroll-Detection, Money-Flash-Animation. Teils legitim (SkiaSharp), aber Hold-Timer und Tap-Detection-State-Machine gehoeren in einen wiederverwendbaren Helper (BomberBlast hat ein aehnliches Muster). Ein Helper `WorkshopCardGestureRecognizer` wuerde ~150 Zeilen ausziehen.

### [DI-1] GuildFacade reduziert nicht den Konstruktor-Druck (Gering)
IGuildFacade existiert mit Kommentar "reduziert GuildViewModel-Ctor von 14 auf 7 Parameter". GuildViewModel ist trotzdem 1719 Zeilen. Facade bringt nur marginale Verbesserung, aber erhoeht Indirektion (jede Aenderung muss durch Facade). Nicht akut, aber Warnzeichen: Facade hat 61 Treffer = wird wirklich genutzt, bleibt OK.

### [DOC-1] CLAUDE.md Status-Tabelle v2.0.29 vs csproj v2.0.31 (Gering)
App-CLAUDE.md Zeile 9: "Version: 2.0.31 (VersionCode 39) | Status: Produktion" — synchron. OK. Haupt-CLAUDE.md (F:\Meine_Apps_Ava\CLAUDE.md) listet HandwerkerImperium v2.0.31, passt.

## Kategorien ohne Befund

- **Service-Locator:** 0 Treffer in Views-Code-Behinds (sauber)
- **DataContext =:** 0 Treffer in Views (ViewLocator sauber)
- **StateLoaded-Pattern:** 8 Services abonnieren korrekt (RebirthService, PrestigeService, CraftingService, EventService, GoalService, VipService, ResearchService, GameLoopService)
- **Layer-Verletzungen:** Services->ViewModels-Direktzugriff nicht gefunden
- **Duplikation (intra-App):** Keine relevante gefunden (Premium-Multiplier-"Duplikation" ist bewusst, siehe Abend-Report)
- **ViewModel-Dead:** Alle 49 registrierten VMs werden genutzt (HeaderVM/PrestigeBannerVM/GoalBannerVM/WelcomeFlowVM exposed als Properties)

## Score: 87 | Top-3 Empfehlungen

1. IGameCurrencyService/IGameOrderService/IGameWorkshopService ersatzlos loeschen (213 Zeilen, 1 Merge)
2. 5 Raise*-Methoden `internal` machen (Interface-Contract verschlanken, Kompatibilitaet: nur GameStateService.Orders.cs ruft auf — aber intern)
3. DashboardView Gesture-State-Machine in Helper extrahieren (~150 Zeilen weniger Code-Behind)
