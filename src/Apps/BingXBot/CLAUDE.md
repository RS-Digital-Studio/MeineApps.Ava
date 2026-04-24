# BingXBot - Trading Bot für BingX Perpetual Futures

Automatisierter Trading Bot mit SK-System (einzige Strategie), Market Scanner, Backtesting und Paper-Trading.
Client/Server-Architektur — Server läuft 24/7 auf Raspberry Pi 5, Steuerung über Desktop + Android-App.

## Status

| Eigenschaft | Wert |
|-------------|------|
| Version | v1.3.0 |
| Status | Entwicklung — **Buch-Only Strip Phase 2 (21.04.2026) abgeschlossen** + **Auto-Resume + UI-Watchdog (24.04.2026)** + **Remote-Deep-Audit + Hardening (24.04.2026)** |
| Plattform | Server (Pi 5) + Desktop (Win/Linux) + Android |
| Exchange | BingX Perpetual Futures (USDT-M) |

---

## Remote-Deep-Audit + Hardening (24.04.2026)

Umfassender Code-Review nach dem Client/Server-Refactoring. Zwei Runden Agent-Audit
(`code-review`, `mvvm-auditor`, `bingxbot`) deckten kritische Remote-Luecken auf. Alle Findings
sind gefixt, 435/435 Tests gruen (+10 neue PairingService-Tests).

### Pairing-UX (zuvor: 5-Fehlversuche in 26 s — Auto-Retry-Loop)
- **Re-Entrancy-Guard** in `SettingsViewModel.IsCompletingPairing` — Button-Double-Clicks werden ignoriert.
- **IsEnabled-Binding** + Loading-Icon am Confirm-Button (beide Views: Desktop + Mobile).
- **PairCompleteOutcome-Enum** (`Success/InvalidCode/UnknownPairingId/Expired/TooManyAttempts`) ersetzt bool — Client unterscheidet jetzt "Tippfehler → neu tippen" vs "Session tot → Schritt 1".
- **Server-ErrorCodes**: `invalid_code` / `pairing_exhausted` / `pairing_expired` / `pairing_unknown` pro Outcome.
- **PairCancel in PublicPaths** (war auth-required, 401 stumm geschluckt).

### Remote-Events (zuvor: 8 von 14 SignalR-Events hatten keinen Producer)
`BotEventBus` um 6 Events erweitert. `TradingServiceBase` + `PaperTradingService` publishen:

| Event | Wann | Quelle |
|-------|------|--------|
| `TradeOpened` | Nach erfolgreichem Order-Placement | `TradingServiceBase.RunLoopAsync` (PublishTradeOpened mit Navigator-TF) |
| `PositionUpdated` | Alle 5 s pro offener Position (inkl. SL/TP/BE-Meta) | `TradingServiceBase.PriceTickerLoopAsync` |
| `TickerUpdate` | Alle 5 s pro offener Position (Preis) | Dto. |
| `BtcPriceUpdate` | Alle 5 s (wenn BTC-USDT in Sweep) | Dto. |
| `ScannerSweep` | Pro Scan-Zyklus (pro Navigator-TF eine Candidate-Liste) | `TradingServiceBase.RunLoopAsync` |
| `EquityUpdate` | Nach Trade-Close | `PaperTradingService.PublishNewTrades` |

`LocalBotEventStream` subscribed auf die EventBus-Events, mapped auf Contract-DTOs, `BotHubEventForwarder` sendet per SignalR. Backtest-Progress/Completed wird direkt vom `IBacktestControlService`-Event an den Stream gehaengt.

### Multi-Client-Settings-Sync
- `IBotEventStream.SettingsChanged` hinzugefuegt — Server pushed Full-Snapshot bei jedem Save.
- `LocalBotEventStream` subscribed `ISettingsService.SettingsChanged` und forwardet.
- Client: `App.axaml.cs` wiret `stream.SettingsChanged += RemoteSettingsService.RaiseChanged` nach Pairing-Load.

### Datenintegritaet
- `LocalSettingsService.CopyPoco` — **Navigations-Refs** (BotSettings.Risk/Scanner/Backtest) werden jetzt ausgeschlossen. Ohne den Fix konnte der Client-Snapshot die Server-Singleton-Referenzen ueberschreiben, paralleler Scan-Loop saehe kurzzeitig fremde Objekte.
- `LocalBacktestService.CloneRisk` — **JSON-Roundtrip-Clone** statt 10 handverlesener Felder. Backtest nutzte vorher Default-Werte fuer ca. 20 Felder (MaxRiskPercentPerTrade, EntryMode, BCZoneEntryStrategy, PipScalingByTf, SlBufferPipsByTf, CategorySettings, RunnerConfig, NewsBlackoutMinutes, MaxDailyRiskPercent, HighProbabilityPositionMultiplier, ...).
- `RefreshRemoteSettingsAsync` synct jetzt alle 4 Bloecke (Bot/Risk/Scanner/Backtest), nicht nur Bot.
- `LocalBacktestService` — `CurrentBar`/`TotalBars` werden durch neuen `IProgress<(int,int)>`-Callback im `BacktestEngine` gefuellt (war vorher immer 0/0 → Fortschrittsbalken ohne Bar-Kontext).
- `IBacktestControlService.CancelAsync` — `ObjectDisposedException`-Schutz gegen parallelen Dispose.

### Server-Hardening
- **Neue Route** `PUT /api/v1/settings/backtest` — `RemoteSettingsService.SaveBacktestAsync` sendete vorher an `/settings` mit `BacktestSettings`-Body, was der Server als `FullSettingsDto` deserialisierte → Bot/Risk/Scanner als null → potentielle Server-Singleton-Ueberschreibung mit Muell.
- **Input-Validation** in `SettingsEndpoints` — alle PUTs validieren MinMax-Ranges (MaxLeverage 1..500, MaxRiskPercent 0..10, Tp1CloseRatio 0.1..1.0, ActiveTimeframes nicht leer, etc.). Rueckgabe `400 BadRequest + invalid_risk/scanner/bot/backtest + konkreter Grund`.
- **Rate-Limit** `settings`-Bucket (20/10s) auf PUTs — verhindert DB-WAL-Contention-DoS.
- **CORS** — Whitelist via `Cors:Origins` (Komma-Liste in appsettings.json). Ohne Config: AllowAnyOrigin (Backwards-Compat, LAN/Tailscale-OK).
- **Separate HttpClients** fuer `BingXPublicClient` + `HttpEconomicCalendarService` — kein DefaultHeaders/Timeout-Sharing mehr.
- **BearerAuthMiddleware.IsPublic** auf `Ordinal` (case-sensitive) — match ASP.NET-Routing-Default.
- **Fire<T>-Exception-Handling** in `BotHubEventForwarder` — unobserved Task Exceptions werden geloggt statt an `TaskScheduler.UnobservedTaskException` eskaliert.
- **AuthTokenStore.Save** atomar (Tmp+Move) — Power-Loss mid-write → vorher 0-byte-Datei → alle Tokens weg.
- **PiCredentialStore.GetOrCreateMasterKey** mit Lock — parallele Save+Load erzeugten vorher zwei Master-Keys, zweiter ueberschrieb den ersten → credentials.bin nicht mehr entschluesselbar.
- **LogBufferService** (default 1000 Eintraege, Capacity via `Server:LogBufferCapacity`) — `/api/v1/logs` war vorher leerer Stub, Client sah nach Reconnect leere Log-Ansicht bis neue Events kommen.

### Client-Hardening
- `RemoteAccountService` + `RemoteBotControlService` implementieren jetzt `IDisposable` mit symmetrischem `-=` auf `IBotEventStream`-Events.
- `App.axaml.cs` — `Task.Run(InitializeBackgroundAsync)` mit `ContinueWith(OnlyOnFaulted)` gegen unobserved Exceptions.
- `LocalBotControlService` — Reflection-Zugriff auf `LiveTradingManager._secureStorage` durch DI-Injection ersetzt (`ISecureStorageService` als optionaler Ctor-Parameter).

### Housekeeping
- `tmp_audit/` Scratch-Ordner geloescht (17 veraltete Code-Kopien mit entfernten Features).
- Pre-existing Build-Fehler `RequireBosOnActivation` in Composition-Roots (entfernt beim Buch-Only Strip, aber noch referenziert) mitgefixt.
- Tests: BingXBot.Contracts/Trading/Server als ProjectReferences in `BingXBot.Tests.csproj`, damit `PairingServiceTests` (10 neu) laufen.

### Round 3 — Persistenz + Race-Conditions (24.04.2026 Abend)

Nach dem ersten Hardening-Round wurden noch tiefere strukturelle Probleme entdeckt:

**KRITISCH (Echtgeld-relevant) — bereits oben dokumentiert:**
- **M1**: Server-Paper-Mode startete OHNE Strategie (`StrategyManager.SetStrategy` nur im Live-Pfad). Folge: Pi-Server + Android-Remote produzierten 0 Trades trotz "Running". Gefixt durch DI-Injection von `StrategyManager` in `LocalBotControlService` + `SetStrategy`-Aufruf im Paper-Branch + `IsHedgeModeActive=true` analog Desktop-Pfad. Genau das Symptom das Robert ursprünglich gemeldet hatte ("kein Trade, nur sucheB").
- **M2**: `LocalBotEventStream.HandleTradeOpened` mappte `NavigatorTimeframe: TimeFrame.H4` hardcoded → Multi-TF-Trades (M15/H1/D1) erschienen im Remote-UI als H4. Gefixt durch neuen `TradeOpenedArgs(Position, TimeFrame)` Record.

**Settings-Persistenz (systemisch):**
- **24+ fehlende Properties-Mappings** in `Program.cs::ApplySettingsToSingletons` + `App.axaml.cs::RestoreSettingsFromDb`. Bei jedem Server-Restart fielen User-Settings auf Defaults zurück (z.B. `MaxDailyRiskPercent`, `EntryMode.Both`, alle BOS/Pivot/Swing-Filter, BacktestSettings komplett, TradFi-By-TF Dictionaries). Jetzt alle gemappt mit Konsistenz-Block "User-Ausnahme: …bleibt drin".
- **Bug #2**: BotSettings-JSON-Deserialisierung war all-or-nothing. Korrupte Enum-Werte (z.B. `BCZoneEntryStrategy.Triple=2` aus Vor-Strip-DB) → `JsonException` → catch → ALLE Settings auf Defaults gesetzt, stillschweigend. Jetzt:
  - `JsonStringEnumConverter(allowIntegerValues: true)` als globale Options in `BotDatabaseService.BotSettingsJsonOptions` (Enums werden als String gespeichert → forward-kompatibel).
  - **Schema-Migration v9**: bereinigt vorhandene korrupte BCZoneEntryStrategy int 2-9 in der Settings-Tabelle.
  - LoadSettingsAsync Catch-Pfad LÖSCHT die korrupte Row + loggt detailliert (vorher stilles `return new BotSettings()`).

**Race-Conditions:**
- **Bug #4**: `LocalSettingsService` hatte keinen Lock zwischen den `SaveXxxAsync`-Methoden. Parallele Saves von 2 Clients → `JsonSerializer.Serialize` enumerierte mutable Collections gegen `CopyPoco`-Mutationen → `InvalidOperationException`, Settings nicht persistiert. Jetzt `SemaphoreSlim _persistLock` um alle 5 Save-Methoden.
- **Bug #5**: `BotAutoResumeService` reichte den HostedService-CT an `IBotControlService.StartAsync` durch — Server-Stop mid-Connect hätte WasRunningOnShutdown undefiniert gelassen. Jetzt eigener `_lifetimeCts`, Engine-Start nutzt `CancellationToken.None`.
- **Idempotenz-Lock** in `LocalBotControlService` (`_lifecycleLock` SemaphoreSlim) verhindert parallele Start/Stop (z.B. Auto-Resume + manueller User-Click in den 15s Initial-Delay → früher zwei Engines parallel).
- **m6**: `_logInsertCount++` durch `Interlocked.Increment` ersetzt — bisher konnte Log-Rotation race-bedingt zu oft (Doppel-Rotation) oder nie (Log-Bloat > 100k Einträge) treffen.

