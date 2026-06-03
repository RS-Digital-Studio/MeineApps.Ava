# Converters — XAML-Wert-Konverter

App-spezifische `IValueConverter`-Implementierungen für BingXBot-XAML-Bindings.
Generische Converter (Bool→Visibility, etc.) kommen aus `MeineApps.Core.Ava`.

## Dateien

| Datei | Zweck |
|-------|-------|
| `NullableDecimalConverter.cs` | `decimal?` ↔ `string` für TextBox-Bindings (SL/TP-Felder). Leeres Feld → null. Kein Exponential-Format (Krypto-Preise wie 0.00005625 müssen lesbar bleiben). Komma und Punkt als Dezimaltrennzeichen akzeptiert. Ungültiger Input → `BindingNotification.Error` (roter Rahmen, kein Crash). |
| `StaleOpacityConverter.cs` | `bool` (IsStale) → `double` Opacity: true → 0.40, false → 1.0. Dimmt eine Anzeige visuell wenn der Scan-Watchdog Inaktivität meldet. |

## Verwendungs-Pattern

Beide Converter sind als `static readonly Instance`-Singletons verfügbar:

```xaml
<!-- In View-Resources oder direkt als StaticResource -->
<BingXBot:NullableDecimalConverter x:Key="DecimalConverter"/>
<!-- oder inline -->
Converter="{x:Static converters:NullableDecimalConverter.Instance}"
```

## Gotcha — Krypto-Dezimalformat

`NullableDecimalConverter` verwendet `"F20".TrimEnd('0')` statt `"G"` oder `"N2"`, weil
`ToString("G")` bei kleinen Werten Exponentialnotation (`5.625E-05`) produziert.
BingX-Preise für Memecoins können 8+ Nachkommastellen haben — Exponentialnotation ist für
Trader unlesbar und würde bei `ConvertBack` einen Parse-Fehler auslösen.
