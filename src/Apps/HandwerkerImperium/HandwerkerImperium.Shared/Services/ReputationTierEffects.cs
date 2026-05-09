using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// AAA-Audit P0 Zerlegungs-Sprint: Reputation-Tier-Up-Effekte aus dem MainViewModel
/// extrahiert. ~40 Zeilen Logik + Lokalisierungs-Map liegt jetzt isoliert.
/// </summary>
public sealed class ReputationTierEffects : IReputationTierEffects
{
    private readonly ILocalizationService _localizationService;
    private readonly IAudioService _audioService;

    public ReputationTierEffects(ILocalizationService localizationService, IAudioService audioService)
    {
        _localizationService = localizationService;
        _audioService = audioService;
    }

    public void HandleTierChanged(
        ReputationTierChangedEventArgs e,
        Action<string, string> floatingTextRaiser,
        Action celebrationRaiser,
        Action<string, string>? achievementDialog)
    {
        if (!e.IsUp) return;

        var tierName = _localizationService.GetString(e.NewTier.GetLocalizationKey())
                       ?? e.NewTier.ToString();
        var format = _localizationService.GetString("RepTierUpFormat") ?? "{0} reached!";
        var headline = string.Format(format, tierName);

        floatingTextRaiser(headline, "level");
        celebrationRaiser();
        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        // v2.0.39 Audit-Fix U7: Modal-Dialog mit Tier-Effekten (nur bei Tier-Aufstieg
        // ueber Beginner — Beginner ist der Default-Start-Tier und braucht keine Erklaerung).
        if (achievementDialog == null) return;
        if (e.NewTier <= CustomerReputationTier.Beginner) return;

        var effectsKey = e.NewTier switch
        {
            CustomerReputationTier.CityKnown => "RepTierCityKnownEffects",
            CustomerReputationTier.RegionStar => "RepTierRegionStarEffects",
            CustomerReputationTier.IndustryLegend => "RepTierIndustryLegendEffects",
            _ => null
        };
        if (string.IsNullOrEmpty(effectsKey)) return;

        var effectsText = _localizationService.GetString(effectsKey);
        if (string.IsNullOrEmpty(effectsText)) return;

        achievementDialog(headline, effectsText);
    }
}
