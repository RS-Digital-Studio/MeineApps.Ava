using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// MiniGame-Navigation + Abbruch-Bestaetigung (Schritt 3 aus velvety-booping-peacock-Plan).
/// Die Route-Tabelle und alle verknuepften Helfer leben hier statt in MainViewModel.Navigation.
/// </summary>
public sealed class MiniGameNavigator : IMiniGameNavigator
{
    /// <summary>Statische Route-Map (Allokations-frei).</summary>
    private static readonly Dictionary<string, ActivePage> s_miniGameRoutes = new()
    {
        ["minigame/sawing"] = ActivePage.SawingGame,
        ["minigame/pipes"] = ActivePage.PipePuzzle,
        ["minigame/wiring"] = ActivePage.WiringGame,
        ["minigame/painting"] = ActivePage.PaintingGame,
        ["minigame/rooftiling"] = ActivePage.RoofTilingGame,
        ["minigame/blueprint"] = ActivePage.BlueprintGame,
        ["minigame/designpuzzle"] = ActivePage.DesignPuzzleGame,
        ["minigame/inspection"] = ActivePage.InspectionGame,
        ["minigame/forge"] = ActivePage.ForgeGame,
        ["minigame/invent"] = ActivePage.InventGame,
    };

    private readonly ILocalizationService _localization;
    private INavigationHost? _host;

    public MiniGameNavigator(ILocalizationService localization)
    {
        _localization = localization;
    }

    public void AttachHost(INavigationHost host) => _host = host;

    public bool TryResolveRoute(string routePart, out ActivePage page)
        => s_miniGameRoutes.TryGetValue(routePart, out page);

    public void NavigateToMiniGame(string routePart, string orderId)
    {
        if (_host == null) return;
        if (!s_miniGameRoutes.TryGetValue(routePart, out var page)) return;
        _host.ActivePage = page;
        _host.ActiveMiniGameViewModel?.SetOrderId(orderId);
    }

    public bool IsAnyMiniGamePlaying()
    {
        var vm = _host?.ActiveMiniGameViewModel;
        return vm != null && (vm.IsPlaying || vm.IsCountdownActive);
    }

    public async Task ConfirmMiniGameAbortAsync()
    {
        if (_host == null) return;
        var title = _localization.GetString("MiniGameAbortTitle") ?? "MiniGame abbrechen?";
        var msg = _localization.GetString("MiniGameAbortMessage") ?? "Dein Fortschritt geht verloren.";
        var confirm = _localization.GetString("MiniGameAbortConfirm") ?? "Abbrechen";
        var cancel = _localization.GetString("Cancel") ?? "Zurueck";
        bool confirmed = await _host.DialogVM.ShowConfirmDialog(title, msg, confirm, cancel);
        if (confirmed)
        {
            StopCurrent();
            _host.SelectDashboardTab();
        }
    }

    public void StopCurrent()
    {
        _host?.ActiveMiniGameViewModel?.StopGame();
    }
}
