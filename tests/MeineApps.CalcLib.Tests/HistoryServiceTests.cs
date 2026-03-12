using FluentAssertions;
using MeineApps.CalcLib;
using Xunit;

namespace MeineApps.CalcLib.Tests;

/// <summary>
/// Tests für HistoryService: Einträge hinzufügen, löschen, Limit (100 Einträge),
/// HistoryChanged-Event, LoadEntries, Clear.
/// </summary>
public class HistoryServiceTests
{
    private readonly HistoryService _sut = new();

    #region AddEntry

    [Fact]
    public void AddEntry_GueltigerEintrag_WirdInHistoryGespeichert()
    {
        _sut.AddEntry("5+3", "8", 8);
        _sut.History.Should().HaveCount(1);
        _sut.History[0].Expression.Should().Be("5+3");
        _sut.History[0].Result.Should().Be("8");
        _sut.History[0].ResultValue.Should().Be(8);
    }

    [Fact]
    public void AddEntry_NeuesteEintraegeAmAnfang()
    {
        _sut.AddEntry("1+1", "2", 2);
        _sut.AddEntry("3+3", "6", 6);

        // Neuester Eintrag steht an Index 0
        _sut.History[0].Expression.Should().Be("3+3");
        _sut.History[1].Expression.Should().Be("1+1");
    }

    [Fact]
    public void AddEntry_LeererAusdruck_WirdIgnoriert()
    {
        _sut.AddEntry("", "8", 8);
        _sut.History.Should().BeEmpty();
    }

    [Fact]
    public void AddEntry_LeereResult_WirdIgnoriert()
    {
        _sut.AddEntry("5+3", "", 8);
        _sut.History.Should().BeEmpty();
    }

    [Fact]
    public void AddEntry_NurLeerzeichen_WirdIgnoriert()
    {
        _sut.AddEntry("   ", "   ", 0);
        _sut.History.Should().BeEmpty();
    }

