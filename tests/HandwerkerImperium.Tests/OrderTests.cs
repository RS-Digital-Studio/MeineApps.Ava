using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Order: FinalReward, FinalXp, IsCompleted, CurrentTask,
/// RecordTaskResult, CalculateEstimatedReward, IsExpired, IsRegularCustomerOrder.
/// </summary>
public class OrderTests
{
    // ═══════════════════════════════════════════════════════════════════
    // IsCompleted
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsCompleted_KeineAufgaben_IstTrue()
    {
        // Vorbereitung
        var order = new Order { Tasks = [] };

        // Prüfung: Ohne Aufgaben gilt der Auftrag als abgeschlossen
        order.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void IsCompleted_AufgabenVorhanden_IstFalse()
    {
        // Vorbereitung
        var order = new Order
        {
            Tasks = [new OrderTask { GameType = MiniGameType.Sawing }],
            CurrentTaskIndex = 0
        };

        // Prüfung
        order.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void IsCompleted_AlleAufgabenErledigt_IstTrue()
    {
        // Vorbereitung
        var order = new Order
        {
            Tasks = [new OrderTask { GameType = MiniGameType.Sawing }],
            CurrentTaskIndex = 1
        };

        // Prüfung
        order.IsCompleted.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CurrentTask
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CurrentTask_ErsteAufgabe_GibtKorrekteAufgabeZurück()
    {
        // Vorbereitung
        var task = new OrderTask { GameType = MiniGameType.Sawing };
        var order = new Order
        {
            Tasks = [task],
            CurrentTaskIndex = 0
        };

        // Prüfung
        order.CurrentTask.Should().Be(task);
    }

    [Fact]
    public void CurrentTask_AlleAbgeschlossen_IstNull()
    {
        // Vorbereitung
        var order = new Order
        {
            Tasks = [new OrderTask()],
            CurrentTaskIndex = 1
        };

        // Prüfung
        order.CurrentTask.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // FinalReward
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FinalReward_KeineErgebnisse_IstNull()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseReward = 1000m,
            TaskResults = []
        };

        // Prüfung: Keine Ergebnisse = 0 Belohnung
        order.FinalReward.Should().Be(0m);
    }

    [Fact]
    public void FinalReward_PerfectRating_GibtBestmöglicheBelohnung()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseReward = 1000m,
            Difficulty = OrderDifficulty.Easy,     // 1.0x
            OrderType = OrderType.Standard,         // 1.0x
            TaskResults = [MiniGameRating.Perfect]  // 1.5x
        };

        // Ausführung
        var reward = order.FinalReward;

        // Prüfung: 1000 * 1.5 * 1.0 * 1.0 = 1500
        reward.Should().Be(1500m);
    }

