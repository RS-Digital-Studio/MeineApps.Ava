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
    private const int CurrentSchemaVersion = 2;

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
        await _db.CreateTableAsync<FeatureSnapshotEntity>();

        // Indices für häufige Abfragen (idempotent dank IF NOT EXISTS)
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_ExitTime ON Trades (ExitTime DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_Mode ON Trades (Mode)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Trades_Symbol ON Trades (Symbol)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_EquitySnapshots_Time ON EquitySnapshots (Time)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_LogEntries_Timestamp ON LogEntries (Timestamp DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_LogEntries_Level ON LogEntries (Level)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_FeatureSnapshots_Timestamp ON FeatureSnapshots (Timestamp DESC)");
        await _db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_FeatureSnapshots_Outcome ON FeatureSnapshots (Outcome)");

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
        {
            try
            {
                await _db.ExecuteAsync("ALTER TABLE Trades ADD COLUMN FundingPaid REAL DEFAULT 0");
            }
            catch { /* Spalte existiert bereits */ }
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

    // === ATI State ===

    public async Task SaveAtiStateAsync(string stateJson)
    {
        EnsureInitialized();
        await _db!.InsertOrReplaceAsync(new SettingEntity { Key = "AtiState", Value = stateJson });
    }

    public async Task<string?> LoadAtiStateAsync()
    {
        EnsureInitialized();
        var entity = await _db!.FindAsync<SettingEntity>("AtiState");
        return entity?.Value;
    }

    // === Feature Snapshots (ATI ML-Training) ===

    public async Task SaveFeatureSnapshotAsync(FeatureSnapshotEntity snapshot)
    {
        EnsureInitialized();
        await _db!.InsertAsync(snapshot);
    }

    public async Task<List<FeatureSnapshotEntity>> GetFeatureSnapshotsAsync(int limit = 1000)
    {
        EnsureInitialized();
        return await _db!.Table<FeatureSnapshotEntity>()
            .OrderByDescending(f => f.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<FeatureSnapshotEntity>> GetLabeledSnapshotsAsync(int limit = 5000)
    {
        EnsureInitialized();
        return await _db!.Table<FeatureSnapshotEntity>()
            .Where(f => f.Outcome != 0)
            .OrderByDescending(f => f.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task UpdateSnapshotOutcomeAsync(int snapshotId, int outcome, decimal pnl, int holdTimeMinutes)
    {
        EnsureInitialized();
        await _db!.ExecuteAsync(
            "UPDATE FeatureSnapshots SET Outcome = ?, Pnl = ?, HoldTimeMinutes = ? WHERE Id = ?",
            outcome, pnl, holdTimeMinutes, snapshotId);
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
}
