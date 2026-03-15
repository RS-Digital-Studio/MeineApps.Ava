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