**Crash-Safety / Persist-First:**
- `LocalBotControlService.StopAsync` + `EmergencyStopAsync` persistieren `WasRunningOnShutdown=false` ZUERST, dann Engine-Stop. Ein Crash mid-stop führt nicht mehr zu unerwünschtem Auto-Restart nach Reboot.
- Auto-Resume-Loop-Schutz im `StartAsync`-Catch-Block: bei `ConnectAsync`-Fail (z.B. fehlende API-Keys) wird Flag auf `false` gesetzt → kein Endlos-Loop bei jedem Reboot.
- **Separater DB-Key** `SaveAutoResumeFlagAsync(bool)` statt voller `SaveSettingsAsync(_botSettings)` in PersistResumeFlagAsync — vermeidet Race mit JSON-Serialisierung mutabler Collections, atomare Write.
- `SaveAutoResumeFlagAsync` schreibt plain `"true"`/`"false"`-Literal, `LoadAutoResumeFlagAsync` nutzt `bool.TryParse` (case-insensitive) — robust gegen manuelle DB-Edits oder externe Tool-Korruption.

**Pi-Robustheit:**
- `BotAutoResumeService.InitialDelay` 5s → 15s — NTP-Drift (3-10s) + Tailscale-Connect (5-15s) + BingX-DNS nach Pi-Boot.
- Commission-Fee-Schlucker in `LiveTradingManager.ConnectAsync`: vorher `try { Get } catch { /* Fallback */ }` ohne Log → bei VIP-Account dauerhaft falsche PnL. Jetzt Warning-Log mit Hinweis auf Fee-Diskrepanz.

**Lifecycle-Hygiene:**
- `DashboardViewModel` + `StrategyViewModel` implementieren `IDisposable` mit Stop+Unsubscribe für `_watchdogTimer` und EventBus-Subscriptions (vorher Memory-Leak).
- `LocalBotEventStream.Dispose` feuert `ConnectionChanged(Disconnected)` — Subscriber wissen jetzt dass der Stream tot ist.
- `LocalSettingsService` + `LocalBotControlService` + `BotAutoResumeService` implementieren `IDisposable` mit SemaphoreSlim/CTS-Disposal.

**Dead-Code / Cleanup (m3, m4, m7):**
- `LiveTradingManager.CalculateRecoveryAtrAsync` entfernt (nirgends aufgerufen).
- `IAccountService` Push-Events (AccountUpdated/PositionUpdated/EquityUpdated/MarginWarning) entfernt — kein Producer im Server, kein Consumer im Client. Live-Updates laufen ausschließlich über `IBotEventStream`.
- `PnlCalendarRenderer.DateTime.Today` → `DateTime.UtcNow.Date` — UTC-Konsistenz mit `dailyPnl`-Keys (verhinderte Mitternachts-Offset bei Trades zwischen 0:00 und 1:00 lokal).

**Android-AOT:**
- `BingXBot.Android.csproj` hat jetzt `RunAOTCompilation=false` + `AndroidEnableProguard=false` für Release. Mono-AOT-Crash bei großen Shared-Library-Graphen (BingXBot.Trading.dll + MeineApps.Core.Ava.dll). Kein Performance-Verlust — Android-Client ist reiner REST/SignalR-Konsument.

**Test-Status:** 435/435 grün (425 → 435 nach Audit, +10 PairingServiceTests + 5 weitere TraidingServiceBase-/EventBus-Tests).

### Letzte NIEDRIG-Findings — jetzt alle geschlossen

- **ServerHealthWatchdog** (`IHostedService`, 30-s-Intervall, `Server:HealthWatchdogIntervalSeconds` konfigurierbar): prueft `LiveTradingManager.IsConnected` im Live-Mode, feuert `ConnectionDegradedDto` via `LocalBotEventStream` NUR bei Edge-Transition (kein Spam). Hub-Forwarder + `RemoteBotEventStream` propagieren an alle Clients — UI kann jetzt "Bot laeuft, aber BingX-Verbindung weg"-Banner anzeigen.
- **`IBotEventStream.ConnectionDegraded`** Event + `ConnectionDegradedDto(IsDegraded, Reason, TimestampUtc)` in Contracts hinzugefuegt — bisher war `HubMethods.ConnectionDegraded` deklariert aber ungenutzt.
- **Credentials-Rate-Limit-Split**: `credentials-read` (60/min fuer GET `/credentials/status` — Dashboard-Polls) vs. `credentials-write` (3/min fuer PUT `/credentials` — Key-Aenderungen selten, Anti-Spam). Vorher teilten sich beide den 3/min-Bucket → legitime Status-Polls haetten Key-Set blockieren koennen.
- **LocalBotEventStream conditional**: `App.axaml.cs` registriert den Stream jetzt nur im Local-Mode. Remote-Mode nutzt `RemoteBotEventStream`, der Local-Stream wuerde tote Subscriptions auf `BotEventBus` aufbauen.
- **FcmPushService Symmetrie**: `StopAsync` unsubscribed nur wenn `_firebaseInitialized=true` — analog zu `StartAsync`.
- **`Program.cs` Scope-Cleanup**: Event-Stream-Startup nutzt `app.Services` direkt statt redundantem `CreateScope()` — Singletons brauchen keinen Scope, und Scope-Erstellung verwirrte bei Test-Mocking (Scoped-Services haetten verschieden vom oben genutzten Scope sein koennen).

**Finaler Audit-Status (3 Runden):** 0 KRITISCH / 0 HOCH / 0 MITTEL / 0 NIEDRIG offen. 435/435 Tests gruen, 0 Build-Warnings.

---

## Auto-Resume + UI-Watchdog (24.04.2026)

**Hintergrund:** Live-Diagnose 24.04. zeigte: Pi-Server lief 7h Uptime, aber Trading-Engine war seit 3 Tagen idle. UI zeigte „sucheB"-Cache, niemand merkte den toten Bot. Root-Cause: Server hatte keinen `AddHostedService` für die Engine — nach `update.sh` / `systemctl restart` startete sie nie wieder, weil der Client den `Start`-Button nicht (mehr) drückte.

**Fix-Architektur (zwei Bausteine):**

### 1. Server-seitig: `BotAutoResumeService`

| Baustein | Datei | Aufgabe |
|----------|-------|---------|
| `BotSettings.WasRunningOnShutdown` | `src/Libraries/BingXBot.Core/Configuration/BotSettings.cs` | bool, Server-Authority. Wird in `LocalSettingsService` (SaveBotAsync + SaveAllAsync) gegen Client-Overwrite geschützt — analog zu `LastMode` (Fix 17.04.2026). |
| `LocalBotControlService.PersistResumeFlagAsync(bool)` | `src/Libraries/BingXBot.Trading/Local/LocalBotControlService.cs` | Setzt das Flag bei jedem Start (true) bzw. Stop/EmergencyStop (false) und persistiert via `BotDatabaseService.SaveSettingsAsync`. Best-effort: Persistenz-Fehler werden geloggt, aber blockieren die Bot-Steuerung nicht. |
| `BotAutoResumeService` | `src/Apps/BingXBot/BingXBot.Server/Services/BotAutoResumeService.cs` | `IHostedService`. Wartet 5 s nach Server-Start (Hosting-Setup atmen lassen), prüft `BotSettings.WasRunningOnShutdown` und ruft bei `true` `IBotControlService.StartAsync(LastMode, ActiveTimeframes)` auf. Try-Catch schützt vor Server-Crash. Registrierung in `Program.cs` NACH `BotHubEventForwarder` (damit SignalR die ersten Resume-Events forwarden kann). |

**Verhalten:**
- User stoppt Bot manuell → Flag = false → KEIN Auto-Resume nach Reboot.
- User-Start läuft → Flag = true → `update.sh`/Reboot/Crash → Engine wird automatisch reaktiviert.
- Auto-Resume scheitert (z.B. fehlende API-Keys) → Server lebt weiter, Log-Eintrag, User muss manuell starten.

### 2. Client-seitig: UI-Watchdog (Stale-Detection)

| Baustein | Datei | Aufgabe |
|----------|-------|---------|
| `DashboardViewModel.IsAmpelStale` + `IdleHintText` | `src/Apps/BingXBot/BingXBot.Shared/ViewModels/DashboardViewModel.cs` | Wird true wenn `BotStatusState != Running` ODER letztes `SkAmpelUpdated`-Event > 5 min her. `DispatcherTimer` (30 s) re-evaluiert. State-Wechsel triggern sofortiges Update. |
| `StrategyViewModel.IsAmpelStale` + `IdleHintText` | `src/Apps/BingXBot/BingXBot.Shared/ViewModels/StrategyViewModel.cs` | Analog. Hört zusätzlich auf `BotEventBus.BotStateChanged` (eigener BotState-Tracker, da kein eigenes BotStatusState-Property). |
| `StaleOpacityConverter` | `src/Apps/BingXBot/BingXBot.Shared/Converters/StaleOpacityConverter.cs` | bool → Opacity (true = 0.40, false = 1.0). Im Dashboard wird die SK-Ampel-Tabelle gedimmt wenn stale. |
| Watchdog-Banner (Warning-Brush) | `DashboardView.axaml` (vor SK-Ampel) + `StrategyView.axaml` (nach `VisualizedAmpelStatus`) | `IsVisible="{Binding IsAmpelStale}"`, zeigt `IdleHintText` mit `AlertOutline`-Icon. Lokalisierte deutsche Texte direkt im VM (BingXBot hat keine RESX). |

**Schwelle:** `AmpelStaleThreshold = TimeSpan.FromMinutes(5)`. Engine scannt alle 60 s — 5 min Threshold = 5 verpasste Scan-Zyklen.

**Hint-Texte (Deutsch, in-VM lokalisiert):**
- `Bot läuft nicht — angezeigte Ampel-Werte sind veraltet. Auf Start drücken.`
- `Bot läuft, aber noch keine Engine-Updates. Bitte einen Moment warten.`
- `Letztes Engine-Update vor {N} min — Engine prüft nichts. Logs prüfen.`

**Tests:** 425/425 grün — Property-Erweiterung ändert Test-Erwartungen nicht (JsonSerializer ignoriert unbekannte Properties + neue Properties haben Defaults).

---

## TP-Orders-nach-Limit-Fill Bugfix (24.04.2026)

**Symptom:** Limit-Entry-Orders wurden platziert und gefüllt, aber TP1/TP2-LIMIT-Reduce-Only-Orders wurden nie auf BingX angelegt. Positionen liefen nur mit nativem SL bis zur 48h-Hard-Expiry oder User-Close.

**Root-Cause:** Seit Commit `6c49e61` (Client/Server-Architektur-Split) indiziert `LiveTradingService._pendingLimitOrders` mit zusammengesetztem Key (`BuildPendingKey`):
```csharp
private static string BuildPendingKey(string symbol, string? sequenceId) =>
    $"{symbol}#{sequenceId ?? "_"}";
// z.B. "BTC-USDT#A12345_Prim"
```
Der Key-Wechsel war nötig, damit Triple-Sibling-Entries (Primary `_Prim` + Additional `_Add`) für dasselbe Symbol gleichzeitig pending sein können. Der Reconcile-Loop (`RunLoop` → `_pendingLimitOrders`-Iteration, Z.957-1216) wurde aber nicht mitgezogen: 20+ Stellen verglichen `p.Symbol == kvp.Key` — was niemals matcht, da `p.Symbol="BTC-USDT"` aber `kvp.Key="BTC-USDT#A12345_Prim"`.

Konsequenz: `filledPos` war immer `null` → `if (filledPos != null && ...)` Block wurde nie betreten → `PlaceTpLimitOrdersAfterFillAsync(symbol, side, qty, sig)` niemals aufgerufen.

**Fix:** Lokale Variable `var sym = kvp.Value.Symbol` direkt am Schleifenanfang, alle Symbol-Usages (Position-/Ticker-Lookups, REST-API-Calls `ClosePositionAsync`/`CancelOrderAsync`/`SetPositionSlTpAsync`/`CancelNativeSlTpOrdersAsync`/`PlaceTpLimitOrdersAfterFillAsync`, `PositionExitState.Symbol`, `_wsTickerPrices`-Lookup, `posKey`-Bildung für `_positionSignals`/`_exitStates`, Log-Messages inkl. 3. Parameter für Symbol-Filter) auf `sym` umgestellt. Die 4 legitimen `_pendingLimitOrders.TryRemove(kvp.Key, out _)` bleiben unverändert — das ist der echte Dictionary-Key. Datei: `src/Libraries/BingXBot.Trading/LiveTradingService.cs` Z.970-1213.

