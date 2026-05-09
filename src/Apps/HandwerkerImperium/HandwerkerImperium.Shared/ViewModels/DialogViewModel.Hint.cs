using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// AAA-Audit P0 (DialogViewModel-Strukturschnitt): Kontextuelle Hints (Tooltip-Bubble / Dialog)
/// inkl. Hint-Verkettung im DismissHint-Command. Aus DialogViewModel.cs herausgezogen.
/// </summary>
public sealed partial class DialogViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // KONTEXTUELLER HINT PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isHintVisible;

    [ObservableProperty]
    private string _activeHintTitle = "";

    [ObservableProperty]
    private string _activeHintText = "";

    /// <summary>True wenn der Hint als zentrierter Dialog angezeigt wird (z.B. Welcome).</summary>
    [ObservableProperty]
    private bool _isHintDialog;

    /// <summary>True wenn Tooltip-Bubble oben positioniert ist (HintPosition.Below = Bubble zeigt von oben nach unten).</summary>
    [ObservableProperty]
    private bool _isHintTooltipAbove;

    /// <summary>True wenn Tooltip-Bubble unten positioniert ist (HintPosition.Above = Bubble zeigt von unten nach oben).</summary>
    [ObservableProperty]
    private bool _isHintTooltipBelow;

    [ObservableProperty]
    private string _hintDismissButtonText = "Verstanden";

    // ═══════════════════════════════════════════════════════════════════════
    // KONTEXTUELLER HINT COMMANDS + Hint-Verkettung
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissHint()
    {
        var currentHintId = _contextualHintService.ActiveHint?.Id;
        _contextualHintService.DismissHint();

        // ONB-3: Erweiterte Hint-Verkettung fuer gefuehrten Einstieg.
        // Welcome -> FirstWorkshop -> AcceptOrder -> WorkerUnlock (Lv3) -> QuickJobs (Lv5) -> CraftingHint (Lv8).
        if (currentHintId == ContextualHints.Welcome.Id)
        {
            _contextualHintService.TryShowHint(ContextualHints.FirstWorkshop);
        }
        else if (currentHintId == ContextualHints.FirstWorkshop.Id)
        {
            _contextualHintService.TryShowHint(ContextualHints.AcceptOrder);
        }
        else if (currentHintId == ContextualHints.AcceptOrder.Id)
        {
            _contextualHintService.TryShowHint(ContextualHints.WorkerUnlock);
        }
        else if (currentHintId == ContextualHints.WorkerUnlock.Id)
        {
            _contextualHintService.TryShowHint(ContextualHints.QuickJobs);
        }
        else if (currentHintId == ContextualHints.QuickJobs.Id)
        {
            _contextualHintService.TryShowHint(ContextualHints.CraftingHint);
        }

        DeferredDialogCheckRequested?.Invoke();
    }

    /// <summary>
    /// Reagiert auf HintChanged-Event vom ContextualHintService.
    /// Aktualisiert die UI-Properties fuer die Tooltip-Bubble / den Dialog.
    /// </summary>
    private void OnHintChanged(object? sender, ContextualHint? hint)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (hint == null)
            {
                IsHintVisible = false;
                IsHintDialog = false;
                IsHintTooltipAbove = false;
                IsHintTooltipBelow = false;
                return;
            }

            ActiveHintTitle = _localizationService.GetString(hint.TitleKey) ?? hint.TitleKey;
            ActiveHintText = _localizationService.GetString(hint.TextKey) ?? hint.TextKey;
            HintDismissButtonText = _localizationService.GetString("HintDismissButton") ?? "Got it";

            IsHintDialog = hint.IsDialog;
            IsHintTooltipAbove = !hint.IsDialog && hint.Position == HintPosition.Below;
            IsHintTooltipBelow = !hint.IsDialog && hint.Position == HintPosition.Above;

            IsHintVisible = true;
        });
    }
}
