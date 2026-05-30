using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BomberBlast.Tests.Ui;

/// <summary>
/// Sub-ViewModel-Stellvertreter fuer das WhatsNewViewModel: ein Text + ein Command.
/// Spiegelt das Binding-Profil (Text-Property + RelayCommand) des echten Overlays.
/// </summary>
public sealed partial class InnerTestViewModel : ObservableObject
{
    [ObservableProperty] private string _buttonText = "Verstanden";

    public bool CommandExecuted { get; private set; }

    [RelayCommand]
    private void Press() => CommandExecuted = true;
}

/// <summary>
/// Aussen-ViewModel-Stellvertreter fuer das MainViewModel: haelt das Sub-VM unter einer
/// Property (analog MainViewModel.WhatsNewVm) + ein Sichtbarkeits-Flag (analog IsWhatsNewVisible).
/// </summary>
public sealed partial class OuterTestViewModel : ObservableObject
{
    public InnerTestViewModel Inner { get; } = new();

    [ObservableProperty] private bool _isModalVisible = true;
}