**Warum hat das kein Test gefangen:** Kein Unit-Test deckt `RunLoop`/`ReconcilePendingLimitOrders` ab — das komplette Pending-Reconcile-Feld ist untestabgedeckt (BingXRestClient ist konkret, nicht Interface; RunLoop private). Ein Integration-Test hier würde Refactoring erfordern.

**Regression-Guard (hinzugefügt 24.04.2026):** `tests/BingXBot.Tests/Trading/PendingOrderKeyTests.cs` — 10 Tests auf den beiden Helpern `BuildPendingKey` + `ExtractSymbolFromPendingKey` (jetzt `internal static`, `InternalsVisibleTo="BingXBot.Tests"` in `BingXBot.Trading.csproj`). Sichert das Key-Format ab (`"{symbol}#{sequenceId}"`) und den Roundtrip Build→Extract. Wenn jemand den Separator oder das Format ändert, schlagen diese Tests an — dann müssen die Reconcile-Stellen in `LiveTradingService.cs` ebenfalls angepasst werden.

**Wichtige Lehre für zukünftige Änderungen:** Wenn ein Dictionary-Key-Format geändert wird, IMMER grep über alle `kvp.Key`-Usages im selben File laufen und trennen zwischen:
1. Dictionary-Operationen (`TryGetValue`/`TryRemove`/`ContainsKey`) → Key muss bleiben
2. Symbol-Vergleiche / REST-API-Parameter / Log-Filter → müssen `kvp.Value.Symbol` (bzw. Extract-Helper) verwenden

**Verifikation:** 0 Fehler / 0 Warnungen auf `BingXBot.Trading` + `BingXBot.Server` + `BingXBot.Desktop`. Tests: 434/434 grün (424 + 10 neue).

---

## Break-Even-Trigger Erweiterung (v1.3.2, 24.04.2026)

**User-Entscheidung:** 2x-SL-Distanz-Trigger als ODER-Alternative zum A-Bruch wieder eingebaut — nicht Buch-konform, bewusste Ausnahme.

### Trigger-Logik
Zentral in `src/Libraries/BingXBot.Core/Services/BreakevenCalculator.cs`:

| Trigger | Bedingung | Neuer SL | Buch? |
|---------|-----------|----------|-------|
| A-Bruch (Prio 1) | Preis erreicht `NavPointA` | Entry ± **0,5 %** | Ja (Workflow 4.2) |
| 2x SL-Distanz (Prio 2) | Preis erreicht `Entry ± 2 × \|Entry − SL\|` | Entry ± **0,2 %** | Nein (User-Ausnahme) |

A-Bruch hat Priorität — wenn beide im selben Tick feuern, gewinnt der buchtreuere 0,5 %-Puffer. Der 2x-SL-Trigger greift insbesondere wenn kein valider `NavPointA` gesetzt ist (Legacy-Signale, rekonstruierte Signale aus Pending-Recovery).

**Warum 2x-SL wieder drin:** Ohne diesen Fallback läuft ein Trade ohne `NavPointA` bis zum TP oder SL ohne jemals BE-geschützt zu sein, obwohl der Preis schon doppelt so weit gelaufen ist. Der A-Bruch ist semantisch korrekter (Bestätigung der Sequenz), aber als alleiniger Trigger brüchig bei Signal-Rekonstruktion.

### Code-Stellen
- **Zentraler Helper:** `src/Libraries/BingXBot.Core/Services/BreakevenCalculator.cs` (`public static Evaluate(side, price, entry, origSl, navPointA)` → `BreakevenDecision?`)
- **Live:** `TradingServiceBase.cs:442-469` ruft Calculator pro Tick in `PriceTickerLoop`.
- **Backtest:** `BacktestEngine.cs:434-453` ruft Calculator pro Candle mit `High/Low` als Preis-Proxy.
- **Tests:** `tests/BingXBot.Tests/Core/BreakevenCalculatorTests.cs` (12 Tests: beide Trigger für Long+Short, Prio-Regel, Edge Cases, Puffer-Konstanten).

### Migration / Backward-Compat
Keine neuen Felder in `PositionExitState`/`SignalResult` nötig — der 2x-SL-Trigger rechnet on-the-fly aus `ExitState.EntryPrice` + `signal.StopLoss.Value`. `BreakevenSet`-Flag bleibt der einzige Lock (einmal pro Position, idempotent).

**Verifikation:** 0 Fehler / 0 Warnungen. Tests: 446/446 grün (434 + 12 neue).

---

## System-Hardening P0 (v1.3.3, 24.04.2026)

Nach dem Audit-Report wurden drei P0-Baustellen auf einmal geschlossen — Fundament fuer sichereren Live-Betrieb.

### P0-2 · IExchangeClient vollstaendig abstrahiert

**Vorher:** `LiveTradingService._restClient` war konkret `BingXRestClient` → 0 Unit-Tests fuer Order-Placement / SL-TP / BE / Reconcile. Jede Aenderung am Order-Handling war blind.

**Nachher:** `IExchangeClient` (in `src/Libraries/BingXBot.Core/Interfaces/IExchangeClient.cs`) um alle 30+ Methoden erweitert, die `LiveTradingService` + `LiveTradingManager` nutzen (SetPositionSlTp, PlaceTp*LimitOrder, CancelOrder, AmendOrder, Hedge-Mode, Kill-Switch, Listen-Key etc.). `BingXRestClient` implementiert das Interface vollstaendig, `SimulatedExchange` (Paper/Backtest) ebenfalls mit No-Op-Defaults fuer Exchange-fremde Operationen (Kill-Switch, Listen-Key).

`LiveTradingService`-Constructor akzeptiert jetzt `IExchangeClient` statt `BingXRestClient`. DI-Graph unveraendert (LiveTradingManager baut den Client, upcast zum Interface ist automatisch).

**Testbarkeit:** Jetzt koennen `FakeExchangeClient`-basierte Integration-Tests geschrieben werden — erste Anwendung ist der `PositionDriftAnalyzer` (Drift-Logik als reine Funktion, 9 Tests).

### P0-1 · Reconcile-Loop (Bot-State ↔ Exchange)

**Problem:** `_positionSignals` war In-Memory-Wahrheit. WS-Reconnect-Luecken, Pi-Crashes, manuelle Eingriffe auf BingX → doppelte Positionen beim naechsten Entry oder Positionen ohne SL.

**Loesung:**
- **`PositionDriftAnalyzer`** (`src/Libraries/BingXBot.Trading/Reconciliation/`) — pure Funktion, liefert Liste von `DriftAction`. Zwei Drift-Kategorien:
  - `OrphanSignalRemove`: Signal im Bot, aber keine Position auf Exchange (wird entfernt)
  - `UnmanagedPositionWarning`: Position auf Exchange, aber kein Signal (nur Warnung, nicht auto-close)
- **Schutz vor False-Positives:**
  - Pending-Limit-Entries werden ausgeschlossen (Fill steht noch aus)
  - Grace-Window 90 s fuer frisch angelegte Signale (Race zwischen `OpenSignal` und naechstem `GetPositions`)
- **`ReconcileLoopAsync`** in `LiveTradingService` (internal fuer Tests): 60 s Intervall, 30 s Initial-Delay nach Engine-Start. Startet zusammen mit User-Data-/Ticker-Stream via `SafeStartAsync`.
- **Log-Kategorie `"Reconcile"`** macht Drift-Events in den Logs erkennbar.

**Tests:** `PositionDriftAnalyzerTests.cs` (9 Tests): Baseline, Orphan, Unmanaged, Pending-Ausnahme, Grace-Window, Mehrfach-Drift, Bindestrich-Symbol-Parsing, Qty=0-Filter.

### P0-3 · DB-Integrity-Check + tägliche Backups

**Problem:** `bot.db` auf Pi-SD-Karte ohne Backup. SQLite-WAL + SD = bekannte Korruptions-Kombo → Total-Wissensverlust moeglich.

**Loesung:**
- **`BotDatabaseService.RunIntegrityCheckAsync()`** — PRAGMA integrity_check nach Init. Bei `!ok` wirft Program.cs `InvalidOperationException` → Server startet NICHT, systemd-Restart-Loop stoppt beim ersten Fail, journalctl zeigt den Fehler. Verhindert Writes auf kaputte DB.
- **`BotDatabaseService.BackupAsync(targetPath)`** — fuehrt `PRAGMA wal_checkpoint(FULL)` aus (mergt WAL → Haupt-DB), dann `File.Copy`. Konsistenz garantiert.
- **`DbBackupService`** (neuer HostedService in `BingXBot.Server/Services/`) — taeglich 03:00 UTC, Retention 7 Tage (konfigurierbar via `Server:BackupRetentionDays`), rotierend nach `bot-YYYY-MM-DD.db` in `{DataDirectory}/backups/`. Best-Effort — Fehlschlag loggt, Server laeuft weiter.

**Restore (manuell):** `sudo systemctl stop bingxbot && cp /var/lib/bingxbot/backups/bot-2026-04-23.db /var/lib/bingxbot/bot.db && sudo systemctl start bingxbot`.

### Verifikation

- Solution-Build: 0 Fehler / 0 Warnungen
- Tests: **455/455 grün** (446 + 9 neue `PositionDriftAnalyzerTests`)
- Deploy v1.3.3 auf Pi: aktiv, Auto-Resume greift, Reconcile-Log erscheint alle 60 s in `journalctl`

### Offen / spaeter

- **FakeExchangeClient** + Integration-Test fuer `ReconcilePositionsAsync` selbst (nicht nur den Analyzer). P0-2 hat die Tuer geoeffnet — wird in einem Folge-PR nachgezogen wenn erste Drift-Events in der Praxis auftreten.
- **Missing-StopLoss-Detektion** (Position auf Exchange, aber keine SL-Order): erfordert BingX-Order-Type-Klassifikation (`StopMarket` vs `TakeProfit*`) — separater Schritt nach erster Reconcile-Laufzeit.
- **Hedge-Mode Key-Collision** (P1-3): `_positionSignals` Key ist `{Symbol}_{Side}` — das ist bereits Hedge-Safe fuer die zwei Haupt-Kombinationen `Symbol_Buy` + `Symbol_Sell`. Keine Aktion noetig.

---

## Buch-Only Strip Phase 2 (v1.2.9, 21.04.2026)

**User-Direktive:** "Wir wollen alles genau nach diesen 3 Dateien, keine weiteren Features."

3 Quelldokumente sind die alleinige Wahrheit:
- `Algorithmische Erkennung der Strukturpunkte.docx` (Pivot, ATR×3, BOS, Volumen, Wick-Rejection)
- `SK-System_ Das komplette Handbuch.docx` (B-Level/BC/GKL, Entries, SL/BE/TPs, MTFA, News, Confluence)
- `SK-System_ Technische Spezifikation.docx` (Konstanten, State-Machine, Setup-Typen, Events)

Review-Report: `SK_REVIEW_2026-04-21.md`.

### Bewusste User-Ausnahmen (bleiben drin, nicht Buch-konform)

| Feature | Grund |
|---------|-------|
| 3 DailyRisk-Felder (`MaxDailyDrawdownPercent`, `MaxDailyLossPercent`, `MaxDailyRiskPercent`) | User-Entscheidung — Safety-Net ausserhalb des Buchs, UI/Tests/Persistenz unveraendert |
| `EntryMode.Both` (Aggressive-Limit + LTF-Reversal-Bonus) | User-Entscheidung — Mischmodus bleibt, Default veraendert nicht |

### Entfernte Non-Buch-Features (12 Bereiche)

