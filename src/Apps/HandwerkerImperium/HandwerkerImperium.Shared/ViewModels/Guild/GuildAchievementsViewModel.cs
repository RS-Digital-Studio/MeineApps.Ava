using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>Thin-Wrapper-VM fuer <c>GuildAchievementsView</c> (ViewLocator-Mapping, 17.04.2026).</summary>
public sealed class GuildAchievementsViewModel : ViewModelBase
{
    public GuildViewModel Guild { get; }
    public GuildAchievementsViewModel(GuildViewModel guild) { Guild = guild; }
}
