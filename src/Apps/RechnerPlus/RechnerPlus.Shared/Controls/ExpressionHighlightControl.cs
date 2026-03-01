using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace RechnerPlus.Controls;

/// <summary>
/// TextBlock mit Syntax-Highlighting für Rechner-Ausdrücke.
/// Zahlen: TextPrimaryBrush, Operatoren: PrimaryBrush, Klammern: TextMutedBrush (halbtransparent).
/// Brush-Lookups werden gecacht und nur bei Theme-Wechsel invalidiert.
/// </summary>
public class ExpressionHighlightControl : TextBlock
{
    public static readonly StyledProperty<string> ExpressionProperty =
        AvaloniaProperty.Register<ExpressionHighlightControl, string>(nameof(Expression), "");

    public string Expression
    {
        get => GetValue(ExpressionProperty);
        set => SetValue(ExpressionProperty, value);
    }

    // Brush-Cache: Lookups werden nur einmal pro Theme durchgeführt
    private IBrush? _cachedPrimary;
    private IBrush? _cachedText;
    private IBrush? _cachedMuted;

    static ExpressionHighlightControl()
    {
        ExpressionProperty.Changed.AddClassHandler<ExpressionHighlightControl>(
            (ctrl, _) => ctrl.RebuildInlines());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Theme-Wechsel-Event abonnieren um Brush-Cache zu invalidieren
        ActualThemeVariantChanged += OnThemeVariantChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeVariantChanged;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        // Theme gewechselt → Brush-Cache invalidieren
        _cachedPrimary = null;
        _cachedText = null;
        _cachedMuted = null;
        RebuildInlines();
    }

    /// <summary>Füllt den Brush-Cache wenn noch nicht geschehen.</summary>
    private void EnsureBrushes()
    {
        _cachedPrimary ??= GetBrush("PrimaryBrush") ?? Brushes.CornflowerBlue;
        _cachedText ??= GetBrush("TextPrimaryBrush") ?? Brushes.White;
        _cachedMuted ??= GetBrush("TextMutedBrush") ?? Brushes.Gray;
    }

    private void RebuildInlines()
    {
        Inlines?.Clear();
        var expr = Expression;
        if (string.IsNullOrEmpty(expr)) return;

        // Gecachte Brushes verwenden (kein GetBrush()-Aufruf pro Rebuild)
        EnsureBrushes();
        var primaryBrush = _cachedPrimary!;
        var textBrush = _cachedText!;
        var mutedBrush = _cachedMuted!;

        // Nur bei kurzen Ausdrücken highlighten (Performance)
        if (expr.Length > 50)
        {
            Inlines ??= new InlineCollection();
            Inlines.Add(new Run { Text = expr, Foreground = mutedBrush });
            return;
        }

        Inlines ??= new InlineCollection();
        var current = new System.Text.StringBuilder();
        TokenType currentType = TokenType.Number;

        foreach (char c in expr)
        {
            var type = ClassifyChar(c);
            if (type != currentType && current.Length > 0)
            {
                AddRun(current.ToString(), currentType, primaryBrush, textBrush, mutedBrush);
                current.Clear();
            }
            current.Append(c);
            currentType = type;
        }

        if (current.Length > 0)
            AddRun(current.ToString(), currentType, primaryBrush, textBrush, mutedBrush);
    }

    private void AddRun(string text, TokenType type, IBrush primary, IBrush textColor, IBrush muted)
    {
        var brush = type switch
        {
            TokenType.Operator => primary,
            TokenType.Parenthesis => muted,
            _ => muted // Zahlen in Expression-Zeile auch in Muted
        };

        var run = new Run { Text = text, Foreground = brush };
        if (type == TokenType.Parenthesis)
            run.FontWeight = FontWeight.Normal;
        else if (type == TokenType.Operator)
            run.FontWeight = FontWeight.Bold;

        Inlines!.Add(run);
    }

    private static TokenType ClassifyChar(char c) => c switch
    {
        '+' or '-' or '\u2212' or '*' or '\u00D7' or '/' or '\u00F7' or '^' => TokenType.Operator,
        '(' or ')' => TokenType.Parenthesis,
        _ => TokenType.Number
    };

    private IBrush? GetBrush(string key)
    {
        if (Application.Current?.TryGetResource(key, ActualThemeVariant, out var resource) == true)
            return resource as IBrush;
        return null;
    }

    private enum TokenType { Number, Operator, Parenthesis }
}