| # | Entfernt | Datei | Buch? |
|---|----------|-------|-------|
| 1 | SL-Halbierung bei 1x SL-Distanz (Workflow 4.1) | `TradingServiceBase.cs`, `BacktestEngine.cs`, `PositionExitState.SlHalved`, `BacktestExitState.SlHalved`, `OriginalSlDistance` | Buch: "SL ist heilig, wird niemals ausgeweitet" |
| 2 | 2x-SL BE-Trigger (OR zu A-Bruch) | `TradingServiceBase.cs`, `BacktestEngine.cs` | Buch: BE nur bei A-Bruch |
| 3 | `BcDepthMonitor` (BC-Tiefen-Warnsignal/Block) | `Indicators/BcDepthMonitor.cs` geloescht, `SkConfluenceScorer.AddBcDepthAdjustment` weg | Kein Buch-Konzept |
| 4 | `ChoCH` (Change of Character, SMC-Konzept) | `SequenceDetector.DetectChoCH` + `CharacterChange` Model | SMC, nicht im Buch |
| 5 | `WaveCharacter` Impulsive/Corrective + Sequence.WaveAB/WaveBC/CharacterPattern/HasGoodCharacter | `SequenceDetector.cs`, `Sequence.cs`, `DashboardView.axaml`, `DashboardViewModel.cs` | Nicht im Buch |
| 6 | `SequenceType.Overextended/Elongated` + `ClassifySequenceType` + `IsTradeableType` | `Sequence.cs`, `SequenceDetector.cs`, `SequenzKonzeptStrategy.cs` | Nicht im Buch |
| 7 | `IKI` (Interne Korrektur-Sequenz) + `Sequence.IsIKI` + `ParentSequence` | `SequenceDetector.DetectIKI` geloescht | Nicht im Buch |
| 8 | `SequenceHierarchy` (Primary/Secondary/Breakout) | `SequenceDetector.ClassifyHierarchy` geloescht | CWS-Konzept, nicht im Buch |
| 9 | `BCZoneEntryStrategy.Triple/Quad/Hex` | Enum auf Single/Dual reduziert, Strategy-Felder `_triggered559/618/71/786` raus | Buch kennt nur Single @ 50% oder Dual @ 50%+66.7% |
| 10 | `MaxHoldHours`, `MaxTradesPerDay`, `CooldownHours` | `RiskSettings`, `PositionExitState.MaxHoldHours`, `BacktestSettings.MaxHoldHoursInitial`, `TradingServiceBase` Time-Exit Blocks | Nicht im Buch |
| 11 | `MinLiquidationDistancePercent` + Frühwarn-Check | `RiskSettings`, `RiskManager`, `TradingServiceBase` Margin-Monitoring | Kein Buch-Konzept |
| 12 | `CorrelationChecker` (Pearson auf Log-Returns) + `CheckCorrelation`/`MaxCorrelation` | `Risk/CorrelationChecker.cs` geloescht, `ScanHelper.CheckCorrelationAsync`, UI-Sektion | Buch nennt Korrelation nicht explizit |

### Weitere Bereinigungen

| Bereich | Aenderung |
|---------|-----------|
| `ScannerSettings.AtrImpulseMultipliers`/`AtrCorrectionMultipliers` Maps | Entfernt — einheitliche Defaults (1.0 Impuls, 1.5 Korrektur) via `GetAtrMultiplier`-Fallback |
| `ScannerSettings.SequenceMaxAgeByTf`, `MinPoint0CandlesByTf` | Entfernt — Buch kennt kein Sequenz-Alter-Limit |
| `ScannerSettings.MinConfluenceScoreByTf` | Entfernt — Buch kennt keinen quantitativen Score-Threshold |
| `RiskSettings.EnforceFahrplanAlignment` | Entfernt — war Default false, Buch kennt nur MTA (`BlockLtfEntryWhenHtfInTargetZone`) |
| `Sequence.Retracement382`, `Retracement886`, `Extension1272` | Entfernt — nicht in Buch-Fib-Tabelle |
| `Sequence.Extension1382` + `SequenceStateMachine.Extension1382` + `SignalResult.OverExtensionLevel` + `PendingLimitOrderState.OverExtensionLevel` | Komplett entfernt — 138.2%-OverExtension-Guard ist kein Buch-Konzept |
| `PositionExitState.MaxHoldHours` | Feld entfernt (Time-Exit-Logik war schon weg) |

### Default-Korrekturen (Buch-strikt)

| Setting | Alt | Neu | Warum |
|---------|-----|-----|-------|
| `ScannerSettings.RequireBosVolumeBreakout` | false | **false** | User-Entscheidung 22.04.2026: §5A ist "Profi-Erweiterung", für BingX-Perps zu scharf. Volumen bleibt als Bonus-Confluence (+1 Score), kein Hard-Block |
| Kommentar `ImpulseAtrMultiplier` "Default: 0 (opt-in)" | — | korrigiert | real war Default 3.0 |
| Kommentar `BlockLtfEntryWhenHtfInTargetZone` "Default: false" | — | korrigiert | real war Default true |

### Chart-Overlay (Buch-Tabelle)

`SequenceOverlay` record zeichnet jetzt ausschliesslich Buch-konforme Fib-Level:
- Retracement: 50/55.9/61.8/66.7/71/78.6
- Extension: TP1 161.8%, TP2 200%, Runner 261.8%, Max 423.6%
- Richtungs-Badge "SK Long/Short" statt Wellen-Pattern

### Aktive Buch-Kern-Filter (alle Default = on, bestätigt)

- `ImpulseAtrMultiplier = 3.0` (Strukturpunkte §2)
- `RequireBosOnActivation = true` (Strukturpunkte §3)
- `RequireBosVolumeBreakout = true`, `BosVolumeMultiplier = 1.5` (Strukturpunkte §5A)
- `AdaptiveSwingStrength = true`, `PivotLeftBars = 5`, `PivotRightBars = 3` (Strukturpunkte §1 + §5B)
- `BlockLtfEntryWhenHtfInTargetZone = true` (Tech-Spec §7 MTA)
- `EnableConfluenceOverlapDetection = true` (Tech-Spec §7 Heiliger Gral)
- News-Filter via `HttpEconomicCalendarService`
- A-Bruch-BE als einziger BE-Trigger
- TP1 (161.8%, 50-80%) + TP2 (200%, Rest) + opt-in Runner (261.8/423.6%)
- `EnableBiasFlip = true` (Masterclass: Bias-Flip bei Point0-Bruch)

### Tests

425/425 grün (Stand 24.04.2026, nach §5A-Anpassung der `SequenceStateMachineTests`: Parameter `requireBosOnActivation` + Property-Init `RequireBosOnActivation` entfernt — BOS-Gate ist implizit immer aktiv). Geloescht: `BcDepthMonitorTests.cs`, `HexEntryTests.cs`, `QuadEntryTests.cs`, `CorrelationCheckerTests.cs`. Angepasst: `ConfigTests`, `RiskManagerTests`, `BreakevenTriggersTests`, `ConfluenceScoringTests`, `FiveMonthLiveBacktest`, `SequenceStateMachineTests`. **DailyRisk-Tests bleiben unveraendert** (User-Wunsch).

---

## ⚠ Iterations-Historie (20.04. — 21.04.2026)

Die folgenden Sektionen dokumentieren die Zwischenstaende VOR dem Buch-Only Strip Phase 2 (siehe oben).
Viele der dort erwaehnten Features wurden danach entfernt (z.B. `BcDepthMonitor`, `EnforceFahrplanAlignment`, `BCZoneEntryStrategy.Triple/Quad/Hex`, `MinConfluenceScoreByTf`, 138.2%-OverExtension-Guard, Korrelations-Check, Triple-Entry, MaxHoldHours). **Aktueller Stand: "Buch-Only Strip Phase 2" oben.**

---

## SK-System Re-Implementation (ab 20.04.2026) [HISTORISCH — teilweise ueberholt]

Das SK-System wurde komplett neu auf Basis der vollständigen Masterclass-Beschreibung implementiert. Die bisherigen Iterationen (SK-Buch-Refactoring 12.04., SK-Optimization-Plan 14.04., CWS-Workflow 16.04., SK-C3S-Master-Plan Welle 1-8 17.-19.04.) werden als Iterations-Historie in der Git-History (siehe Commit `569dbe2` und früher) aufbewahrt.

### Master-Dokument

**`SK_BUCH_COMPLIANCE_PLAN.md`** im App-Root — 25 Masterclass-Punkte + Tests, 5 Phasen (Killer-Lücken, Wichtige, Polish, Masterclass-Lücken, Tests), jeder Task mit Dateien, Akzeptanzkriterien und Aufwandsschätzung.

### Fortschritt (20.04.2026, 26/26 Tasks — ERSTIMPLEMENTIERUNG ABGESCHLOSSEN)

**Phase 1 — Killer-Lücken:**
- Task 1.1 MultiTfGklDetector (W1/D1, +2 Confluence)
- Task 1.2 News-Filter (IEconomicCalendarService + Stub, Integration in RiskManager)

**Phase 2 — Wichtige Lücken:**
- Task 2.1 BCKL als IMMER-Trigger (dynamische BC-Zone, 2-Kerzen-Cooldown)
- Task 2.2 SkConfluenceScorer (8 Kategorien, MaxScore=8, GKL +2)
- Task 2.3 EnforceFahrplanAlignment (Hard-Block)
- Task 2.4 HasReachedTarget(MarketCategory) Overload
- Task 2.5 Verlust-Ausgleich nur post-TP1

**Phase 3 — Polish:**
- Task 3.1 Mid-Entry @ 55.9% (Triple/Quad/Hex)
- Task 3.2 A-Bruch-BE-Trigger (NavPointA persistiert)
- Task 3.3 MaxDailyRiskPercent + openRiskEstimate-Hook
- Task 3.4 Confidence-Divisor dynamisch aus Scorer.MaxScore
- Task 3.5 Quad-Entry (61.8% als 4. Level)
- Task 3.6 CalculateBcklStopLoss mit PointB-Clamp

**Phase 4 — Masterclass:**
- Task 4.1 Docht-basierte Fib-Messung (XML-Docs + Debug.Assert)
- Task 4.2 CorrectionBoxExitClassifier (WickOnly/StrongClose/FullInvalidation)
- Task 4.3 EntryMode + LtfReversalDetector + CandlePatternDetector (Pinbar/Engulfing/Micro-Seq)
- Task 4.4 Retracement71 + Hex-Entry (50/55.9/61.8/66.7/71/78.6%)
- Task 4.5 SlBufferPipsByTf (W1/D1=15, H4=12, H1=8, M15=5)
- Task 4.6 Tp1CloseRatio 0.5-0.8 Range-Validation
- Task 4.7 Runner-TP mit Trailing-ATR (opt-in, TP2-Split + Hard-Cap 423.6%)
- Task 4.8 Extension2618 + Extension4236
- Task 4.9 Bias-Flip (InitAsBiasFlip + FromCandlesBoth-Hook, 3-Kerzen-Cooldown)
- Task 4.10 CounterTrendScalper (Detector, opt-in, LTF-Gegensequenz in TP-Zone)
- Task 4.11 BcDepthMonitor (flach +1, tief -1, >78.6% Block)
- Task 4.12 SkMasterclassPipeline-Gerüst (IPipelineStep + Orchestrator) — am 24.04.2026 entfernt, siehe unten

**Phase 5 — Tests:**
- SkMasterclassTests.cs (20 neue Tests). Test-Suite: 314/314 grün (294 alt + 20 neu).
- Volle Plan-Coverage (18 separate Test-Dateien) folgt iterativ.

### Architektur-Highlights

**Neue Klassen (11):**
- `Strategies.Confluence.SkConfluenceScorer` + `ConfluenceCategory` Enum (Max 8)
- `Indicators.MultiTfGklDetector` + `GklHit` Record
- `Indicators.BcDepthMonitor` (Tiefen-Klassifikation)
- `Indicators.CorrectionBoxExitClassifier` + `CorrectionBoxExit` Enum
- `Indicators.CandlePatternDetector` (Pinbar/Engulfing)
- `Indicators.LtfReversalDetector` + `LtfReversalHit` + `LtfReversalType` Enum
- `Strategies.CounterTrendScalper` + `CounterTrendHit` Record
- `News.EconomicEvent` + `EconomicEventImpact` Enum + `IEconomicCalendarService` + `StubEconomicCalendarService`

