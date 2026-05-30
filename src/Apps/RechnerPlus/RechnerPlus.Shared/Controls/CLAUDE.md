# Controls — App-eigene Custom Controls

Custom Controls, die nur RechnerPlus betreffen. Geteilte Controls → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

| Datei | Zweck |
|-------|-------|
| `ExpressionHighlightControl.cs` | Syntax-Highlighting des Ausdrucks: Zahlen `TextPrimary`, Operatoren `Primary`+Bold, Klammern `Muted`. Brushes (`_cachedPrimary/_cachedText/_cachedMuted`) gecacht, invalidiert bei `ActualThemeVariantChanged`. |
