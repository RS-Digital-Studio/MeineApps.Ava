using System.Collections.Concurrent;

namespace BingXBot.Core.Diagnostics;

/// <summary>
/// v1.5.2 Phase 4 — In-Memory-Ringpuffer fuer <see cref="EvaluationDecision"/>-Eintraege.
/// Default-Kapazitaet 5000 Eintraege; bei Ueberschreitung wird das aelteste Element verworfen
/// (FIFO-Trim). Thread-safe — Append wird aus dem Strategy-Hot-Path aufgerufen.
///
/// Bewusst NICHT DB-persistent in v1.5.2 — die Tabelle <c>EvaluationDecisions</c> + Migration v11
/// kommen in einem Folge-PR. Bis dahin lebt der Trail im Server-Prozess; nach Restart leer.
/// </summary>
public sealed class DecisionTrailBuffer
{
    private readonly ConcurrentQueue<EvaluationDecision> _queue = new();
    private readonly int _capacity;
    private long _appendedCount;

    /// <summary>Default-Kapazitaet — siehe Plan-Spezifikation Phase 4.</summary>
    public const int DefaultCapacity = 5_000;

    public DecisionTrailBuffer(int capacity = DefaultCapacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be positive");
        _capacity = capacity;
    }

    /// <summary>Aktuelle Gesamtzahl Eintraege (begrenzt durch Capacity).</summary>
    public int Count => _queue.Count;

    /// <summary>Wie viele Eintraege bisher angehaengt wurden (kumulativ, fuer Stats).</summary>
    public long AppendedCount => Interlocked.Read(ref _appendedCount);

    /// <summary>Capacity dieses Puffers.</summary>
    public int Capacity => _capacity;

    /// <summary>Haengt eine Decision an. Wenn die Capacity ueberschritten wird, faellt das aelteste Element raus.</summary>
    public void Append(EvaluationDecision decision)
    {
        if (decision is null) return;
        _queue.Enqueue(decision);
        Interlocked.Increment(ref _appendedCount);
        TrimToCapacity();
    }

    /// <summary>Letzte <paramref name="limit"/> Eintraege chronologisch absteigend (jueng-zuerst).</summary>
    public IReadOnlyList<EvaluationDecision> GetLatest(int limit)
    {
        if (limit <= 0) return Array.Empty<EvaluationDecision>();
        var snapshot = _queue.ToArray();
        if (snapshot.Length <= limit) return snapshot.Reverse().ToArray();
        return snapshot.Skip(snapshot.Length - limit).Reverse().ToArray();
    }

    /// <summary>Gefilterte Decisions nach Symbol/TF/Reject-Reason. Hilfsmethode fuer den /decisions-Endpoint.</summary>
    public IReadOnlyList<EvaluationDecision> Filter(
        string? symbol = null,
        BingXBot.Core.Enums.TimeFrame? tf = null,
        string? rejectionReason = null,
        DateTime? since = null,
        int limit = 200)
    {
        var snapshot = _queue.ToArray();
        IEnumerable<EvaluationDecision> q = snapshot;
        if (!string.IsNullOrEmpty(symbol))
            q = q.Where(d => d.Symbol == symbol);
        if (tf.HasValue)
            q = q.Where(d => d.Tf == tf.Value);
        if (!string.IsNullOrEmpty(rejectionReason))
            q = q.Where(d => d.RejectionReason == rejectionReason);
        if (since.HasValue)
            q = q.Where(d => d.UtcTimestamp >= since.Value);
        return q.Reverse().Take(limit).ToArray();
    }

    /// <summary>Loescht alle Eintraege.</summary>
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }

    private void TrimToCapacity()
    {
        while (_queue.Count > _capacity && _queue.TryDequeue(out _)) { }
    }
}
