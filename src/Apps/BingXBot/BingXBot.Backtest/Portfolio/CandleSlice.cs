using BingXBot.Core.Models;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Zero-Copy Slice über eine Candle-Liste (vermeidet GetRange-Allokation pro Candle im Backtest).
/// Wird sowohl von der Single-Symbol-<see cref="BacktestEngine"/> als auch vom
/// <see cref="PortfolioBacktestEngine"/> genutzt — eine gemeinsame Implementierung statt Duplikat.
/// </summary>
internal sealed class CandleSlice : IReadOnlyList<Candle>
{
    private readonly List<Candle> _source;
    private readonly int _offset;
    public int Count { get; }

    public CandleSlice(List<Candle> source, int offset, int count)
    {
        _source = source;
        _offset = offset;
        Count = count;
    }

    public Candle this[int index] => _source[_offset + index];

    public IEnumerator<Candle> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return _source[_offset + i];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
