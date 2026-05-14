using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// V7 (, Plan Section 3.9): Gilden-Mega-Projekte.
/// Mitglieder spenden Materialien aus ihrem Lager → permanenter Gilden-Bonus bei Abschluss.
///
/// Firebase-Pfad: <c>guilds/{guildId}/megaProjects/active</c>. Spende-Operationen via
/// <see cref="IFirebaseService.UpdateAsync"/> (PATCH) — atomar gegen Concurrent-Writes.
/// HMAC-signiert ueber stabile Felder (ProjectId, Type, CreatedAt).
/// ClaimedGuildProjectIds im GameState verhindert Doppel-Belohnung.
/// </summary>
public sealed class GuildMegaProjectService : IGuildMegaProjectService
{
    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameStateService;
    private readonly IGameIntegrityService _integrity;
    private readonly IWarehouseService _warehouse;
    private readonly ICraftingService _crafting;
    private readonly IAnalyticsService? _analytics;

    private const string MegaHmacSalt = "guild-mega-project-v1";

    public event Action<GuildMegaProject>? ProjectUpdated;
    public event Action<GuildMegaProject>? ProjectCompleted;

    public GuildMegaProjectService(
        IFirebaseService firebase,
        IGameStateService gameStateService,
        IGameIntegrityService integrity,
        IWarehouseService warehouse,
        ICraftingService crafting,
        IAnalyticsService? analytics = null)
    {
        _firebase = firebase;
        _gameStateService = gameStateService;
        _integrity = integrity;
        _warehouse = warehouse;
        _crafting = crafting;
        _analytics = analytics;
    }

    private string? CurrentGuildId => _gameStateService.State.GuildMembership?.GuildId;

    private static string GetFirebasePath(string guildId) =>
        $"guilds/{guildId}/megaProjects/active";

    public async Task<GuildMegaProject?> GetActiveProjectAsync()
    {
        if (string.IsNullOrEmpty(CurrentGuildId)) return null;
        var path = GetFirebasePath(CurrentGuildId);
        var project = await _firebase.GetAsync<GuildMegaProject>(path).ConfigureAwait(false);
        // HMAC validieren: tampered Projekte werden ignoriert.
        if (project != null && project.Hmac != ComputeHmac(project))
        {
            System.Diagnostics.Debug.WriteLine("[GuildMegaProject] HMAC-Mismatch — ignoriere Projekt");
            return null;
        }
        return project;
    }

    public async Task<bool> StartProjectAsync(GuildMegaProjectType type)
    {
        if (string.IsNullOrEmpty(CurrentGuildId)) return false;
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return false;

        // Nur Co-Leader/Leader duerfen starten — wird durch Firebase-Rules zusaetzlich validiert.
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return false;

        // Pruefen ob bereits ein aktives Projekt existiert
        var existing = await GetActiveProjectAsync().ConfigureAwait(false);
        if (existing != null && !existing.IsCompleted) return false;

        var project = new GuildMegaProject
        {
            ProjectId = Guid.NewGuid().ToString("N"),
            Type = type,
            CreatedAt = DateTime.UtcNow,
            Contributions = new Dictionary<string, int>(),
            Donations = new Dictionary<string, GuildMegaProjectDonation>(),
            CompletedAt = null
        };
        project.Hmac = ComputeHmac(project);

        var path = GetFirebasePath(CurrentGuildId);
        var ok = await _firebase.SetAsync(path, project).ConfigureAwait(false);
        if (ok) ProjectUpdated?.Invoke(project);
        return ok;
    }

