using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;

namespace HandwerkerRechner.Views;

/// <summary>
/// Basisklasse fuer alle 19 Calculator-Views im HandwerkerRechner.
/// Kapselt das gemeinsame Pattern: PropertyChanged-Subscription auf dem ViewModel
/// mit automatischer Ab-/Anmeldung bei DataContext-Wechsel.
///
/// Abgeleitete Klassen muessen:
/// 1. <see cref="ShouldInvalidateOnPropertyChanged"/> ueberschreiben (Filter-Logik)
/// 2. <see cref="OnResultPropertyChanged"/> ueberschreiben (Reaktion: Animation starten oder Canvas invalidieren)
/// </summary>
public abstract class CalculatorViewBase : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Alten Handler abmelden, um Memory-Leaks zu vermeiden
        if (_currentVm != null && _resultHandler != null)
            _currentVm.PropertyChanged -= _resultHandler;

        _currentVm = DataContext as INotifyPropertyChanged;
        if (_currentVm != null)
        {
            _resultHandler = (_, args) =>
            {
                if (ShouldInvalidateOnPropertyChanged(args.PropertyName))
                    OnResultPropertyChanged();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    /// <summary>
    /// Bestimmt ob eine PropertyChanged-Benachrichtigung die Visualisierung aktualisieren soll.
    /// Standard: true wenn der Property-Name "Result" enthaelt (fuer animierte Visualisierungen).
    /// Ueberschreiben fuer Views mit spezifischen Property-Filtern (z.B. HourlyRate, MaterialCompare, AreaMeasure).
    /// </summary>
    protected virtual bool ShouldInvalidateOnPropertyChanged(string? propertyName)
    {
        return propertyName?.Contains("Result") == true;
    }

    /// <summary>
    /// Wird aufgerufen wenn eine relevante Property sich geaendert hat.
    /// Hier die Visualisierung aktualisieren (z.B. StartAnimation() oder InvalidateSurface()).
    /// </summary>
    protected abstract void OnResultPropertyChanged();

    /// <summary>
    /// Hilfsmethode: Fordert bei laufender Animation einen weiteren Frame an.
    /// Verwendung im OnPaintVisualization-Handler wenn die Visualisierung NeedsRedraw meldet.
    /// </summary>
    protected static void RequestAnimationFrame(object? sender)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            (sender as SKCanvasView)?.InvalidateSurface(),
            Avalonia.Threading.DispatcherPriority.Render);
    }
}
