using SQLite;
using BingXBot.Core.Data;
using BingXBot.Core.Configuration;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BingXBot.Trading;

public class BotDatabaseService
{
    private readonly IAppPaths _paths;
    private SQLiteAsyncConnection? _db;
    private int _logInsertCount;
    private const int LogRotationThreshold = 1000;
    private const int MaxLogEntries = 100_000;
    private const int LogCleanupBatch = 10_000;

    /// <summary>
    /// Aktuelle Schema-Version. Bei Änderungen erhöhen und Migration in RunMigrationsAsync() hinzufügen.
    /// v9 (24.04.2026): Bereinigt korrupte Enum-Werte (BCZoneEntryStrategy.Triple/Quad/Hex int 2/3/4)
    /// die nach Buch-Only Strip Phase 2 nicht mehr im Enum existieren — sonst all-or-nothing-Crash beim
    /// Deserialize, ALLE User-Settings gehen verloren.
    /// v10 (05.05.2026, Phase 0.7 Finding 0.7): Additive Strategy-Felder im PendingLimitOrderState
    /// (NavPointA / IsGklSetup / GklTimeframe / RunnerHardCap / IsCounterTrendScalp /
    /// PositionScaleOverride). Persistenz laeuft als JSON-Blob in Settings-Row "PendingLimitOrders" —
    /// alte Snapshots deserialisieren mit Default-Werten (additiv, keine ALTER TABLE-Aktion noetig).
    /// v11 (05.05.2026, Phase 4 Decision-Trail): Neue Tabelle EvaluationDecisions
    /// (CreateTableAsync legt sie an). Indices auf Timestamp DESC + Symbol + RejectionReason fuer
    /// Filter-Queries. Ringpuffer-Trim auf 50.000 Eintraege via DeleteOldestDecisionsAsync.
    /// v12 (06.05.2026, Phase 14 Settings-Audit-Trail): Neue Tabelle SettingsChanges. Indices
    /// auf Timestamp DESC + Field. Retention via Phase 11 DbArchiveService (90 d Default).
    /// </summary>
    private const int CurrentSchemaVersion = 12;