    public async Task<bool> DonateAsync(string productId, int count)
    {
        if (count <= 0 || string.IsNullOrEmpty(productId)) return false;
        if (string.IsNullOrEmpty(CurrentGuildId)) return false;
        if (string.IsNullOrEmpty(_firebase.PlayerId)) return false;

        var project = await GetActiveProjectAsync().ConfigureAwait(false);
        if (project == null || project.IsCompleted) return false;

        // Sunset-Check (Plan Section 4): Projekte aelter als 30 Tage werden gesonnsuet.
        var ageDays = (DateTime.UtcNow - project.CreatedAt).TotalDays;
        if (ageDays > GuildMegaProjectTemplates.AbandonmentSunsetDays) return false;

        // Verfuegbarkeit pruefen (Reservierungen abziehen)
        int available = _warehouse.GetAvailable(productId);
        if (available < count) return false;

        // Material-Anforderung pruefen — nicht mehr spenden als noch ben
        var requirements = GuildMegaProjectTemplates.GetRequirements(project.Type);
        if (!requirements.TryGetValue(productId, out int required)) return false;
        int alreadyDonated = project.Contributions.GetValueOrDefault(productId, 0);
        int stillNeeded = Math.Max(0, required - alreadyDonated);
        int actualCount = Math.Min(count, stillNeeded);
        if (actualCount <= 0) return false;

        // Spieler-Inventar atomar reduzieren
        var state = _gameStateService.State;
        int current = state.CraftingInventory.GetValueOrDefault(productId, 0);
        if (current < actualCount) return false;
        state.CraftingInventory[productId] = current - actualCount;
        if (state.CraftingInventory[productId] <= 0)
            state.CraftingInventory.Remove(productId);

        // Donation-Eintrag aktualisieren (lokal, dann PATCH)
        var playerId = _firebase.PlayerId!;
        var playerName = state.PlayerName ?? "Player";
        decimal sellPrice = _crafting.GetSellPrice(productId);
        decimal donationValue = sellPrice * actualCount;

        if (!project.Donations.TryGetValue(playerId, out var donation))
        {
            donation = new GuildMegaProjectDonation { PlayerName = playerName };
            project.Donations[playerId] = donation;
        }
        donation.PlayerName = playerName;
        donation.TotalValue += donationValue;
        donation.ItemCount += actualCount;
        donation.LastDonatedAt = DateTime.UtcNow;

        project.Contributions[productId] = alreadyDonated + actualCount;

        // Atomarer PATCH — nur Contributions + Donations-Subpfad, kein Last-Write-Wins.
        var path = GetFirebasePath(CurrentGuildId);
        var updates = new Dictionary<string, object>
        {
            [$"contributions/{productId}"] = project.Contributions[productId],
            [$"donations/{playerId}/playerName"] = playerName,
            [$"donations/{playerId}/totalValue"] = donation.TotalValue,
            [$"donations/{playerId}/itemCount"] = donation.ItemCount,
            [$"donations/{playerId}/lastDonatedAt"] = donation.LastDonatedAt.ToString("O"),
        };

        // Pruefen ob das Projekt mit dieser Spende abgeschlossen wird
        bool isNowComplete = IsAllRequirementsMet(project, requirements);
        if (isNowComplete && !project.IsCompleted)
        {
            project.CompletedAt = DateTime.UtcNow;
            updates["completedAt"] = project.CompletedAt.Value.ToString("O");
        }

        var ok = await _firebase.UpdateAsync(path, updates).ConfigureAwait(false);
        if (!ok)
        {
            // Rollback: Material wieder einlagern
            int rollback = state.CraftingInventory.GetValueOrDefault(productId, 0);
            state.CraftingInventory[productId] = rollback + actualCount;
            return false;
        }

        ProjectUpdated?.Invoke(project);

        // V7 (Telemetrie, Plan Section 8.1): guild_mega_project_donation
        _analytics?.TrackEvent("guild_mega_project_donation", new Dictionary<string, object?>
        {
            ["project_id"] = project.ProjectId,
            ["project_type"] = (int)project.Type,
            ["item_id"] = productId,
            ["count"] = actualCount,
            ["donation_value"] = (double)donationValue
        });

        if (isNowComplete)
        {
            ProjectCompleted?.Invoke(project);
            // Auto-Claim fuer den Spender der das Projekt abschliesst
            await TryClaimRewardAsync().ConfigureAwait(false);
        }

        return true;
    }

    public async Task<bool> TryClaimRewardAsync()
    {
        if (string.IsNullOrEmpty(CurrentGuildId)) return false;
        var project = await GetActiveProjectAsync().ConfigureAwait(false);
        if (project == null || !project.IsCompleted) return false;

        var state = _gameStateService.State;
        // Idempotenz: pro Spieler und Projekt nur einmal claimen.
        if (state.ClaimedGuildProjectIds.Contains(project.ProjectId)) return false;

        var membership = state.GuildMembership;
        if (membership == null) return false;

        // Permanenten Gilden-Bonus addieren.
        var reward = GuildMegaProjectTemplates.GetReward(project.Type);
        membership.MegaProjectCraftingSpeedBonus += reward.CraftingSpeedBonus;
        membership.MegaProjectAutoSellPriceBonus += reward.AutoSellPriceBonus;
        membership.MegaProjectBonusWarehouseSlots += reward.BonusWarehouseSlots;

        // Doppel-Belohnung verhindern.
        state.ClaimedGuildProjectIds.Add(project.ProjectId);
        int typeKey = (int)project.Type;
        if (!membership.CompletedMegaProjectTypes.Contains(typeKey))
            membership.CompletedMegaProjectTypes.Add(typeKey);

        return true;
    }

    private static bool IsAllRequirementsMet(GuildMegaProject project, Dictionary<string, int> requirements)
    {
        foreach (var (id, needed) in requirements)
        {
            int donated = project.Contributions.GetValueOrDefault(id, 0);
            if (donated < needed) return false;
        }
        return true;
    }

    private string ComputeHmac(GuildMegaProject project)
    {
        var oldHmac = project.Hmac;
        project.Hmac = null;
        var raw = $"{MegaHmacSalt}|{project.ProjectId}|{(int)project.Type}|{project.CreatedAt:O}";
        project.Hmac = oldHmac;
        return _integrity.ComputeStringHmac(raw);
    }
}
