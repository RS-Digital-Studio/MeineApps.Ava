using FluentAssertions;
using Xunit;
using FinanzRechner.Models;
using FinanzRechner.Services;
using static FinanzRechner.ViewModels.ExpenseTrackerViewModel;

namespace FinanzRechner.Tests;

/// <summary>
/// Tests für die aus dem ExpenseTrackerViewModel extrahierte Filter-/Sortier-Logik.
/// Sichert die Verhaltensgleichheit der Service-Extraktion ab.
/// </summary>
public class ExpenseFilterServiceTests
{
    private readonly ExpenseFilterService _sut = new();

    private static Expense Make(string description, decimal amount, TransactionType type,
        ExpenseCategory category, DateTime date, string? note = null) =>
        new()
        {
            Description = description,
            Amount = amount,
            Type = type,
            Category = category,
            Date = date,
            Note = note
        };

    private static ExpenseFilterCriteria NoFilter(SortOption sort = SortOption.DateDescending) =>
        new(null, FilterTypeOption.All, null, 0m, 0m, sort);

    private static List<Expense> SampleData() =>
    [
        Make("Lebensmittel", 50m, TransactionType.Expense, ExpenseCategory.Food, new DateTime(2026, 1, 10)),
        Make("Gehalt", 2000m, TransactionType.Income, ExpenseCategory.Salary, new DateTime(2026, 1, 1)),
        Make("Tankstelle", 80m, TransactionType.Expense, ExpenseCategory.Transport, new DateTime(2026, 1, 15)),
        Make("Miete", 900m, TransactionType.Expense, ExpenseCategory.Housing, new DateTime(2026, 1, 5)),
        Make("Bonus", 300m, TransactionType.Income, ExpenseCategory.Salary, new DateTime(2026, 1, 20))
    ];

    #region Filter: Suche

    [Fact]
    public void Apply_SucheNachBeschreibung_FiltertNurTreffer()
    {
        var data = SampleData();
        var criteria = NoFilter() with { SearchTerm = "miete" };

        var result = _sut.Apply(data, criteria);

        result.Should().ContainSingle();
        result[0].Description.Should().Be("Miete");
    }

    [Fact]
    public void Apply_SucheTrifftNote_WirdGefunden()
    {
        var data = new List<Expense>
        {
            Make("Einkauf", 20m, TransactionType.Expense, ExpenseCategory.Shopping, new DateTime(2026, 1, 2), note: "Geschenk fuer Oma")
        };
        var criteria = NoFilter() with { SearchTerm = "oma" };

        var result = _sut.Apply(data, criteria);

        result.Should().ContainSingle();
    }

    [Fact]
    public void Apply_SucheOhneTreffer_GibtLeereListe()
    {
        var result = _sut.Apply(SampleData(), NoFilter() with { SearchTerm = "xyz123" });

        result.Should().BeEmpty();
    }

    [Fact]
    public void Apply_LeererSuchbegriff_FiltertNicht()
    {
        var data = SampleData();

        var result = _sut.Apply(data, NoFilter() with { SearchTerm = "   " });

        result.Should().HaveCount(data.Count);
    }

    #endregion

    #region Filter: Typ

    [Fact]
    public void Apply_TypFilterExpenses_NurAusgaben()
    {
        var result = _sut.Apply(SampleData(), NoFilter() with { TypeFilter = FilterTypeOption.Expenses });

        result.Should().OnlyContain(e => e.Type == TransactionType.Expense);
        result.Should().HaveCount(3);
    }

    [Fact]
    public void Apply_TypFilterIncome_NurEinnahmen()
    {
        var result = _sut.Apply(SampleData(), NoFilter() with { TypeFilter = FilterTypeOption.Income });

        result.Should().OnlyContain(e => e.Type == TransactionType.Income);
        result.Should().HaveCount(2);
    }

    #endregion

    #region Filter: Kategorie & Betrag

    [Fact]
    public void Apply_KategorieFilter_NurPassendeKategorie()
    {
        var result = _sut.Apply(SampleData(), NoFilter() with { CategoryFilter = ExpenseCategory.Transport });

        result.Should().ContainSingle();
        result[0].Category.Should().Be(ExpenseCategory.Transport);
    }

    [Fact]
    public void Apply_MinBetragFilter_SchliesstKleinereAus()
    {
        var result = _sut.Apply(SampleData(), NoFilter() with { MinAmount = 100m });

        result.Should().OnlyContain(e => e.Amount >= 100m);
        result.Should().HaveCount(3); // 2000, 900, 300
    }

