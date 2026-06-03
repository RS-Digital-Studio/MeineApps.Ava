# Views — AXAML-Views & View-Converter

Alle Views sind `UserControl`-Ableitungen. DataContext kommt via Binding aus `MainViewModel`
(ViewModel-First) — kein Service-Locator, keine VM-Erzeugung im Code-Behind.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainView.axaml(.cs)` | Shell: Tab-Leiste, Verbindungs-Statusanzeige (grüner/roter Punkt), Fehler-Banner, ContentControl für Tab-Inhalt. |
| `DashboardView.axaml(.cs)` | Übersicht: Zonen-Cards mit Feuchtigkeitsbalken, Pumpen-Status, Wetteranzeige, Modus-Wechsel, Schnellaktionen. |
| `ZoneControlView.axaml(.cs)` | Manuelle Steuerung: Zone-Liste mit Start/Stopp-Buttons, Dauer-Auswahl, Notstopp. |
| `ScheduleView.axaml(.cs)` | Automatik: Zone-Konfiguration (Schwellenwert, Dauer, Cooldown), Zeitpläne, Modus-Umschalter. |
| `CalibrationView.axaml(.cs)` | Kalibrierung: Live-ADC-Werte, Trocken/Nass-Buttons pro Zone. Lädt Zonen per `OnLoaded`-Event. |
| `HistoryView.axaml(.cs)` | Verlauf: `MoistureChartControl` eingebettet, Zeitfilter-Buttons, Ereignis-Liste, Statistiken. |
| `SettingsView.axaml(.cs)` | Einstellungen: Server-URL-Eingabe, Verbindungstest, Server-Info. `ConnectedTextConverter` zeigt Verbindungsstatus als Text. |

## View-seitige Converter (Code-Behind als static Properties)

Converter leben als `FuncValueConverter<TIn, TOut>` im Code-Behind der View die sie braucht,
damit sie im XAML per `{x:Static local:XxxView.YyyConverter}` gebunden werden können.

### MainView

| Converter | Typ | Logik |
|-----------|-----|-------|
| `ConnectionBrushConverter` | `bool → IBrush` | Verbunden → `#66BB6A` Grün, getrennt → `#EF5350` Rot |
| `ConnectionBgConverter` | `bool → IBrush` | Verbunden → `#15FFFFFF` subtil, getrennt → `#30EF5350` rötlich |

### DashboardView

| Converter | Typ | Logik |
|-----------|-----|-------|
| `PumpBgConverter` | `bool → IBrush` | Aktiv → Blau-LinearGradient (`#1565C0`→`#42A5F5`), inaktiv → `#2A3A4E` |
| `PumpColorConverter` | `bool → IBrush` | Aktiv → `#42A5F5`, inaktiv → `#5A7A96` |
| `PumpTextConverter` | `bool → string` | Aktiv → "Pumpe aktiv", inaktiv → "Pumpe aus" |
| `StringToBrush` | `string → IBrush` | `#RRGGBB`-String → `SolidColorBrush`. Fehlertoleranter Parse. |
| `ModeButtonConverter` | `string → IBrush` | Aktuell immer `Transparent` (vereinfacht) |
| `ThresholdMarginConverter` | `int → Thickness` | Schwellenwert 0-100 → Left-Margin 0-300px für Markierungsbalken |
| `MoistureToGridLength` | `double → GridLength` | 0-100 % → `*`-GridLength für Feuchtigkeitsbalken |
| `MoistureToRestGridLength` | `double → GridLength` | 0-100 % → Rest-GridLength (100 − Wert) für Leerbalken |
| `PauseBgConverter` | `bool → IBrush` | Wetter-Pause aktiv → `#E65100` Orange, inaktiv → `#1B5E20` |

### ZoneControlView

| Converter | Typ | Logik |
|-----------|-----|-------|
| `WateringIconConverter` | `bool → MaterialIconKind` | Bewässert → `Stop`, nicht bewässert → `Play` |
| `WateringTextConverter` | `bool → string` | Bewässert → "Stopp", nicht bewässert → "Start" |

### SettingsView

| Converter | Typ | Logik |
|-----------|-----|-------|
| `ConnectedTextConverter` | `bool → string` | Verbunden → "Verbunden mit Server", getrennt → "Nicht verbunden" |

## Gotchas

- **OnLoaded-Pattern (CalibrationView, HistoryView, ScheduleView):** Rufen ihren jeweiligen
  Lade-Command (`LoadZonesCommand`, `LoadDataCommand`, `LoadConfigCommand`) erst beim ersten
  Sichtbarwerden auf — nicht im ViewModel-Konstruktor, weil die Serververbindung beim Start
  noch nicht steht. Das ist eine bewusste Ausnahme vom "kein Event-Handler im Code-Behind"-Prinzip.
  Das Event ist kein Geschäftslogik-Event, sondern ein reiner View-Lifecycle-Trigger.
- **Feuchtigkeitsbalken ohne ProgressBar:** Verwendet ein zweiteiliges Grid (`MoistureToGridLength` +
  `MoistureToRestGridLength`) statt ProgressBar, weil ProgressBar-Theming auf Android/Desktop
  inkonsistent ist.
