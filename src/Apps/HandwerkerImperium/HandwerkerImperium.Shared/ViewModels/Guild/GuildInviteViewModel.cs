using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>Thin-Wrapper-VM fuer <c>GuildInviteView</c> (ViewLocator-Mapping, Phase 4 17.04.2026).</summary>
public sealed class GuildInviteViewModel : ViewModelBase
{
    public GuildViewModel Guild { get; }
    public GuildInviteViewModel(GuildViewModel guild) { Guild = guild; }
}