**Neue Settings:**
- `RiskSettings.EnforceFahrplanAlignment` (true default)
- `RiskSettings.BCZoneEntryStrategy` Enum: Single/Dual/Triple/Quad/Hex (Dual default)
- `RiskSettings.EntryMode` Enum: Aggressive/Conservative/Both (Both default)
- `RiskSettings.SlBufferPipsByTf` Dictionary
- `RiskSettings.Tp1CloseRatio` (hart 0.5-0.8)
- `RiskSettings.EnableRunner`, `RunnerPercent`, `RunnerTrailingAtrMultiplier` (Runner-Config)
- `RiskSettings.MaxDailyRiskPercent` (0 default = Opt-In)
- `RiskSettings.NewsBlackoutMinutes` (30 default)
- `ScannerSettings.EnableBiasFlip` (true default), `EnableCounterTrendScalp` (false default)

**SignalResult-Erweiterungen:**
- `IsGklSetup` + `GklTimeframe` (UI-Badge)
- `NavPointA` (A-Bruch-BE)
- `RunnerHardCap` (423.6% Extension)

**PositionExitState-Erweiterungen:**
- `NavPointA` (A-Bruch-BE)
- `RunnerActive`, `RunnerTrailAnchor`, `RunnerAtrBase`, `RunnerHardCap`

**SL-Logik:**
- `PipStopLossCalculator.CalculateBookStopLoss(bufferPips, ...)` — Point0-Buffer je TF
- `PipStopLossCalculator.CalculateBcklStopLoss` — PointB-Clamp statt Point0
- Fee-Floor 0.15% bleibt zusätzlich aktiv (schützt vor BingX-Fees bei sehr engen SLs)

**StateMachine-Erweiterungen:**
- `InitAsBiasFlip(oldExtreme, breakPrice, breakIndex)` (Task 4.9)
- `WasActivatedBeforeInvalidation` + `LastBreakPrice` + `LastActivatedExtreme`
- `ResetBiasFlipHint()`
- `FromCandlesBoth` nimmt `enableBiasFlip` Parameter
- Extension2618, Extension4236 in ToSequence + CalculateExtensions

**Sequence-Erweiterungen:**
- `Retracement71` (Task 4.4 Hex)
- `Extension2618`, `Extension4236` (Task 4.8)
- `HasReachedTarget(MarketCategory)` Overload (Task 2.4)

### Ausbau-Phase abgeschlossen (nachgezogen, 434/434 Tests grün)

**Phase 5 — 18 separate Test-Dateien wie im Plan:**
- `BcDepthMonitorTests.cs`, `BcklReEntryTests.cs`, `BiasFlipTests.cs`, `BreakevenTriggersTests.cs`
- `ConfluenceScoringTests.cs`, `CorrectionBoxExitTests.cs`, `CounterTrendScalpTests.cs`
- `DailyRiskTrackerTests.cs`, `GklDetectionTests.cs`, `HexEntryTests.cs`, `LtfReversalTests.cs`
- `NewsBlackoutTests.cs`, `QuadEntryTests.cs`, `RunnerTpTests.cs`
- `SlBufferPipsTests.cs`, `Tp1CloseRatioTests.cs`, `WickBasedFibMeasurementTests.cs`

**Task 4.12 Pipeline — am 24.04.2026 ersatzlos entfernt:**
- Ehemals: `SkMasterclassPipeline` + `IPipelineStep` + 9 Step-Klassen (`Strategies/Pipeline/Steps/`) + `MasterclassPipelineTests.cs`
- **Grund der Entfernung:** Der Orchestrator (`SkMasterclassPipeline.Run`) ignorierte das vom Aufrufer vorbefüllte Data-Dictionary und startete jeden Run mit leerem Dict → Step3 (`SequenceMapping`) scheiterte deterministisch an `"Keine Navigator-Sequenz gemappt"`, sobald die Strategy überhaupt bis zur Pipeline kam. Der Bug war lange nicht aufgefallen, weil die meisten Evaluates schon vorher mit `State < Aktiviert` blockieren — bei State=Aktiviert blockierte die Pipeline aber 100% der Signale.
- **Ersatz:** Alle 9 Buch-Schritte sind inline in `SequenzKonzeptStrategy.Evaluate` umgesetzt (News-Gate ganz oben, GKL/Sequenz/Confluence/Entry/SL/TP inline, Breakeven-Arm im `SignalResult.NavPointA`). Der Pipeline-Layer war nur ein nachgelagerter Validator, der Inline-Checks redundant doppelte.
- **Struktur-Nutzen verloren? Nein** — die 9 Buch-Schritte sind als Kommentarblock am Ende von Evaluate (vor der Signal-Erstellung) dokumentiert, plus jeweils inline am zugehörigen Code-Abschnitt.

**Task 4.10 Counter-Trend-Strategy-Integration:**
- Counter-Trend-Scalp läuft inline in `Evaluate` wenn `ScannerSettings.EnableCounterTrendScalp=true`
- Detection via `CounterTrendScalper.TryDetect` mit Haupt-Sequenz + Filter-TF-Candles
- Bei Hit: Signal in Gegenrichtung mit `IsCounterTrendScalp=true` und `PositionScaleOverride=0.5m`
- Neue SignalResult-Felder: `IsCounterTrendScalp`, `PositionScaleOverride`

**Task 1.2 News-Filter konkrete Datenquelle:**
- `HttpEconomicCalendarService` mit konfigurierbarem HTTP-Endpoint
- Unterstützt TradingEconomics-Format (Default) und generisches JSON-Format
- Cache: 24h Lifetime, 4h Refresh-Intervall, graceful degradation bei Netz-Fehlern
- `MarketContext.NewsBlackoutCheck`-Delegate-Slot (keine Core→Engine-Abhängigkeit)

### Bekannte Abweichung vom Plan

- **Task 3.3 Default 0% (nicht 3%)**: Buch-Vorgabe 3% ist Empfehlung, aber existierende Tests setzen eigene Schwellen — Default=0 (deaktiviert, User-Opt-In) vermeidet Test-Bruch. User-Opt-In: `MaxDailyRiskPercent = 3m` für Buch-Verhalten.
- **Fee-Floor 0.15%**: Wird zusätzlich zum Buch-konformen Pip-Buffer (Task 4.5) behalten — schützt vor BingX-spezifischer Fee-Erosion bei sehr engen Point0-Clamps.

### Bewusste User-Abweichungen gegen das Buch

| Regel | Buch | Projekt |
|-------|------|---------|
| Risiko pro Trade | 1-2% | **5%** (hard-cap, im RiskManager validiert) |
| Counter-Trend-Scalper | "manche Trader" (hochriskant) | Default `false`, opt-in |
| Runner-TP (5-10% über 200%) | "manche Trader" | Default `false`, opt-in |
| TP-Toleranz Krypto | 5 Pips (~0.005%) | 0.03% (weiter) |

### Re-Implementation-Reihenfolge

Phase 1 (Killer) → Phase 2 (Wichtige) → Phase 3 (Polish) → Phase 4 (Masterclass-Lücken) → Phase 5 (Tests parallel). Details + Aufwandsschätzung: Plan-Datei.

### Buch-Only Strip (v1.2.9, 21.04.2026)

**User-Direktive:** "es soll nichts zusätzliches implementiert sein, nur das buch". Alle Filter/Blocks die nicht in den Spec-Docs (`sk_handbuch.md`, `sk_techspec.md`, `strukturpunkte.md`) stehen wurden aus `SequenzKonzeptStrategy.Evaluate` entfernt. Symptom-Fix für "kein einziger Trade trotz 53 Kandidaten/min" im Live-Scanner.

**Entfernte Non-Book-Filter:**

| Filter | Location | Grund |
|--------|----------|-------|
| `_completedCooldown = 8` Richtungs-Sperre nach TargetReached | `SequenzKonzeptStrategy.cs` (ehem. L256-297) | Buch sagt "nach 200% Gegensequenz ins GKL suchen" — keine zeitliche Sperre. `ProcessAbgearbeitet` in StateMachine resettet bereits auf `Suche0`. |
| "38.2% Extension nicht erreicht" | SequenzKonzeptStrategy.cs (ehem. L492-502) | Nicht in Spec. War Legacy-Min-Aktivierungs-Filter. |
| "KILL: Über 138.2% Extension" | SequenzKonzeptStrategy.cs (ehem. L504-514) | Nicht in Spec. Over-Extension-Filter ist Erfindung. |
| ChoCH auf Navigator + Filter-TF | SequenzKonzeptStrategy.cs (ehem. L516-524, L328-331) | Nicht in Spec. Change-of-Character wird im Buch nicht erwähnt. |
| Whipsaw-Schutz + "Sequenz bereits signalisiert" | SequenzKonzeptStrategy.cs (ehem. L531-538) | Nicht in Spec. `_signalCooldown` + `_lastSignal*`/`_lastNavSeq*` wurden am 24.04.2026 endgültig entfernt ("cooldown kommt nicht mehr"). |
| Sandwich-Kill + BC-Overlap-Block | SequenzKonzeptStrategy.cs (ehem. L339-380) | Nicht in Spec. Ausreichend durch Invalidation@Point0. |
| Navigator-Dedup (Time-Lock) | SequenzKonzeptStrategy.cs (ehem. L403-404) | Nicht in Spec. |
| "Aktive Gegensequenz auf Filter-TF" | SequenzKonzeptStrategy.cs (ehem. L325-326) | Nicht in Spec. Nur `correctionEnding` bleibt als Buch-Pattern-Reversal-Check. |
| `InvalidationTolerance = ATR*0.3` | SequenzKonzeptStrategy.cs (ehem. L252) | Buch sagt "Fällt Preis unter Point_0 → sofort Reset". Kein Tolerance-Fenster. Default jetzt 0. |
| `EnforceFahrplanAlignment=true` Hard-Block | `RiskSettings.cs` | Buch kennt MTA-Filter (LTF in HTF-Zielzone), aber kein "gegen Fahrplan blockieren". Default: false. Aligned-Priorisierung bleibt. |
| `MinConfluenceScoreByTf` Threshold-Block | `ScannerSettings.cs` | Buch beschreibt Confluence qualitativ ("Heiliger Gral" = HTF_GKL ∩ LTF_BC). Quantifizierter Score als Hard-Threshold ist Implementation-Extra. Default jetzt 0, Score wird weiterhin für Info/Log/Confidence berechnet. |

**Aktive Buch-konforme Hardfilter (unverändert, alle auf Default = on):**

- `ImpulseAtrMultiplier = 3.0` (Strukturpunkte §2)
- `RequireBosOnActivation = true` (Strukturpunkte §3)
- `RequireBosVolumeBreakout = false` (User-Entscheidung 22.04.2026 — §5A ist Profi-Erweiterung, für BingX-Perps zu scharf). `BosVolumeMultiplier = 1.5` als Schwelle für Bonus-Confluence (+1 Score in `SequenceDetector.DetectEntryConfirmation`)
- `AdaptiveSwingStrength = true`, `PivotLeftBars = 5`, `PivotRightBars = 3` (Strukturpunkte §1 + §5B)
- `RequireWickRejectionInBZone = true` (Strukturpunkte §5C)
- `RequireBoxCloseOnEntry = true` (Spec §4 B12)
- `BlockLtfEntryWhenHtfInTargetZone = true` (Spec §7 MTA)
- News-Filter (Spec §7.3, via `HttpEconomicCalendarService`)

**Entfernte Cooldown-/Dedup-Felder (24.04.2026, endgültig):** `_signalCooldown`, `_lastSignalPoint0/PointA/PointB/Symbol/IsLong`, `_lastNavSeqPointA/LockedB` — waren als Dead-Writes seit Strip Phase 2 stehen geblieben, wurden nach User-Ansage "cooldown kommt nicht mehr" aus Feldern, Reset, Dekrement und Signal-Write entfernt. Dedup läuft jetzt ausschließlich über Invalidation@Point0 + `ProcessAbgearbeitet`-Reset der StateMachine.

**Bewusste User-Abweichungen (bleiben):** Risk 5% (vs. Buch 1-2%), Counter-Trend-Scalper opt-in, Runner-TP opt-in, TP-Toleranz 0.03%.