    [Fact]
    public void AddEntry_Timestamp_IstUTC()
    {
        var vorDemHinzufuegen = DateTime.UtcNow.AddSeconds(-1);
        _sut.AddEntry("5+3", "8", 8);
        var nachDemHinzufuegen = DateTime.UtcNow.AddSeconds(1);

        _sut.History[0].Timestamp.Should().BeAfter(vorDemHinzufuegen);
        _sut.History[0].Timestamp.Should().BeBefore(nachDemHinzufuegen);
        _sut.History[0].Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    #endregion

    #region Limit (100 Einträge)

    [Fact]
    public void AddEntry_100EintraegeHinzugefuegt_AlleGespeichert()
    {
        for (int i = 0; i < 100; i++)
            _sut.AddEntry($"expr{i}", $"result{i}", i);

        _sut.History.Should().HaveCount(100);
    }

    [Fact]
    public void AddEntry_101EintragHinzugefuegt_AeltesterEintragEntfernt()
    {
        // 100 Einträge füllen
        for (int i = 0; i < 100; i++)
            _sut.AddEntry($"alt{i}", $"r{i}", i);

        // 101. Eintrag → ältester (alt0) muss entfernt werden
        _sut.AddEntry("neu", "999", 999);

        _sut.History.Should().HaveCount(100);
        // Neuester ist an Position 0
        _sut.History[0].Expression.Should().Be("neu");
        // Ältester (alt0) ist rausgeflogen - alt1 ist jetzt am Ende
        _sut.History[^1].Expression.Should().Be("alt1");
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_NachMehrerenEintraegen_HistoryLeer()
    {
        _sut.AddEntry("1+1", "2", 2);
        _sut.AddEntry("2+2", "4", 4);
        _sut.Clear();
        _sut.History.Should().BeEmpty();
    }

    [Fact]
    public void Clear_AufLeererHistory_KeineException()
    {
        var action = () => _sut.Clear();
        action.Should().NotThrow();
    }

    #endregion

    #region DeleteEntry

    [Fact]
    public void DeleteEntry_ExistierenderEintrag_WirdEntfernt()
    {
        _sut.AddEntry("5+3", "8", 8);
        var eintrag = _sut.History[0];

        _sut.DeleteEntry(eintrag);

        _sut.History.Should().BeEmpty();
    }

    [Fact]
    public void DeleteEntry_NichtExistierenderEintrag_KeineException()
    {
        var fremderEintrag = new CalculationHistoryEntry("x", "y", 0, DateTime.UtcNow);
        var action = () => _sut.DeleteEntry(fremderEintrag);
        action.Should().NotThrow();
    }

    [Fact]
    public void DeleteEntry_EinEintragVonMehreren_NurDieserEntfernt()
    {
        _sut.AddEntry("1+1", "2", 2);
        _sut.AddEntry("3+3", "6", 6);
        var zweiterEintrag = _sut.History[0]; // neuester = 3+3

        _sut.DeleteEntry(zweiterEintrag);

        _sut.History.Should().HaveCount(1);
        _sut.History[0].Expression.Should().Be("1+1");
    }

    #endregion

    #region LoadEntries

    [Fact]
    public void LoadEntries_ErsetzAktuellHistory()
    {
        _sut.AddEntry("alt", "0", 0);

        var neueEintraege = new[]
        {
            new CalculationHistoryEntry("neu1", "1", 1, DateTime.UtcNow),
            new CalculationHistoryEntry("neu2", "2", 2, DateTime.UtcNow)
        };
        _sut.LoadEntries(neueEintraege);

        _sut.History.Should().HaveCount(2);
        _sut.History[0].Expression.Should().Be("neu1");
    }

    [Fact]
    public void LoadEntries_MehrAls100Eintraege_WirdAuf100Begrenzt()
    {
        var eintraege = Enumerable.Range(0, 150)
            .Select(i => new CalculationHistoryEntry($"e{i}", $"r{i}", i, DateTime.UtcNow));

        _sut.LoadEntries(eintraege);

        _sut.History.Should().HaveCount(100);
    }

    [Fact]
    public void LoadEntries_LeereAuflistung_HistoryLeer()
    {
        _sut.AddEntry("alt", "0", 0);
        _sut.LoadEntries(Array.Empty<CalculationHistoryEntry>());
        _sut.History.Should().BeEmpty();
    }

    #endregion

    #region HistoryChanged-Event

    [Fact]
    public void AddEntry_LoestHistoryChangedAus()
    {
        bool eventAusgeloest = false;
        _sut.HistoryChanged += (s, e) => eventAusgeloest = true;

        _sut.AddEntry("5+3", "8", 8);

        eventAusgeloest.Should().BeTrue();
    }

    [Fact]
    public void AddEntry_LeererAusdruck_LoestKeinHistoryChangedAus()
    {
        bool eventAusgeloest = false;
        _sut.HistoryChanged += (s, e) => eventAusgeloest = true;

        _sut.AddEntry("", "8", 8);

        eventAusgeloest.Should().BeFalse();
    }

    [Fact]
    public void Clear_LoestHistoryChangedAus()
    {
        bool eventAusgeloest = false;
        _sut.HistoryChanged += (s, e) => eventAusgeloest = true;

        _sut.Clear();

        eventAusgeloest.Should().BeTrue();
    }

    [Fact]
    public void DeleteEntry_ExistierenderEintrag_LoestHistoryChangedAus()
    {
        _sut.AddEntry("5+3", "8", 8);
        var eintrag = _sut.History[0];
        bool eventAusgeloest = false;
        _sut.HistoryChanged += (s, e) => eventAusgeloest = true;

        _sut.DeleteEntry(eintrag);

        eventAusgeloest.Should().BeTrue();
    }

    [Fact]
    public void DeleteEntry_NichtExistierenderEintrag_LoestKeinHistoryChangedAus()
    {
        var fremderEintrag = new CalculationHistoryEntry("x", "y", 0, DateTime.UtcNow);
        bool eventAusgeloest = false;
        _sut.HistoryChanged += (s, e) => eventAusgeloest = true;

        _sut.DeleteEntry(fremderEintrag);

        eventAusgeloest.Should().BeFalse();
    }

    [Fact]
    public void LoadEntries_LoestHistoryChangedAus()
    {
        bool eventAusgeloest = false;
        _sut.HistoryChanged += (s, e) => eventAusgeloest = true;

        _sut.LoadEntries(new[] { new CalculationHistoryEntry("x", "1", 1, DateTime.UtcNow) });

        eventAusgeloest.Should().BeTrue();
    }

    #endregion
}
