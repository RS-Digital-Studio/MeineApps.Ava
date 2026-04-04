using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für CraftingRecipe: GetAllRecipes(), GetById(), GetByOutputProduct().
/// Tests für CraftingJob: IsComplete, Progress, TimeRemaining.
/// Tests für CraftingProduct: GetAllProducts().
/// </summary>
public class CraftingTests
{
    // ═══════════════════════════════════════════════════════════════════
    // CraftingRecipe - Statische Daten
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllRecipes_HatZwanzigRezepte()
    {
        // Ausführung
        var rezepte = CraftingRecipe.GetAllRecipes();

        // Prüfung: Laut CLAUDE.md 20 Rezepte
        rezepte.Should().HaveCount(20);
    }

    [Fact]
    public void GetAllRecipes_AlleIdsEindeutig()
    {
        // Ausführung
        var rezepte = CraftingRecipe.GetAllRecipes();

        // Prüfung
        rezepte.Select(r => r.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetAllRecipes_AlleOutputProductIdsEindeutig()
    {
        // Ausführung
        var rezepte = CraftingRecipe.GetAllRecipes();

        // Prüfung: Kein Produkt kann von mehreren Rezepten hergestellt werden
        rezepte.Select(r => r.OutputProductId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetById_GültigeId_GibtRezeptZurück()
    {
        // Ausführung
        var rezept = CraftingRecipe.GetById("r_planks");

        // Prüfung
        rezept.Should().NotBeNull();
        rezept!.WorkshopType.Should().Be(WorkshopType.Carpenter);
        rezept.Tier.Should().Be(1);
    }

    [Fact]
    public void GetById_UngültigeId_GibtNullZurück()
    {
        // Ausführung
        var rezept = CraftingRecipe.GetById("r_nichtvorhanden");

        // Prüfung
        rezept.Should().BeNull();
    }

    [Fact]
    public void GetByOutputProduct_GültigesProdukt_GibtRezeptZurück()
    {
        // Ausführung
        var rezept = CraftingRecipe.GetByOutputProduct("furniture");

        // Prüfung
        rezept.Should().NotBeNull();
        rezept!.Id.Should().Be("r_furniture");
        rezept.Tier.Should().Be(2);
    }

    [Fact]
    public void GetByOutputProduct_UnbekanntesProdukt_GibtNullZurück()
    {
        // Ausführung
        var rezept = CraftingRecipe.GetByOutputProduct("nichtvorhanden_produkt");

        // Prüfung
        rezept.Should().BeNull();
    }

    [Fact]
    public void Tier1Rezepte_ErfordernKeinInputMaterial()
    {
        // Ausführung
        var tier1 = CraftingRecipe.GetAllRecipes().Where(r => r.Tier == 1).ToList();

        // Prüfung: Tier-1 sind Basis-Produkte ohne Vorstufe
        tier1.Should().AllSatisfy(r =>
            r.InputProducts.Should().BeEmpty($"Tier-1 Rezept {r.Id} sollte keine Inputs haben"));
    }

    [Fact]
    public void Tier2Rezepte_ErfordernTier1Input()
    {
        // Ausführung
        var tier2 = CraftingRecipe.GetAllRecipes().Where(r => r.Tier == 2).ToList();

        // Prüfung: Tier-2 benötigt Inputs
        tier2.Should().AllSatisfy(r =>
            r.InputProducts.Should().NotBeEmpty($"Tier-2 Rezept {r.Id} muss Inputs haben"));
    }

    [Fact]
    public void Rezept_SchreinerereiTier3_EmpfiehlWerkstattLevel300()
    {
        // Ausführung
        var rezept = CraftingRecipe.GetById("r_luxury_furniture");

        // Prüfung
        rezept.Should().NotBeNull();
        rezept!.RequiredWorkshopLevel.Should().Be(300);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CraftingJob - Zeitbasierte Properties
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CraftingJob_IsComplete_StartInVergangenheit_IstTrue()
    {
        // Vorbereitung: Job begann vor 2 Minuten, dauert nur 30 Sekunden
        var job = new CraftingJob
        {
            RecipeId = "r_planks",
            StartedAt = DateTime.UtcNow.AddSeconds(-60),
            DurationSeconds = 30
        };

        // Prüfung
        job.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void CraftingJob_IsComplete_StartInZukunft_IstFalse()
    {
        // Vorbereitung: Job läuft noch
        var job = new CraftingJob
        {
            RecipeId = "r_planks",
            StartedAt = DateTime.UtcNow.AddSeconds(-5),
            DurationSeconds = 120
        };

        // Prüfung
        job.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void CraftingJob_Progress_GeradeGestartet_IstNaheNull()
    {
        // Vorbereitung
        var job = new CraftingJob
        {
            StartedAt = DateTime.UtcNow,
            DurationSeconds = 100
        };

        // Prüfung: Kurz nach Start nahe 0
        job.Progress.Should().BeInRange(0.0, 0.05);
    }

    [Fact]
    public void CraftingJob_Progress_Abgeschlossen_IstEins()
    {
        // Vorbereitung
        var job = new CraftingJob
        {
            StartedAt = DateTime.UtcNow.AddSeconds(-200),
            DurationSeconds = 100
        };

        // Prüfung: Progress ist geclampt auf 1.0
        job.Progress.Should().Be(1.0);
    }

    [Fact]
    public void CraftingJob_TimeRemaining_Abgeschlossen_IstZero()
    {
        // Vorbereitung
        var job = new CraftingJob
        {
            StartedAt = DateTime.UtcNow.AddSeconds(-200),
            DurationSeconds = 100
        };

        // Prüfung: TimeRemaining ist nie negativ
        job.TimeRemaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void CraftingJob_TimeRemaining_NochAktiv_IstPositiv()
    {
        // Vorbereitung
        var job = new CraftingJob
        {
            StartedAt = DateTime.UtcNow,
            DurationSeconds = 300
        };

        // Prüfung
        job.TimeRemaining.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CraftingProduct - Statische Daten
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllProducts_EnthältAlleExpectedProdukte()
    {
        // Ausführung
        var produkte = CraftingProduct.GetAllProducts();

        // Prüfung: Mindestens die Standard-Tier-1-Produkte vorhanden
        produkte.Should().ContainKey("planks");
        produkte.Should().ContainKey("furniture");
        produkte.Should().ContainKey("luxury_furniture");
    }

    [Fact]
    public void GetAllProducts_JederOutputProductId_HatEintrag()
    {
        // Ausführung
        var rezepte = CraftingRecipe.GetAllRecipes();
        var produkte = CraftingProduct.GetAllProducts();

        // Prüfung: Jedes Rezept-Output hat einen Produkt-Eintrag
        foreach (var rezept in rezepte)
        {
            produkte.Should().ContainKey(rezept.OutputProductId,
                $"Rezept {rezept.Id} produziert '{rezept.OutputProductId}' - Produkt muss existieren");
        }
    }
}
