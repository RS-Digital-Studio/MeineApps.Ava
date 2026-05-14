using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>
/// Thin-Wrapper-VM fuer <c>GuildWarView</c> (Kriegs-Detail, ViewLocator-Mapping, 17.04.2026).
/// Nicht zu verwechseln mit <see cref="GuildWarSeasonViewModel"/> (Saison-Uebersicht).
/// </summary>
public sealed class GuildWarViewModel : ViewModelBase
{
    public GuildViewModel Guild { get; }
    public GuildWarViewModel(GuildViewModel guild) { Guild = guild; }
}