    [Fact]
    public void Apply_MaxBetragFilter_SchliesstGroessereAus()
    {
        var result = _sut.Apply(SampleData(), NoFilter() with { MaxAmount = 100m });

        result.Should().OnlyContain(e => e.Amount <= 100m);
        result.Should().HaveCount(2); // 50, 80
    }

    [Fact]
    public void Apply_MinUndMaxKombiniert_GibtBereich()
    {
        var result = _sut.Apply(SampleData(), NoFilter() with { MinAmount = 60m, MaxAmount = 950m });

        // 80 (Transport), 900 (Miete), 300 (Bonus) liegen im Bereich [60, 950]
        result.Should().HaveCount(3);
        result.Should().OnlyContain(e => e.Amount >= 60m && e.Amount <= 950m);
    }

    [Fact]
    public void Apply_MehrereFilterKombiniert_AlleBedingungenGelten()
    {
        // Ausgaben + Mindestbetrag 60
        var criteria = NoFilter() with { TypeFilter = FilterTypeOption.Expenses, MinAmount = 60m };

        var result = _sut.Apply(SampleData(), criteria);

        result.Should().HaveCount(2); // 80 (Transport), 900 (Miete) — 50 faellt raus
        result.Should().OnlyContain(e => e.Type == TransactionType.Expense && e.Amount >= 60m);
    }

    #endregion

    #region Sortierung

    [Fact]
    public void Apply_SortDateDescending_NeuesteZuerst()
    {
        var result = _sut.Apply(SampleData(), NoFilter(SortOption.DateDescending));

        result.Select(e => e.Date).Should().BeInDescendingOrder();
    }

    [Fact]
    public void Apply_SortDateAscending_AeltesteZuerst()
    {
        var result = _sut.Apply(SampleData(), NoFilter(SortOption.DateAscending));

        result.Select(e => e.Date).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Apply_SortAmountDescending_HoechsterBetragZuerst()
    {
        var result = _sut.Apply(SampleData(), NoFilter(SortOption.AmountDescending));

        result.Select(e => e.Amount).Should().BeInDescendingOrder();
        result[0].Amount.Should().Be(2000m);
    }

    [Fact]
    public void Apply_SortAmountAscending_NiedrigsterBetragZuerst()
    {
        var result = _sut.Apply(SampleData(), NoFilter(SortOption.AmountAscending));

        result.Select(e => e.Amount).Should().BeInAscendingOrder();
        result[0].Amount.Should().Be(50m);
    }

    [Fact]
    public void Apply_SortDescription_Alphabetisch()
    {
        var result = _sut.Apply(SampleData(), NoFilter(SortOption.Description));

        result.Select(e => e.Description)
            .Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    #endregion

    #region Eingabe-Schutz

    [Fact]
    public void Apply_LaesstEingabeUnveraendert()
    {
        var data = SampleData();
        var originalCount = data.Count;
        var firstRef = data[0];

        _sut.Apply(data, NoFilter() with { TypeFilter = FilterTypeOption.Income });

        data.Should().HaveCount(originalCount);
        data[0].Should().BeSameAs(firstRef);
    }

    [Fact]
    public void Apply_LeereQuelle_GibtLeereListe()
    {
        var result = _sut.Apply([], NoFilter());

        result.Should().BeEmpty();
    }

    #endregion

    #region IsFilterActive

    [Fact]
    public void IsFilterActive_KeineKriterien_False()
    {
        _sut.IsFilterActive(NoFilter()).Should().BeFalse();
    }

    [Theory]
    [InlineData("such")]
    public void IsFilterActive_MitSuchbegriff_True(string term)
    {
        _sut.IsFilterActive(NoFilter() with { SearchTerm = term }).Should().BeTrue();
    }

    [Fact]
    public void IsFilterActive_TypFilterGesetzt_True()
    {
        _sut.IsFilterActive(NoFilter() with { TypeFilter = FilterTypeOption.Expenses }).Should().BeTrue();
    }

    [Fact]
    public void IsFilterActive_KategorieGesetzt_True()
    {
        _sut.IsFilterActive(NoFilter() with { CategoryFilter = ExpenseCategory.Food }).Should().BeTrue();
    }

    [Fact]
    public void IsFilterActive_BetragGesetzt_True()
    {
        _sut.IsFilterActive(NoFilter() with { MinAmount = 10m }).Should().BeTrue();
        _sut.IsFilterActive(NoFilter() with { MaxAmount = 10m }).Should().BeTrue();
    }

    [Fact]
    public void IsFilterActive_NurSortierung_False()
    {
        // Sortier-Auswahl allein ist kein aktiver Filter
        _sut.IsFilterActive(NoFilter(SortOption.AmountDescending)).Should().BeFalse();
    }

    #endregion
}