    [Fact]
    public void FinalReward_MissRating_GibtReduzierteBelohnung()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseReward = 1000m,
            Difficulty = OrderDifficulty.Easy,
            OrderType = OrderType.Standard,
            TaskResults = [MiniGameRating.Miss]  // 0.5x
        };

        // Prüfung: 1000 * 0.5 * 1.0 * 1.0 = 500
        order.FinalReward.Should().Be(500m);
    }

    [Fact]
    public void FinalReward_HardDifficultyMitPerfect_BerücksichtigtDifficultyMultiplier()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseReward = 1000m,
            Difficulty = OrderDifficulty.Hard,     // 3.5x
            OrderType = OrderType.Standard,        // 1.0x
            TaskResults = [MiniGameRating.Perfect] // 1.5x
        };

        // Prüfung: 1000 * 1.5 * 3.5 * 1.0 = 5250
        order.FinalReward.Should().Be(5250m);
    }

    [Fact]
    public void FinalReward_WeeklyOrder_BerücksichtigtOrderTypeMultiplier()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseReward = 1000m,
            Difficulty = OrderDifficulty.Medium,   // 1.5x
            OrderType = OrderType.Weekly,           // 3.0x
            TaskResults = [MiniGameRating.Good]    // 1.0x
        };

        // Prüfung: 1000 * 1.0 * 1.5 * 3.0 = 4500
        order.FinalReward.Should().Be(4500m);
    }

    [Fact]
    public void FinalReward_MehrereTasks_NutztDurchschnitt()
    {
        // Vorbereitung: Perfect (1.5) + Miss (0.5) = Durchschnitt 1.0
        var order = new Order
        {
            BaseReward = 1000m,
            Difficulty = OrderDifficulty.Easy,
            OrderType = OrderType.Standard,
            TaskResults = [MiniGameRating.Perfect, MiniGameRating.Miss]
        };

        // Prüfung: 1000 * 1.0 * 1.0 * 1.0 = 1000
        order.FinalReward.Should().Be(1000m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // FinalXp
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FinalXp_KeineErgebnisse_IstNull()
    {
        // Vorbereitung
        var order = new Order { BaseXp = 100, TaskResults = [] };

        // Prüfung
        order.FinalXp.Should().Be(0);
    }

    [Fact]
    public void FinalXp_PerfectRating_GibtBestmöglicheXP()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseXp = 100,
            Difficulty = OrderDifficulty.Easy,     // 1.0x
            OrderType = OrderType.Standard,        // 1.0x
            TaskResults = [MiniGameRating.Perfect] // 1.5x
        };

        // Prüfung: (int)(100 * 1.5 * 1.0 * 1.0) = 150
        order.FinalXp.Should().Be(150);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateEstimatedReward
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateEstimatedReward_NutztDifficultyUndOrderType()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseReward = 1000m,
            Difficulty = OrderDifficulty.Hard,   // 3.5x
            OrderType = OrderType.Large          // 1.8x
        };

        // Prüfung: 1000 * 3.5 * 1.8 = 6300
        order.CalculateEstimatedReward().Should().Be(6300m);
    }

    [Fact]
    public void EstimatedReward_IstGleichWieCalculateEstimatedReward()
    {
        // Vorbereitung
        var order = new Order
        {
            BaseReward = 500m,
            Difficulty = OrderDifficulty.Medium,
            OrderType = OrderType.Cooperation
        };

        // Prüfung: Property und Methode müssen identisch sein
        order.EstimatedReward.Should().Be(order.CalculateEstimatedReward());
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsExpired, IsRegularCustomerOrder
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsExpired_OhneDeadline_IstFalse()
    {
        // Vorbereitung
        var order = new Order { Deadline = null };

        // Prüfung
        order.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_DeadlineInVergangenheit_IstTrue()
    {
        // Vorbereitung
        var order = new Order { Deadline = DateTime.UtcNow.AddHours(-1) };

        // Prüfung
        order.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_DeadlineInZukunft_IstFalse()
    {
        // Vorbereitung
        var order = new Order { Deadline = DateTime.UtcNow.AddHours(1) };

        // Prüfung
        order.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsRegularCustomerOrder_MitCustomerId_IstTrue()
    {
        // Vorbereitung
        var order = new Order { CustomerId = "kunde_123" };

        // Prüfung
        order.IsRegularCustomerOrder.Should().BeTrue();
    }

    [Fact]
    public void IsRegularCustomerOrder_OhneCustomerId_IstFalse()
    {
        // Vorbereitung
        var order = new Order { CustomerId = null };

        // Prüfung
        order.IsRegularCustomerOrder.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecordTaskResult
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordTaskResult_FügtErgebnisHinzuUndInkrementiertIndex()
    {
        // Vorbereitung
        var order = new Order { CurrentTaskIndex = 0 };

        // Ausführung
        order.RecordTaskResult(MiniGameRating.Good);

        // Prüfung
        order.TaskResults.Should().HaveCount(1);
        order.TaskResults[0].Should().Be(MiniGameRating.Good);
        order.CurrentTaskIndex.Should().Be(1);
    }

    [Fact]
    public void RecordTaskResult_MehrfachAufruf_AlleErgebnisseGespeichert()
    {
        // Vorbereitung
        var order = new Order { CurrentTaskIndex = 0 };

        // Ausführung
        order.RecordTaskResult(MiniGameRating.Perfect);
        order.RecordTaskResult(MiniGameRating.Miss);
        order.RecordTaskResult(MiniGameRating.Ok);

        // Prüfung
        order.TaskResults.Should().HaveCount(3);
        order.CurrentTaskIndex.Should().Be(3);
    }
}
