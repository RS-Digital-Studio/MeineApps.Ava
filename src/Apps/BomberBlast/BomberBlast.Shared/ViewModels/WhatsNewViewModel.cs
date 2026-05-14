using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using BomberBlast.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer das What's-New-Modal (.3 .
/// Wird einmal nach App-Update angezeigt — zeigt die wichtigsten Aenderungen
/// gegenueber der vorherigen Installation. "Spaeter" + "Verstanden" Buttons.
/// </summary>
public sealed partial class WhatsNewViewModel : ViewModelBase
{
    private readonly IWhatsNewService _whatsNew;
    private readonly ILocalizationService _localization;

    public WhatsNewViewModel(IWhatsNewService whatsNew, ILocalizationService localization)
    {
        _whatsNew = whatsNew;
        _localization = localization;
        _versionTitle = $"v{whatsNew.CurrentVersion}";
        _laterText = localization.GetString("WhatsNewLater") ?? "Later";
        _understoodText = localization.GetString("WhatsNewUnderstood") ?? "Got it!";
        ReloadEntries();
    }

    /// <summary>"v2.0.56" formatiert.</summary>
    [ObservableProperty] private string _versionTitle = "";

    /// <summary>Lokalisierter Text "Spaeter".</summary>
    [ObservableProperty] private string _laterText = "Later";
    /// <summary>Lokalisierter Text "Verstanden".</summary>
    [ObservableProperty] private string _understoodText = "Got it!";

    /// <summary>Whats-New-Eintraege fuer Binding.</summary>
    public ObservableCollection<WhatsNewEntryItem> Entries { get; } = new();

    /// <summary>Wird gefeuert wenn User "Verstanden" oder "Spaeter" tippt.</summary>
    public event Action? Closed;

    private void ReloadEntries()
    {
        Entries.Clear();
        foreach (var e in _whatsNew.GetEntries())
        {
            Entries.Add(new WhatsNewEntryItem
            {
                Title = e.Title,
                Bullets = new ObservableCollection<string>(e.Bullets),
            });
        }
    }

    [RelayCommand]
    private void Understood()
    {
        _whatsNew.MarkSeen();
        Closed?.Invoke();
    }

    [RelayCommand]
    private void Later()
    {
        // "Spaeter" markiert NICHT als gesehen — beim naechsten App-Start wird das Modal erneut gezeigt.
        Closed?.Invoke();
    }
}

/// <summary>Darstellungs-Modell fuer Whats-New-Eintraege im Modal.</summary>
public sealed class WhatsNewEntryItem
{
    public string Title { get; init; } = "";
    public ObservableCollection<string> Bullets { get; init; } = new();
}
