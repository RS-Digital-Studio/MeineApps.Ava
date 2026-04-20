using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>Thin-Wrapper-VM fuer <c>GuildMembersView</c> (ViewLocator-Mapping, Phase 4 17.04.2026).</summary>
public sealed class GuildMembersViewModel : ViewModelBase
{
    public GuildViewModel Guild { get; }
    public GuildMembersViewModel(GuildViewModel guild) { Guild = guild; }
}
