using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Testet die Scanner-Rotation-Logik (Fisher-Yates Shuffle + Take(20) aus N Coins).
/// Simuliert die ScanHelper.FilterCandidates Logik ohne Shared-Dependency.
/// </summary>
public class ScanRotationTests
{
    private readonly ITestOutputHelper _output;

    public ScanRotationTests(ITestOutputHelper output) => _output = output;

    [ThreadStatic] private static Random? _rng;

    private static List<string> ShuffleList(List<string> source)
    {
        _rng ??= new Random();
        var list = new List<string>(source);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    [Fact]
    public void Shuffle_Take20_ShouldReturnDifferentSubsets()
    {
        // 80 Coins simulieren (typische Anzahl nach Volume/PriceChange Filter)
        var pool = Enumerable.Range(1, 80).Select(i => $"COIN{i}-USDT").ToList();
        var perScanLimit = 20;

        // 5 Scans simulieren
        var scanResults = new List<HashSet<string>>();
        for (int scan = 0; scan < 5; scan++)
        {
            var shuffled = ShuffleList(pool);
            var selected = shuffled.Take(perScanLimit).ToHashSet();
            scanResults.Add(selected);

            _output.WriteLine($"Scan {scan + 1}: {string.Join(", ", selected.Take(8))}...");
        }

        // Prüfung 1: Jeder Scan hat genau 20 Symbole
        foreach (var result in scanResults)
            result.Should().HaveCount(perScanLimit, "jeder Scan soll genau 20 Coins haben");

        // Prüfung 2: Die Sets sind NICHT identisch (unterschiedliche Symbole)
        var identicalCount = 0;
        for (int i = 0; i < scanResults.Count - 1; i++)
        {
            if (scanResults[i].SetEquals(scanResults[i + 1]))
                identicalCount++;
        }
        identicalCount.Should().Be(0, "keine zwei aufeinanderfolgenden Scans sollten identische Symbole haben");

        // Prüfung 3: Überlappung zwischen Scan 1 und 2 ist < 100%
        var overlap = scanResults[0].Intersect(scanResults[1]).Count();
        _output.WriteLine($"Überlappung Scan1↔Scan2: {overlap}/20 ({overlap * 100 / 20}%)");
        overlap.Should().BeLessThan(20, "es sollte nicht 100% Überlappung geben");

        // Prüfung 4: Nach 5 Scans à 20 sollten deutlich mehr als 20 verschiedene Coins gesehen worden sein
        var allSeen = new HashSet<string>();
        foreach (var result in scanResults)
            allSeen.UnionWith(result);
        _output.WriteLine($"Nach 5 Scans gesehen: {allSeen.Count}/80 Coins");
        allSeen.Count.Should().BeGreaterThan(50, "nach 5 Scans sollten >50 der 80 Coins gesehen worden sein");
    }

    [Fact]
    public void Shuffle_SmallPool_ShouldReturnAll()
    {
        // Wenn Pool <= perScanLimit → alle Coins kommen rein
        var pool = Enumerable.Range(1, 15).Select(i => $"COIN{i}-USDT").ToList();
        var perScanLimit = Math.Min(20, pool.Count); // = 15

        var shuffled = ShuffleList(pool);
        var selected = shuffled.Take(perScanLimit).ToList();

        selected.Should().HaveCount(15, "bei Pool=15 sollten alle 15 Coins ausgewählt werden");
        selected.ToHashSet().SetEquals(pool.ToHashSet()).Should().BeTrue("alle Pool-Coins müssen enthalten sein");
    }

    [Fact]
    public void Shuffle_OrderShouldVary()
    {
        var pool = Enumerable.Range(1, 80).Select(i => $"COIN{i}-USDT").ToList();

        var order1 = ShuffleList(pool).Take(20).ToList();
        var order2 = ShuffleList(pool).Take(20).ToList();

        // Die Reihenfolge sollte unterschiedlich sein
        var sameOrder = order1.SequenceEqual(order2);
        sameOrder.Should().BeFalse("zwei Shuffles sollten verschiedene Reihenfolgen haben");
    }
}
