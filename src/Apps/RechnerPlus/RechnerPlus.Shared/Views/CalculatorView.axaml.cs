using Avalonia.Controls;
using Avalonia.Input;
using RechnerPlus.ViewModels;

namespace RechnerPlus.Views;

public partial class CalculatorView : UserControl
{
    public CalculatorView()
    {
        InitializeComponent();
        Focusable = true;
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is CalculatorViewModel vm)
        {
            vm.ClipboardCopyRequested += OnClipboardCopy;
            vm.ClipboardPasteRequested += OnClipboardPaste;
        }
    }

    private async Task OnClipboardCopy(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(text);
    }

    private async Task OnClipboardPaste()
    {
        if (DataContext is not CalculatorViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null) return;
#pragma warning disable CS0618 // GetTextAsync ist veraltet, TryGetTextAsync braucht IAsyncDataTransfer
        var text = await topLevel.Clipboard.GetTextAsync();
#pragma warning restore CS0618
        vm.PasteValue(text);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CalculatorViewModel vm) return;

        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0:
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    vm.InputDigitCommand.Execute("0");
                else
                    vm.InputParenthesisCommand.Execute(")"); // Shift+0 = )
                break;
            case Key.D1 or Key.NumPad1: vm.InputDigitCommand.Execute("1"); break;
            case Key.D2 or Key.NumPad2: vm.InputDigitCommand.Execute("2"); break;
            case Key.D3 or Key.NumPad3: vm.InputDigitCommand.Execute("3"); break;
            case Key.D4 or Key.NumPad4: vm.InputDigitCommand.Execute("4"); break;
            case Key.D5 or Key.NumPad5: vm.InputDigitCommand.Execute("5"); break;
            case Key.D6 or Key.NumPad6: vm.InputDigitCommand.Execute("6"); break;
            case Key.D7 or Key.NumPad7: vm.InputDigitCommand.Execute("7"); break;
            case Key.D8 or Key.NumPad8:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    vm.InputOperatorCommand.Execute("\u00d7");
                else
                    vm.InputDigitCommand.Execute("8");
                break;
            case Key.D9 or Key.NumPad9:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    vm.InputParenthesisCommand.Execute("("); // Shift+9 = (
                else
                    vm.InputDigitCommand.Execute("9");
                break;
            case Key.C:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    vm.CopyDisplayCommand.Execute(null);
                else return;
                break;
            case Key.V:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    vm.PasteFromClipboardCommand.Execute(null);
                else return;
                break;
            case Key.Add: vm.InputOperatorCommand.Execute("+"); break;
            case Key.Subtract: vm.InputOperatorCommand.Execute("\u2212"); break;
            case Key.Multiply: vm.InputOperatorCommand.Execute("\u00d7"); break;
            case Key.Divide: vm.InputOperatorCommand.Execute("\u00f7"); break;
            case Key.OemPlus:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    vm.InputOperatorCommand.Execute("+");
                else
                    vm.CalculateCommand.Execute(null);
                break;
            case Key.OemMinus: vm.InputOperatorCommand.Execute("\u2212"); break;
            case Key.Enter: vm.CalculateCommand.Execute(null); break;
            case Key.Back: vm.BackspaceCommand.Execute(null); break;
            case Key.Delete: vm.ClearEntryCommand.Execute(null); break;
            case Key.Escape: vm.ClearCommand.Execute(null); break;
            case Key.OemPeriod or Key.Decimal: vm.InputDecimalCommand.Execute(null); break;
            case Key.OemComma: vm.InputDecimalCommand.Execute(null); break;
            case Key.Oem2: vm.InputOperatorCommand.Execute("\u00f7"); break; // / key
            default: return;
        }

        e.Handled = true;
    }
}
