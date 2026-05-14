using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer das Spotlight-Overlay-Control.
/// Subscribed auf <see cref="IFtueService.CurrentStepChanged"/> und exponiert
/// die UI-Properties (Title/Text/CanSkip + IsVisible).
///
/// Spotlight-Coordinates (X/Y/Radius in dp) werden NICHT vom ViewModel berechnet —
/// die View misst beim Layout-Pass die Bounds des Elements mit der
/// <see cref="FtueStep.SpotlightAutomationId"/> und setzt sie via
/// <see cref="SetSpotlightBounds"/>.
/// </summary>
public sealed partial class FtueOverlayViewModel : ViewModelBase
{
    private readonly IFtueService _ftueService;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private bool _canSkip;

    /// <summary>X-Center des Spotlights in dp. Negativ = kein Spotlight (Welcome-Step).</summary>
    [ObservableProperty]
    private float _spotlightX = -1f;

    /// <summary>Y-Center des Spotlights in dp. Negativ = kein Spotlight.</summary>
    [ObservableProperty]
    private float _spotlightY = -1f;

    /// <summary>Spotlight-Radius in dp.</summary>
    [ObservableProperty]
    private float _spotlightRadius = Graphics.FtueSpotlightRenderer.DefaultRadiusDp;

    /// <summary>SpotlightAutomationId des aktuellen Schritts — die View nutzt sie zur Element-Suche.</summary>
    [ObservableProperty]
    private string? _spotlightAutomationId;

    /// <summary>True wenn der aktuelle Step nur "TapContinue" erfordert (Bubble-Button sichtbar).</summary>
    [ObservableProperty]
    private bool _showContinueButton;

    public FtueOverlayViewModel(IFtueService ftueService, ILocalizationService localization)
    {
        _ftueService = ftueService;
        _localization = localization;
        _ftueService.CurrentStepChanged += OnCurrentStepChanged;
        _ftueService.FtueFinished += OnFinished;

        // Initial-State falls FTUE bereits laeuft (z.B. nach App-Restart mitten im Tutorial).
        if (_ftueService.IsActive && _ftueService.CurrentStep != null)
            ApplyStep(_ftueService.CurrentStep);
    }

    private void OnCurrentStepChanged(object? sender, FtueStep? step)
    {
        if (step == null)
        {
            IsVisible = false;
            return;
        }
        ApplyStep(step);
    }

    private void OnFinished(object? sender, EventArgs e)
    {
        IsVisible = false;
    }

    private void ApplyStep(FtueStep step)
    {
        Title = _localization.GetString(step.TitleKey) ?? step.TitleKey;
        Text = _localization.GetString(step.TextKey) ?? step.TextKey;
        CanSkip = step.CanSkip;
        SpotlightAutomationId = step.SpotlightAutomationId;
        ShowContinueButton = step.ExpectedAction == FtueExpectedAction.TapContinue;

        // Wenn kein Spotlight-Target → komplett-overlay (Welcome-Step).
        if (string.IsNullOrEmpty(step.SpotlightAutomationId))
        {
            SpotlightX = -1f;
            SpotlightY = -1f;
        }
        // Sonst: View misst die Bounds und ruft SetSpotlightBounds() auf.
        IsVisible = true;
    }

    /// <summary>
    /// View misst die Bounds des Spotlight-Targets via AutomationId und setzt sie hier.
    /// Wird im FtueOverlayControl-LayoutUpdated-Handler gerufen.
    /// </summary>
    public void SetSpotlightBounds(float centerXDp, float centerYDp, float radiusDp)
    {
        SpotlightX = centerXDp;
        SpotlightY = centerYDp;
        SpotlightRadius = radiusDp;
    }

    [RelayCommand]
    private void Continue()
    {
        _ftueService.CompleteCurrentStep();
    }

    [RelayCommand]
    private void Skip()
    {
        _ftueService.SkipAll();
    }
}
