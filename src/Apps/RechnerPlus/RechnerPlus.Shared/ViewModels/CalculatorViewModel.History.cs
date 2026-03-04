using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using MeineApps.CalcLib;

namespace RechnerPlus.ViewModels;

/// <summary>
/// History-Verwaltung, Undo/Redo, Memory-Funktionen, Clipboard-Commands.
/// </summary>
public sealed partial class CalculatorViewModel
{
    #region Undo/Redo

    public bool CanUndo => _undoList.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Erstellt einen Snapshot des aktuellen Rechner-Zustands.</summary>
    private CalculatorState CreateCurrentState() => new(
        Display, Expression, _isNewCalculation,
        ActiveOperator, _lastOperator, _lastOperand,
        HasError, ErrorMessage, PreviewResult, _lastResult);

    /// <summary>Speichert den aktuellen Zustand auf den Undo-Stack (LinkedList, O(1)).</summary>
    private void SaveState()
    {
        // Ältesten Eintrag entfernen wenn Limit erreicht (O(1) statt Array-Umkopieren)
        if (_undoList.Count >= MaxUndoStates)
            _undoList.RemoveFirst();

        _undoList.AddLast(CreateCurrentState());

        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>Stellt einen gespeicherten Zustand wieder her.</summary>
    private void RestoreState(CalculatorState state)
    {
        Display = state.Display;
        Expression = state.Expression;
        _isNewCalculation = state.IsNewCalculation;
        ActiveOperator = state.ActiveOperator;
        _lastOperator = state.LastOperator;
        _lastOperand = state.LastOperand;
        HasError = state.HasError;
        ErrorMessage = state.ErrorMessage;
        PreviewResult = state.PreviewResult;
        _lastResult = state.LastResult;
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undoList.Count == 0) return;

        // Aktuellen Zustand auf Redo-Stack sichern
        _redoStack.Push(CreateCurrentState());

        // Letzten Eintrag aus LinkedList holen und entfernen (neuester = letzter Knoten)
        var state = _undoList.Last!.Value;
        _undoList.RemoveLast();
        RestoreState(state);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        _haptic.Tick();
    }

    [RelayCommand]
    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        // Aktuellen Zustand auf Undo-Liste sichern
        if (_undoList.Count >= MaxUndoStates)
            _undoList.RemoveFirst();
        _undoList.AddLast(CreateCurrentState());