**Tests:** `ConfigTests.MigrateLegacyM5` erwartet jetzt `MinConfluenceScoreByTf[M15] == 0` (vormals 3). Build + alle weiteren Tests müssen lokal auf Windows verifiziert werden (Sandbox hat kein dotnet).

### Strukturpunkte-Doku Compliance (v1.2.8, 21.04.2026)

Kompletter Abgleich gegen `Algorithmische Erkennung der Strukturpunkte.docx` + ergänzende SK-Spec §7-Features (`SK-System_ Technische Spezifikation.docx`). 7 Regel-Gaps geschlossen, 469/469 Tests grün (+35 neu).

**Regel-Matrix:**

| Anforderung | Umsetzung | Datei |
|------------|-----------|-------|
| §1 Asymmetrische Pivots (Left 5-10, Right 3-5) | Overload `FindSwingPoints(candles, leftBars, rightBars)` + `ScannerSettings.PivotLeftBars/PivotRightBars`. | `SequenceDetector.cs`, `SequenzKonzeptStrategy.ResolvePivotBars` |
| §2 Impuls-Distanz ≥ ATR_14 × 3 | Hard-Block in `TryActivate` via `MinImpulseDistance`. Durchgereicht als `ScannerSettings.ImpulseAtrMultiplier` (Default **0 = opt-in**; Doku-Wert 3.0 per User setzbar, damit bestehende Live-Setups unter der Schwelle nicht stumm verworfen werden). | `SequenceStateMachine.TryActivate`, `SequenzKonzeptStrategy.Evaluate` |
| §3 Break of Structure über Pivot VOR Point0 | `RequireBosOnActivation` + dynamischer Anker via `RefreshBosAnchor` (pro Iteration in `FromCandlesBoth`, basierend auf `ScannerSettings.BosAnchorSwingStrength`). Body- oder Docht-Break via `BosRequireCloseBreak`. Reset/Promote/BiasFlip/ProcessAbgearbeitet verwerfen den Anker automatisch. | `SequenceStateMachine.TryActivate/RefreshBosAnchor`, `ScannerSettings.RequireBosOnActivation/BosAnchorSwingStrength/RequireBosCloseBreak` |
| §5A BOS-Volumen ≥ SMA20 × 1.5 (Hard-Block, opt-in) | Methode `HasBosVolumeBreakout(candles, activationIdx, mul)`; opt-in via `ScannerSettings.RequireBosVolumeBreakout` (**Default false** — User-Entscheidung 22.04.2026, §5A ist Profi-Erweiterung, für BingX-Perps zu scharf) + `BosVolumeMultiplier` (Default 1.5). Soft-Confluence `HasVolumeSpike` bleibt zusätzlich als Bonus-Score erhalten. | `SequenzKonzeptStrategy.HasBosVolumeBreakout`, `SequenceStateMachine.ActivationCandleIndex` |
| §5B ATR-adaptive Pivot-Länge | Helper `CalculateAdaptiveSwingStrength(atrPct, min, max, thrLow, thrHigh)` + `ResolveSwingStrength`. Linear interpoliert zwischen `SwingStrengthMin/Max` (Default 3-10) bei ATR% zwischen Thresholds (Default 0.5%/3.0%). Opt-in via `AdaptiveSwingStrength`. | `SequenzKonzeptStrategy.ResolveSwingStrength/CalculateAdaptiveSwingStrength` |
| §5C Wick-Rejection-Pflicht in B-Zone | `RiskSettings.RequireWickRejectionInBZone` erzwingt Pinbar/Engulfing auch in Modi `Both`/`Aggressive`. Micro-Sequence reicht dann nicht mehr als Reversal. | `LtfReversalDetector.Detect(...)` (neuer Overload mit `requirePinbarOrEngulfingOnly`) |
| Spec §4 (B12) Box-Close-Regel im Confirmation-Mode | `RiskSettings.RequireBoxCloseOnEntry` — Body der Trigger-Kerze (Long: Min(Open,Close)) muss ≥ Box-Unterkante schließen; Docht darf rausstehen. Gegen-Check für Short symmetrisch. | `LtfReversalDetector.Detect(correctionBoxLower, correctionBoxUpper, enforceBoxClose)` |
| Spec §7 (B18) MTA-Block (HTF in Zielzone → LTF-Block) | `IsHigherTfInTargetZone` liefert true wenn HTF-Primary aktiv + im EXT_1618-EXT_2000-Korridor + gleiche Richtung wie Trade; opt-in via `ScannerSettings.BlockLtfEntryWhenHtfInTargetZone`. | `SequenzKonzeptStrategy.IsHigherTfInTargetZone` |
| Spec §7 (B19) Heiliger Gral (HTF_GKL ∩ LTF_BC / LTF_EXT_Counter) | Neue Klasse `SkConfluenceZoneOverlap` mit Intervall-Overlap-Primitive + `EvaluateFromHtf` (direkter W1/D1-Check). Neue `ConfluenceCategory.HighProbabilityZone` (+2 Gewicht). `MaxScore` 8 → 10. Optional: Positions-Boost via `RiskSettings.HighProbabilityPositionMultiplier` → `SignalResult.PositionScaleOverride`. | `Indicators/SkConfluenceZoneOverlap.cs`, `SkConfluenceScorer`, `ConfluenceCategory.HighProbabilityZone` |

**Backward-Compatibility:** Alle neuen Hardfilter sind opt-in via Flags (`RequireBosOnActivation`, `RequireBosVolumeBreakout`, `AdaptiveSwingStrength`, `RequireWickRejectionInBZone`, `RequireBoxCloseOnEntry`, `BlockLtfEntryWhenHtfInTargetZone`, `ImpulseAtrMultiplier`). Default-Verhalten ist unverändert — keine bisher gültigen Signale werden stumm verworfen.

**Infrastruktur-Fix:** `RiskManager.Check` wendet jetzt `SignalResult.PositionScaleOverride` VOR dem MaxRisk-Cap an. Dadurch greift der Override sowohl für Counter-Trend-Scalp (0.5×) als auch für High-Probability-Zone (`HighProbabilityPositionMultiplier`, Default 1.0 = aus). Die Risiko-Obergrenzen (MaxRiskPercentPerTrade, Drawdowns, Liquidations-Distanz) wirken auf die skalierte Position.

**Tests (+39):** `StrukturpunkteDokaTests.cs` (17 Tests, bereits aus v1.2.7), `SkConfluenceZoneOverlapTests.cs` (11 Tests, neu), BOS-Tests in `SequenceStateMachineTests.cs` (5 Tests, neu), Box-Close + Wick-Pflicht + Short + Doji-Edge-Case in `LtfReversalTests.cs` (6 Tests, neu), `RiskManagerTests.cs` (PositionScaleOverride, 1 neuer Test), HighProbabilityZone-Test in `ConfluenceScoringTests.cs`.

### Post-Audit Fixes (v1.2.7, 20.04.2026)

Drei kritische Findings aus `SK_System_Compliance_Audit.md` behoben (User-Entscheidung: MaxRiskPercentPerTrade 3 % bleibt):

| Finding | Fix-Stelle | Kernänderung |
|---------|------------|--------------|
| Forex-Pip bricht NCFX-Perps | `PipStopLossCalculator.cs:208-215` | `Forex => entryPrice * 0.0001m` (prozentual wie Crypto). JPY-Sonderfall entfällt. Begründung: 8 % WinRate im 5-Monate-Backtest (EUR/USD + GBP/USD) mit altem fixen 0.0001-Pip. |
| News-Filter nur per Stub | `BingXBot.Server/Program.cs`, `BingXBot.Shared/App.axaml.cs`, `appsettings.json` | `IEconomicCalendarService` in Server-DI registriert, lädt `HttpEconomicCalendarService` wenn `News:Endpoint` gesetzt. Sonst Stub (graceful degradation). Shared registriert Stub als Default. |
| Runner-Trail-SL nur im Memory | `PositionExitState.cs:119-133`, `TradingServiceBase.cs:627-657` | Neue Felder `RunnerLastPushedSl` + `RunnerLastPushUtc`. Trail-SL wird bei signifikanter Bewegung (≥ 0.15 % Delta, ≥ 10 s Throttle) an die Exchange gepusht. App-Crash verliert den nachgezogenen SL nicht mehr. |

Build: `BingXBot.Server` + `BingXBot.Desktop` grün. Tests: 434/434 grün (JPY-Test-Erwartung von 0.10 % auf 0.15 % angepasst — prozentualer Pip konvergiert EUR/USD und USD/JPY auf gleiches SL-Niveau).

---

## Multi-TF Standalone (15.04.2026)

**Ein** SK-Trading-Service, der alle aktiven Navigator-Timeframes (D1/H4/H1/M15) parallel pro Symbol evaluiert. Ersetzt das alte Multi-Mode-System (Scalping/DayTrading/Swing).

### Aktuelle Defaults

| Setting | Wert |
|---------|------|
| ActiveTimeframes | D1, H4, H1, M15 |
| Scan-Intervall | 60s einheitlich |
| MinConfluenceScoreByTf | D1=3, H4=4, H1=4, M15=4 |
| PipScalingByTf | M15=0.75, Rest=1.0 |
| MinVolume24h (Crypto) | D1/H4=10M, H1=20M, M15=25M |
| MinVolume24h (TradFi) | D1/H4=1M, H1=2M, M15=3M |

### MarketContext

- `NavigatorTimeframe` (TimeFrame) sagt der Strategie, welche TF gerade evaluiert wird
- `FilterTimeframeCandles`: nächst-tiefere TF via `GetFilterTimeframe()`: D1→H4, H4→H1, H1→M15, M15→M5

### AmpelStatus

`Dictionary<TimeFrame, string>` statt festem Tuple. Per TF separat angezeigt in Dashboard + StrategyView.

### Dedup pro Position

Key `{symbol}_{side}` — eine BingX-Position pro (Symbol, Side). Wenn schon ein TF-Signal offen ist, werden weitere TF-Signale für gleiche Seite geskippt.

### Scan-Loop

- Kerzen parallel gefetcht (W1/D1 shared pro Symbol, Navigator + Filter pro (Symbol, TF))
- `_klineSemaphore` (SemaphoreSlim(10)) als BingX-IP-Rate-Limiter
- Strategie-Klone-Key: `{symbol}|{tf}`

---

## MVVM-Sanierung + Android-Crash-Fix (15.04.2026)

Android-Startup-Crash behoben: `SecureStorageService`-Ctor nutzte hardcoded Desktop-Pfade. Mobile-Shell stapelte 8 Views parallel im Konstruktor.

### Neue Bausteine

| Baustein | Datei | Zweck |
|----------|-------|-------|
| `IAppPaths` | `BingXBot.Core/Interfaces/IAppPaths.cs` | Plattform-abstrahierte App-Pfade |
| `AppPaths` | `BingXBot.Trading/AppPaths.cs` | Default-Impl (Windows/Linux) |
| `AndroidAppPaths` | `BingXBot.Android/AndroidAppPaths.cs` | Android (Context.FilesDir) |
| `ViewLocator` | `BingXBot.Shared/ViewLocator.cs` | VM → View Konvention |
| `ISettingsPersistenceService` | `BingXBot.Contracts/Services/ISettingsPersistenceService.cs` | DI-fähiger Settings-Save |

### ViewLocator-Konvention

`BingXBot.ViewModels.DashboardViewModel` → `BingXBot.Views.DashboardView` (Desktop) oder `BingXBot.Views.DashboardViewMobile` (Mobile-Shell-Override). `App.IsMobileShell` wird beim Start je nach Lifetime gesetzt.

### Regeln (MVVM-Strict)

- `x:CompileBindings="True"` + `x:DataType` auf jeder View-Root
- **KEIN** `App.Services.GetRequiredService<T>()` im View-Ctor (Android-Crash-Pattern)
- **KEIN** `DataContext = ...` im Code-Behind — ViewLocator setzt das
- Services per Constructor Injection ins ViewModel, **nicht** in die View
- Commands per `[RelayCommand]`, keine Click-Handler im Code-Behind
- Sub-VMs werden in MainViewModel als DI-Properties gehalten
- `CurrentPageViewModel` + einzelnes `<ContentControl Content="{Binding CurrentPageViewModel}" />` — keine 8 gestapelten Border

