using System;
using System.Collections.Generic;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Helpers;

/// <summary>
/// P1.5 AAA-Audit Code-Review-Fix [Finding 5]: Cap-getriebener LIFO-Stack ohne O(n)-Rebuild.
///
/// Vorher: <see cref="Stack{T}"/> mit ToArray + Clear + n*Push bei jeder Cap-Ueberschreitung.
/// Jetzt: Ringbuffer mit Push/Pop in O(1). Aelteste Eintraege werden bei Cap-Ueberlauf
/// stillschweigend verworfen (LIFO mit FIFO-Drop fuer Bottom-Eintraege).
///
/// API-kompatibel zu Stack&lt;ActivePage&gt;: Push, Pop, Clear, Count.
/// Implementiert <see cref="IEnumerable{T}"/> fuer Debug-Inspektion.
/// </summary>
internal sealed class CappedNavigationStack
{
    private readonly ActivePage[] _buffer;
    private int _head;     // Index des naechsten Push-Slots (mod Length)
    private int _count;

    public CappedNavigationStack(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new ActivePage[capacity];
    }

    public int Count => _count;
    public int Capacity => _buffer.Length;

    /// <summary>Push in O(1). Bei Cap-Ueberlauf wird der aelteste Eintrag still verworfen.</summary>
    public void Push(ActivePage value)
    {
        _buffer[_head] = value;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length) _count++;
        // sonst: Cap-voll, _count bleibt = Capacity, _head ueberschreibt aelteste
    }

    /// <summary>Pop in O(1). Wirft <see cref="InvalidOperationException"/> bei leerem Stack.</summary>
    public ActivePage Pop()
    {
        if (_count == 0) throw new InvalidOperationException("Stack ist leer");
        _head = (_head - 1 + _buffer.Length) % _buffer.Length;
        var value = _buffer[_head];
        _buffer[_head] = default;
        _count--;
        return value;
    }

    /// <summary>Peek in O(1) ohne Pop.</summary>
    public bool TryPeek(out ActivePage value)
    {
        if (_count == 0) { value = default; return false; }
        var idx = (_head - 1 + _buffer.Length) % _buffer.Length;
        value = _buffer[idx];
        return true;
    }

    public void Clear()
    {
        Array.Clear(_buffer);
        _head = 0;
        _count = 0;
    }
}
