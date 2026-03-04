using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Generiert und verwaltet Welcome-Back-Angebote basierend auf Abwesenheitsdauer.
/// Standard (24-72h), Premium (72h+) und StarterPack (einmalig ab Level 5).
/// </summary>
public sealed class WelcomeBackService : IWelcomeBackService
{
    private readonly IGameStateService _gameStateService;

    public event Action? OfferGenerated;

    public WelcomeBackService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
    }

    public void CheckAndGenerateOffer()
    {
        var state = _gameStateService.State;

        // Bereits ein aktives, nicht-abgelaufenes Angebot vorhanden
        if (state.ActiveWelcomeBackOffer != null && !state.ActiveWelcomeBackOffer.IsExpired)
            return;

        // Abgelaufenes Angebot verwerfen
        if (state.ActiveWelcomeBackOffer != null && state.ActiveWelcomeBackOffer.IsExpired)
        {
            state.ActiveWelcomeBackOffer = null;
            _gameStateService.MarkDirty();
        }

        var now = DateTime.UtcNow;
        var absence = now - state.LastPlayedAt;

        // Einkommens-Basis für Belohnungen
        var netPerSecond = Math.Max(1m, state.NetIncomePerSecond);

        WelcomeBackOffer? offer = null;

        // StarterPack: Einmalig ab Level 5, wenn noch nicht beansprucht
        if (state.PlayerLevel >= 5 && !state.ClaimedStarterPack)
        {
            offer = new WelcomeBackOffer
            {
                Type = WelcomeBackOfferType.StarterPack,
                GoldenScrewReward = 10,
                MoneyReward = 50_000m,
                XpReward = 0,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            };
        }
        // Premium: 72h+ Abwesenheit
        else if (absence.TotalHours >= 72)
        {
            // 1h Einkommen als Geld-Belohnung
            var moneyReward = Math.Round(netPerSecond * 3600m, 0);

            offer = new WelcomeBackOffer
            {
                Type = WelcomeBackOfferType.Premium,
                GoldenScrewReward = 8,
                MoneyReward = Math.Max(5000m, moneyReward),
                XpReward = 0,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            };
        }
        // Standard: 24-72h Abwesenheit
        else if (absence.TotalHours >= 24)
        {
            // 30min Einkommen als Geld-Belohnung
            var moneyReward = Math.Round(netPerSecond * 1800m, 0);

            offer = new WelcomeBackOffer
            {
                Type = WelcomeBackOfferType.Standard,
                GoldenScrewReward = 5,
                MoneyReward = Math.Max(2000m, moneyReward),
                XpReward = 0,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24)
            };
        }

        if (offer != null)
        {
            state.ActiveWelcomeBackOffer = offer;
            _gameStateService.MarkDirty();
            OfferGenerated?.Invoke();
        }
    }

    public void ClaimOffer()
    {
        var state = _gameStateService.State;
        var offer = state.ActiveWelcomeBackOffer;

        if (offer == null || offer.IsExpired)
            return;

        // Belohnungen gutschreiben
        if (offer.MoneyReward > 0)
            _gameStateService.AddMoney(offer.MoneyReward);

        if (offer.GoldenScrewReward > 0)
            _gameStateService.AddGoldenScrews(offer.GoldenScrewReward);

        if (offer.XpReward > 0)
            _gameStateService.AddXp(offer.XpReward);

        // StarterPack als beansprucht markieren
        if (offer.Type == WelcomeBackOfferType.StarterPack)
            state.ClaimedStarterPack = true;

        state.ActiveWelcomeBackOffer = null;
        _gameStateService.MarkDirty();
    }

    public void DismissOffer()
    {
        var state = _gameStateService.State;

        if (state.ActiveWelcomeBackOffer == null)
            return;

        state.ActiveWelcomeBackOffer = null;
        _gameStateService.MarkDirty();
    }
}