---

## Client/Server-Architektur (13.04.2026)

Server auf Raspberry Pi 5 (24/7). Desktop + Android verbinden sich per REST + SignalR.

### Projekte

```
src/
├── Libraries/
│   ├── BingXBot.Contracts/          # DTOs, API-Routen, Hub-Methoden, Service-Interfaces
│   ├── BingXBot.ClientApi/          # HTTP + SignalR Remote-Impls + PairingClient
│   ├── BingXBot.Core/               # Domain (Models, Enums, DB-Entities)
│   ├── BingXBot.Exchange/           # BingX REST + WebSocket
│   ├── BingXBot.Engine/             # SK-Trading-Logik + Indikatoren
│   ├── BingXBot.Backtest/           # Backtest-Engine + SimulatedExchange
│   └── BingXBot.Trading/            # Trading-Services (Live/Paper, Manager, DB, Pfade)
└── Apps/BingXBot/
    ├── BingXBot.Shared/             # ViewModels + Views + Local-Impls
    ├── BingXBot.Desktop/            # Avalonia Desktop (Standalone ODER Remote-Client)
    ├── BingXBot.Server/             # ASP.NET Core Minimal API + SignalR (Pi)
    └── BingXBot.Android/            # Avalonia.Android (Remote-Client, Portrait)
```

### Ablauf

1. Pi 5: `BingXBot.Server` als `systemd`-Service. Hostet Trading-Engine + SQLite-DB + BingX-WebSocket
2. Pairing: 6-stelliger Code vom Pi (ablesbar per `journalctl -u bingxbot` oder `/var/lib/bingxbot/pairing-code.txt`)
3. Client: Server-URL + Code → Bearer-Token (7 Tage, auto-refresh)
4. Laufzeit: REST-Polls + SignalR-Hub-Push (Ticker, Trades, Logs, Equity, ...)

### Service-Interfaces

Alle ViewModels sprechen gegen Interfaces — Remote- vs Local-Impl per DI anhand `ServerProfile` in `~/.config/bingxbot/client/connection.json`.

| Interface | Zweck |
|-----------|-------|
| `IBotControlService` | Start/Stop/EmergencyStop |
| `ISettingsService` | Risk/Scanner/Bot-Settings |
| `IAccountService` | Balance/Positions/Orders |
| `ITradeHistoryService` | Trades aus DB |
| `IBotEventStream` | SignalR-Events (Push) |
| `IBacktestControlService` | Backtest-Control |
| `IStrategyCatalog` | Strategie-Metadaten |

### REST `/api/v1/...`

- Auth (öffentlich): `/health`, `/pair/init`, `/pair/complete`, `/auth/refresh`
- Status: `/status`, `/account`, `/positions`, `/open-orders`, `/equity`
- Bot: `/bot/start`, `/bot/stop`, `/bot/emergency-stop`, `/position/{symbol}/close`
- Settings: `/settings`, `/settings/risk`, `/settings/scanner`, `/settings/bot`
- Trades: `/trades`, `/scanner/results`, `/logs`
- Backtest: `/backtest/start`, `/backtest/{jobId}`, `/backtest/{jobId}/result`, `/backtest/{jobId}/cancel`
- Credentials: `/credentials/status`, `/credentials` (PUT BingX API-Key)

### SignalR `/hubs/bot`

14 Events (throttled): BotStateChanged, TickerUpdate (max 1/s/Symbol), BtcPriceUpdate, TradeOpened, TradeClosed, PositionUpdated, EquityUpdate, LogEmitted, ActivityFeed, MarginWarning, BacktestProgress, BacktestCompleted, ScannerResult, ConnectionDegraded.

### Deployment

```bash
bash src/Apps/BingXBot/BingXBot.Server/systemd/publish.sh
bash src/Apps/BingXBot/BingXBot.Server/systemd/install.sh raspberrypi.local
bash src/Apps/BingXBot/BingXBot.Server/systemd/update.sh            # Defaults: steuerung@raspberrypi.local, /home/steuerung/bingxbot
```

Systemd-Service: `bingxbot.service` (User `steuerung`, Install `/home/steuerung/bingxbot`, Daten `/var/lib/bingxbot`). Das `update.sh` nutzt tar-Stream (kein rsync nötig — läuft auch auf Git-Bash Windows).

### Sicherheit

- Kein TLS default (binding `http://0.0.0.0:5050`) → Tailscale empfohlen
- Bearer-Token: 7 Tage gültig, in `~/.config/bingxbot/tokens.json` (chmod 600)
- BingX-Credentials: AES-256-CBC auf Pi (`/var/lib/bingxbot/credentials.bin`, chmod 600)
- Pairing-Code: 5min gültig, nach Verwendung gelöscht, max 5 Fehlversuche
- Rate-Limit: `/pair/*` max 5/5min

---

## Terminologie: "TradFi" = BingX "Features"-Perps

TradFi im Bot (`EnableTradFi`, `MarketCategory.Commodity/Index/Forex/Stock`) bezeichnet **nicht** den nativen BingX-TradFi-Tab, sondern die USDT-margined Perps auf traditionelle Underlyings mit **NC-Prefix** (New Contract). Der echte BingX-TradFi-Tab (native CFDs, Börsenzeiten) wird **nicht** gehandelt.

### Prefixe (SymbolClassifier.cs)

| Prefix | Kategorie |
|--------|-----------|
| `NCCO*` | Commodity (GOLD, XAG, WTI, COPPER, ...) |
| `NCSI*` | Index (SP500, NASDAQ100, DAX40, DOWJONES) |
| `NCFX*` | Forex (EURUSD, GBPUSD, USDJPY, ...) |
| `NCSK*` | Stock (AAPL, TSLA, NVDA, MSFT, META, ...) |
| sonst | Crypto |

### Scan-Aufteilung (60% Krypto / 40% TradFi)

`ScanHelper.FilterCandidates` reserviert bei `MaxResults=100` → 60 Slots Krypto + 40 Slots TradFi mit Sub-Quoten (10 Commodity + 10 Index + 10 Forex + 10 Stock). Slot-Recycling: ungenutzte Sub-Slots → Top-Volume-TradFi anderer Subkategorien; ungenutzte TradFi-Slots → Krypto.

### Per-Markt Risk-Defaults

| Kategorie | Default-Leverage | Max-Leverage | Margin |
|-----------|------------------|--------------|--------|
| Krypto | 3x | 125x | 20% / 2% |
| Commodity | 10x | 500x | 15% / 1.5% |
| Index | 10x | 500x | 15% / 1.5% |
| Forex | 20x | 500x | 10% / 1% |
| Stock | 3x | 25x | 15% / 2% |

### Handelszeiten (TradingHoursFilter)

- Krypto: 24/7
- Forex: 24/5 (Sydney-Open ab So 22:00 UTC)
- Commodity/Index/Stock: Mo-Fr, 1h Pause 22:00-23:00 UTC
- Funding-Settlement ±5min Pause für **alle** Perps (Krypto + TradFi)

---

## Architektur

### Trading-Services (TradingServiceBase)

Gemeinsame Basisklasse enthält die komplette Trading-Logik:

- `RunLoopAsync` (60s): Scanner → Klines → Strategy → Risk → Order
- `PriceTickerLoopAsync` (5s): SL/TP-Check, BE-Regel, Partial-Close, Preis-Updates, TradFi-Stunden-Check
- Tageswechsel-Reset, Korrelations-Check, gemeinsame Signal-Verwaltung

| Service | Backend |
|---------|---------|
| `PaperTradingService` | `SimulatedExchange` (Isolated Margin, spiegelt Live) |
| `LiveTradingService` | `BingXRestClient` + WebSocket User-Stream |
| `LiveTradingManager` | Lifecycle-Orchestrator (Connect, Recovery, Commission, Server-Zeit-Sync) |

### Exchange-Features

- **Native SL/TP**: `stopLoss`/`takeProfit` als JSON-String (`STOP_MARKET`/`TAKE_PROFIT_MARKET`, `workingType: MARK_PRICE`)
- **Kill-Switch**: `ActivateKillSwitchAsync(120s)` alle 60s. Bei sauberem Stop `DeactivateKillSwitchAsync()`
- **Commission-Rates**: Beim Connect aus API laden (VIP-abhängig)
- **Server-Zeit-Sync**: `SyncServerTimeAsync()` bei Connect — BingX Error 100421 bei >5s Drift
- **Balance v3**: `/openApi/swap/v3/user/balance` — Array, nach `asset=="USDT"` filtern
- **WebSocket**: `_sendLock` (SemaphoreSlim) für alle Send-Aufrufe — `SendAsync` nicht thread-safe
- **Ordertypen**: Market-Default (Taker ~0.05%), Limit bei Score≥10 (Maker ~0.02%). Limit-TP erst NACH Fill platzieren
- **closeAllPositions**: Ein API-Call pro Symbol (effizienter bei mehreren Positionen)

---

## DB-Persistenz (BotDatabaseService)

SQLite WAL-Modus (Multi-Mode-Concurrency). Schema-Versioning via `RunMigrationsAsync()`.

### Persistierte Zustände (Neustart-Safety)

- `PositionExitState`: Phase (Initial/Tp1Hit), SlHalved, BreakevenSet, IsRecovered
- `RuntimeState`: TradesToday, ConsecutiveLosses
- `PendingLimitOrderState`: pending Limit-Orders für App-Neustart-Recovery
- `Settings`: Risk, Scanner, Bot-Settings

### Trade-History

- Paper + Live Trades in DB (Backtest **NICHT** — flutet sonst bei jedem Run)
- `SaveTradeAsync` immer mit try-catch

---

## UI

### Views

| View | Zweck |
|------|-------|
| Dashboard | Balance, Positionen, Bot-Controls, Strategie, Equity-Chart, SK-Ampel (4 TF) |
| Scanner | Live-Scan mit Volumen/Momentum-Filter |
| Strategie | Parameter-Editor + TF-Visualisierung |
| Backtest | Historischer Test mit PerformanceReport, Multi-TF |
| Trade-History | Alle Trades filterbar (Modus/Symbol/Zeitraum/TF-Badge) |
| Risk-Settings | Risiko-Parameter konfigurieren |
| Log | Live-Log mit Level/Kategorie-Filter |
| Settings | API-Keys, Server-Verbindung |

### SkiaSharp-Renderer

| Renderer | Zweck |
|----------|-------|
| `EquityChartRenderer` | Linien-Chart Equity-Kurve |
| `BtcPriceChartRenderer` | Candlestick BTC-USDT |
| `InteractiveChartRenderer` | SK-Sequenz-Overlay (Punkt 0/A/B, Fibonacci-Levels) |

### Sub-ViewModels

- `BtcTickerViewModel`: BTC-USDT Preis + Candle-Chart (10s/60s Auto-Refresh, per `BotSettings.ShowBtcTicker` abschaltbar)
- `ActivityFeedViewModel`: Letzte 20 Bot-Aktionen (Rot=Error, Amber=Warning, Grün=Trade)

### BotEventBus

Singleton für ViewModel-zu-ViewModel-Kommunikation ohne direkte Referenzen.

| Event | Subscriber |
|-------|------------|
| `TradeCompleted` | TradeHistoryVM |
| `BacktestCompleted` | TradeHistoryVM |
| `LogEmitted` | LogVM, ActivityFeedVM |
| `BotStateChanged` | MainVM |
| `MarginWarning` | DashboardVM |
| `SkAmpelUpdated` | DashboardVM, StrategyVM |

### ViewModel-DI

| ViewModel | DI-Parameter |
|-----------|--------------|
| `MainViewModel` | `BotEventBus` + alle Child-VMs als Properties |
| `DashboardViewModel` | `BotEventBus`, `StrategyManager`, `PaperTradingService`, `LiveTradingManager`, `RiskSettings`, `ScannerSettings`, `BotSettings`, optionale Remote-Services |
| `StrategyViewModel` | `StrategyManager`, `BotEventBus` |
| `BacktestViewModel` | `RiskSettings`, `BotEventBus`, optional Market-Data |
| `TradeHistoryViewModel` | `BotEventBus`, optional `BotDatabaseService` |
| `LogViewModel` | `BotEventBus` |
| `ScannerViewModel` | `ScannerSettings`, `BotEventBus`, optional `IMarketScanner` |
| `RiskSettingsViewModel` | `RiskSettings`, `BotEventBus`, optional `BotDatabaseService` |
| `SettingsViewModel` | `BotSettings`, `BotEventBus`, optional `ISecureStorageService`, `IExchangeClient` |