    /// <summary>
    /// JSON-Optionen fuer BotSettings (24.04.2026): Enums werden als String geschrieben (forward-kompatibel,
    /// menschlich lesbar in der DB), int-Werte werden weiterhin akzeptiert (backward-kompatibel zu alten
    /// persistierten Werten von vor diesem Fix). Bei UNBEKANNTEM int- oder String-Wert wirft JsonStringEnumConverter
    /// trotzdem — daher zusaetzlich Migration v9 + Catch-And-Reset in LoadSettingsAsync.
    /// </summary>
    private static readonly JsonSerializerOptions BotSettingsJsonOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(allowIntegerValues: true)
        }
    };

    /// <summary>
    /// Optionaler DB-Pfad-Override. Wenn null: Standard-Pfad aus IAppPaths (plattformabhängig).
    /// Wird im Server-Modus gesetzt (z.B. /var/lib/bingxbot/bot.db), damit Desktop + Server parallel laufen koennen.
    /// </summary>
    public string? DatabasePathOverride { get; set; }

    public BotDatabaseService(IAppPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// Liefert den tatsaechlich verwendeten DB-Pfad (Override oder IAppPaths-Default).
    /// Wird vom DbBackupService genutzt, um die Datei zu kopieren.
    /// </summary>
    public string ResolveDatabasePath() => DatabasePathOverride ?? _paths.DatabasePath;

    /// <summary>
    /// SQLite-Integrity-Check (PRAGMA integrity_check). Prueft die Konsistenz der DB-Datei.
    /// Liefert ("ok", "") wenn alles in Ordnung ist, sonst ("fail", Details).
    /// Sollte beim Startup aufgerufen werden — bei Fehler darf die Engine NICHT starten,
    /// sonst arbeitet sie auf korrupter DB und verliert Trade-History/Signale.
    /// </summary>
    public async Task<(bool Ok, string Details)> RunIntegrityCheckAsync()
    {
        if (_db == null)
            throw new InvalidOperationException("BotDatabaseService nicht initialisiert. InitializeAsync() zuerst aufrufen.");

        try
        {
            // PRAGMA integrity_check gibt eine Zeile "ok" bei sauberer DB zurueck,
            // sonst mehrere Zeilen mit konkreten Fehlern.
            var result = await _db.ExecuteScalarAsync<string>("PRAGMA integrity_check");
            var ok = string.Equals(result?.Trim(), "ok", StringComparison.OrdinalIgnoreCase);
            return (ok, ok ? "" : result ?? "unknown");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Erzeugt ein konsistentes Backup der DB nach <paramref name="targetPath"/>.
    /// Fuehrt vor dem Copy einen WAL-Checkpoint aus, damit offene WAL-Transaktionen in die
    /// Haupt-DB gemerged werden — sonst waere das Backup evtl. unvollstaendig.
    /// </summary>
    public async Task BackupAsync(string targetPath)
    {
        if (_db == null)
            throw new InvalidOperationException("BotDatabaseService nicht initialisiert. InitializeAsync() zuerst aufrufen.");

        var sourcePath = ResolveDatabasePath();
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"DB-Datei nicht gefunden: {sourcePath}");

        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
            Directory.CreateDirectory(targetDir);

        // WAL-Checkpoint: mergt ausstehende WAL-Writes in die Haupt-DB (FULL = wartet auf Writer).
        // ExecuteScalarAsync<string> weil PRAGMA-Returns in sqlite-net-pcl als Fehler interpretiert werden
        // wenn man ExecuteAsync nutzt (historischer Quirk — siehe CurrentSchemaVersion-Kommentar).
        try { await _db.ExecuteScalarAsync<string>("PRAGMA wal_checkpoint(FULL)"); }
        catch { /* best-effort — ein fehlgeschlagener Checkpoint heisst nicht dass die DB kaputt ist */ }

        // File.Copy ist thread-safe gegenueber sqlite-net-pcl-Zugriffen, weil SQLite im WAL-Modus
        // die Haupt-DB nur anhaengt (nie in-place umschreibt). Haupt-DB-Snapshot nach Checkpoint = konsistent.
        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    public async Task InitializeAsync()
    {
        var dbPath = DatabasePathOverride ?? _paths.DatabasePath;

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<TradeEntity>();
        await _db.CreateTableAsync<EquityEntity>();
        await _db.CreateTableAsync<LogEntity>();
        await _db.CreateTableAsync<SettingEntity>();
        await _db.CreateTableAsync<BacktestJobEntity>();
        // v1.6.3 Phase 14 — Settings-Audit-Trail-Tabelle.
        await _db.CreateTableAsync<SettingsChangeEntity>();

        // Indices für häufige Abfragen (idempotent dank IF NOT EXISTS)
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_ExitTime ON Trades (ExitTime DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_Mode ON Trades (Mode)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_Symbol ON Trades (Symbol)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_EquitySnapshots_Time ON EquitySnapshots (Time)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_LogEntries_Timestamp ON LogEntries (Timestamp DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_LogEntries_Level ON LogEntries (Level)");
        // v1.6.3 Phase 14 — Settings-Audit-Trail-Indices.
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_SettingsChanges_Ts ON SettingsChanges (Timestamp DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_SettingsChanges_Field ON SettingsChanges (Field)");

        // Schema-Versioning und Migrationen
        await RunMigrationsAsync();
    }

    /// <summary>
    /// Schema-Migrations-System: Prüft gespeicherte Version und führt ausstehende Migrationen aus.
    /// Neue Migrationen am Ende hinzufügen, Version in CurrentSchemaVersion erhöhen.
    /// </summary>
    private async Task RunMigrationsAsync()
    {
        var versionEntity = await _db!.FindAsync<SettingEntity>("SchemaVersion");
        var currentVersion = 1; // Default: Version 1 (initialer Stand)
        if (versionEntity != null && int.TryParse(versionEntity.Value, out var v))
            currentVersion = v;

        if (currentVersion >= CurrentSchemaVersion) return;

        // Migration v1 → v2: Funding-Rate-Spalte in Trades (für spätere Auswertung)
        if (currentVersion < 2)
            await TryAddColumnAsync("Trades", "FundingPaid", "REAL DEFAULT 0");

        // Migrations v2→v5 betrafen die FeatureSnapshots-Tabelle (ATI-ML-Features).
        // ATI wurde komplett entfernt (Buch-Refactoring) → Migrations übersprungen.
        // Alte Installs haben die Spalten bereits, neue Installs brauchen sie gar nicht.

        // Migration v5 → v6: WAL-Modus für bessere Concurrency (Regime-Spalte ATI-spezifisch, skipped)
        if (currentVersion < 6)
        {
            // sqlite-net-pcl-Quirk: PRAGMA journal_mode gibt ein Result zurueck ("wal"/"delete"),
            // ExecuteAsync interpretiert das als Fehler ("not an error"). Mit ExecuteScalarAsync umgehen.
            try { await _db!.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL"); }
            catch { /* best-effort: WAL ist Performance-Optimierung, kein Blocker */ }
        }

        // Migration v6 → v7: Verwaiste ATI-Daten aus alten Installs löschen
        if (currentVersion < 7)
        {
            try { await _db!.ExecuteAsync("DELETE FROM Settings WHERE Key='AtiState'"); } catch { /* best-effort */ }
            try { await _db!.ExecuteAsync("DROP TABLE IF EXISTS FeatureSnapshots"); } catch { /* best-effort */ }
        }

        // Migration v7 → v8: Multi-TF Standalone (15.04.2026)
        //   - TradingModePreset-Spalten/Settings sind nicht mehr relevant
        //   - BotSettings.LastTradingModePreset wurde aus dem Model entfernt — JsonSerializer ignoriert
        //     unbekannte Properties beim Deserialisieren, daher keine Migrations-Aktion nötig.
        //   - Legacy ExitStates/Pending-Orders werden weiterhin gelesen; Key-Schema bleibt "{symbol}_{side}".
        //   - Verwaiste TradingModePreset-Keys in Settings-Tabelle löschen.
        if (currentVersion < 8)
        {
            try { await _db!.ExecuteAsync("DELETE FROM Settings WHERE Key='LastTradingModePreset'"); } catch { /* best-effort */ }
        }

        // Migration v8 → v9 (24.04.2026): Korrupte Enum-Werte aus Buch-Only Strip Phase 2 (21.04.2026) bereinigen.
        // Hintergrund: Enum BCZoneEntryStrategy hatte vorher Single=0, Dual=1, Triple=2, Quad=3, Hex=4.
        // Nach dem Strip: nur noch Single=0, Dual=1. User mit alter DB hatten z.B. Triple (int 2) persistiert.
        // JsonSerializer.Deserialize<BotSettings> wirft JsonException auf den ungueltigen int — Catch fing das ab,
        // ALLE persistierten User-Settings (Risk, Scanner, Backtest, ServerUrl, ...) wurden stillschweigend
        // auf Defaults gesetzt. Symptom: User-Pi nach update.sh hat nur noch Default-Settings.
        // Strategie: Settings-Row mit korruptem JSON loeschen → naechster Save schreibt frische Werte
        // mit JsonStringEnumConverter (siehe SaveSettingsAsync). User merkt es einmalig (Settings-Reset),
        // Konsequenz aber vorhersehbar statt stiller Datenverlust.
        if (currentVersion < 9)
        {
            try
            {
                // Pattern matcht alle korrupten BCZoneEntryStrategy int-Werte 2-9 (Triple/Quad/Hex und alles >Dual).
                // Single=0/Dual=1 sind erlaubt → die werden nicht geloescht.
                await _db!.ExecuteAsync(
                    "DELETE FROM Settings WHERE Key='BotSettings' AND " +
                    "(Value LIKE '%\"BCZoneEntryStrategy\":2%' OR " +
                    " Value LIKE '%\"BCZoneEntryStrategy\":3%' OR " +
                    " Value LIKE '%\"BCZoneEntryStrategy\":4%' OR " +
                    " Value LIKE '%\"BCZoneEntryStrategy\":5%' OR " +
                    " Value LIKE '%\"BCZoneEntryStrategy\":6%' OR " +
                    " Value LIKE '%\"BCZoneEntryStrategy\":7%' OR " +
                    " Value LIKE '%\"BCZoneEntryStrategy\":8%' OR " +
                    " Value LIKE '%\"BCZoneEntryStrategy\":9%')");
            }
            catch { /* best-effort: schlimmster Fall = Catch-And-Reset in LoadSettingsAsync uebernimmt */ }
        }

        // Migration v9 → v10 (05.05.2026, Phase 0.7 Finding 0.7): Strategy-Felder im
        // PendingLimitOrderState (NavPointA/IsGklSetup/GklTimeframe/RunnerHardCap/
        // IsCounterTrendScalp/PositionScaleOverride). Persistenz ist additiv (JSON-Blob in
        // Settings-Row "PendingLimitOrders") → keine SQL-Aktion noetig. Alte Snapshots
        // deserialisieren mit Default-Werten; neue Server-Versionen brechen nicht (forwards/
        // backwards JSON-tolerant).
        // if (currentVersion < 10) { /* no-op, additiv */ }

        // Migration v10 → v11 (05.05.2026, Phase 4 Decision-Trail): Tabelle EvaluationDecisions
        // wird durch CreateTableAsync<EvaluationDecisionEntity>() oben angelegt. Hier nur Marker
        // setzen — kein expliziter SQL-Schritt noetig.
        // if (currentVersion < 11) { /* no-op, CreateTableAsync ist idempotent */ }

        // Schema-Version aktualisieren
        await _db.InsertOrReplaceAsync(new SettingEntity
        {
            Key = "SchemaVersion",
            Value = CurrentSchemaVersion.ToString()
        });
    }

    // === Trades ===

    public async Task SaveTradeAsync(CompletedTrade trade)
    {
        EnsureInitialized();
        await _db!.InsertAsync(TradeEntity.FromRecord(trade));
    }

    public async Task<List<CompletedTrade>> GetTradesAsync(TradingMode? modeFilter = null, int limit = 500)
    {
        EnsureInitialized();
        var query = _db!.Table<TradeEntity>();
        if (modeFilter.HasValue)
            query = query.Where(t => t.Mode == (int)modeFilter.Value);
        var entities = await query.OrderByDescending(t => t.ExitTime).Take(limit).ToListAsync();
        return entities.Select(e => e.ToRecord()).ToList();
    }

    // === Equity ===

    public async Task SaveEquitySnapshotAsync(EquityPoint point)
    {
        EnsureInitialized();
        await _db!.InsertAsync(new EquityEntity { Time = point.Time, Equity = point.Equity });
    }

    public async Task<List<EquityPoint>> GetEquitySnapshotsAsync(DateTime? from = null)
    {
        EnsureInitialized();
        var query = _db!.Table<EquityEntity>();
        if (from.HasValue)
            query = query.Where(e => e.Time >= from.Value);
        var entities = await query.OrderBy(e => e.Time).ToListAsync();
        return entities.Select(e => new EquityPoint(e.Time, e.Equity)).ToList();
    }

    // === Settings ===

    public async Task SaveSettingsAsync(BotSettings settings)
    {
        EnsureInitialized();
        // 24.04.2026: Mit JsonStringEnumConverter — Enums werden als String geschrieben.
        // Das uebersteht zukuenftige Enum-Reorderings (Triple/Quad/Hex-Bug Phase 2).
        var json = JsonSerializer.Serialize(settings, BotSettingsJsonOptions);
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "BotSettings", Value = json });
    }

    public async Task<BotSettings> LoadSettingsAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("BotSettings");
        if (entity == null) return new BotSettings();
        try
        {
            return JsonSerializer.Deserialize<BotSettings>(entity.Value, BotSettingsJsonOptions)
                ?? new BotSettings();
        }
        catch (JsonException ex)
        {
            // 24.04.2026: Korrupte Settings → DELETE der Row + Defaults zurueck.
            // Hintergrund: Frueher landete man hier still beim "return new BotSettings()" und
            // ALLE User-Settings (Risk, Scanner, Backtest, ServerUrl, ...) waren weg ohne dass
            // der User es merkte. Jetzt:
            // - Row aus DB loeschen → naechster Save schreibt frische Werte (vermeidet Endlos-Loop)
            // - Debug.WriteLine NICHT in System-Out, sondern Debug-Channel (wird beim Server-Start
            //   sichtbar wenn Logger noch nicht steht — der Catch lebt VOR DI-Build)
            // - Migration v9 sollte die haeufigste Korruption (Triple/Quad/Hex int 2-9) abfangen.
            //   Wenn DAS hier trotzdem feuert, ist eine neue Klasse Korruption am Werk.
            System.Diagnostics.Debug.WriteLine(
                $"BotSettings JSON korrupt ({ex.Message}). Snapshot wird geloescht, Defaults verwendet — " +
                $"User muss Settings einmalig neu speichern. Korrupter Wert (gekuerzt): " +
                $"{(entity.Value?.Length > 200 ? entity.Value[..200] + "..." : entity.Value)}");
            try { await _db!.ExecuteAsync("DELETE FROM Settings WHERE Key='BotSettings'"); }
            catch { /* best-effort: schlimmster Fall = beim naechsten Start nochmal Defaults */ }
            return new BotSettings();
        }
    }

    /// <summary>
    /// Separater Auto-Resume-Flag (24.04.2026). Bewusst NICHT Teil von `SaveSettingsAsync(BotSettings)`,
    /// damit Start/Stop den Flag persistieren können, ohne die komplette `BotSettings`-Serialisierung
    /// (mit mutablen Collections wie `Scanner.ActiveTimeframes`, `Whitelist`, `CategorySettings`, ...)
    /// auszulösen. Das vermeidet die `JsonSerializer`-Race wenn der User parallel UI-Änderungen macht
    /// während die Engine gerade startet/stoppt (bekannter Gotcha: Collection-Modifikation während Serialize).
    /// </summary>
    public async Task SaveAutoResumeFlagAsync(bool value)
    {
        EnsureInitialized();
        // Bewusst Lowercase-Literal statt JsonSerializer (24.04.2026 Robustness #4):
        // JsonSerializer.Deserialize<bool> ist case-sensitiv und akzeptiert NUR "true"/"false".
        // Wenn das DB-File jemals manuell editiert oder von externem Tool geschrieben wird (z.B. "True"
        // mit Großbuchstabe), wuerde Deserialize JsonException werfen → Flag stillschweigend false.
        // Plain "true"/"false" String + Bool.TryParse beim Lesen ist forgiving.
        var literal = value ? "true" : "false";
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "AutoResumeFlag", Value = literal });
    }

    /// <summary>Lädt das persistierte Auto-Resume-Flag. Default `false` wenn fehlt oder korrupt.</summary>
    public async Task<bool> LoadAutoResumeFlagAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("AutoResumeFlag");
        if (entity?.Value == null) return false;
        // Bool.TryParse ist case-insensitive und akzeptiert "True"/"true"/"TRUE" gleichermaßen.
        // Fallback fuer alte JSON-serialisierte Werte ("true"/"false") gleich.
        return bool.TryParse(entity.Value.Trim(), out var result) && result;
    }

    /// <summary>
    /// Persistiert Mode + Engine des letzten Engine-Starts als separate Keys (analog AutoResumeFlag,
    /// bewusst NICHT im BotSettings-Blob — der wird nur bei Client-Settings-Saves geschrieben).
    /// Ohne diese Persistenz startete Auto-Resume nach einem Pi-Reboot IMMER den Scalper
    /// (BotStartRequest-Default), auch wenn zuvor Cross-Sectional lief — im Worst-Case der
    /// Live-Scalper mit Echtgeld statt des Paper-Xsec-Tests.
    /// </summary>
    public async Task SaveResumeEngineAsync(TradingMode mode, EngineMode engine)
    {
        EnsureInitialized();
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "ResumeMode", Value = mode.ToString() });
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "ResumeEngine", Value = engine.ToString() });
    }

    /// <summary>Lädt Mode + Engine des letzten Engine-Starts. Null wenn nie persistiert/korrupt.</summary>
    public async Task<(TradingMode Mode, EngineMode Engine)?> LoadResumeEngineAsync()
    {
        EnsureInitialized();
        var modeEntity = await _db!.FindAsync<SettingEntity>("ResumeMode");
        var engineEntity = await _db!.FindAsync<SettingEntity>("ResumeEngine");
        if (modeEntity?.Value == null || engineEntity?.Value == null) return null;
        if (!Enum.TryParse<TradingMode>(modeEntity.Value.Trim(), ignoreCase: true, out var mode)) return null;
        if (!Enum.TryParse<EngineMode>(engineEntity.Value.Trim(), ignoreCase: true, out var engine)) return null;
        return (mode, engine);
    }

    /// <summary>
    /// Phase 18 / B2 — Persistiert den letzten Bot-Heartbeat-Zeitstempel (UTC, ISO-8601 "O").
    /// Wird vom <c>TradingServiceBase</c> alle 30 s in der PriceTickerLoop geschrieben. Beim
    /// Server-Resume liest <c>BotAutoResumeService</c> den Wert und entscheidet, ob ein
    /// Trade-Replay (GetUserTradesAsync since=lastHeartbeat) noetig ist.
    /// </summary>
    public async Task SaveLastHeartbeatAsync(DateTime utc)
    {
        EnsureInitialized();
        await _db!.InsertOrReplaceAsync(new SettingEntity
        {
            Key = "LastHeartbeatUtc",
            Value = utc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    /// <summary>
    /// Phase 18 / B2 — Liest den letzten persistierten Heartbeat. NULL = kein bisheriger
    /// Heartbeat (Frischer Pi, oder fruehere Version ohne Persistenz).
    /// </summary>
    public async Task<DateTime?> LoadLastHeartbeatAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("LastHeartbeatUtc");
        if (string.IsNullOrEmpty(entity?.Value)) return null;
        return DateTime.TryParse(entity.Value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    // === SK-VERIFY: [6.1] ExitState + Runtime-State Persistenz ===

    /// <summary>Speichert alle ExitStates für offene Positionen (bei Bot-Stop/Crash-Recovery).</summary>
    public async Task SaveExitStatesAsync(Dictionary<string, PositionExitState> exitStates)
    {
        EnsureInitialized();
        var json = JsonSerializer.Serialize(exitStates);
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "ExitStates", Value = json });
    }

    /// <summary>Lädt gespeicherte ExitStates für Crash-Recovery.</summary>
    public async Task<Dictionary<string, PositionExitState>?> LoadExitStatesAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("ExitStates");
        if (entity?.Value == null) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, PositionExitState>>(entity.Value); }
        catch { return null; }
    }

    /// <summary>Speichert Runtime-State (TradesToday, ConsecutiveLosses, aktive Strategie) für Crash-Recovery.
    /// Der Strategie-Name erlaubt beim Start einen Reset der Loss-Historie bei Strategiewechsel.</summary>
    public async Task SaveRuntimeStateAsync(int tradesToday, int consecutiveLosses, string? strategyName = null)
    {
        EnsureInitialized();
        var state = new { TradesToday = tradesToday, ConsecutiveLosses = consecutiveLosses, StrategyName = strategyName };
        var json = JsonSerializer.Serialize(state);
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "RuntimeState", Value = json });
    }

    /// <summary>Lädt Runtime-State für Crash-Recovery. StrategyName ist null bei altem Format (vor Strategie-Tagging).</summary>
    public async Task<(int TradesToday, int ConsecutiveLosses, string? StrategyName)?> LoadRuntimeStateAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("RuntimeState");
        if (entity?.Value == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(entity.Value);
            var root = doc.RootElement;
            var tradesToday = root.GetProperty("TradesToday").GetInt32();
            var consecutiveLosses = root.GetProperty("ConsecutiveLosses").GetInt32();
            var strategyName = root.TryGetProperty("StrategyName", out var sn) && sn.ValueKind == JsonValueKind.String
                ? sn.GetString() : null;
            return (tradesToday, consecutiveLosses, strategyName);
        }
        catch { return null; }
    }

    // === Pending Limit Orders Persistenz (TP-Recovery nach App-Neustart) ===

    // === Backtest-Jobs (persistiert ueber Server-Restarts) ===

    public async Task UpsertBacktestJobAsync(BacktestJobEntity job)
    {
        EnsureInitialized();
        await _db!.InsertOrReplaceAsync(job);
    }

    public async Task<BacktestJobEntity?> GetBacktestJobAsync(string jobId)
    {
        EnsureInitialized();
        return await _db!.FindAsync<BacktestJobEntity>(jobId);
    }

    public async Task<List<BacktestJobEntity>> GetAllBacktestJobsAsync()
    {
        EnsureInitialized();
        return await _db!.Table<BacktestJobEntity>().ToListAsync();
    }

    /// <summary>
    /// Markiert alle Queued/Running-Jobs als Failed — wird beim Server-Start aufgerufen,
    /// damit Clients keine Orphan-Jobs im "Running"-State sehen.
    /// </summary>
    public async Task MarkOrphanedBacktestJobsAsFailedAsync()
    {
        EnsureInitialized();
        await _db!.ExecuteAsync(
            "UPDATE BacktestJobs SET State=?, Error=?, CompletedAtUtc=? WHERE State IN (?, ?)",
            "Failed", "Server wurde neu gestartet, bevor der Job fertig war.",
            DateTime.UtcNow, "Queued", "Running");
    }

    // === Logs ===

    public async Task SaveLogAsync(LogEntry entry)
    {
        EnsureInitialized();
        await _db!.InsertAsync(new LogEntity
        {
            Timestamp = entry.Timestamp,
            Level = (int)entry.Level,
            Category = entry.Category,
            Message = entry.Message,
            Symbol = entry.Symbol
        });

        // 24.04.2026 Phase-4-Audit m6: Interlocked statt int++.
        // Bisher: _logInsertCount++ war race-anfaellig gegen parallele BotEventBus.LogEmitted-Subscriber
        // → konnte den Threshold zu oft (Doppel-Rotation) oder nie (verlorene Increments) treffen.
        // Worst-Case: Log-Bloat > 100k Eintraege → DB wird langsam.
        var newCount = System.Threading.Interlocked.Increment(ref _logInsertCount);
        if (newCount >= LogRotationThreshold)
        {
            // Atomar zuruecksetzen — nur der Thread der genau die Schwelle trifft fuehrt Rotation aus.
            System.Threading.Interlocked.Exchange(ref _logInsertCount, 0);
            await RotateLogsAsync();
        }
    }

    public async Task<List<LogEntry>> GetLogsAsync(int limit = 200, Core.Enums.LogLevel? levelFilter = null)
    {
        EnsureInitialized();
        var query = _db!.Table<LogEntity>();
        if (levelFilter.HasValue)
            query = query.Where(l => l.Level == (int)levelFilter.Value);
        var entities = await query.OrderByDescending(l => l.Timestamp).Take(limit).ToListAsync();
        return entities.Select(e => new LogEntry(
            e.Timestamp, (Core.Enums.LogLevel)e.Level, e.Category, e.Message, e.Symbol)).ToList();
    }

    private async Task RotateLogsAsync()
    {
        var count = await _db!.Table<LogEntity>().CountAsync();
        if (count > MaxLogEntries)
        {
            // Lösche die ältesten Einträge
            await _db.ExecuteAsync(
                $"DELETE FROM LogEntries WHERE Id IN (SELECT Id FROM LogEntries ORDER BY Timestamp ASC LIMIT {LogCleanupBatch})");
        }
    }

    private void EnsureInitialized()
    {
        if (_db == null)
            throw new InvalidOperationException("BotDatabaseService nicht initialisiert. InitializeAsync() zuerst aufrufen.");
    }

    // === v1.6.3 Phase 14 — Settings-Audit-Trail Persistenz ====================================

    /// <summary>
    /// Persistiert eine Liste von Settings-Aenderungen in einem einzigen Insert-Batch.
    /// Best-effort — DB-Fehler werden geschluckt (Audit-Log darf den Save-Pfad nicht blockieren).
    /// </summary>
    public async Task LogSettingsChangesAsync(IReadOnlyList<BingXBot.Core.Models.SettingsChange> changes)
    {
        EnsureInitialized();
        if (changes.Count == 0) return;
        var entities = changes.Select(c => new SettingsChangeEntity
        {
            Timestamp = c.Timestamp,
            Field = c.Field,
            OldValue = c.OldValue,
            NewValue = c.NewValue,
            Source = c.Source,
            Snapshot = c.Snapshot,
        }).ToList();
        try { await _db!.InsertAllAsync(entities).ConfigureAwait(false); }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Liefert die Settings-History optional gefiltert nach Feld + Zeitraum.
    /// Default-Sortierung: Timestamp DESC.
    /// </summary>
    public async Task<List<BingXBot.Core.Models.SettingsChange>> GetSettingsHistoryAsync(
        string? fieldFilter = null, DateTime? since = null, int limit = 200)
    {
        EnsureInitialized();
        var query = _db!.Table<SettingsChangeEntity>();
        if (!string.IsNullOrEmpty(fieldFilter))
            query = query.Where(c => c.Field == fieldFilter);
        if (since.HasValue)
        {
            var s = since.Value;
            query = query.Where(c => c.Timestamp >= s);
        }
        var rows = await query.OrderByDescending(c => c.Timestamp).Take(limit).ToListAsync().ConfigureAwait(false);
        return rows.Select(r => new BingXBot.Core.Models.SettingsChange(
            Timestamp: r.Timestamp,
            Field: r.Field,
            OldValue: r.OldValue,
            NewValue: r.NewValue,
            Source: r.Source,
            Snapshot: r.Snapshot)).ToList();
    }

    /// <summary>
    /// v1.6.1 Phase 11 — Loescht Settings-Changes aelter als <paramref name="cutoff"/>.
    /// Wird vom DbArchiveService monatlich aufgerufen.
    /// sqlite-net bindet DateTime nativ — kein String-Format-Geraet noetig.
    /// </summary>
    public async Task PurgeOldSettingsChangesAsync(DateTime cutoff)
    {
        EnsureInitialized();
        try
        {
            // sqlite-net laesst DateTime in DateTime.Ticks oder ISO-String konvertieren —
            // wir nutzen LINQ-Where statt rohem SQL um Format-Bugs zu vermeiden.
            var stale = await _db!.Table<SettingsChangeEntity>()
                .Where(e => e.Timestamp < cutoff)
                .ToListAsync().ConfigureAwait(false);
            foreach (var s in stale) await _db.DeleteAsync(s).ConfigureAwait(false);
        }
        catch { /* best-effort */ }
    }

    // === v1.6.1 Phase 11 — DB-Archivierung (Trades + Decisions) ==============================

    /// <summary>
    /// Verschiebt Trades aelter als <paramref name="cutoff"/> in eine separate Archiv-DB
    /// <c>{archiveDir}/bot-archive-{YYYY-MM}.db</c> und loescht sie aus der Live-DB.
    /// VACUUM danach gibt freigewordenen Speicher frei. Idempotent — zweimal ausgefuehrt
    /// erzeugt keine Doppel-Inserts (Pre-Check via Trade-EntryTime).
    /// </summary>
    /// <returns>Anzahl archivierter Trades.</returns>
    public async Task<int> ArchiveTradesAsync(DateTime cutoff, string archiveDir)
    {
        EnsureInitialized();
        Directory.CreateDirectory(archiveDir);

        var stale = await _db!.Table<TradeEntity>()
            .Where(t => t.EntryTime < cutoff)
            .ToListAsync().ConfigureAwait(false);
        if (stale.Count == 0) return 0;

        // Archive-File-Naming basiert auf dem aeltesten Trade im Batch (UTC-Monat).
        var oldest = stale.Min(t => t.EntryTime);
        var archiveFile = Path.Combine(archiveDir, $"bot-archive-{oldest:yyyy-MM}.db");

        var archiveDb = new SQLiteAsyncConnection(archiveFile);
        try
        {
            await archiveDb.CreateTableAsync<TradeEntity>().ConfigureAwait(false);
            // Idempotenz: Trade-Id ist Auto-Increment der Live-DB → in der Archiv-DB
            // koennte derselbe Trade schon liegen. InsertAllAsync mit Conflict-Ignore.
            // sqlite-net bietet das via SQLite.Insert OnConflict — wir nutzen InsertOrReplaceAsync
            // pro Trade, weil Replace bei gleichem PK keine Duplikate erzeugt.
            foreach (var t in stale) await archiveDb.InsertOrReplaceAsync(t).ConfigureAwait(false);
        }
        finally
        {
            await archiveDb.CloseAsync().ConfigureAwait(false);
        }

        // Aus Live-DB loeschen + VACUUM.
        foreach (var t in stale) await _db.DeleteAsync(t).ConfigureAwait(false);
        try { await _db.ExecuteAsync("VACUUM").ConfigureAwait(false); }
        catch { /* VACUUM kann fehlschlagen wenn andere Verbindungen offen sind — best-effort */ }
        return stale.Count;
    }

    /// <summary>
    /// v1.6.4 Phase 13 — Sucht den letzten Settings-Snapshot vor <paramref name="atUtc"/>.
    /// Liefert das deserialisierte BotSettings-Objekt oder null wenn kein Snapshot existiert.
    /// Wird vom Trade-Replay-Runner aufgerufen, um Settings zur Trade-Zeit zu rekonstruieren.
    /// </summary>
    public async Task<BotSettings?> GetSettingsSnapshotAtAsync(DateTime atUtc)
    {
        EnsureInitialized();
        try
        {
            // Suche letzten Eintrag VOR atUtc mit nicht-null Snapshot.
            var rows = await _db!.Table<SettingsChangeEntity>()
                .Where(c => c.Timestamp <= atUtc)
                .OrderByDescending(c => c.Timestamp)
                .Take(50) // Cap — der erste mit Snapshot gewinnt
                .ToListAsync().ConfigureAwait(false);
            var withSnap = rows.FirstOrDefault(r => !string.IsNullOrEmpty(r.Snapshot));
            if (withSnap == null) return null;
            return JsonSerializer.Deserialize<BotSettings>(withSnap.Snapshot!, BotSettingsJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// v1.6.1 Phase 11 — Liefert Trades inklusive Archive-Files. Attacht alle bot-archive-*.db
    /// im Archive-Ordner temporaer + UNION-Query + Detach. Wird vom Trade-History-Endpoint
    /// genutzt wenn Client ?archive=true uebergibt.
    /// </summary>
    public async Task<List<CompletedTrade>> GetTradesIncludingArchiveAsync(string archiveDir,
        TradingMode? modeFilter = null, int limit = 1000)
    {
        EnsureInitialized();
        var live = await GetTradesAsync(modeFilter, limit).ConfigureAwait(false);
        if (!Directory.Exists(archiveDir)) return live;

        var combined = new List<CompletedTrade>(live);
        foreach (var archiveFile in Directory.GetFiles(archiveDir, "bot-archive-*.db"))
        {
            try
            {
                var conn = new SQLiteAsyncConnection(archiveFile);
                try
                {
                    var query = conn.Table<TradeEntity>();
                    if (modeFilter.HasValue)
                    {
                        var mf = (int)modeFilter.Value;
                        query = query.Where(t => t.Mode == mf);
                    }
                    var rows = await query.OrderByDescending(t => t.ExitTime).Take(limit).ToListAsync()
                        .ConfigureAwait(false);
                    combined.AddRange(rows.Select(r => r.ToRecord()));
                }
                finally { await conn.CloseAsync().ConfigureAwait(false); }
            }
            catch { /* Skip kaputte Archive-Files — best-effort */ }
        }

        return combined.OrderByDescending(t => t.ExitTime).Take(limit).ToList();
    }

    /// <summary>
    /// Fügt eine Spalte hinzu falls sie nicht existiert. Fängt nur "duplicate column" Fehler,
    /// propagiert alle anderen (Syntax, Permissions, I/O).
    /// </summary>
    private async Task TryAddColumnAsync(string table, string column, string type)
    {
        try
        {
            await _db!.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {type}");
        }
        catch (SQLite.SQLiteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // Spalte existiert bereits → ok
        }
    }
}
