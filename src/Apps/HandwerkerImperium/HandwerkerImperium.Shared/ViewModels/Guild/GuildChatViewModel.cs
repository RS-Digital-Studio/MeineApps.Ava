using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>Thin-Wrapper-VM fuer <c>GuildChatView</c> (ViewLocator-Mapping, 17.04.2026).</summary>
public sealed class GuildChatViewModel : ViewModelBase
{
    public GuildViewModel Guild { get; }
    public GuildChatViewModel(GuildViewModel guild) { Guild = guild; }
}