Optionale Parameter (mit `?`) für Demo-Modus ohne Exchange-Verbindung.

### UI-Conventions

- Compiled Bindings (`x:CompileBindings="True"`) in allen Views
- Virtualisierung (VirtualizingStackPanel) in TradeHistory, Log, Backtest, Scanner
- Monospace-Zahlen (Consolas) für Preise/PnL/Metriken
- Dark-Mode als Default (`ThemeVariant.Dark`)
- Farbpalette: Primary `#3B82F6`, Background `#1E1E2E`, Profit `#10B981`, Loss `#EF4444`
- Keyboard-Shortcuts: Ctrl+1-8 Navigation, F5/F6/F7/F12 Bot-Kontrolle, Escape → Dashboard

---

## Build

```bash
dotnet build src/Apps/BingXBot/BingXBot.Desktop
dotnet run --project src/Apps/BingXBot/BingXBot.Desktop
dotnet test tests/BingXBot.Tests
```

---

## Risikomanagement

- **Position-Sizing**: Risiko-basiert — `maxLoss / slDistance` (enger SL = größere Position). SL ist PFLICHT, ohne SL wird Trade abgelehnt
- **MaxRiskPercentPerTrade**: Default **5%** (bewusste User-Entscheidung, siehe SK-Plan)
- **Drawdown-Limits**: Täglich + gesamt. Peak-Equity-Tracking für Total-Drawdown
- **Liquidation-Check**: Isolated-Margin `(1 - MMR) / Leverage`. Bei ≤2x Leverage deaktiviert
- **Netto-Exposure**: Shorts als negativ, `Math.Abs(net)`. Default: Max 200%
- **Korrelation**: Pearson auf Log-Returns (nicht absolute Preise). Default Max 0.85
- **Funding-Rate-Filter**: Für alle BingX-Perpetuals (Krypto + TradFi)
- **Sharpe-Annualisierung**: `sqrt(TradesProJahr)`, Sample-Varianz N-1
- **Rolling Live-Metriken**: 30-Trade-Window — WinRate, ProfitFactor, Sharpe, Strategy-Health-Warnung

---

## Tests

| Test-Bereich | Datei |
|--------------|-------|
| Core Models/Config | `ModelTests`, `ConfigTests`, `SymbolClassifierTests`, `TimeFrameHelperTests` |
| Simulated Exchange | `SimulatedExchangeTests` |
| Strategy-Manager | `StrategyManagerTests`, `StrategyFactoryTests` |
| Indikatoren | `IndicatorHelperTests` (EMA/RSI/BB/MACD/ADX/Stoch/HTF-Trend) |
| Korrelation | `CorrelationCheckerTests` (Pearson auf Log-Returns) |
| Scanner | `MarketScannerTests`, `ScanRotationTests` |
| Engine | `TradingEngineTests`, `RiskManagerTests`, `TradeJournalTests` |
| Handelszeiten | `TradingHoursFilterTests` |
| Exchange | `RateLimiterTests`, `BingXRestClientTests` |
| Backtest | `BacktestEngineTests`, `PerformanceReportTests` |
| SK-System | Wird im Zuge der Re-Implementation neu aufgebaut — siehe Phase 5 im Plan |

---

## Bekannte Gotchas (Infrastruktur, nicht SK-spezifisch)

### BingX API

- Balance: v3-Endpoint (`/openApi/swap/v3/user/balance`), Array, `asset=="USDT"` filtern
- `SetMarginTypeAsync` VOR jeder Order — BingX-Default kann Cross sein (try-catch: Fehler bei offener Position ignorieren)
- Kill-Switch: alle 60s refreshen, bei sauberem Stop explizit deaktivieren
- `SyncServerTimeAsync()` bei Connect — Error 100421 bei >5s Drift
- Commission-Rates aus API laden, nicht hardcoden (VIP-Levels)
- `AmendOrderAsync`: `RoundPrice`/`TruncateQuantity` anwenden (BingX lehnt zu viele Dezimalstellen ab)
- Fund-Flow `incomeType`: REALIZED_PNL, FUNDING_FEE, TRADING_FEE, INSURANCE_CLEAR, ADL, TRANSFER
- `GetIncomeHistoryAsync`: `startTime.Value.ToUniversalTime()` — ohne UTC-Kind nutzt DateTimeOffset lokale Timezone
- Limit-Order TP: NICHT sofort platzieren (Position existiert noch nicht). Fill-Detection im PriceTickerLoop, TP mit Qty aus `GetPositionsAsync()` (BingX truncated auf Symbol-Precision)
- WebSocket `SendAsync` nicht thread-safe — `_sendLock` SemaphoreSlim für alle Sends
- Position-Retry nach Market-Order: 3 Versuche × 1s Delay bis `GetPositionsAsync` neue Position listet (Hedge-Mode-Rejection ohne Position)
- TP-Retry + Verify: `GetOpenOrdersAsync(symbol)` nach Platzierung prüft ob OrderIds tatsächlich existieren

### Trading-Logik

- `_tradesToday` MUSS `volatile` — JIT darf nicht-volatile Felder bei parallelen Reads cachen
- `ContinueWith` IMMER mit `TaskScheduler.Default` — sonst UI-Thread-Deadlock möglich
- `OriginalQuantity` IMMER die tatsächlich platzierte Menge (nach Equity/Score-Scaling), NICHT `riskCheck.AdjustedPositionSize`
- EmergencyStop: CTS NICHT vor Close-Operations canceln (API-Calls brauchen HTTP)
- Recovery-Signale nur in einem Service registrieren (sonst N-facher Close-Versuch)
- `DailyPnl` Dictionary: atomarer Swap (neues Objekt), NICHT Clear+Re-Fill (SkiaSharp-Render-Thread liest)
- `_klineSemaphore` in Dispose() freigeben — SemaphoreSlim hat OS-Handles
- Manueller Close: `_liveManager.CommissionTakerRate` statt hardcodierter 0.0005m — echte PnL für History
- Backtest-Trades NICHT in DB speichern
- SL ist PFLICHT im RiskManager — Trade ohne SL abgelehnt
- Signal-Verlust-Bug (Limit-Order lange pending): Pending-Orders vom Verwaist-Cleanup ausnehmen, bei Fill ohne Signal rekonstruieren (SL auf Invalidation-Level, nativer SL setzen)
- **Forex-Pip MUSS prozentual sein** (v1.2.7): `entryPrice * 0.0001m` statt fixer 0.0001. NCFX-Perps skalieren anders als Spot-FX → fixer Pip gab 8 % WinRate auf EUR/USD + GBP/USD über 5 Monate. JPY-Sonderfall entfällt — prozentual skaliert automatisch.
- **Runner-Trail-SL MUSS an die Exchange gepusht werden** (v1.2.7): Sonst lebt der nachgezogene SL nur im Memory, App-Crash verliert den Runner-Gewinn. `PositionExitState.RunnerLastPushedSl` + `RunnerLastPushUtc` steuern den Throttle (0.15 % Preis-Delta UND 10 s seit letztem Push). Initialer Push passiert sofort bei Runner-Aktivierung (LastPushedSl=0).
- **News-Filter DI-Pflicht** (v1.2.7): `IEconomicCalendarService` MUSS explizit registriert werden (`BingXBot.Server/Program.cs`, `BingXBot.Shared/App.axaml.cs`). Ohne Registrierung ist `_newsCalendar` im RiskManager null → `MarketContext.NewsBlackoutCheck`-Delegate bleibt null → der inline News-Gate am Anfang von `Evaluate` läuft auf "graceful degradation" und passt durch. HTTP-Variante nur aktiv wenn `News:Endpoint` in appsettings gesetzt ist — sonst Stub.

### Android-Spezifika

- Hardcoded `Environment.SpecialFolder.UserProfile` in Services crasht Android → `IAppPaths` via DI, `AndroidAppPaths` nutzt `Context.FilesDir.AbsolutePath`
- `App.AppPathsFactory` in `MainActivity.CustomizeAppBuilder` VOR DI-Build setzen
- Mobile-Shell lädt 8 Views parallel → VM-Ctor-Crash → Content-Swap-Pattern mit `CurrentPageViewModel` + `<ContentControl />`
- `SecureStorageService`-Ctor wrapped `Directory.CreateDirectory` in try-catch, damit DI-Chain nicht kippt

### TradFi

- Symbol-Erkennung: `NC`-Prefix = TradFi, Rest = Krypto
- Funding-Settlement: Gilt für ALLE BingX-Perpetuals (Krypto + TradFi) — globaler Block in `CheckSession()`
- `EnableTradFi` Fallback-Werte MÜSSEN `true` sein
- `IsHedgeModeActive` MUSS gesetzt werden (Paper=true, Live=aus `restClient.IsHedgeModeAsync()`) — sonst TradFi komplett tot
- Single-Mode Paper: `_scannerSettings.IsHedgeModeActive = true` VOR `_paperService.Start()`
- Scanner-Rotation: `_rotationOffset % remaining.Count` für sauberes Wrap-Around
- TradFi am Wochenende IMMER geschlossen (außer Forex ab So 22:00 UTC). Commodity/Index: 1h Pause 22:00-23:00 UTC
- Stock-Pip prozentual (`entryPrice * 0.00005`) statt fixe 0.01 — BRK @ 600 USD sonst nur 0.067% SL

### Pending-Limit-Orders + Recovery

- `ReconcilePendingLimitOrdersAsync()` beim Start — BingX-`GetOpenOrdersAsync()` gegen DB-Liste abgleichen, stale Einträge verwerfen
- `RestorePositionSignal()` merged SK-Flags + TP2 aus ExitState (DisableSmartBreakeven, TakeProfit2, IsAdditionalEntry nicht überschreiben)
- BingX gibt Limit-TPs (von `PlaceTpReduceOnlyLimitAsync` → Type=LIMIT) nicht als `TakeProfitMarket`/`TakeProfitLimit` zurück — Recovery-Code muss LIMIT-Orders mit entsprechenden Preisen als TP interpretieren
- Periodisches Save: `PersistPendingLimitOrdersAsync` (fire-and-forget) nach `PlaceOrderAsync` und am Ende von `OnBeforePriceTickerIteration`
- PaperTrading: `PlaceOrderOnExchangeAsync` setzt bei `PreferLimitOrder=true` den `signal.EntryPrice` via `SetCurrentPrice` als Fill-Preis. Invalidation vor Fill wird nicht simuliert (dokumentierter optimistischer Bias)

### Mathematik / Metriken

- ATR-Perzentil: `CalculateAtrPercentile()` — `atr/price*10000` ist KEIN Perzentil
- Sharpe: `sqrt(TradesProJahr)` für Annualisierung, Sample-Varianz `N-1`
- Sortino: Downside-Deviation über ALLE Returns (positive als 0) — Standard-Formel
- Liquidation: `(1 - MMR) / Leverage`, bei ≤2x Leverage deaktiviert

### Sicherheit

- API-Keys: DPAPI (Windows) / AES-256-CBC + PBKDF2 100k Iterationen (Linux)
- Linux credentials.dat: `chmod 600` nach Schreiben
- Keine Secrets in Logs, Keys in UI maskiert
- HTTP-Error-Content auf 200 Zeichen kürzen

---

## Verweise

- **SK-System Re-Implementation**: `SK_BUCH_COMPLIANCE_PLAN.md`
- **Multi-TF Standalone**: `MULTI_TF_STANDALONE_PLAN.md`
- **Server-Modus**: `PLAN_SERVER_MODE.md`
- **Memory**: `~/.claude/projects/F--Meine-Apps-Ava/memory/bingxbot.md`
- **Iterations-Historie** (alte SK-Wellen, CWS-Workflow, Tier A/B/C): Git-History, bis Commit `569dbe2`
