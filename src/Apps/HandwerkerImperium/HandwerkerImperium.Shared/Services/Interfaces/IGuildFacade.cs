namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Facade ueber alle 7 Gilden-Subsystem-Services. Buendelt Abhaengigkeiten
/// fuer Konsumenten (vorrangig <see cref="HandwerkerImperium.ViewModels.GuildViewModel"/>),
/// die sonst 7 einzelne Services injizieren muessten.
///
/// Entwurfs-Entscheidung: Service-Container-Pattern (Properties), keine Flat-Methoden-Delegation.
/// Vorteile:
/// - Kein Boilerplate (keine 40+ Pass-Through-Methoden).
/// - Consumer-Code bleibt semantisch identisch (_guildService.X → _facade.Guild.X).
/// - Keine Namens-Kollisionen (GuildService.InitializeAsync vs. WarSeasonService.InitializeAsync).
/// - Sub-VMs koennen dieselbe Facade nutzen, wenn noetig.
///
/// Die Facade ist Singleton und disposed die inneren Services NICHT
/// (Ownership liegt beim DI-Container, vgl. <see cref="HandwerkerImperium.App.DisposeServices"/>).
/// </summary>
public interface IGuildFacade
{
    /// <summary>Kern-Gildenservice (CRUD, Browse, Rollen, MemberCount).</summary>
    IGuildService Guild { get; }

    /// <summary>Einladungs-Subsystem (Invite-Codes, Spieler-Browser, Inbox).</summary>
    IGuildInviteService Invite { get; }

    /// <summary>Gilden-Forschungsbaum (18 Technologien, Timer, Effekt-Cache).</summary>
    IGuildResearchService Research { get; }

    /// <summary>Gilden-Chat (Polling, Cooldown, Moderation).</summary>
    IGuildChatService Chat { get; }

    /// <summary>Gilden-Kriegs-Saison (Matchmaking, Scoring, Ligen, Bonus-Missionen).</summary>
    IGuildWarSeasonService WarSeason { get; }

    /// <summary>Kooperative Gilden-Bosse (6 Typen, Schadens-Tracking).</summary>
    IGuildBossService Boss { get; }

    /// <summary>Gilden-Hauptquartier (10 Gebaeude, Upgrades, Effekt-Cache).</summary>
    IGuildHallService Hall { get; }

    /// <summary>Kontextuelle Gilden-Tipps (First-Time-Flags, 24h-Cooldown).</summary>
    IGuildTipService Tip { get; }

    /// <summary>Gilden-Achievements (30 Stueck, 3 Tiers).</summary>
    IGuildAchievementService Achievement { get; }
}
