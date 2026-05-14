using Avalonia.Threading;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// (MainViewModel-Zerlegung): Cinematic-Coordinator. Subscribed auf
/// <see cref="IPrestigeService.CinematicReady"/>, lokalisiert die Tier-Namen, spielt
/// die Celebration-Music ab und triggert die View ueber das <see cref="CinematicReady"/>-Event.
///
/// Lifecycle: Singleton im DI. <see cref="StartListening"/> wird vom MainViewModel-Ctor
/// einmalig aufgerufen — danach laeuft alles selbststaendig.
/// </summary>
public sealed class CinematicCoordinator : ICinematicCoordinator, IDisposable
{
    private readonly IPrestigeService _prestigeService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IAnalyticsService? _analyticsService;
    private bool _started;
    private bool _disposed;

    public event Action<PrestigeCinematicData>? CinematicReady;

    public CinematicCoordinator(
        IPrestigeService prestigeService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IAnalyticsService? analyticsService = null)
    {
        _prestigeService = prestigeService;
        _audioService = audioService;
        _localizationService = localizationService;
        _analyticsService = analyticsService;
    }

    /// <summary>Aktiviert die Cinematic-Subscription. Idempotent — mehrfacher Aufruf ist sicher.</summary>
    public void StartListening()
    {
        if (_started) return;
        _started = true;
        _prestigeService.CinematicReady += OnCinematicReady;
    }

    private void OnCinematicReady(object? sender, PrestigeCinematicData data)
    {
        // Tier-Name lokalisieren — RESX-Keys "PrestigeBronze", "PrestigeSilver" etc.
        var localizedTierName = _localizationService.GetString($"Prestige{data.Tier}")
                                ?? data.Tier.ToString();
        var resolved = new PrestigeCinematicData
        {
            MoneyAtPrestige = data.MoneyAtPrestige,
            Tier = data.Tier,
            BasePrestigePoints = data.BasePrestigePoints,
            BonusPrestigePoints = data.BonusPrestigePoints,
            TierMultiplierRaw = data.TierMultiplierRaw,
            DiminishingReturnsFactor = data.DiminishingReturnsFactor,
            TierMultiplierEffective = data.TierMultiplierEffective,
            TierCount = data.TierCount,
            RunDurationSeconds = data.RunDurationSeconds,
            ActiveChallengeCount = data.ActiveChallengeCount,
            TierDisplayName = localizedTierName,
        };

        Dispatcher.UIThread.Post(() =>
        {
            // Celebration-Track waehrend der Cinematic, Audio-Fehler duerfen blockieren.
            try { _ = _audioService.PlayMusicAsync(MusicTrack.Celebration, crossfade: true); }
            catch { /* Audio-Fehler ignorieren */ }
            CinematicReady?.Invoke(resolved);
        });
    }

    public void OnSkipped()
    {
        _analyticsService?.TrackEvent(AnalyticsEvents.PrestigeCinematicSkipped);
    }

    public void OnDismissed()
    {
        _analyticsService?.TrackEvent(AnalyticsEvents.PrestigeCinematicCompleted);
        // Zurueck zum Default-Track
        try { _ = _audioService.PlayMusicAsync(MusicTrack.IdleWorkshop, crossfade: true); }
        catch { /* Audio-Fehler ignorieren */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_started)
        {
            try { _prestigeService.CinematicReady -= OnCinematicReady; } catch { }
        }
    }
}
