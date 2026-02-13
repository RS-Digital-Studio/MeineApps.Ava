using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;
using ZeitManager.ViewModels;

namespace ZeitManager.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private int _onboardingStep;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
            TryStartOnboarding();
        }
    }

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            "info" => Color.Parse("#3B82F6"),
            _ => Color.Parse("#3B82F6")
        };
        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;
        FloatingTextCanvas.ShowFloatingText(text, w * 0.3 + new Random().NextDouble() * w * 0.4, h * 0.35, color, 18);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }

    /// <summary>
    /// Startet das Onboarding wenn es noch nicht abgeschlossen wurde.
    /// </summary>
    private async void TryStartOnboarding()
    {
        try
        {
            var prefs = App.Services.GetRequiredService<IPreferencesService>();
            if (prefs.Get("onboarding_completed", false))
                return;

            // Kurz warten bis UI aufgebaut ist
            await Task.Delay(800);

            var localization = App.Services.GetRequiredService<ILocalizationService>();

            _onboardingStep = 0;
            OnboardingTooltip.Dismissed += OnTooltipDismissed;

            // Schritt 1: Quick-Timer Tipp (oben)
            OnboardingTooltip.Arrow = MeineApps.UI.Controls.ArrowPosition.Top;
            OnboardingTooltip.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            OnboardingTooltip.Margin = new Avalonia.Thickness(32, 80, 32, 0);
            OnboardingTooltip.Text = localization.GetString("OnboardingQuickTimer");
            OnboardingOverlay.IsVisible = true;
            OnboardingTooltip.Show();
        }
        catch
        {
            // Onboarding ist optional - bei Fehler einfach überspringen
        }
    }

    private void OnTooltipDismissed(object? sender, EventArgs e)
    {
        _onboardingStep++;

        if (_onboardingStep == 1)
        {
            // Schritt 2: Custom-Timer Tipp (unten)
            var localization = App.Services.GetRequiredService<ILocalizationService>();
            OnboardingTooltip.Arrow = MeineApps.UI.Controls.ArrowPosition.Bottom;
            OnboardingTooltip.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
            OnboardingTooltip.Margin = new Avalonia.Thickness(32, 0, 32, 100);
            OnboardingTooltip.Text = localization.GetString("OnboardingCreateTimer");

            // Kurz warten, dann zeigen
            DispatcherTimer.RunOnce(() => OnboardingTooltip.Show(), TimeSpan.FromMilliseconds(300));
        }
        else
        {
            // Onboarding abgeschlossen
            OnboardingOverlay.IsVisible = false;
            OnboardingTooltip.Dismissed -= OnTooltipDismissed;

            var prefs = App.Services.GetRequiredService<IPreferencesService>();
            prefs.Set("onboarding_completed", true);
        }
    }
}
