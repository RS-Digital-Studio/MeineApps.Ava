---
name: BingXBot Health Analyse 2026-04-05
description: Codebase-Gesundheitsanalyse BingXBot Trading Bot - Architektur, toter Code, Tests, technische Schulden
type: project
---

Analyse vom 05.04.2026. 113 CS-Dateien, ~14.100 Zeilen Produktionscode, 210 Tests (alle gruen).

**Sauber:**
- Abhaengigkeitsrichtung Core <- Exchange <- Engine <- Backtest <- Shared korrekt, keine zirkulaeren Referenzen
- TradingServiceBase-Vererbung (Paper/Live) eliminiert Duplikation in Trading-Logik
- Build 0 Warnungen, 0 Fehler
- Keine TODO/FIXME/HACK Kommentare
- Alle NuGet-Packages aktuell, keine Vulnerabilities

**Findings:**
1. Microsoft.ML + LightGBM in Engine.csproj referenziert aber nie importiert (~200MB+ NuGet-Ballast, "Phase 2" Kommentar)
2. IDataFeed Interface ohne Implementierung, wird in MarketScanner injiziert aber nie in DI registriert
3. Retry-Logik in BingXPublicClient.SendWithRetryAsync und BingXRestClient.SendSignedRequestAsync dupliziert
4. ATI-Subsystem (7 Klassen, ~1.850 Zeilen) komplett ohne Tests
5. CryptoTrendPro (Hauptstrategie, 434 Zeilen) ohne Tests
6. MarketFilter ohne Tests
7. DashboardViewModel (924Z) und TradingServiceBase (876Z) sind die groessten Dateien
