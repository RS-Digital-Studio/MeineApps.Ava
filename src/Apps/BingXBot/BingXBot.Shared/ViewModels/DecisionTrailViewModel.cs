using System.Collections.ObjectModel;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Diagnostics;
using BingXBot.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;

namespace BingXBot.ViewModels;

/// <summary>
/// v1.6.0 Phase 10A — DecisionTrailViewModel.
/// Subscribt auf <see cref="IBotEventStream.EvaluationDecided"/> fuer Live-Push,
/// haelt einen Ringpuffer von max <see cref="MaxItems"/> (500) Decisions im UI,
/// bietet Filter nach Symbol/TF/RejectionReason/OnlyRejected.
/// </summary>
public partial class DecisionTrailViewModel : ViewModelBase, IDisposable
{
    public const int MaxItems = 500;

    private readonly IBotEventStream? _stream;
    private bool _disposed;

    /// <summary>Alle bisher empfangenen Decisions (chronologisch absteigend).</summary>
    public ObservableCollection<EvaluationDecisionDto> Decisions { get; } = new();

    /// <summary>Gefiltert nach Symbol/TF/Reason/OnlyRejected — wird im View gebunden.</summary>
    public ObservableCollection<EvaluationDecisionDto> FilteredDecisions { get; } = new();

    [ObservableProperty] private string? _selectedSymbol;
    [ObservableProperty] private TimeFrame? _selectedTf;
    [ObservableProperty] private string? _selectedRejectionReason;
    [ObservableProperty] private bool _onlyRejected;
    [ObservableProperty] private int _displayedCount;

    /// <summary>Verfuegbare RejectionReason-Konstanten fuer den Filter-Picker.</summary>
    public IReadOnlyList<string> AvailableReasons { get; } = new[]
    {
        RejectionReasons.NewsBlackout,
        RejectionReasons.StateNotActivated,
        RejectionReasons.ImpulseBelowAtr,
        RejectionReasons.NoHtfConfluence,
        RejectionReasons.ScoreBelowMin,
        RejectionReasons.RrrTooSmall,
        RejectionReasons.BoxCloseViolated,
        RejectionReasons.MissingWickRejection,
        RejectionReasons.MtaTargetZoneBlock,
        RejectionReasons.EntriesAlreadyTriggered,
        RejectionReasons.MissingStrukturpunkte,
        RejectionReasons.CounterTrendInactive,
        RejectionReasons.SlippageTooHigh,
        RejectionReasons.TfAutoDisabled,
        RejectionReasons.Other,
    };

    public DecisionTrailViewModel(IBotEventStream? stream = null)
    {
        _stream = stream;
        if (_stream != null)
            _stream.EvaluationDecided += OnEvaluationDecided;
    }

    private void OnEvaluationDecided(EvaluationDecisionDto dto)
    {
        DispatchToUi(() =>
        {
            // Neuer Eintrag oben einfuegen — Ringpuffer-Trim auf MaxItems.
            Decisions.Insert(0, dto);
            while (Decisions.Count > MaxItems)
                Decisions.RemoveAt(Decisions.Count - 1);
            ApplyFilter();
        });
    }

    /// <summary>Manueller Refresh-Trigger fuer Tests + UI-Button.</summary>
    [RelayCommand]
    private void RefreshFilter() => ApplyFilter();

    partial void OnSelectedSymbolChanged(string? value) => ApplyFilter();
    partial void OnSelectedTfChanged(TimeFrame? value) => ApplyFilter();
    partial void OnSelectedRejectionReasonChanged(string? value) => ApplyFilter();
    partial void OnOnlyRejectedChanged(bool value) => ApplyFilter();

    /// <summary>Sammelt initiale Decisions aus dem REST-Endpoint (UI-Init).</summary>
    public void LoadInitial(IEnumerable<EvaluationDecisionDto> initialBatch)
    {
        DispatchToUi(() =>
        {
            Decisions.Clear();
            foreach (var d in initialBatch.OrderByDescending(d => d.UtcTimestamp).Take(MaxItems))
                Decisions.Add(d);
            ApplyFilter();
        });
    }

    /// <summary>
    /// Marshalt Aktionen auf den UI-Thread wenn Avalonia laeuft, sonst direkt synchron
    /// (relevant fuer Unit-Tests ohne Avalonia-Application-Kontext).
    /// </summary>
    private static void DispatchToUi(Action action)
    {
        try
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                action();
            else
                Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
        catch
        {
            // Dispatcher nicht initialisiert (Test-Umgebung) → synchron ausfuehren.
            action();
        }
    }

    private void ApplyFilter()
    {
        FilteredDecisions.Clear();
        IEnumerable<EvaluationDecisionDto> q = Decisions;
        if (!string.IsNullOrWhiteSpace(SelectedSymbol))
            q = q.Where(d => d.Symbol == SelectedSymbol);
        if (SelectedTf.HasValue)
        {
            var tfInt = (int)SelectedTf.Value;
            q = q.Where(d => d.Tf == tfInt);
        }
        if (!string.IsNullOrEmpty(SelectedRejectionReason))
            q = q.Where(d => d.RejectionReason == SelectedRejectionReason);
        if (OnlyRejected)
            q = q.Where(d => !d.Triggered);
        foreach (var d in q) FilteredDecisions.Add(d);
        DisplayedCount = FilteredDecisions.Count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_stream != null)
            _stream.EvaluationDecided -= OnEvaluationDecided;
    }
}
