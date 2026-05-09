using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents a contract/order that players can complete for rewards.
/// Orders have types, optional deadlines, and can come from regular customers.
/// Implementiert <see cref="INotifyPropertyChanged"/> fuer den Live-Countdown
/// (<see cref="LiveCountdownText"/>) — eine externe Komponente (Dashboard-Timer)
/// ruft <see cref="RaiseLiveCountdownChanged"/> 1x/Sekunde fuer aktive Live-Auftraege auf.
/// </summary>
public class Order : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Feuert PropertyChanged fuer LiveCountdownText und LiveCountdownSeconds.
    /// Wird von einem externen 1Hz-Timer (DashboardView) pro sichtbarem Live-Auftrag aufgerufen.
    /// Kein interner Timer — Ressourcen-Ersparnis bei N Orders.
    /// </summary>
    public void RaiseLiveCountdownChanged()
    {
        OnPropertyChanged(nameof(LiveCountdownText));
        OnPropertyChanged(nameof(LiveCountdownSeconds));
    }

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("titleKey")]
    public string TitleKey { get; set; } = string.Empty;

    [JsonPropertyName("titleFallback")]
    public string TitleFallback { get; set; } = string.Empty;

    [JsonPropertyName("workshopType")]
    public WorkshopType WorkshopType { get; set; }

    [JsonPropertyName("difficulty")]
    public OrderDifficulty Difficulty { get; set; } = OrderDifficulty.Medium;

    /// <summary>
    /// Type of order (Quick, Standard, Large, Weekly, Cooperation).
    /// </summary>
    [JsonPropertyName("orderType")]
    public OrderType OrderType { get; set; } = OrderType.Standard;

    /// <summary>
    /// Spieler-gewaehlte Strategie (Safe/Standard/Risk, v2.0.35).
    /// Wirkt auf MiniGame-Schwierigkeit + Reward-Multiplikator + Miss-Handling.
    /// Default Standard bis der Spieler im Strategy-Dialog waehlt.
    /// </summary>
    [JsonPropertyName("strategy")]
    public OrderStrategy Strategy { get; set; } = OrderStrategy.Standard;

    /// <summary>
    /// Ob der Auftrag durch einen Miss unter <see cref="OrderStrategy.Risk"/> komplett gescheitert ist.
    /// Wenn true: <see cref="FinalReward"/> = 0, Reputation-Penalty wurde angewendet.
    /// </summary>
    [JsonPropertyName("hasHardFailed")]
    public bool HasHardFailed { get; set; }

    /// <summary>
    /// Ob dieser Auftrag live ueber den OrderLive-Stream generiert wurde (v2.0.35).
    /// Live-Auftraege haben <see cref="ExpiresAt"/> gesetzt und verschwinden wenn abgelaufen.
    /// UI zeigt Countdown + "LIVE"-Badge.
    /// </summary>
    [JsonPropertyName("isLive")]
    public bool IsLive { get; set; }

    /// <summary>
    /// Ob dieser Auftrag ein seltener Premium/VIP-Auftrag ist (v2.0.35).
    /// Premium-Auftraege haben 3x Reward und kuerzere Deadlines (45-90s).
    /// Spawn-Chance ~5% bei Live-Generation.
    /// </summary>
    [JsonPropertyName("isPremium")]
    public bool IsPremium { get; set; }

    [JsonPropertyName("tasks")]
    public List<OrderTask> Tasks { get; set; } = [];

    [JsonPropertyName("baseReward")]
    public decimal BaseReward { get; set; }

    [JsonPropertyName("baseXp")]
    public int BaseXp { get; set; }

    [JsonPropertyName("requiredLevel")]
    public int RequiredLevel { get; set; } = 1;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Deadline for timed orders (Weekly).
    /// </summary>
    [JsonPropertyName("deadline")]
    public DateTime? Deadline { get; set; }

    /// <summary>
    /// v2.0.37: Zeitpunkt zu dem die Order pausiert wurde (App im Hintergrund).
    /// Null = nicht pausiert. Beim Resume wird die Differenz auf <see cref="AccumulatedPauseDuration"/> addiert.
    /// </summary>
    [JsonPropertyName("pausedAt")]
    public DateTime? PausedAt { get; set; }

    /// <summary>
    /// v2.0.37: Akkumulierte Pause-Dauer (in TimeSpan). Wird zur ExpiresAt-Pruefung addiert,
    /// damit Live-Orders nicht waehrend Hintergrund-Sessions ablaufen. Cap: 5 Minuten.
    /// </summary>
    [JsonPropertyName("accumulatedPauseDuration")]
    public TimeSpan AccumulatedPauseDuration { get; set; }

    /// <summary>
    /// Customer ID if this order is from a regular customer.
    /// </summary>
    [JsonPropertyName("customerId")]
    public string? CustomerId { get; set; }

    /// <summary>
    /// Generated customer name for display.
    /// </summary>
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Seed for customer avatar generation.
    /// </summary>
    [JsonPropertyName("customerAvatarSeed")]
    public string CustomerAvatarSeed { get; set; } = Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// Required workshop types for Cooperation orders.
    /// </summary>
    [JsonPropertyName("requiredWorkshops")]
    public List<WorkshopType>? RequiredWorkshops { get; set; }

    /// <summary>
    /// Benötigte Materialien für Lieferaufträge (MaterialOrder).
    /// Key = Produkt-ID, Value = benötigte Menge.
    /// </summary>
    [JsonPropertyName("requiredMaterials")]
    public Dictionary<string, int>? RequiredMaterials { get; set; }

    /// <summary>
    /// Reputation bonus/penalty on completion.
    /// </summary>
    [JsonPropertyName("reputationBonus")]
    public decimal ReputationBonus { get; set; }

    /// <summary>
    /// Ob die Belohnung durch Rewarded Ad verdoppelt wurde.
    /// </summary>
    [JsonPropertyName("isScoreDoubled")]
    public bool IsScoreDoubled { get; set; }

    /// <summary>
    /// Combo-Multiplikator aus PaintingGame (1.0 = kein Combo).
    /// </summary>
    [JsonPropertyName("comboMultiplier")]
    public decimal ComboMultiplier { get; set; } = 1m;

    [JsonPropertyName("currentTaskIndex")]
    public int CurrentTaskIndex { get; set; }

    [JsonPropertyName("taskResults")]
    public List<MiniGameRating> TaskResults { get; set; } = [];

    [JsonIgnore]
    public string DisplayTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayWorkshopName { get; set; } = string.Empty;

    /// <summary>
    /// Lokalisierter Auftragstyp-Name (z.B. "Großauftrag", "Wochenauftrag").
    /// </summary>
    [JsonIgnore]
    public string DisplayOrderType { get; set; } = string.Empty;

    /// <summary>
    /// Beschreibung der benötigten Materialien (nur Lieferaufträge).
    /// </summary>
    [JsonIgnore]
    public string DisplayDescription { get; set; } = string.Empty;

    /// <summary>
    /// Icon für den Auftragstyp (z.B. ⚡, 📦, 📅, 🤝).
    /// </summary>
    [JsonIgnore]
    public string OrderTypeIcon { get; set; } = string.Empty;

    /// <summary>
    /// Badge-Farbe für den Auftragstyp (Large=Orange, Weekly=Gold, Cooperation=Teal, Standard/Quick=leer).
    /// </summary>
    [JsonIgnore]
    public string OrderTypeBadgeColor { get; set; } = string.Empty;

    /// <summary>
    /// Ob das Auftragstyp-Badge angezeigt werden soll (nur bei Large, Weekly, Cooperation).
    /// </summary>
    [JsonIgnore]
    public bool ShowOrderTypeBadge { get; set; }

    [JsonIgnore]
    public bool IsCompleted => CurrentTaskIndex >= Tasks.Count;

    [JsonIgnore]
    public OrderTask? CurrentTask => CurrentTaskIndex < Tasks.Count ? Tasks[CurrentTaskIndex] : null;

    /// <summary>
    /// Whether this order has a deadline and it has passed.
    /// Beruecksichtigt <see cref="ExpiresAt"/> + <see cref="AccumulatedPauseDuration"/> (v2.0.35/37).
    /// </summary>
    [JsonIgnore]
    public bool IsExpired =>
        (Deadline != null && DateTime.UtcNow > Deadline) ||
        (ExpiresAt != null && GetEffectiveNow() > ExpiresAt);

    /// <summary>
    /// v2.0.37: „Effektive Jetzt-Zeit" fuer Live-Order-Ablauf — zieht akkumulierte Pause-Dauer ab,
    /// sodass Hintergrund-Zeit nicht zaehlt. Bei aktivem Pause-State (PausedAt != null) wird
    /// die laufende Pause zusaetzlich abgezogen (Cap 5 Minuten).
    /// </summary>
    private DateTime GetEffectiveNow()
    {
        var now = DateTime.UtcNow;
        var pauseTotal = AccumulatedPauseDuration;
        if (PausedAt.HasValue)
        {
            var currentPause = now - PausedAt.Value;
            if (currentPause < TimeSpan.Zero) currentPause = TimeSpan.Zero;
            pauseTotal += currentPause;
        }
        // Cap auf 5 Minuten — sonst koennten Spieler Live-Orders unbegrenzt „bunkern".
        if (pauseTotal > TimeSpan.FromMinutes(5)) pauseTotal = TimeSpan.FromMinutes(5);
        return now - pauseTotal;
    }

    /// <summary>Verbleibende Sekunden bis <see cref="ExpiresAt"/>. Null wenn kein Live-Auftrag (v2.0.35).</summary>
    [JsonIgnore]
    public double? LiveCountdownSeconds
    {
        get
        {
            if (!IsLive || !ExpiresAt.HasValue) return null;
            var remaining = (ExpiresAt.Value - GetEffectiveNow()).TotalSeconds;
            return remaining < 0 ? 0 : remaining;
        }
    }

    /// <summary>Formatierter Countdown-String ("1m 45s", "30s") fuer Live-Auftrags-Karten (v2.0.35).</summary>
    [JsonIgnore]
    public string LiveCountdownText
    {
        get
        {
            if (LiveCountdownSeconds is not { } sec) return "";
            if (sec >= 60)
            {
                int minutes = (int)(sec / 60);
                int seconds = (int)(sec % 60);
                return $"{minutes}m {seconds:00}s";
            }
            return $"{(int)sec}s";
        }
    }

    /// <summary>
    /// Whether this is from a regular customer.
    /// </summary>
    [JsonIgnore]
    public bool IsRegularCustomerOrder => CustomerId != null;

    [JsonIgnore]
    public decimal FinalReward
    {
        get
        {
            if (HasHardFailed) return 0;        // Risk-Strategy Hard-Fail: 0 Reward
            if (TaskResults.Count == 0) return 0;
            decimal avgPercentage = TaskResults.Average(r => r.GetRewardPercentage());
            return BaseReward * avgPercentage * Difficulty.GetRewardMultiplier() * OrderType.GetRewardMultiplier() * Strategy.GetRewardMultiplier();
        }
    }

    [JsonIgnore]
    public int FinalXp
    {
        get
        {
            if (HasHardFailed) return 0;        // Risk-Strategy Hard-Fail: 0 XP
            if (TaskResults.Count == 0) return 0;
            decimal avgPercentage = TaskResults.Average(r => r.GetXpPercentage());
            return (int)(BaseXp * avgPercentage * Difficulty.GetXpMultiplier() * OrderType.GetXpMultiplier() * Strategy.GetXpMultiplier());
        }
    }

    /// <summary>
    /// Berechnet die geschätzte Belohnung unter Einbeziehung aller Multiplikatoren.
    /// Geht von "Good"-Rating (100%) aus als Referenzwert.
    /// </summary>
    public decimal CalculateEstimatedReward()
    {
        return BaseReward * Difficulty.GetRewardMultiplier() * OrderType.GetRewardMultiplier() * Strategy.GetRewardMultiplier();
    }

    /// <summary>
    /// Berechnet die geschätzte XP-Belohnung unter Einbeziehung aller Multiplikatoren.
    /// Geht von "Good"-Rating (100%) aus als Referenzwert.
    /// </summary>
    public int CalculateEstimatedXp()
    {
        return (int)(BaseXp * Difficulty.GetXpMultiplier() * OrderType.GetXpMultiplier() * Strategy.GetXpMultiplier());
    }

    /// <summary>
    /// Geschätzte Belohnung inkl. Difficulty + OrderType (für Dashboard-Binding).
    /// </summary>
    [JsonIgnore]
    public decimal EstimatedReward => CalculateEstimatedReward();

    /// <summary>
    /// Geschätzte XP inkl. Difficulty + OrderType (für Dashboard-Binding).
    /// </summary>
    [JsonIgnore]
    public int EstimatedXp => CalculateEstimatedXp();

    public void RecordTaskResult(MiniGameRating rating)
    {
        TaskResults.Add(rating);
        CurrentTaskIndex++;
    }
}

/// <summary>
/// A single task within an order.
/// </summary>
public class OrderTask
{
    [JsonPropertyName("gameType")]
    public MiniGameType GameType { get; set; }

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = string.Empty;

    [JsonPropertyName("descriptionFallback")]
    public string DescriptionFallback { get; set; } = string.Empty;
}
