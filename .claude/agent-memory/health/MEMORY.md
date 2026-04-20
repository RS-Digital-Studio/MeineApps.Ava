# Health Agent Memory

- [BingXBot Analyse 2026-04-05](bingxbot_health_2026-04-05.md) -- Architekturanalyse, saubere Schichtung, ML-Packages unbenutzt, Retry-Duplikation
- [HandwerkerImperium 2026-04-17](handwerkerimperium_health_2026-04-17.md) -- v2.0.29, 49 Services, MainVM 2277 Zeilen, 44 Test-Files, MVVM sauber
- [HandwerkerImperium 2026-04-18](handwerkerimperium_health_2026-04-18.md) -- v2.0.30, Phase 2-4 Refactor sauber, Score 87, CLAUDE.md-Versions-Drift, Splash-Fallback v2.0.22 stale
- [HandwerkerImperium 2026-04-18 Abend](handwerkerimperium_health_2026-04-18_abend.md) -- Score 89, CLAUDE.md+Splash gefixt. Neues Finding: RebirthService.cs:105 AddGoldenScrews ohne fromPurchase=true = doppelter Premium-Bonus bei Rollback.
- [Alle 12 Apps 2026-04-17](all_apps_health_2026-04-17.md) -- Build gruen, 0 Vulns, Score B. ZeitManager/RechnerPlus/FinanzRechner haben Service-Locator in Views, RebornSaga setzt DataContext im Code-Behind, GardenControl ohne BackPressHelper, BingXBot-CLAUDE.md inkonsistent zum ViewLocator-Refactor.
- [Solution-Health 2026-04-18](solution_health_2026-04-18.md) -- Score B+. Service-Locator-Regressionen behoben. Hauptbefund: 4x Microsoft.ML + GeneticSharp in Directory.Packages.props unbenutzt (0 csproj-Refs). RebornSaga setzt DataContext weiter im Code-Behind. HandwerkerImperium NotificationReceiver 5 CS8602-Warnings + doppelter using.
- [BomberBlast 2026-04-18](bomberblast_health_2026-04-18.md) -- v2.0.30, Score 8.5. MVVM sauber (0 Service-Locator), GameEngine-Refactor -836 Zeilen. Next: GameRenderer.Grid.cs 2057 Zeilen, ShopVM 1010 splitten, v2.0.29+ Drift in App-CLAUDE.md.
- [HandwerkerImperium 2026-04-20](handwerkerimperium_health_2026-04-20.md) -- v2.0.31, Score 87. NEU: IGameCurrency/Order/Workshop-Sub-Interfaces (213 Zeilen) werden NIEMALS einzeln injiziert. 5 Raise*-Methoden im Interface haben 0 externe Aufrufer. DashboardView.axaml.cs 802 Zeilen durch Gesture-State-Machine.
