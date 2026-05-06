# FinanzRechner — Optimierungs-Audit (gründlich)

**App-Version:** v2.0.7 · Geschlossener Test
**Stand:** 6. Mai 2026
**Umfang:** alle 22 ViewModels, 14 Services, 28 XAML-Views, 12 Skia-Renderer, App.axaml.cs, MainActivity, Loading-Pipeline, AppPalette, i18n.
**Methode:** Komplettes Lesen kritischer Dateien (1215-Zeilen-VM, 800-Zeilen-Service, 1046-Zeilen-View) plus parallele Sub-Audit-Agenten plus Verifikation aller Findings mit grep gegen den aktuellen Quellcode.

---

## Inhalt

1. [Executive Summary](#1-executive-summary)
2. [Findings nach Severity](#2-findings-nach-severity)
3. [Critical](#3-critical)
4. [High](#4-high)
5. [Medium](#5-medium)
6. [Low](#6-low)
7. [Positiv: was sauber gelöst ist](#7-positiv-was-sauber-gelöst-ist)
8. [Empfohlener Umsetzungs-Fahrplan](#8-empfohlener-umsetzungs-fahrplan)
9. [Anhang: Verworfene Behauptungen](#9-anhang-verworfene-behauptungen)

---

## 1. Executive Summary

FinanzRechner ist eine sauber strukturierte Avalonia-11-App mit DI, partiellen ViewModel-Klassen, Cache-Invalidierungs-Disziplin und konsequentem Material-Design. Persistenz erfolgt nicht über sqlite-net (anders als CLAUDE.md vermuten lässt), sondern über JSON-Dateien im LocalApplicationData-Ordner mit atomarem Temp-Move-Pattern. Die SkiaSharp-Renderer halten statische Paint-/Filter-Caches und folgen damit der dokumentierten Best Practice.

Der Audit findet **24 konkrete Verbesserungen**, davon **eine architektonisch kritische** (Geldwerte als `double` statt `decimal`), **sechs hohe** (CompiledBindings, Virtualisierung, UTC-Persistenz, NaN-Schutz, Timer-Lebensdauer auf inaktiven Tabs, RecurringTransactions werden nur einmal pro App-Lifetime verarbeitet), und der Rest gut adressierbar in jeweils unter einer Stunde.

**Bewertung pro Dimension**

| Dimension | Note | Kommentar |
|---|---|---|
| UI/UX | gut | Konsistente Nav, schöne Skia-Visuals, Animationen mit korrektem `RenderTransform`-Initialwert. Vereinzelt Hardcoded-Farben, fehlende A11y-Labels. |
| Architektur | ordentlich | DI sauber, Service-Trennung klar, partielle Klassen für Home-Logik. Schwäche: `double` für Finanzbeträge. |
| Performance | gut | Optimierbar bei großen Listen (kein Virtualizer), redundante Timer auf inaktiven Tabs, kleinere LINQ-Mehrfach-Enumerationen. |
| Memory | unauffällig | Keine echten Skia-Leaks, statische Caches sind intentional. Marginaler GC-Druck durch `new ObservableCollection(...)`-Pattern. |
| Robustheit | lückenhaft | NaN/∞-Guards, `DateTime.Today`-Persistenz, Race in `_expenses`-Reads ohne Semaphore. |

---

## 2. Findings nach Severity

| # | Finding | Sev | Kategorie |
|---|---|---|---|
| F-01 | Geldwerte als `double` statt `decimal` (gesamte Datenpipeline) | **Critical** | Architektur |
| F-02 | `x:CompileBindings` fehlt auf allen 22 Views | High | Performance / Robustheit |
| F-03 | `ItemsControl` ohne Virtualisierung für Transaktionslisten | High | Performance |
| F-04 | `ProcessDueRecurringTransactionsAsync` läuft nur einmalig pro App-Start | High | Bug |
| F-05 | HomeView 60-fps-Timer läuft auch auf inaktiven Tabs | High | Performance / Akku |
| F-06 | `DateTime.Today` in Persistenz und Daueraufträgen | High | Bug / Timezone |
| F-07 | NaN/∞-Schutz fehlt in Calculator-VMs und `ValidateExpense` | High | Robustheit |
| F-08 | `_expenses`-Reads ohne Semaphore (Collection-Modified-Race) | High | Thread-Safety |
| F-09 | `ObservableCollection` bei Filter/Sort komplett rekonstruiert (mit irreführendem Kommentar) | Medium | Performance |
| F-10 | Lokalisierungs-`GetString()` im Property-Getter (Hot-Path) | Medium | Performance |
| F-11 | Hardcoded Theme-Farben in MainView/HomeView/ExpenseTrackerView | Medium | UI/UX |
| F-12 | `AutomationProperties.Name` fehlt auf Icon-Buttons | Medium | A11y |
| F-13 | `UpdateGroupedExpenses` nutzt `g.Key.ToString("dddd, dd. MMMM")` ohne Culture | Medium | i18n |
| F-14 | `Quick-Add`-Items werden bei jedem Type-Switch neu erzeugt | Medium | Performance |
| F-15 | Forecast-Trend-Loop ist O(daysPassed × monthExpenses.Count) | Medium | Performance |
| F-16 | Fire-and-Forget ohne Fehler-Handling (Insights, Status, Search) | Medium | Robustheit |
| F-17 | `LoadFinancialInsightsAsync` übergibt CancellationToken nicht an Service-Call | Medium | Robustheit |
| F-18 | Bottom-Margin 120 in HomeView überdimensioniert | Low | UI/UX |
| F-19 | `RecurringTransaction.AmountDisplay` ignoriert Type=Transfer | Low | Bug |
| F-20 | Range-Validierung im Export fehlt | Low | Robustheit |
| F-21 | `UpdateLocalizedTexts` feuert 23 PropertyChanged-Events | Low | Performance |
| F-22 | Inkonsistentes `IDisposable`-Pattern bei VMs | Low | Wartbarkeit |
| F-23 | TODO unerledigt: Account/Budget-Emojis durch MaterialIcons ersetzen | Low | UI/UX |
| F-24 | `WriteAtomicAsync`-Code in mehreren Services dupliziert | Low | Wartbarkeit (DRY) |

---

## 3. Critical

### F-01 · Geldwerte als `double` statt `decimal`

**Kategorie:** Architektur · **Severity:** Critical

**Stellen (verifiziert):**

| Datei | Zeile | Property |
|---|---|---|
| `Models/Expense.cs` | 13 | `public double Amount { get; set; }` |
| `Models/Account.cs` | 29 | `public double InitialBalance` |
| `Models/Budget.cs` | 12, 18 | `MonthlyLimit`, `WarningThreshold` |
| `Models/SavingsGoal.cs` | 14, 17 | `TargetAmount`, `CurrentAmount` |
| `Models/DebtEntry.cs` | 15, 18, 21, 24 | `OriginalAmount`, `RemainingAmount`, `InterestRate`, `MonthlyPayment` |
| `Models/RecurringTransaction.cs` | 24 | `Amount` |
| `Models/FinanceEngine.cs` | 225–291 | sämtliche Result-Records |
| `Helpers/CurrencyHelper.cs` | 42, 50, 59, 68, 76 | alle Format-Methoden nehmen `double` |
| `Services/AccountService.cs` | 129, 136, 179, 187 | alle Balance-APIs |
| `Services/SavingsGoalService.cs`, `DebtService.cs` | – | alle Mutation-APIs |

**Problem.** Die App ist eine Finanz-Anwendung, in der Beträge addiert, prozentual aufgeteilt, im Schuldenrechner über Monate iteriert und exportiert werden. `double` (IEEE-754) erzeugt unvermeidlich Rundungsfehler: `0.1 + 0.2 = 0.30000000000000004`. In Aggregaten über ein Jahr kumuliert das im Cent-Bereich. In der Schuldentilgung kann `RemainingAmount` schließlich als `3E-7 €` „angezeigt werden, eigentlich 0". Das ist für eine Finanz-App kein Stilproblem, sondern eine architektonische Entscheidung, die jetzt günstiger zu korrigieren ist als später (mehr Migrations-Daten = teurer).

**Fix.** Migration auf `decimal` für die gesamte **Datenebene** (Models, Services, JSON-Persistenz, ViewModels-Properties). Die **Calculator-Berechnungen** in `FinanceEngine` (Compound Interest, Loan, Inflation) dürfen intern `double` benutzen — sie sind Closed-Form-Math mit `Math.Pow`, dort ist `decimal` nicht praktikabel — geben aber `decimal` zurück. Migration in einer Init-Routine: alte JSON-Datei lesen (ist gleich, nur Zahlen werden vom JSON-Parser anders interpretiert), neu serialisieren als `decimal`. Der Backup-Code (`ExpenseService.ExportToJsonAsync`) muss gleichzeitig migriert werden, damit alte Backups noch lesbar sind.

```csharp
// Models/Expense.cs (vorher → nachher)
public decimal Amount { get; set; }

// Models/FinanceEngine.cs Calculate-Signatur
public CompoundInterestResult Calculate(decimal principal, decimal annualRate, int years, int n)
{
    double p = (double)principal, r = (double)annualRate;
    double finalAmount = p * Math.Pow(1 + r / n, n * years);
    return new CompoundInterestResult
    {
        Principal       = principal,
        FinalAmount     = Math.Round((decimal)finalAmount, 2),
        InterestEarned  = Math.Round((decimal)(finalAmount - p), 2)
    };
}

// Helpers/CurrencyHelper.cs
public static string Format(decimal amount) {
    var c = _config;
    return c.SymbolAfter ? $"{amount:N2} {c.Symbol}" : $"{c.Symbol}{amount:N2}";
}
```

**Migrationsstrategie.**

1. Models und CurrencyHelper auf `decimal`.
2. Services (`ExpenseService`, `AccountService`, `SavingsGoalService`, `DebtService`, `FinancialAnalysisService`) — die Compiler-Fehler führen einen durch.
3. ViewModels: `[ObservableProperty] private double _foo;` → `decimal`. XAML-Bindings bleiben unberührt (der `StringFormat` formatiert `decimal` identisch).
4. Backup-Import: alte JSON kann unverändert per `JsonSerializer.Deserialize<List<Expense>>` gelesen werden — `System.Text.Json` parst Zahlen direkt in `decimal`, sofern das Property den Typ hat. Alte Daten von vor der Migration funktionieren also automatisch.
5. AppChecker laufen lassen, alle Stellen prüfen, dann Versions-Bump.

---

## 4. High

### F-02 · `x:CompileBindings` fehlt auf allen 22 Views

**Stellen (verifiziert per grep):** Alle 22 Views haben `x:DataType` deklariert, aber **keine** hat `x:CompileBindings="True"`. Beispiele:

```
Views/MainView.axaml:12               x:DataType="vm:MainViewModel"
Views/HomeView.axaml:12               x:DataType="vm:MainViewModel"
Views/ExpenseTrackerView.axaml:10     x:DataType="vm:ExpenseTrackerViewModel"
Views/StatisticsView.axaml:11         x:DataType="vm:StatisticsViewModel"
Views/Calculators/AmortizationView.axaml:9  …
```

**Problem.** Compiled Bindings sind in Avalonia 11 die einzige Möglichkeit, Bindings zur Build-Zeit zu validieren und zur Laufzeit ohne Reflection-Overhead aufzulösen. Ohne `x:CompileBindings="True"` werden Bindings reflektiv resolviert und Tippfehler im Property-Namen fallen erst bei Tab-Wechsel auf — wenn überhaupt.

**Fix.** Pro View ein Attribut auf der UserControl-Wurzel:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             x:Class="FinanzRechner.Views.HomeView"
             x:DataType="vm:MainViewModel"
             x:CompileBindings="True">
```

Build danach wird neue Warnungen zeigen (insbesondere `$parent[UserControl].((vm:ExpenseTrackerViewModel)DataContext).DeleteExpenseCommand` in `ExpenseTrackerView.axaml:357` ist wackelig). Diese sollten direkt mitkorrigiert werden.

---

### F-03 · `ItemsControl` ohne Virtualisierung für Transaktionslisten

**Stellen (verifiziert):**

| Datei | Zeile | Bindung |
|---|---|---|
| `Views/ExpenseTrackerView.axaml` | 317 | `GroupedExpenses` (Tagesgruppen) |
| `Views/ExpenseTrackerView.axaml` | 344 | `{Binding}` (Transaktionen pro Tag) |
| `Views/HomeView.axaml` | 328 | `RecentTransactions` (Top 3, unkritisch) |
| `Views/StatisticsView.axaml` | 385 | `ExpensesByCategory` |

**Problem.** `ItemsControl` rendert *alle* Items auf einmal. Bei einem Jahr mit z. B. 15 Transaktionen × 30 Tage × 12 Monate = 5400 Items in einer Liste — jedes mit eigener Border, Grid, MaterialIcon, `SwipeToRevealBehavior` und `StaggerFadeInBehavior`. Das ist langsam beim ersten Render und beim Scroll.

**Fix.** Äußere Liste auf `ItemsRepeater` mit `StackLayout`, oder gleich `ListBox` mit `ItemsPanel` = `VirtualizingStackPanel`:

```xml
<!-- Statt -->
<ItemsControl ItemsSource="{Binding GroupedExpenses}">
  <ItemsControl.ItemTemplate>...</ItemsControl.ItemTemplate>
</ItemsControl>

<!-- Besser: ItemsRepeater mit Virtualisierung -->
<ScrollViewer>
  <ItemsRepeater ItemsSource="{Binding GroupedExpenses}">
    <ItemsRepeater.Layout>
      <StackLayout Orientation="Vertical" Spacing="12"/>
    </ItemsRepeater.Layout>
    <ItemsRepeater.ItemTemplate>...</ItemsRepeater.ItemTemplate>
  </ItemsRepeater>
</ScrollViewer>
```

Innere Liste pro Tag (Zeile 344) kann `ItemsControl` bleiben — pro Tag selten mehr als 5–10 Items.

---

### F-04 · `ProcessDueRecurringTransactionsAsync` nur einmalig pro App-Lifetime

**Stellen (verifiziert):**

| Datei | Zeile | Code |
|---|---|---|
| `ViewModels/MainViewModel.Home.cs` | 304–321 | `OnAppearingAsync` ruft `ProcessDueRecurringTransactionsAsync` auf |
| `Views/HomeView.axaml.cs` | 64–71 | `OnAttachedToVisualTree → vm.OnAppearingAsync()` |
| `ViewModels/MainViewModel.cs` | 340–348 | Tab-Switch ruft **nur** `LoadMonthlyDataAsync`, **nicht** `OnAppearingAsync` |
| `Views/MainView.axaml` | 245, 270, 302, 334 | Alle 4 Tab-Panels parallel im Visual Tree, nur per `IsVisible` umgeschaltet |

**Problem.** `HomeView.OnAttachedToVisualTree` feuert nur **einmal** beim ersten Anzeigen. Da MainView die vier Tabs als parallel im Visual Tree liegende Panels mit `IsVisible`-Toggle realisiert (statt DataTemplate-Wechsel), bleibt `HomeView` dauerhaft attached. Der zweite Aufruf von `OnAppearingAsync` kommt nur bei vollständigem App-Restart. Wer die App über Mitternacht offen lässt (Smartphones tun das oft), verpasst seine Daueraufträge: keine automatische Miete, kein automatisches Gehalt.

**Fix.** Daueraufträge bei Tab-Wechsel auf Home zusätzlich verarbeiten — am sinnvollsten innerhalb von `LoadMonthlyDataAsync` selbst, mit Idempotenz-Check (der existiert bereits in `WasProcessedTodayAsync`):

```csharp
// MainViewModel.Home.cs · LoadMonthlyDataAsync (vor Z. 327)
private async Task LoadMonthlyDataAsync()
{
    try
    {
        // Daueraufträge nachholen — ist no-op wenn heute schon verarbeitet
        try { await _expenseService.ProcessDueRecurringTransactionsAsync(); }
        catch (Exception ex) { Debug.WriteLine($"[Home] Recurring failed: {ex}"); }

        var today = DateTime.Today;
        var summary = await _expenseService.GetMonthSummaryAsync(today.Year, today.Month);
        // …
```

`OnAppearingAsync` kann dann den Recurring-Aufruf entfernen oder behalten (idempotent).

Alternativ: `App.LifetimeChanged`/`Activity.OnResume` triggert eine zentrale Tagesinitialisierung. Auf Android ist `MainActivity.OnResume` die saubere Stelle.

---

### F-05 · HomeView 60-fps-Timer läuft auch auf inaktiven Tabs

**Stellen (verifiziert):**

| Datei | Zeile | Code |
|---|---|---|
| `Views/HomeView.axaml.cs` | 64 | Timer startet in `OnAttachedToVisualTree` |
| `Views/HomeView.axaml.cs` | 73 | Timer stoppt erst in `OnDetachedFromVisualTree` |
| `Views/HomeView.axaml.cs` | 87 | `Interval = 16ms` → ~60 fps |
| `Views/MainView.axaml` | 245 ff. | Vier Tab-Panels, nur `IsVisible` toggelt |

**Problem.** In Avalonia 11 detacht `IsVisible="False"` einen Control nicht aus dem Visual Tree — der Control bleibt logisch attached, also feuert `OnDetachedFromVisualTree` nicht. Folge: Wenn der User auf Tracker/Stats/Settings ist, läuft trotzdem der 60-fps-Dispatcher-Timer der HomeView, ruft jeden Frame `FinanceDashboardRenderer.Update(dt)` auf und `InvalidateSurface()` auf einer unsichtbaren Canvas. CPU- und Akku-Last fallen für „nichts" an.

**Fix.** Auf `IsHomeActive` lauschen und Timer entsprechend pausieren:

```csharp
// HomeView.axaml.cs
protected override void OnDataContextChanged(EventArgs e)
{
    base.OnDataContextChanged(e);
    if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
    _vm = DataContext as MainViewModel;
    if (_vm != null) _vm.PropertyChanged += OnVmPropertyChanged;
    UpdateTimerState();
}

private void OnVmPropertyChanged(object? s, PropertyChangedEventArgs e)
{
    // … bestehende switch-Logik …
    if (e.PropertyName == nameof(_vm.IsHomeActive)) UpdateTimerState();
}

private void UpdateTimerState()
{
    var shouldRun = _vm?.IsHomeActive == true && IsAttachedToVisualTree();
    if (shouldRun) StartDashboardTimer();
    else StopDashboardTimer();
}
```

Zusätzlich gilt das gleiche Pattern für `MainView.axaml.cs` (5-fps-Background-Timer Z. 81, läuft immer). Hier ist der Impact kleiner, aber ebenso unnötig wenn der User nicht zuschaut — sinnvoll wäre `Window.Activated`/`Deactivated` zu beobachten.

---

### F-06 · `DateTime.Today` in Persistenz und Daueraufträgen

**Stellen (verifiziert):**

| Datei | Zeile | Code |
|---|---|---|
| `Models/Expense.cs` | 11 | `DateTime Date = DateTime.Today;` (Default-Initializer) |
| `Services/ExpenseService.cs` | 310, 329, 414, 417, 711, 721 | `DateTime.Today`, `today.ToString("yyyy-MM-dd")` |
| `ViewModels/ExpenseTrackerViewModel.cs` | 45, 208, 332, 338, 646, 660, 929 | überall lokal |
| `ViewModels/MainViewModel.Home.cs` | 134, 327, 528, 601, 675 | überall lokal |

**Problem.** CLAUDE.md fordert für Persistenz `DateTime.UtcNow` und ISO-8601 (`O`). FinanzRechner verwendet durchgehend `DateTime.Today` (lokal, ohne Timezone) und speichert direkt JSON-Standard-Format. Für Daueraufträge wird `today.ToString("yyyy-MM-dd")` (ExpenseService:711) als Idempotenz-Key benutzt — geht der User am Abend des 5.5. um 23:59 nach Hause und öffnet die App früh am 6.5. nach einer Reise nach Tokio (UTC+9), dann ist `DateTime.Today` in Tokio bereits der 6.5., während der Server-/Cloud-Vergleich noch 5.5. annimmt. Backup-Export/Import fügt eine zweite Schicht von Off-by-Day-Fehlern hinzu.

Außerdem ist `DateTime.Today` nicht durchgehend für Default-Initialisierung gedacht — bei Deserialisierung wird der Default überschrieben, beim Bauen einer neuen Expense aber nicht.

**Fix.**

```csharp
// Models/Expense.cs
public DateTime Date { get; set; } = DateTime.UtcNow;

// Services/ExpenseService.cs · WasProcessedTodayAsync
private async Task<bool> WasProcessedTodayAsync(DateTime utcToday)
{
    if (!File.Exists(_lastProcessedFilePath)) return false;
    var dateStr = await File.ReadAllTextAsync(_lastProcessedFilePath);
    return dateStr.Trim() == utcToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

// Anzeige bleibt lokal
// XAML: StringFormat="\\{0:dd. MMMM yyyy\\}" auf einer Property,
// die intern .ToLocalTime() aufruft.
```

Migration ist datentyp-kompatibel — bestehende `DateTime`-Werte sind weiterhin lesbar; sie sind nur nun als Local interpretiert. Wer streng korrekt sein will, schreibt eine Einmal-Migration in `LoadExpensesAsync` die alle Daten als `DateTime.SpecifyKind(d, DateTimeKind.Local).ToUniversalTime()` neuschreibt.

---

### F-07 · NaN/∞-Schutz fehlt in Calculator-Math und `ValidateExpense`

**Stellen (verifiziert):**

| Datei | Zeile | Problem |
|---|---|---|
| `Services/ExpenseService.cs` | 733 | `if (expense.Amount <= 0)` lässt `double.NaN` durch (NaN ≤ 0 = false) |
| `ViewModels/Calculators/AmortizationViewModel.cs` | 165–184 | Berechnung ohne `double.IsFinite`-Check |
| `ViewModels/Calculators/CompoundInterestViewModel.cs` | 155–171 | `Math.Pow(1 + r/n, n*y)` ohne Vorab-Range-Check |
| `ViewModels/Calculators/LoanViewModel.cs` | 146–165 | dito |
| `ViewModels/MainViewModel.Home.cs` | 662 | Quick-Add: `double.TryParse` OK, aber `amount <= 0` lässt NaN durch |

**Problem.** `double.NaN` und `double.PositiveInfinity` sind in C# semantisch *keine* Zahlen, aber Vergleichsoperatoren wie `<= 0` und `> MaxAmount` liefern *false* — NaN passt also keinen Filter. Eine Eingabe von `1e500` im Quick-Add führt nach Parse zu `Infinity`, wird gespeichert, summiert, und der Home-Dashboard zeigt `MonthlyExpenses = ∞`. SkiaSharp-Renderer crashen je nach Operation.

**Fix.** Zentrale Helper-Methode:

```csharp
public static class AmountGuard
{
    public static bool IsValidAmount(double v) =>
        double.IsFinite(v) && v >= 0 && v < 999_999_999d;
    public static bool IsValidAmount(decimal v) =>
        v >= 0 && v < 999_999_999m;
}

// ExpenseService.cs · ValidateExpense
if (!AmountGuard.IsValidAmount(expense.Amount))
    throw new ArgumentException("Betrag ungültig (NaN, ∞ oder außerhalb des Bereichs).");

// Calculator-VMs
if (!double.IsFinite(LoanAmount) || LoanAmount <= 0) return;
if (!double.IsFinite(AnnualRate) || AnnualRate < 0)  return;
if (Years <= 0 || Years > 100) return;

var result = _engine.CalculateLoan(LoanAmount, AnnualRate, Years);
if (!double.IsFinite(result.MonthlyPayment))
{
    MessageRequested?.Invoke("Ungültig", "Eingaben führen zu Überlauf.");
    return;
}
```

Nach F-01 (decimal-Migration) entfällt das Problem für die Datenebene weitgehend; die Calculator-Math bleibt aber `double` und braucht den Guard.

---

### F-08 · `_expenses`-Reads ohne Semaphore (Collection-Modified-Race)

**Stelle (verifiziert):** `Services/ExpenseService.cs:85–123, 326–348, 763–792`.

**Problem.** Alle `Get*Async`-Methoden lesen `_expenses` ohne `_semaphore.WaitAsync()`. Schreibvorgänge (`AddExpenseAsync`, `UpdateExpenseAsync`, `DeleteExpenseAsync`, `ProcessDueRecurringTransactionsAsync`, `ImportFromJsonAsync`) halten den Semaphore. Wenn jetzt der Home-Tab `LoadMonthlyDataAsync` aufruft (das letztlich `GetExpensesByMonthAsync` enumeriert) während gleichzeitig `CheckBudgetWarningAsync` (Z. 145, fire-and-forget) parallel im `Task.Run` läuft *und* der User per Quick-Add eine neue Transaktion hinzufügt, dann iteriert eine `LINQ Where`-Enumeration über eine `_expenses`-Liste, die parallel mutiert wird → `InvalidOperationException: Collection was modified`.

Race-Trigger ist beobachtbar bei schnellem Quick-Add direkt nach App-Start, wenn das Background-Backup noch läuft.

**Fix.** Reader-Writer-Pattern: entweder alle Reads in den Semaphore (einfach, aber bremst), oder `ImmutableList<Expense>` mit atomarem Swap:

```csharp
// ExpenseService.cs
private System.Collections.Immutable.ImmutableList<Expense> _expenses
    = System.Collections.Immutable.ImmutableList<Expense>.Empty;

// Add (innerhalb Semaphore)
_expenses = _expenses.Add(expense);
await SaveExpensesAsync();   // serialisiert die unveränderliche Liste — kein Race

// Read (ohne Semaphore — atomarer Snapshot)
public Task<IReadOnlyList<Expense>> GetAllExpensesAsync()
{
    return Task.FromResult<IReadOnlyList<Expense>>(
        _expenses.OrderByDescending(e => e.Date).ToList());
}
```

Alternativ einfacher und ausreichend: Reads auch in den Semaphore, aber der Mehraufwand ist messbar — Profiling vorab.

---

## 5. Medium

### F-09 · `ObservableCollection` bei Filter/Sort komplett rekonstruiert

**Stelle:** `ViewModels/ExpenseTrackerViewModel.cs:625, 666`.

```csharp
// Z. 622–625, mit eigenem Kommentar:
// "Bei Avalonia effizienter, da Clear+Add viele einzelne CollectionChanged-Events
//  feuert (je eines pro Add), während eine neue Collection nur ein einziges
//  PropertyChanged auslöst."
Expenses = new ObservableCollection<Expense>(filtered);
```

**Problem.** Der Kommentar ist irreführend. Eine neue `ObservableCollection`-Zuweisung löst `PropertyChanged` aus, woraufhin der View **alle** Item-Container disposed und neu erzeugt — inkl. `SwipeToRevealBehavior`, `StaggerFadeInBehavior`, MaterialIcon-Caches. Das kostet sichtbar mehr als 100 sequentielle `Add`-Calls auf derselben Collection, die im Idealfall N existing Containers wiederverwenden. Sichtbar als Flackern beim Filter-Tippen.

**Fix.** `Clear()` + `AddRange()`:

```csharp
// Helper in BasisVM:
public static void Refresh<T>(ObservableCollection<T> target, IEnumerable<T> source)
{
    target.Clear();
    foreach (var item in source) target.Add(item);
}

// Anwendung
Refresh(Expenses, filtered);
HasExpenses = Expenses.Count > 0;
```

Optional: `Avalonia.Collections.AvaloniaList<T>` benutzt — unterstützt `AddRange` nativ und feuert einen Batch-Reset-Event statt N einzelne. Für Sortier-Operationen ist auch `DataGridCollectionView` mit `SortDescriptions` denkbar.

---

### F-10 · Lokalisierungs-`GetString()` im Property-Getter

**Stellen:** `MainViewModel.cs:499–528` (~30 Properties), `ExpenseTrackerViewModel.cs:51–83` (33 Properties), `RecurringTransactionsViewModel.cs:28–47`, `StatisticsViewModel.cs:68–94`.

```csharp
// Beispiel
public string FinanceTrackerText
    => _localizationService.GetString("FinanceTracker") ?? "Finance Tracker";
```

**Problem.** Bei jedem `OnPropertyChanged(nameof(FinanceTrackerText))` und jedem Tab-Switch werden alle 33 Properties neu evaluiert — pro Property ein `ResourceManager`-Lookup plus String-Allocation. Per-Switch grob 100-200 µs Overhead und eine Welle von String-Allocations.

**Fix.** Backing-Field + Refresh in `UpdateLocalizedTexts`:

```csharp
// statt
public string FinanceTrackerText => _localizationService.GetString("FinanceTracker") ?? "Finance Tracker";

// nutze
[ObservableProperty] private string _financeTrackerText = "";

private void RefreshLocalizedTexts() {
    FinanceTrackerText = _localizationService.GetString("FinanceTracker") ?? "Finance Tracker";
    // …
}
```

`UpdateLocalizedTexts` wird ohnehin bei Sprache-Wechsel und initialem Load aufgerufen — keine Verhaltensänderung, nur einmalige Materialisierung statt N-fach.

---

### F-11 · Hardcoded Theme-Farben

**Stellen (verifiziert):**

| Datei | Zeile | Wert |
|---|---|---|
| `Views/MainView.axaml` | 390 | `Background="#33EF4444"` |
| `Views/MainView.axaml` | 410, 427 | `#3322C55E`, `#22C55E` |
| `Views/ExpenseTrackerView.axaml` | 355 | `Background="#EF4444"` (Delete-Layer) |
| `Views/ExpenseTrackerView.axaml` | 632 | `Background="#33EF4444"`, `BorderBrush="#EF4444"` |
| `Views/HomeView.axaml` | 89–104, 261–272 | mehrere Income-/Expense-Hex-Codes |
| `Themes/AppPalette.axaml` | – | `IncomeBrush`/`ExpenseBrush`/`DangerBrush` **nicht definiert** |

**Problem.** Income- und Expense-Farben sind als Hex-Literale eingestreut, obwohl semantische Tokens fehlen. Wenn Marketing entscheidet, dass Income jetzt smaragdgrün statt apfelgrün sein soll, muss man manuell durchsuchen.

**Fix.**

```xml
<!-- Themes/AppPalette.axaml ergänzen -->
<Color           x:Key="IncomeColor">#22C55E</Color>
<Color           x:Key="ExpenseColor">#EF4444</Color>
<SolidColorBrush x:Key="IncomeBrush"  Color="{DynamicResource IncomeColor}"/>
<SolidColorBrush x:Key="ExpenseBrush" Color="{DynamicResource ExpenseColor}"/>

<!-- in den Views ersetzen -->
<Border Background="{DynamicResource ExpenseBrush}"/>
```

`SuccessBrush` und `ErrorBrush` existieren bereits in der Palette (`AppPalette.axaml:100, 104`) — alternativ aliasen.

---

### F-12 · `AutomationProperties.Name` fehlt auf Icon-Buttons

**Stellen:** `MainView.axaml:348`, `ExpenseTrackerView.axaml:531`, `BudgetsView.axaml:252`, weitere FABs in HomeView/StatisticsView.

**Problem.** Avalonia setzt für Icon-only-Buttons keinen Default-Namen — TalkBack/Narrator sagt „Button" oder „Klickbar". Für Sehbehinderte unbedienbar.

**Fix.**

```xml
<Button AutomationProperties.Name="{Binding AddTransactionLabel}"
        ToolTip.Tip="{Binding AddTransactionLabel}"
        Command="{Binding ShowAddExpenseFormCommand}">
  <mi:MaterialIcon Kind="Plus" Width="24" Height="24"/>
</Button>
```

Lokalisiert binden, nicht hardcoden.

---

### F-13 · `ToString("dddd, dd. MMMM")` ohne Culture

**Stelle:** `ViewModels/ExpenseTrackerViewModel.cs:660`, ähnlich `:164` (`MMMM yyyy`).

```csharp
dateDisplay = g.Key.ToString("dddd, dd. MMMM");
```

**Problem.** Verwendet `CultureInfo.CurrentCulture`. Wenn der User in Settings → Sprache auf Spanisch wechselt, aber das System-Locale Deutsch ist, bleiben die Tagesnamen deutsch.

**Fix.**

```csharp
var culture = CultureInfo.GetCultureInfo(_localizationService.CurrentLanguage);
dateDisplay = g.Key.ToString("dddd, dd. MMMM", culture);
```

Vorausgesetzt `ILocalizationService.CurrentLanguage` ist verfügbar (Convention im Repository).

---

### F-14 · `Quick-Add`-Items werden bei jedem Type-Switch neu erzeugt

**Stelle:** `MainViewModel.Home.cs:250–267, 269–275`.

**Problem.** `OnQuickAddTypeChanged` ruft `UpdateQuickCategoryItems` — eine neue `ObservableCollection<CategoryDisplayItem>` mit 5–9 Items wird erzeugt, das Binding rebindet, kurzes Flackern. Da sich nur die Auswahl ändert, könnte man zwei vorberechnete Listen halten.

**Fix.**

```csharp
// In Konstruktor
private readonly ObservableCollection<CategoryDisplayItem> _quickExpenseItems = BuildItems(QuickExpenseCategories);
private readonly ObservableCollection<CategoryDisplayItem> _quickIncomeItems  = BuildItems(QuickIncomeCategories);

partial void OnQuickAddTypeChanged(TransactionType value)
{
    OnPropertyChanged(nameof(IsQuickExpenseSelected));
    OnPropertyChanged(nameof(IsQuickIncomeSelected));
    QuickAddCategory = value == TransactionType.Expense ? ExpenseCategory.Other : ExpenseCategory.Salary;
    QuickCategoryItems = value == TransactionType.Expense ? _quickExpenseItems : _quickIncomeItems;
    SyncSelection();
}
```

Sprachwechsel (`UpdateLocalizedTexts`) muss beide Listen refreshen.

---

### F-15 · Forecast-Trend-Loop ist O(daysPassed × monthExpenses.Count)

**Stelle:** `Services/FinancialAnalysisService.cs:265–271`.

```csharp
for (var day = 1; day <= daysPassed; day++)
{
    cumulative += monthExpenses
        .Where(e => e.Type == TransactionType.Expense && e.Date.Day == day)
        .Sum(e => e.Amount);
    trend.Add((day, cumulative));
}
```

**Problem.** Bei `daysPassed = 31` und `monthExpenses.Count = 100` sind das 3100 LINQ-Iterationen pro Insights-Call. Insights wird auf jedem Home-Tab-Switch geladen.

**Fix.** Single-Pass:

```csharp
var byDay = new double[32]; // Index 1..31
foreach (var e in monthExpenses)
    if (e.Type == TransactionType.Expense && e.Date.Day is >= 1 and <= 31)
        byDay[e.Date.Day] += e.Amount;

double cumulative = 0;
var trend = new List<(int Day, double CumulativeExpenses)>(daysPassed);
for (var day = 1; day <= daysPassed; day++)
{
    cumulative += byDay[day];
    trend.Add((day, cumulative));
}
```

Reduziert von O(N×D) auf O(N+D).

---

### F-16 · Fire-and-Forget ohne Fehler-Handling

**Stellen:**

| Datei | Zeile | Code |
|---|---|---|
| `MainViewModel.cs` | 342 | `_ = LoadMonthlyDataAsync().ContinueWith(...)` — TaskContinuationOptions fängt Faults nicht ab |
| `MainViewModel.cs` | 346, 348 | `_ = ExpenseTrackerViewModel.OnAppearingAsync()` |
| `MainViewModel.Home.cs` | 347 | `_ = LoadFinancialInsightsAsync()` |
| `ExpenseService.cs` | 145–149 | `_ = Task.Run(...)` für CheckBudgetWarning |
| `StatisticsViewModel.cs` | 169, 174, 631, 686, 746 | mehrere |
| `ExpenseTrackerViewModel.cs` | 1066, 1101, 1106, 1135, 1140 | Status-Toasts |

**Problem.** `_ = SomeAsync()` schluckt Exceptions. In Release auf Android bleiben Fehler komplett unsichtbar. Bei schnellem Tab-Spam laufen mehrere parallele Insights-Loads, alle gegen dieselben Services.

**Fix.** Zentraler Helper in `ViewModelBase`:

```csharp
protected static void SafeFireAndForget(
    Func<Task> task, string ctx,
    Action<Exception>? onError = null)
{
    _ = Task.Run(async () =>
    {
        try { await task().ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FAF:{ctx}] {ex}");
            onError?.Invoke(ex);
        }
    });
}

// Anwendung
SafeFireAndForget(LoadFinancialInsightsAsync, "Insights");
```

---

### F-17 · `LoadFinancialInsightsAsync` übergibt Token nicht an Service

**Stelle:** `MainViewModel.Home.cs:716–760`.

```csharp
private async Task LoadFinancialInsightsAsync()
{
    _insightsCts?.Cancel();
    _insightsCts?.Dispose();
    var cts = new CancellationTokenSource();
    _insightsCts = cts;

    try
    {
        var bundle = await _financialAnalysisService.GetAllInsightsAsync();   // ← kein CT
        if (cts.Token.IsCancellationRequested) return;
        // …
    }
    catch (OperationCanceledException) { }
    catch (Exception) { }
}
```

**Problem.** `GetAllInsightsAsync()` nimmt keinen `CancellationToken` an und wird auch nicht unterbrochen. Bei schnellem Tab-Spam laufen N parallele Insight-Berechnungen, alle blockieren über Task.WhenAll dieselben Services. Die nachträgliche `IsCancellationRequested`-Prüfung verhindert nur den UI-Update, nicht die Arbeit.

**Fix.** API erweitern:

```csharp
public async Task<FinancialInsightsBundle> GetAllInsightsAsync(CancellationToken ct = default)
{
    var currentMonthTask = _expenseService.GetMonthSummaryAsync(today.Year, today.Month);
    // … Tasks anlegen …
    await Task.WhenAll(tasks).WaitAsync(ct).ConfigureAwait(false);
    ct.ThrowIfCancellationRequested();
    // …
}

// Aufruf
var bundle = await _financialAnalysisService.GetAllInsightsAsync(cts.Token);
```

`Task.WaitAsync(CancellationToken)` ist der saubere .NET-6+-Weg, eine bestehende Task abzubrechen ohne sie selbst zu modifizieren.

---

## 6. Low

### F-18 · Bottom-Margin 120 in HomeView überdimensioniert

**Stelle:** `Views/HomeView.axaml:18` — `<StackPanel Margin="0,0,0,120">`.

CLAUDE.md fordert mindestens 60dp Margin auf scrollbarem Inhalt. 120dp = doppelt so viel wie nötig. Bei aktivem Ad-Spacer (Row 1, 64dp) plus Tab-Bar (Row 2, 56dp) ist der Container ohnehin schon vertikal versetzt.

**Fix.** `Margin="0,0,0,60"` reicht.

---

### F-19 · `RecurringTransaction.AmountDisplay` ignoriert Type=Transfer

**Stelle:** `Models/RecurringTransaction.cs:36–38`.

```csharp
public string AmountDisplay => Type == TransactionType.Expense
    ? $"-{CurrencyHelper.Format(Amount)}"
    : $"+{CurrencyHelper.Format(Amount)}";
```

`Type=Transfer` fällt in den `+`-Zweig — eine Umbuchung als „+" anzuzeigen ist semantisch falsch.

**Fix.**

```csharp
public string AmountDisplay => Type switch
{
    TransactionType.Expense  => $"-{CurrencyHelper.Format(Amount)}",
    TransactionType.Income   => $"+{CurrencyHelper.Format(Amount)}",
    TransactionType.Transfer => $"↔ {CurrencyHelper.Format(Amount)}",
    _                        => CurrencyHelper.Format(Amount)
};
```

---

### F-20 · Range-Validierung im Export fehlt

**Stelle:** `Services/ExportService.cs:27–45 (CSV)`, `:200–225 (PDF)` und `StatisticsViewModel.cs:724`.

Wenn `startDate > endDate`, läuft der Export ohne Fehler durch und produziert eine leere Datei.

**Fix.**

```csharp
public Task<string> ExportToCsvAsync(int year, int month, string? targetPath)
{
    if (year < 2000 || year > 2100) throw new ArgumentOutOfRangeException(nameof(year));
    if (month is < 1 or > 12)       throw new ArgumentOutOfRangeException(nameof(month));
    // …
}

public Task<string> ExportToCsvAsync(DateTime startDate, DateTime endDate, string? targetPath)
{
    if (endDate < startDate) (startDate, endDate) = (endDate, startDate);
    // …
}
```

---

### F-21 · `UpdateLocalizedTexts` feuert 23+ PropertyChanged-Events

**Stelle:** `StatisticsViewModel.cs:95–123`, ähnlich in fast jedem VM.

Bei einem Sprach-Wechsel werden bis zu 30 `PropertyChanged`-Events sequentiell gefeuert, jedes triggert UI-Bindings. Sichtbar als kurzer Hänger.

**Fix.** Wenn F-10 umgesetzt ist (Backing-Fields), reduziert sich das automatisch auf nur die geänderten Properties. Alternativ einen einzelnen `LocalizationVersion`-Counter und Bindings über `MultiBinding` an dessen Änderung — overkill für diesen Fall.

---

### F-22 · Inkonsistentes `IDisposable`-Pattern

**Stand:**

| VM | Dispose? |
|---|---|
| `MainViewModel` | ✓ (vollständig, Z. 598–629) |
| `ExpenseTrackerViewModel` | ✓ |
| `BudgetsViewModel` | ✓ |
| `AmortizationViewModel` | ✓ |
| `LoanViewModel` | ✓ |
| `CompoundInterestViewModel` | ✓ |
| `StatisticsViewModel` | ✓ (Teil-Cleanup) |
| `AccountsViewModel` | ✗ |
| `DebtTrackerViewModel` | ✗ |
| `SavingsGoalsViewModel` | ✗ |
| `CustomCategoriesViewModel` | ✗ |
| `RecurringTransactionsViewModel` | ✗ |
| `SettingsViewModel` | ✗ |

**Problem.** Da alle VMs als Singletons via DI registriert sind, leben sie bis zum App-Ende. Dispose wird de facto nie aufgerufen; das Pattern existiert nur als Vorsichtsmaßnahme. Inkonsistent bedeutet: ein neuer Mitarbeiter weiß nicht, ob er für eine neue VM Dispose schreiben soll.

**Fix.** Entweder alle VMs konsequent `IDisposable` (mit Cleanup von Service-Subscriptions) und Dispose im `App.OnExit`/`OnDestroy` aufrufen, oder ausdrücklich in einem ADR dokumentieren: „VMs sind Singletons mit App-Lifetime, Dispose entfällt." Die zweite Option ist hier pragmatischer.

---

### F-23 · TODO unerledigt: Account/Budget-Emojis durch MaterialIcons

**Stellen:**

| Datei | Zeile | Code |
|---|---|---|
| `Models/Account.cs` | 32 | `Icon = "\U0001F3E6"` (🏦) |
| `Models/Budget.cs` | 39–41 | TODO-Kommentar |

**Problem.** Auf manchen Android-Geräten und Custom-ROMs werden Emojis als Tofu-Boxen oder verzerrt gerendert. Material-Icon-Bibliothek liefert konsistente Glyphen.

**Fix.** AccountType → `MaterialIconKind` Mapping anlegen, View per `<mi:MaterialIcon Kind="{Binding IconKind}">` statt `<TextBlock Text="{Binding Icon}">`.

---

### F-24 · `WriteAtomicAsync`-Code in mehreren Services dupliziert

**Stellen:** `ExpenseService.cs:645–650`, `AccountService.cs:254–260`, `SavingsGoalService.cs` (analog), `DebtService.cs` (analog).

**Fix.** Verschiebe in `MeineApps.Core.Ava.Services` einen statischen Helper:

```csharp
public static class FileIO
{
    public static async Task WriteAtomicAsync(string path, string content)
    {
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
```

---

## 7. Positiv: was sauber gelöst ist

Diese Punkte sind **nicht zu ändern** — sie folgen Best Practice und/oder sind über die CLAUDE.md hinaus durchdacht:

- **Statische SkiaSharp-Caches** (`FinanceDashboardRenderer.cs:59–61` für `BlurSmall/Medium/Large`, `CalculatorHeaderRenderer.cs:82–83`, `BudgetGaugeVisualization.cs:20`). Lebensdauer = AppDomain ist intentional und vermeidet das in CLAUDE.md dokumentierte „SKMaskFilter native Memory Leak".
- **`FinanzRechnerSplashRenderer`** nutzt Instanz-Felder mit `OnDispose()` (Z. 290–313) — korrektes Cleanup für die kurzlebige Splash-Lifetime.
- **`MainViewModel.Dispose()`** unsubscribt sauber alle Service-/Child-VM-Events (Z. 598–628). Mustergültig.
- **`_isHomeDataStale`-Flag** korrekt verdrahtet: initial `true` (`MainViewModel.cs:197`), in `OnTrackerDataChanged` invalidiert (Z. 592), beim erfolgreichen Load zurückgesetzt (Z. 343 via `ContinueWith(OnlyOnRanToCompletion)`) — kein „dirty cache als clean markiert".
- **`RecurringTransaction.GetNextMonthlyDate`/`GetNextYearlyDate`** (`Models/RecurringTransaction.cs:59–75`) hat **explizite Drift-Protection** für 31er-Tage und 29.2. Schaltjahr. Code-Kommentare beschreiben das korrekt.
- **`CurrencyHelper`** ist thread-safe via volatile Snapshot-Pattern (`Helpers/CurrencyHelper.cs:13–20`).
- **`FinanzRechnerLoadingPipeline.cs:30–49`** parallelisiert DB+Shader+Käufe+Konten+Sparziele+Schulden+Kategorien korrekt mit `Task.WhenAll`. Shader-Compile läuft im `Task.Run` (Z. 33), blockiert nicht.
- **`Backup/Restore`** (`SettingsViewModel`) mit `JsonSerializer` + atomic Move via Temp-File. Solide.
- **`MaxIterationsPerRecurring = 365`** (`ExpenseService.cs:427`) als Schutz vor Endlosschleifen bei beschädigter `last_processed.txt`-Datei. Gute Defensive-Coding-Disziplin.
- **`RenderTransform="scale(1)"` + `RenderTransformOrigin="50%,50%"`** auf allen Buttons mit `TransformOperationsTransition` (`MainView.axaml:582, 628, 674, 720, 197 …`) — der dokumentierte Avalonia-Crash ist vermieden.
- **MainView Grid `RowDefinitions="*,Auto,Auto"`** mit 64-dp Ad-Spacer und 56-dp Tab-Bar (`MainView.axaml:235, 559–565`) — exakt nach CLAUDE.md-Spec.
- **i18n-Vollständigkeit:** Alle 6 Sprachen (de, en, es, fr, it, pt) sind als `.resx` mit ~45 KB pro Sprache präsent (`Resources/Strings/`), zuletzt aktualisiert am 7.4.2026.
- **Single-Pass-Aggregation** in `StatisticsViewModel.LoadStatisticsAsync` (Z. 348–365) und `MainViewModel.Home.GenerateAndShowBudgetAnalysis` (Z. 540–554) — bewusst optimiert.
- **`AccountService.GetAllAccountBalancesAsync`** (Z. 143–177) optimiert mit zwei `GroupBy`+`Dictionary`-Lookups statt N+1-Queries; Kommentar dokumentiert es.
- **`Undo-Queue` für Delete** (`ExpenseTrackerViewModel.cs:823–851`) mit `CancellationTokenSource` + `Queue<Expense>` — verhindert Race-Conditions bei schnellem Delete.

---

## 8. Empfohlener Umsetzungs-Fahrplan

Vorschlag in fünf Sprints, geordnet nach Risiko/Wert.

| Sprint | Dauer | Maßnahmen | Findings |
|---|---|---|---|
| **1** | 2–3 Tage | **decimal-Migration** über Models/Services/VMs/Helpers. Backup-Datei aus altem Format einlesen testen. AppChecker laufen lassen. | F-01 |
| **2** | 1 Tag | **Performance-Quick-Wins:** `x:CompileBindings="True"` auf alle 22 Views, Transaktionsliste auf `ItemsRepeater` umstellen, Lokalisierungs-Backing-Fields. | F-02, F-03, F-10 |
| **3** | 1 Tag | **Robustheit:** `DateTime.UtcNow` konsequent, NaN/∞-Guards, Range-Checks, Recurring-Run bei jedem Tab-Wechsel auf Home, `_expenses`-Read-Race fixen. | F-04, F-06, F-07, F-08, F-20 |
| **4** | 0,5 Tag | **UI/UX & A11y:** Hardcoded-Farben → `IncomeBrush`/`ExpenseBrush`-Tokens, `AutomationProperties.Name` auf Icon-Buttons, Bottom-Margin korrigieren, Material-Icons statt Emojis. | F-11, F-12, F-18, F-23 |
| **5** | 1 Tag | **Feinschliff:** Timer-Pause auf inaktiven Tabs, `ObservableCollection` Clear+Add, Quick-Add-Caching, Single-Pass-Aggregation in `CalculateForecast`, `SafeFireAndForget`, `CancellationToken` an Service-Calls, `WriteAtomicAsync`-Helper extrahieren. | F-05, F-09, F-14, F-15, F-16, F-17, F-24 |

**Validierungs-Schritte nach Sprint 1:**

- AppChecker: `dotnet run --project tools/AppChecker FinanzRechner`
- Backup-Datei aus altem Format (mit `double`-Werten) importieren — alle Werte exakt erhalten?
- Stichprobe: 10 Transaktionen anlegen, je 5,1 % Steuer hinzurechnen — Cent-genau?
- Ein Dauerauftrag über Mitternacht UTC laufen lassen.
- Splash-Pipeline-Zeit messen (Stopwatch in `App.axaml.cs:113`).

---

## 9. Anhang: Verworfene Behauptungen

Während des Audits sind drei zunächst plausible Befunde durch Code-Verifikation verworfen worden:

1. **„SKMaskFilter leakt"** — falsch. Die Filter sind `static readonly` in den Renderer-Klassen, leben damit per AppDomain, und das ist exakt das von CLAUDE.md vorgeschriebene Pattern („Gecachte statische `SKMaskFilter` verwenden"). Kein Fix nötig.

2. **„`_isHomeDataStale` wird nirgends auf `true` gesetzt"** — falsch. Initialwert ist `true` (`MainViewModel.cs:197`), Re-Setzung erfolgt in `OnTrackerDataChanged` (Z. 592) bei jedem Datenänderungs-Event aus Sub-VMs.

3. **„Margin=\"16\" auf StackPanel im ScrollViewer blockiert Scrollen"** — falsch. CLAUDE.md sagt: „`Padding` auf `ScrollViewer` blockiert, stattdessen `Margin` auf direktes Kind verwenden". Genau das macht der Code (Z. 21). Korrekt umgesetzt.

4. **„AccountService GetAllAccountBalancesAsync ist O(N²)"** — falsch. Der Code ist optimiert (Kommentar Z. 143: „Einmal GroupBy statt O(N) pro Konto"), nutzt zwei `Dictionary`-Lookups. Per-Konto-Zeile 169 (`Where(monthStart)`) iteriert nur über die jeweiligen `accountExpenses`, gesamt linear über alle Transaktionen.

5. **„Loading-Pipeline: Shader synchron"** — falsch. Z. 33: `Task.Run(() => ShaderPreloader.PreloadAll())` läuft im Pool-Thread, blockiert die Pipeline nicht. Die übrigen Tasks laufen via `Task.WhenAll` parallel.

6. **„`FinancialAnalysisService` Division-by-Zero"** — falsch. Z. 99–102 prüft `previous.TotalExpenses > 0 ? … : 0` und `previous.TotalIncome > 0 ? … : 0`. Z. 190 setzt `monthlyIncome = currentMonth.TotalIncome > 0 ? currentMonth.TotalIncome : 1` als Fallback.

Diese Falschtreffer sind in Sub-Audit-Agenten-Reports entstanden, weil sie nur einzelne Methoden ohne Kontext gesehen haben. Die Verifikation gegen den vollen Codebase ist im Bericht oben dokumentiert.

---

*Erstellt 2026-05-06 · Robert Schneider · `MeineApps.Ava` v2.0.7*
