using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels.Guild;

/// <summary>
/// Thin-Wrapper-ViewModel fuer <c>GuildResearchView</c> (ViewLocator-Mapping).
/// Alle Properties und Commands leben weiterhin im <see cref="GuildViewModel"/>;
/// diese Wrapper-Klasse existiert nur, damit der ViewLocator per Konvention
/// <c>ViewModels.Guild.GuildResearchViewModel → Views.Guild.GuildResearchView</c> auflösen kann.
/// AXAML bindet ueber <c>{Binding Guild.X}</c>.
/// </summary>
public sealed class GuildResearchViewModel : ViewModelBase
{
    public GuildViewModel Guild { get; }
    public GuildResearchViewModel(GuildViewModel guild) { Guild = guild; }
}
