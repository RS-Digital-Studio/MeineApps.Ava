namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bounded-Context "Onboarding": Bündelt die Spielerführungs-Subsysteme — FTUE, Story-Kapitel,
/// kontextuelle Hints, Welcome-Back-Belohnungen, Referrals. AAA-Audit P1 Service-Sprawl-Reduction.
///
/// MainViewModel hat aktuell 5 Einzel-Dependencies fuer Onboarding-Logik — die Facade
/// reduziert das auf 1 Parameter.
/// </summary>
public interface IOnboardingFacade
{
    /// <summary>First-Time-User-Experience State-Machine (10-Step-Sequenz).</summary>
    IFtueService Ftue { get; }

    /// <summary>Story-Kapitel (35+ Meister-Hans-Texte, Level/Prestige-getriggert).</summary>
    IStoryService? Story { get; }

    /// <summary>Kontextuelle Hints (Tap-Anywhere-to-Dismiss, 24h-Cooldown).</summary>
    IContextualHintService Hint { get; }

    /// <summary>Welcome-Back-Belohnungen (Streak-Tage, Comeback-Bonus).</summary>
    IWelcomeBackService WelcomeBack { get; }

    /// <summary>Friend-Invite-Loop (6-stellige Codes, 3-Tier-Rewards).</summary>
    IReferralService? Referral { get; }
}
