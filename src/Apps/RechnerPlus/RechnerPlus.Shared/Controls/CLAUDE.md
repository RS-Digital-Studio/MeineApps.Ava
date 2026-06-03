# Controls — App-eigene Custom Controls

Custom Controls, die nur RechnerPlus betreffen. Geteilte Controls → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

| Datei | Zweck |
|-------|-------|
| `ExpressionHighlightControl.cs` | Syntax-Highlighting des Ausdrucks (erbt von `TextBlock`). Operatoren: `PrimaryBrush`+Bold, Klammern: `TextMutedBrush`, Zahlen: `TextMutedBrush`. Brushes (`_cachedPrimary`/`_cachedText`/`_cachedMuted`) gecacht, invalidiert bei `ActualThemeVariantChanged` (Attach/Detach-Pattern). Ausdrücke > 50 Zeichen werden ungeparst als einzelner Muted-Run ausgegeben (Performance-Grenze). |
