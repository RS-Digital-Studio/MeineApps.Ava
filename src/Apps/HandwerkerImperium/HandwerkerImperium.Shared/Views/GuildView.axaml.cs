using Avalonia.Controls;
using Avalonia.Interactivity;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.ViewModels;

namespace HandwerkerImperium.Views;

/// <summary>
/// Multiplayer-Gilden: Firebase-basiert, echte Spieler.
/// Lädt Daten beim Sichtbar-Werden.
/// </summary>
public partial class GuildView : UserControl
{
    private bool _hasLoaded;

    public GuildView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Gilden-Daten laden wenn der Tab zum ersten Mal sichtbar wird
        if (!_hasLoaded && DataContext is GuildViewModel vm)
        {
            _hasLoaded = true;
            vm.LoadGuildDataCommand.ExecuteAsync(null).SafeFireAndForget();
        }
    }
}
