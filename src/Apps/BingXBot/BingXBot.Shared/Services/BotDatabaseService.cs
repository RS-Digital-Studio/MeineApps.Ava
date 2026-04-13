using SQLite;
using BingXBot.Core.Data;
using BingXBot.Core.Configuration;
using BingXBot.Core.Models;
using BingXBot.Core.Enums;
using System.Text.Json;

namespace BingXBot.Services;

public class BotDatabaseService
{
    private SQLiteAsyncConnection? _db;
    private int _logInsertCount;
    private const int LogRotationThreshold = 1000;
    private const int MaxLogEntries = 100_000;
    private const int LogCleanupBatch = 10_000;

    /// <summary>Aktuelle Schema-Version. Bei Änderungen erhöhen und Migration in RunMigrationsAsync() hinzufügen.</summary>
    private const int CurrentSchemaVersion = 7;

    public async Task InitializeAsync()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BingXBot", "bot.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<TradeEntity>();
        await _db.CreateTableAsync<EquityEntity>();
        await _db.CreateTableAsync<LogEntity>();
        await _db.CreateTableAsync<SettingEntity>();

        // Indices für häufige Abfragen (idempotent dank IF NOT EXISTS)
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_ExitTime ON Trades (ExitTime DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_Mode ON Trades (Mode)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_Symbol ON Trades (Symbol)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_EquitySnapshots_Time ON EquitySnapshots (Time)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_LogEntries_Timestamp ON LogEntries (Timestamp DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_LogEntries_Level ON LogEntries (Level)");

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
            await _db!.ExecuteAsync("PRAGMA journal_mode=WAL");
        }

        // Migration v6 → v7: Verwaiste ATI-Daten aus alten Installs löschen
        if (currentVersion < 7)
        {
            try { await _db!.ExecuteAsync("DELETE FROM Settings WHERE Key='AtiState'"); } catch { /* best-effort */ }
            try { await _db!.ExecuteAsync("DROP TABLE IF EXISTS FeatureSnapshots"); } catch { /* best-effort */ }
        }

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
        var json = JsonSerializer.Serialize(settings);
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "BotSettings", Value = json });
    }

    public async Task<BotSettings> LoadSettingsAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("BotSettings");
        if (entity == null) return new BotSettings();
        try
        {
            return JsonSerializer.Deserialize<BotSettings>(entity.Value) ?? new BotSettings();
        }
        catch (JsonException)
        {
            // Korrupte Settings-Daten → Defaults verwenden
            System.Diagnostics.Debug.WriteLine("BotSettings JSON korrupt, verwende Defaults");
            return new BotSettings();
        }
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

    /// <summary>Speichert Runtime-State (TradesToday, ConsecutiveLosses) für Crash-Recovery.</summary>
    public async Task SaveRuntimeStateAsync(int tradesToday, int consecutiveLosses)
    {
        EnsureInitialized();
        var state = new { TradesToday = tradesToday, ConsecutiveLosses = consecutiveLosses };
        var json = JsonSerializer.Serialize(state);
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "RuntimeState", Value = json });
    }

    /// <summary>Lädt Runtime-State für Crash-Recovery.</summary>
    public async Task<(int TradesToday, int ConsecutiveLosses)?> LoadRuntimeStateAsync()
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
            return (tradesToday, consecutiveLosses);
        }
        catch { return null; }
    }

    // === Pending Limit Orders Persistenz (TP-Recovery nach App-Neustart) ===

    /// <summary>
    /// Speichert pending Limit-Orders für Recovery nach App-Neustart.
    /// Ohne Persistenz geht nach Neustart die Fill-Detection verloren
    /// und TP-Orders werden nie platziert.
    /// </summary>
    public async Task SavePendingLimitOrdersAsync(Dictionary<string, PendingLimitOrderState> pendingOrders)
    {
        EnsureInitialized();
        var json = JsonSerializer.Serialize(pendingOrders);
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "PendingLimitOrders", Value = json });
    }

    /// <summary>Lädt gespeicherte Pending Limit-Orders für Crash-Recovery.</summary>
    public async Task<Dictionary<string, PendingLimitOrderState>?> LoadPendingLimitOrdersAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("PendingLimitOrders");
        if (entity?.Value == null) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, PendingLimitOrderState>>(entity.Value); }
        catch { return null; }
    }

    /// <summary>Löscht gespeicherte Pending Limit-Orders (nach erfolgreicher Recovery).</summary>
    public async Task ClearPendingLimitOrdersAsync()
    {
        EnsureInitialized();
        try { await _db!.ExecuteAsync("DELETE FROM Settings WHERE Key='PendingLimitOrders'"); }
        catch { /* best-effort */ }
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

        _logInsertCount++;
        if (_logInsertCount >= LogRotationThreshold)
        {
            _logInsertCount = 0;
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
