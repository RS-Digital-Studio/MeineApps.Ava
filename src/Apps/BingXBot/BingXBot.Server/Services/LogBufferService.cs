using System.Collections.Concurrent;
using BingXBot.Contracts.Dto;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading;

namespace BingXBot.Server.Services;

/// <summary>
/// Ringpuffer fuer Log-Entries (default 1000) — fuellt den Server-seitigen Log-Endpoint
/// <c>/api/v1/logs</c> und den Initial-State nach Reconnect.
///
/// Ohne diesen Puffer sah der Client nach SignalR-Reconnect oder App-Neustart eine leere
/// Log-Ansicht bis neue Events kommen. Mit Ringpuffer bekommt er die letzten N Eintraege
/// per GET-Request zurueck.
///
/// Subscribt auf <see cref="BotEventBus.LogEmitted"/>. Keine eigene Persistenz — die DB wird
/// vom Log-Subsystem selbst angesprochen falls noetig.
/// </summary>
public sealed class LogBufferService : IDisposable
{
    private readonly BotEventBus _bus;
    private readonly ConcurrentQueue<LogEntryDto> _buffer = new();
    private readonly int _capacity;

    public LogBufferService(BotEventBus bus, IConfiguration config)
    {
        _bus = bus;
        _capacity = Math.Max(100, config.GetValue<int>("Server:LogBufferCapacity", 1000));
        _bus.LogEmitted += OnLogEmitted;
    }

    private void OnLogEmitted(object? sender, LogEntry entry)
    {
        _buffer.Enqueue(new LogEntryDto(entry.Timestamp, entry.Level, entry.Category, entry.Message, entry.Symbol));
        // Kapazitaet enforcen — ConcurrentQueue hat kein Bounded-Limit, also manuell trimmen.
        while (_buffer.Count > _capacity && _buffer.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Liefert die letzten Eintraege (pageSize begrenzt auf Capacity), optional gefiltert nach Level.
    /// Neueste zuerst. Pagination: Page=0 holt die neuesten <paramref name="pageSize"/>, Page=1 die
    /// davor, usw. Wenn weniger Eintraege als angefordert existieren, wird der Rest zurueckgegeben.
    /// </summary>
    public PagedResult<LogEntryDto> Query(int page, int pageSize, BingXBot.Core.Enums.LogLevel? minLevel = null)
    {
        page = Math.Max(0, page);
        pageSize = Math.Clamp(pageSize, 1, _capacity);

        // Snapshot — ConcurrentQueue.ToArray() ist atomar-konsistent (mit kleinen Ausnahmen bei hoher
        // Concurrency, aber fuer GET-Endpoint ausreichend).
        var snapshot = _buffer.ToArray();
        IEnumerable<LogEntryDto> filtered = minLevel.HasValue
            ? snapshot.Where(e => e.Level >= minLevel.Value)
            : snapshot;
        var total = filtered is ICollection<LogEntryDto> col ? col.Count : filtered.Count();

        // Neueste zuerst — wir reversen das Snapshot und paginieren.
        var items = filtered.Reverse().Skip(page * pageSize).Take(pageSize).ToArray();
        return new PagedResult<LogEntryDto>(items, total, page, pageSize);
    }

    public void Dispose()
    {
        _bus.LogEmitted -= OnLogEmitted;
        _buffer.Clear();
    }
}
