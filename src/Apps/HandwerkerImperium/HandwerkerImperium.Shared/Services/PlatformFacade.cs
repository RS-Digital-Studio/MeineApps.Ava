using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IPlatformFacade"/> — Service-Container für Plattform-
/// und Cloud-Dienste. Singleton, kein State. Optional-Services (Analytics, RemoteConfig,
/// PlayGames, Review, CloudSave) sind nullable, weil sie nur auf Android/iOS verfuegbar
/// sind und auf Desktop fehlen koennen.
/// </summary>
public sealed class PlatformFacade : IPlatformFacade
{
    public IAudioService Audio { get; }
    public INotificationService Notifications { get; }
    public ICloudSaveService? CloudSave { get; }
    public IAnalyticsService? Analytics { get; }
    public IRemoteConfigService? RemoteConfig { get; }
    public IPlayGamesService? PlayGames { get; }
    public IReviewService? Review { get; }

    public PlatformFacade(
        IAudioService audio,
        INotificationService notifications,
        ICloudSaveService? cloudSave = null,
        IAnalyticsService? analytics = null,
        IRemoteConfigService? remoteConfig = null,
        IPlayGamesService? playGames = null,
        IReviewService? review = null)
    {
        Audio = audio;
        Notifications = notifications;
        CloudSave = cloudSave;
        Analytics = analytics;
        RemoteConfig = remoteConfig;
        PlayGames = playGames;
        Review = review;
    }
}