        RestoreState(_redoStack.Pop());
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        _haptic.Tick();
    }

    #endregion

    #region History Commands

    [RelayCommand]
    private void ShowHistory() => IsHistoryVisible = true;

    [RelayCommand]
    private void HideHistory() => IsHistoryVisible = false;

    [RelayCommand]
    private void ClearHistory()
    {
        ShowClearHistoryConfirm = true;
    }

    [RelayCommand]
    private void ConfirmClearHistory()
    {
        ShowClearHistoryConfirm = false;
        _historyService.Clear();
    }

    [RelayCommand]
    private void CancelClearHistory()
    {
        ShowClearHistoryConfirm = false;
    }

    [RelayCommand]
    private void DeleteHistoryEntry(CalculationHistoryEntry entry)
    {
        _historyService.DeleteEntry(entry);
    }

    [RelayCommand]
    private async Task CopyDisplay()
    {
        if (HasError) return;
        if (ClipboardCopyRequested != null)
            await ClipboardCopyRequested.Invoke(Display);
        FloatingTextRequested?.Invoke(_localization.GetString("CopySuccess") ?? "Copied!", "info");
        CopyFeedbackRequested?.Invoke(this, EventArgs.Empty);
        _haptic.Tick();
    }

    [RelayCommand]
    private async Task ShareDisplay()
    {
        if (HasError) return;
        string shareText;
        if (string.IsNullOrEmpty(Expression))
        {
            // Kein laufender Ausdruck → nur Display teilen
            shareText = Display;
        }
        else if (_isNewCalculation)
        {
            // Gerade berechnet → Expression ohne Display (Display hat Ergebnis)
            shareText = $"{Expression.TrimEnd()} = {Display}";
        }
        else
        {
            // Mitte der Eingabe → Expression + aktuellen Wert
            shareText = $"{Expression.TrimEnd()} {Display}";
        }
        if (ShareRequested != null)
            await ShareRequested.Invoke(shareText);
        else if (ClipboardCopyRequested != null)
        {
            // Fallback: In Zwischenablage kopieren (Desktop)
            await ClipboardCopyRequested.Invoke(shareText);
            FloatingTextRequested?.Invoke(_localization.GetString("CopySuccess") ?? "Copied!", "info");
        }
        _haptic.Tick();
    }

    [RelayCommand]
    private async Task PasteFromClipboard()
    {
        if (ClipboardPasteRequested != null)
            await ClipboardPasteRequested.Invoke();
    }

    /// <summary>Tap auf History-Eintrag: Ergebnis ins Display übernehmen.</summary>
    [RelayCommand]
    private void SelectHistoryEntry(CalculationHistoryEntry entry)
    {
        // ResultValue statt Result verwenden → korrekt nach Locale-Wechsel
        Display = FormatResult(entry.ResultValue);
        Expression = "";
        _isNewCalculation = true;
        IsHistoryVisible = false;
        ActiveOperator = null;
        PreviewResult = "";
        ClearError();
    }

    /// <summary>Long-Press auf History-Eintrag: Expression in Zwischenablage kopieren.</summary>
    [RelayCommand]
    private async Task CopyHistoryExpression(CalculationHistoryEntry entry)
    {
        if (ClipboardCopyRequested != null)
            await ClipboardCopyRequested.Invoke($"{entry.Expression} = {entry.Result}");
        FloatingTextRequested?.Invoke(_localization.GetString("CopySuccess") ?? "Copied!", "info");
        _haptic.Tick();
    }

    #endregion

    #region Verlauf-Persistenz

    private void LoadHistory()
    {
        _isLoading = true;
        try
        {
            var json = _preferences.Get<string>(HistoryKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var entries = JsonSerializer.Deserialize<List<CalculationHistoryEntry>>(json);
                if (entries is { Count: > 0 })
                {
                    _historyService.LoadEntries(entries);
                }
            }
        }
        catch
        {
            // Beschädigten Verlauf ignorieren
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_historyService.History);
            _preferences.Set(HistoryKey, json);
        }
        catch
        {
            // Speicherfehler ignorieren
        }
    }

    #endregion

    #region Memory-Persistenz

    private void LoadMemory()
    {
        var hasMemory = _preferences.Get(MemoryHasKey, false);
        if (hasMemory)
        {
            _memory = _preferences.Get(MemoryKey, 0.0);
            _hasMemory = true;
        }
    }

    private void SaveMemory()
    {
        _preferences.Set(MemoryKey, Memory);
        _preferences.Set(MemoryHasKey, HasMemory);
    }

    #endregion

    #region Memory Commands

    [RelayCommand]
    private void MemoryClear()
    {
        Memory = 0;
        HasMemory = false;
        OnPropertyChanged(nameof(MemoryDisplay));
        SaveMemory();
        _haptic.Click();
    }

    [RelayCommand]
    private void MemoryRecall()
    {
        if (HasMemory)
        {
            Display = FormatResult(Memory);
            _isNewCalculation = true;
            _haptic.Click();
        }
    }

    [RelayCommand]
    private void MemoryAdd()
    {
        if (TryParseDisplay(out var value))
        {
            Memory += value;
            HasMemory = true;
            OnPropertyChanged(nameof(MemoryDisplay));
            SaveMemory();
            _haptic.Click();
        }
    }

    [RelayCommand]
    private void MemorySubtract()
    {
        if (TryParseDisplay(out var value))
        {
            Memory -= value;
            HasMemory = true;
            OnPropertyChanged(nameof(MemoryDisplay));
            SaveMemory();
            _haptic.Click();
        }
    }

    [RelayCommand]
    private void MemoryStore()
    {
        if (TryParseDisplay(out var value))
        {
            Memory = value;
            HasMemory = true;
            OnPropertyChanged(nameof(MemoryDisplay));
            SaveMemory();
            _haptic.Click();
        }
    }

    #endregion
}
