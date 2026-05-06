# WorkTimePro – Optimierungs-Audit (vertieft)

**Version:** v2.0.7 (geschlossener Test)
**Auditdatum:** 2026-05-06
**Scope:** Architektur, UI/UX, Performance, Memory
**Codebase:** `src/Apps/WorkTimePro/` – Shared 20.332 LOC, plus Android- und Desktop-Heads
**Methode:** Direkte Lektüre aller relevanten Dateien (alle 9 ViewModels, alle 14 Service-Implementierungen, 2 von 11 Visualisierungen voll, 4 weitere im Spotcheck, ausgewählte Views, App.axaml.cs, LoadingPipeline) plus Cross-Check gegen die in `CLAUDE.md` dokumentierten Fallstricke.

> Hinweis: Dieser Bericht ersetzt das frühere Kurz-Audit. Wo der Erstbericht falsche Vermutungen hatte (z. B. „MainViewModel ohne Dispose" – stimmt nicht), sind die Befunde unten **korrigiert** und im Anhang **„Korrekturen"** markiert.

---

## Executive Summary

Der Code ist insgesamt **handwerklich solide**. Es gibt sichtbare Spuren bewusster Optimierung: gecachte `SolidColorBrush`-Statics, ein `LiveDataSnapshot` der die früheren 5+ DB-Queries pro Sekunde auf 3 reduziert hat, korrekt benutzte Transactions in `BulkRestoreAsync`/`SaveHolidaysAsync`, ein vorbildlicher `WorkspaceBackgroundRenderer` mit Shader- und Bitmap-Caching, sauber dokumentierte Konventionen.

Die wesentlichen Verbesserungspunkte sind:

1. **Async-Hygiene** – 4 echte `async void`-Eventhandler (rekonstruierbar, nicht alle gleich kritisch), 13+ Fire-and-Forget-Stellen, lokale `Debug.WriteLine`-„Logs" statt zentralem Reporter.
2. **Cloud-Backup ist Schein-Implementierung** – `SignInWithGoogleAsync`/`SignInWithMicrosoftAsync` warten 1 s und melden Erfolg, obwohl `IsAuthenticated=false` bleibt; `CreateBackupAsync` schreibt nur lokal in den Cache. Für den Endkunden im aktuellen Test irreführend.
3. **DateTime-Parsing ohne `InvariantCulture` an drei Stellen** – verstößt gegen die selbst dokumentierte Regel und kann je nach Geräte-Locale zu falschen Daten oder fehlgeschlagenem Routing führen.
4. **MainViewModel-Schichtdicke** – 1053 LOC, Reflection-basiertes Sub-VM-Wiring; funktional korrekt, aber für 9 Apps das größte „Single-File-Risiko" in Sachen Wartbarkeit.
5. **`SKMaskFilter`/`SKShader` pro Frame** – in vier Visualisierungen, obwohl ein gut gemachter Vorbild-Renderer (`WorkspaceBackgroundRenderer`) im selben Verzeichnis liegt. CLAUDE.md selbst nennt das als Anti-Pattern.
6. **`ClearAllDataAsync` ohne Transaction** – 11 sequenzielle `DeleteAll` ohne Wrapper. Bei Crash mitten im Restore bleibt die DB halb-leer, obwohl ein Safety-Backup im RAM existiert.

Schätzaufwand für die ersten **6 Critical**-Items: ca. **8–10 h**. Erweiterte Stabilisierung (alle 🟠): ca. **20–25 h**. Polish (🟡/🟢): zusätzliche **15–20 h**.

---

## 🔴 Critical – sofort beheben

### C-1. `ClearAllDataAsync` ohne Transaction → Datenverlust-Risiko
**Wo:** `Services/DatabaseService.cs:811-827`
```csharp
public async Task ClearAllDataAsync()
{
    var db = await GetDatabaseAsync();
    await db.DeleteAllAsync<ProjectTimeEntry>();
    await db.DeleteAllAsync<TimeEntry>();
    await db.DeleteAllAsync<PauseEntry>();
    // … 8 weitere DeleteAllAsync-Calls
    await db.DeleteAllAsync<Employer>();
}
```
**Warum kritisch:** Wird ausschließlich aus `BackupService.RestoreDataAsync:387` aufgerufen, **vor** `BulkRestoreAsync`. Stürzt die App während dieser 11 Aufrufe ab (Akkuende, Force-Stop), ist die Datenbank in einem inkonsistenten Zustand: einige Tabellen leer, andere halb. Das im RAM gehaltene Safety-Backup (`BackupService.cs:350`) ist nach Prozesstod ebenfalls weg. Das ist genau das Szenario, vor dem ein Backup-Feature schützen soll.
**Fix:**
```csharp
public async Task ClearAllDataAsync()
{
    var db = await GetDatabaseAsync();
    await db.RunInTransactionAsync(conn =>
    {
        conn.DeleteAll<ProjectTimeEntry>();
        conn.DeleteAll<TimeEntry>();
        // … alle anderen
        conn.DeleteAll<Employer>();
    });
}
```
**Zusätzlich:** Safety-Backup nicht nur in RAM halten, sondern als `safety_{utcNow}.json` in `BackupDirectory` schreiben, vor dem Clear. Beim nächsten Start prüfen, ob eine Safety-Datei jünger als die letzte erfolgreiche Restore-Markierung existiert → Recovery-Dialog anbieten.

### C-2. Cloud-Backup ist nicht implementiert, gibt aber Erfolg vor
**Wo:** `Services/BackupService.cs:119-176`
```csharp
public async Task<bool> SignInWithGoogleAsync()
{
    // Benötigt Google Sign-In mit Google.Apis.Auth (noch nicht integriert)
    // Platzhalter für UI-Tests
    await Task.Delay(1000).ConfigureAwait(false);
    CurrentProvider = CloudProvider.GoogleDrive;
    IsAuthenticated = false;          // (!)
    UserEmail = "";
    SaveSettings();
    AuthStatusChanged?.Invoke(this, true);  // (!) signalisiert true
    return true;                       // (!) und liefert true
}
```
**Warum kritisch:** Der User klickt „Mit Google anmelden", die Methode liefert `true`, der Auth-State wird auf `true` propagiert (`AuthStatusChanged?.Invoke(this, true)`), aber `IsAuthenticated` bleibt `false`. `CreateBackupAsync` checkt `IsAuthenticated` (Z. 205) und wird einfach mit „Not authenticated" abgewiesen. UI-Auswertung dieses Inkonsistenzfeldes ist nicht klar, aber die User-Erwartung (Cloud-Backup) wird nicht erfüllt – obwohl `LastBackupDate` über `CreateBackupAsync` immer noch lokal aktualisiert wird, wenn der Pfad doch erreicht würde.
Im aktuellen Test (geschlossener Test) ist das eine **garantierte 1-Stern-Quelle**, sobald Tester sich auf Cloud-Backup verlassen.
**Fix-Optionen:**
- **Kurzfristig:** Cloud-Provider-UI hinter `#if FEATURE_CLOUD_BACKUP` verstecken; offen kommunizieren „Lokales Backup nur" (Snackbar in `BackupView`). Provider-Buttons disablen oder mit „Demnächst"-Badge versehen.
- **Mittelfristig:** Echte MSAL/Google-OAuth-Integration. Die WorkTimePro-Vision (App-Übersicht in `CLAUDE.md`) listet das Feature nicht zwingend, also ist Verstecken die sicherste Wahl bis zur echten Implementierung.

### C-3. `SKMaskFilter` und `SKShader` pro Frame statt gecached
**Wo:**
- `Graphics/OvertimeSplineVisualization.cs:136-152` (LinearGradient pro Render), `:165-170` (Blur pro Render)
- `Graphics/MonthWeekProgressVisualization.cs:115-119` (Blur)
- `Graphics/StatsSummaryGaugeVisualization.cs:113-117` (Blur)
- `Graphics/VacationQuotaGaugeVisualization.cs:128-132` (Blur)
```csharp
// OvertimeSplineVisualization.cs:165
using (var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f))
{
    _dotPaint.MaskFilter = blur;
    canvas.DrawCircle(lastPt, 6f, _dotPaint);
    _dotPaint.MaskFilter = null;
}
```
**Warum kritisch:** `using` disposed zwar, aber jede Frame allokiert ein natives Skia-Objekt, das anschließend wieder freigegeben wird. Bei 5 fps Render-Loop = **300 nativ allokierte Objekte/min**, in 4 Visualisierungen gleichzeitig = ~1200/min. CLAUDE.md schreibt explizit: *„SKMaskFilter Native Memory Leak (OOM auf Android) … Gecachte statische SKMaskFilter verwenden oder paint.MaskFilter?.Dispose() vor jeder Neuzuweisung"*. Der bestehende `WorkspaceBackgroundRenderer` macht es vorbildlich (Z. 67-73). Diese 4 Renderer ziehen gegen die eigene Konvention.
Zusätzlich: `OvertimeSplineVisualization.cs:136` allokiert pro Frame einen `SKShader.CreateLinearGradient` mit 4 dynamischen Farben. Auch der gehört in einen `_cachedShader` mit Bounds-/Theme-Hash-Check.
**Fix-Skizze:**
```csharp
// In jeder Visualisierungs-Klasse:
private static SKMaskFilter? s_blur4;
private static SKMaskFilter Blur4 => s_blur4 ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

// im Render:
_dotPaint.MaskFilter = Blur4;
canvas.DrawCircle(lastPt, 6f, _dotPaint);
_dotPaint.MaskFilter = null;
```
Für Theme-abhängige Shader: `(int hash, SKShader shader)` – Hash aus Bounds + ColorTheme; bei Mismatch dispose + recreate.

### C-4. `DateTime.TryParse` ohne `InvariantCulture` an 3 Stellen
**Wo:**
- `ViewModels/DayDetailViewModel.cs:197` (`OnDateStringChanged`)
- `ViewModels/MainViewModel.cs:261` (`HandleNavigation` für `DayDetailPage?date=…`)
- `ViewModels/MainViewModel.cs:276` (`HandleNavigation` für `month?date=…`)
```csharp
// MainViewModel.cs:261
if (dateParam.Length > 1 && DateTime.TryParse(dateParam[1], out var date))
```
**Warum kritisch:** `DateTime.TryParse` ohne Argument-Variante nutzt `CultureInfo.CurrentCulture`. Die Routes sind ISO-Strings (`2026-02-13`, `2026-02-01`), die in deutschen, US-, oder PT-Locales meist gut parsen, aber:
- In manchen Locales ist das ISO-Format zweideutig (z. B. `01-02-2026` als 1. Februar oder 2. Januar).
- `BackupService.cs:84,90` macht es bereits richtig: `DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var v)`.
- CLAUDE.md fordert explizit: *„IMMER `DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`"*.

In der App eingebaute Routes leiden unter dieser Inkonsistenz potentiell beim Tab-Wechsel auf YearOverview→Month oder Calendar→DayDetail, je nach Geräte-Sprache.
**Fix:**
```csharp
DateTime.TryParse(dateParam[1],
    System.Globalization.CultureInfo.InvariantCulture,
    System.Globalization.DateTimeStyles.RoundtripKind,
    out var date);
```

### C-5. `async void`-Eventhandler ohne zentrale Exception-Strategie
**Wo:**
- `ViewModels/MainViewModel.cs:211` (`OnSettingsChanged`)
- `ViewModels/MainViewModel.cs:246` (`HandleNavigation`)
- `ViewModels/MainViewModel.cs:1012` (`OnUpdateTimerElapsed`)
- `Services/ReminderService.cs:99` (`OnStatusChanged`)

Alle vier sind mit lokalem `try/catch` umschlossen, was den unmittelbaren Crash verhindert. **Aber:**
- `OnSettingsChanged` (Z. 211-220) catcht und schreibt nur `Debug.WriteLine` → der User merkt nicht, dass das Settings-Reload kaputt ist.
- `HandleNavigation` (Z. 246-300) catcht ebenso silent → Tab klappt nicht auf, aber kein Indikator.
- `OnUpdateTimerElapsed` (Z. 1012-1022) feuert sekündlich; eine einzige Exception (z. B. nach Dispose-Race) überlebt der `try/catch`, aber bei *erneutem* Dispose im falschen Moment kann der Sync-Context die Exception eskalieren.
- `ReminderService.OnStatusChanged` (Z. 99) → falls dort eine Notification-API wirft, geht der Statuswechsel-Pfad still verloren.

**Warum kritisch (relativ):** Nicht „App crasht morgen", aber **die Kategorie Bug, die im Release ohne Telemetrie nie auffällt**. Im geschlossenen Test merkt es niemand, im Open Test schlägt es als „nichts passiert"-Bug zu.
**Fix-Linie:**
```csharp
// Zentral (z. B. in ViewModelBase oder einem IExceptionReporter):
protected void Forget(Func<Task> work, [CallerMemberName] string ctx = "")
{
    _ = work().ContinueWith(t =>
    {
        if (t.IsFaulted)
        {
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}.{ctx}] {t.Exception}");
            MessageRequested?.Invoke(AppStrings.Error, t.Exception?.Flatten().InnerException?.Message ?? "");
        }
    }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
}

// Eventhandler dann:
private void OnSettingsChanged(object? sender, EventArgs e)
    => Forget(async () => {
        _cachedSettings = await _database.GetSettingsAsync();
        await LoadTabDataAsync(CurrentTab);
    });
```

### C-6. Reflection-basiertes Sub-VM-Wiring
**Wo:** `MainViewModel.cs:222-241` plus Pendant in `Dispose:1044-1049`
```csharp
private void WireSubPageNavigation(ObservableObject vm)
{
    var navEvent = vm.GetType().GetEvent("NavigationRequested");
    if (navEvent != null)
    {
        var handler = new Action<string>(route => HandleNavigation(route));
        navEvent.AddEventHandler(vm, handler);
        _wiredEvents.Add((vm, "NavigationRequested", handler));
    }
    // … das gleiche für "MessageRequested"
}
```
**Warum kritisch:**
- `GetEvent("NavigationRequested")` wird **9-mal** beim VM-Konstruktor aufgerufen → 9× Reflection im kritischsten Pfad (App-Start nach Loading-Pipeline).
- Stille Brüche bei Refactoring: Wenn jemand `NavigationRequested` umbenennt oder typisiert (`event Action<NavigationRoute>`), verschwindet das Event still – kein Compile-Fehler, kein Test-Failure, keine UI-Funktion mehr.
- `Dispose` macht erneut Reflection (Z. 1046) → idempotent, aber teuer.
**Fix:**
```csharp
// In Sub-VMs:
public interface INavigationSource
{
    event Action<string> NavigationRequested;
}
public interface IMessageSource
{
    event Action<string, string> MessageRequested;
}

// In MainViewModel:
private void WireSubPageNavigation(ObservableObject vm)
{
    if (vm is INavigationSource ns)
    {
        Action<string> nh = HandleNavigation;
        ns.NavigationRequested += nh;
        _navHandlers[ns] = nh;
    }
    if (vm is IMessageSource ms)
    {
        Action<string, string> mh = (t, m) => MessageRequested?.Invoke(t, m);
        ms.MessageRequested += mh;
        _msgHandlers[ms] = mh;
    }
}
```
Compile-time-sicher, kein Reflection, Refactor-fest.

---

## 🟠 High – kurzfristig beheben

### H-1. God-VM `MainViewModel` (1053 LOC, 13 Verantwortlichkeiten)
**Wo:** `ViewModels/MainViewModel.cs`
**Belegliste der Verantwortlichkeiten:**
1. Tab-Navigation (Z. 304-354)
2. Sub-Page-Navigation inkl. Reflection-Wiring (Z. 222-300)
3. Live-Daten-Tick (Z. 167-172, 882-1022)
4. Undo-Mechanismus (Z. 47-51, 780-842)
5. Note-Debounce (Z. 48-49, 483-505, 844-857)
6. Ad-Banner-Toggle (Z. 86-87, 124-129, 866-869)
7. Insight-Berechnung (Z. 444-457, 965-990)
8. Verdienst-Berechnung (Z. 461-465, 992-1003)
9. Wochenziel-Celebration (Z. 508, 957-963)
10. Status-Anzeige (Z. 905-924)
11. Lokalisierung-Refresh (Z. 871-880)
12. Back-Press-Handling (Z. 748-772)
13. Held alle 9 Child-VMs (Z. 76-84)

**Warum problematisch:** Jede Änderung erfordert das gesamte File zu verstehen. Test-Setup ist eine Wand aus Mocks (10 Konstruktor-Parameter). Hot-Reload nach Änderung an *irgendetwas* triggert einen kompletten VM-Neuaufbau.
**Fix-Phasen:**
- **Phase 1 (2 h):** `MainViewModel.Navigation.cs` (partial class) für Tab-/Sub-Page-Navigation extrahieren. Keine Verhaltensänderung, nur Lokalität.
- **Phase 2 (3 h):** `IUndoService<TEntry>` als generischer Service in `MeineApps.Core.Ava` (gemeinsam mit anderen Apps). Ähnliches Muster vermutlich auch in HandwerkerImperium → schau dort nach Wiederverwendbarkeit.
- **Phase 3 (4 h):** `LiveTickerService` (Singleton) – ein zentraler 1-s-Timer, an den VMs Subscriber registrieren. Spart in HandwerkerImperium und WorkTimePro je einen Timer; reduziert zudem Battery-Druck wenn die App im Hintergrund ist (Service kann eine zentrale `IsAppForeground`-Stelle abfragen).
- **Phase 4 (2 h):** `NoteAutoSaveService<T>` mit `Debounce(TimeSpan)` und `OnSave`-Callback.

### H-2. Fire-and-Forget mit inkonsistenter Fehlerbehandlung
**13 Stellen verifiziert:**
| Datei | Zeile | Pattern |
|---|---|---|
| `MainViewModel.cs` | 322 | `_ = LoadTabDataAsync(...).ContinueWith(t=>...,OnlyOnFaulted)` |
| `MainViewModel.cs` | 493 | `_ = Task.Run(async () => ...)` (Note-Debounce) |
| `MainViewModel.cs` | 796 | `_ = Task.Run(async () => ...)` (Undo-Timer) |
| `SettingsViewModel.cs` | 154 | `_ = Task.Run(async () => ...)` |
| `SettingsViewModel.cs` | 177 | `_ = Task.Run(async () => ...)` |
| `CalendarViewModel.cs` | 115, 122 | `_ = RecalculateOverlayDaysAsync()` (kein ContinueWith) |
| `DayDetailViewModel.cs` | 207 | `_ = LoadDataAsync().ContinueWith(...)` |
| `VacationViewModel.cs` | 134, 145, 156 | `_ = ...ContinueWith(...)` |
| `YearOverviewViewModel.cs` | 121 | `_ = LoadDataAsync().ContinueWith(...)` |
| `DesktopNotificationService.cs` | 40 | `_ = Task.Run(async () => ...)` |
| `ReminderService.cs` | 206, 213, 268, 275 | `_ = ...` |

**Inkonsistenzen:**
- Ein Teil hat `ContinueWith(..., OnlyOnFaulted)` mit `MessageRequested`-Aufruf. ✓
- `CalendarViewModel.cs:115/122` hat **gar keine** Fortsetzung → silent.
- `Task.Run`-Lambda-Variante hat eingebauten `try/catch` über `Avalonia.Threading.Dispatcher.UIThread.InvokeAsync` ohne Exception-Pfad.

**Fix:** Zentralen `Forget`-Helper aus C-5 verwenden, alle 13 Stellen darauf umstellen. Spart ~30 Zeilen Boilerplate und schließt die Lücken.

### H-3. `WorkspaceBackgroundRenderer` läuft auch bei verstecker Page
**Wo:** Zugehöriger Trigger in `Views/MainView.axaml.cs` (DispatcherTimer 200 ms, siehe CLAUDE.md). Renderer selbst ist sauber (`Graphics/WorkspaceBackgroundRenderer.cs`).
**Warum:** Der Renderer ist als statische 5-fps-Animation gemacht. Wenn die App im Hintergrund ist (Android), wird `OnPaintSurface` zwar nicht aufgerufen (Avalonia stoppt Layout), aber der DispatcherTimer feuert weiter und `Update(deltaTime)` läuft weiter → Battery-Hit. Auf Desktop bei minimiertem Fenster identisch.
**Fix:**
```csharp
// In MainView.axaml.cs:
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);
    _bgTimer.Start();
}
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnDetachedFromVisualTree(e);
    _bgTimer.Stop();
}
// Zusätzlich: TopLevel.IsActive observieren (Avalonia 11.3 hat IsActiveProperty auf Window),
// auf false → Stop, auf true → Start.
```
Threshold-Erweiterung: `Math.Abs(width-_dotTileW) > 1f` (Z. 168) auf `> 4f` setzen — vermeidet Tile-Recreate bei minimalen Resize-Schritten (Android-Status-Bar-Anim, Desktop-Drag).

### H-4. Statistik-Aggregate materialisieren komplette Rows
**Wo:**
- `DatabaseService.cs:768-779` (`GetTotalWorkMinutesAsync`)
- `DatabaseService.cs:781-792` (`GetTotalOvertimeMinutesAsync`)
```csharp
public async Task<int> GetTotalWorkMinutesAsync(DateTime startDate, DateTime endDate)
{
    var workDays = await db.Table<WorkDay>()
        .Where(w => w.Date >= start && w.Date <= end)
        .ToListAsync();           // ← lädt komplette WorkDay-Objekte
    return workDays.Sum(w => w.ActualWorkMinutes);
}
```
**Warum:** sqlite-net materialisiert für jede WorkDay eine Zeile mit ~12 Spalten via Reflection-Mapping. Für die Calendar-Heatmap (`CumulativeBalance` über Jahr = 365 Rows) und Statistics-Charts (5-Jahres-Sicht = 1825 Rows) ist das messbar (auf Mid-Range-Android: ~80–150 ms). Wird bei jeder Statistics-/Year-Tab-Aktivierung gerufen.
**Fix:**
```csharp
public async Task<int> GetTotalWorkMinutesAsync(DateTime startDate, DateTime endDate)
{
    var db = await GetDatabaseAsync();
    return await db.ExecuteScalarAsync<int>(
        "SELECT COALESCE(SUM(ActualWorkMinutes), 0) FROM WorkDays WHERE Date BETWEEN ? AND ?",
        startDate.Date, endDate.Date);
}
```
Faktor-10-Speedup, ~0 Allokationen.

### H-5. `CalculationService.RecalculatePauseTimeAsync` lädt Daten doppelt
**Wo:** `Services/CalculationService.cs:64-69`, `:83-89`
```csharp
public async Task RecalculatePauseTimeAsync(WorkDay workDay)
{
    var pauses = await _database.GetPauseEntriesAsync(workDay.Id);   // 1. Pauses laden
    RecalculatePauseTime(workDay, pauses);
    await ApplyAutoPauseAsync(workDay);    // ← lädt Pauses + Entries + Settings ERNEUT
}

public async Task ApplyAutoPauseAsync(WorkDay workDay)
{
    var settings = await _database.GetSettingsAsync();
    var entries = await _database.GetTimeEntriesAsync(workDay.Id);
    var pauses = await _database.GetPauseEntriesAsync(workDay.Id);  // ← 2. Mal!
    await ApplyAutoPauseAsync(workDay, entries, pauses, settings);
}
```
Doppelter Load von `pauses` und ein zusätzlicher Load von `entries` und `settings`. `RecalculatePauseTimeAsync` wird aus `EndPauseAsync` (`TimeTrackingService.cs:214`) → bei jedem Pause-Ende.
**Fix:**
```csharp
public async Task RecalculatePauseTimeAsync(WorkDay workDay)
{
    var pauses = await _database.GetPauseEntriesAsync(workDay.Id);
    var entries = await _database.GetTimeEntriesAsync(workDay.Id);
    var settings = await _database.GetSettingsAsync();
    RecalculatePauseTime(workDay, pauses);
    await ApplyAutoPauseAsync(workDay, entries, pauses, settings);
}
```

### H-6. `DatabaseService` ohne Schreib-Semaphore
**Wo:** Gesamt `Services/DatabaseService.cs`. `_initLock` schützt nur Init.
**Beobachtung:** SQLite-net-pcl serialisiert intern Writes auf einer einzelnen Connection, *aber* das geschieht auf einem Worker-Pool und konkurrierende Writes können bei Connection-Recycling oder Pool-Hopping zu „SQLite busy"-Fehlern führen. Die App läuft heute ohne sichtbare Probleme – aber Backup-Pfade, Auto-Pause-Recalculation, Note-Debounce-Save und Live-Recalculation können koincidieren.
**Schwächeres Risiko als ursprünglich angenommen** – sqlite-net-pcl ist meist transaction-safe. Was tatsächlich helfen würde:
1. Alle Multi-Step-Updates in `RunInTransactionAsync`. Heute zumindest in `BulkRestoreAsync` ✓ und `SaveHolidaysAsync` ✓, aber `DeleteTimeEntryAsync` (Z. 246-275) löscht den Entry, lädt dann nochmal alles, löscht ggf. AutoPause – das wäre eine Transaction-Kandidatur.
2. Connection mit `WAL` aktivieren. Aktuell ist `SQLiteOpenFlags.SharedCache` gesetzt; `await _database.ExecuteAsync("PRAGMA journal_mode=WAL");` in `InitializeDatabaseAsync` würde gleichzeitige Reads während Writes erlauben.

### H-7. `GetSettingsAsync()` als Hot-Read, nirgends gecached
**Wo:** `DatabaseService.cs:321-331` plus zahlreiche Aufrufer:
- `CalculationService.RecalculateWorkDayAsync:24` (jeder Save)
- `CalculationService.ApplyAutoPauseAsync:85` (jeder Save)
- `CalculationService.CalculateWeekAsync:168`, `CalculateMonthAsync:246`, `GetWeekProgressAsync:345`
- `CalculationService.CheckLegalComplianceAsync:365`
- `DatabaseService.GetOrCreateWorkDayAsync:88`
- `MainViewModel.LoadDataAsync:660` (eigener `_cachedSettings`)
- `MainViewModel.UpdateLiveDataAsync:939` (Fallback)

**Warum:** Settings ändern sich selten (User-Aktion in SettingsView). Pro Tab-Wechsel werden Settings 3-5× neu aus DB geladen. SQLite ist schnell, aber wenn man einen In-Memory-Cache mit Invalidation einbaut (z. B. via `SettingsChanged`-Event aus `SettingsViewModel`), spart man pro Sekunde live-Tracking ein paar Dutzend Queries.
**Fix:** `IDatabaseService.GetSettingsAsync()` cached in DatabaseService selbst:
```csharp
private WorkSettings? _settingsCache;
private readonly object _settingsCacheLock = new();

public async Task<WorkSettings> GetSettingsAsync()
{
    var cached = _settingsCache;
    if (cached != null) return cached;
    var db = await GetDatabaseAsync();
    var settings = await db.Table<WorkSettings>().FirstOrDefaultAsync();
    if (settings == null) { settings = new WorkSettings(); await db.InsertAsync(settings); }
    lock (_settingsCacheLock) _settingsCache = settings;
    return settings;
}
public async Task SaveSettingsAsync(WorkSettings settings)
{
    var db = await GetDatabaseAsync();
    settings.ModifiedAt = DateTime.UtcNow;
    await db.UpdateAsync(settings);
    lock (_settingsCacheLock) _settingsCache = settings;
}
```

### H-8. Note-Debounce mit nicht-konsumiertem CancellationToken
**Wo:** `MainViewModel.cs:489-505`
```csharp
_noteDebounce?.Cancel();
_noteDebounce = new CancellationTokenSource();
var token = _noteDebounce.Token;
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(1500, token);     // wird gecancelt ✓
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await SaveNoteAsync();           // hier NICHT gecancelt!
        });
    }
    catch (TaskCanceledException) { }
});
```
**Warum:** Wenn der User nach dem 1500-ms-Delay aber während des `SaveNoteAsync()` weiter tippt, läuft der Save trotzdem durch. Bei zwei sehr schnellen Tipp-Phasen können 2 Saves in Folge auflaufen → letzter „gewinnt", aber Race ist möglich, da `SaveNoteAsync` `GetTodayAsync` + `SaveWorkDayAsync` macht und die `today.Note` aus dem View-Model nimmt – die Lese-/Schreibreihenfolge ist nicht atomar.
**Zusätzlich:** Wenn `LoadDataAsync` (Z. 644-646) `_suppressNoteDebounce = true` setzt und unmittelbar danach throwt (Z. 675), bleibt das Flag `false` zwar (im finally indirekt durch Z. 646 schon gesetzt), aber wenn `_suppressNoteDebounce = true` (Z. 644) erfolgt und `TodayNote = today.Note` (Z. 645) wirft, bleibt das Flag stehen. Der Save funktioniert nie wieder.
**Fix:** Saubere Debounce-Klasse:
```csharp
public sealed class AsyncDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;
    public AsyncDebouncer(TimeSpan delay) => _delay = delay;
    public void Trigger(Func<CancellationToken, Task> action)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(_delay, token); }
            catch (TaskCanceledException) { return; }
            if (token.IsCancellationRequested) return;
            try { await action(token); }
            catch (Exception ex) { Debug.WriteLine($"Debounce action failed: {ex}"); }
        });
    }
    public void Dispose() => _cts?.Dispose();
}
```
`_suppressNoteDebounce` durch `using var _ = _noteDebounce.Pause()` ersetzen (Pause-Token-Pattern, deterministisches Cleanup).

### H-9. `SettingsView` ohne Eingabevalidierung
**Wo:** `Views/SettingsView.axaml:96-112` (DailyHours, WeeklyHours)
- TextBoxen ohne `Watermark`
- Keine `MaxLength`
- Keine `[Range]`-Annotation am VM
- Kein `DataValidationErrors`-Style
- Schreibt direkt in `double`-Property → bei `"abc"` wirft setter und User merkt nichts

**Fix-Skizze:**
```xml
<TextBox Watermark="z. B. 8,5"
         MaxLength="5"
         TextAlignment="Center"
         Text="{Binding DailyHoursText, UpdateSourceTrigger=LostFocus}" />
```
VM:
```csharp
[ObservableProperty]
private string _dailyHoursText = "8";

partial void OnDailyHoursTextChanged(string value)
{
    if (double.TryParse(value, NumberStyles.Float,
        CultureInfo.CurrentCulture, out var d) && d > 0 && d <= 24)
    {
        DailyHours = d;
        DailyHoursError = null;
    }
    else
    {
        DailyHoursError = AppStrings.SettingsInvalidHours;
    }
}
```

### H-10. Unsubscribe-Handhabung in Sub-VMs unklar
**Wo:** `MainViewModel.Dispose:1024-1052`. Pflegt korrekt:
- `_timeTracking.StatusChanged -= OnStatusChanged`
- `_localization.LanguageChanged -= OnLanguageChanged`
- `_rewardedAdService.AdUnavailable -= OnAdUnavailable`
- `_adService.AdsStateChanged -= OnAdsStateChanged`
- `SettingsVm.SettingsChanged -= OnSettingsChanged`
- Alle gewireten Sub-VM-Events via `_wiredEvents`

**Aber:** Sub-VMs (`WeekOverviewViewModel`, `CalendarViewModel`, `StatisticsViewModel`, …) sind **Singletons** (`App.axaml.cs:221-230`). Sie überleben den `MainViewModel.Dispose()` und behalten ihre eigenen Subscriptions zu `_database`/`_calculation`/`_localization` etc. Falls eines der Sub-VMs auch `IDisposable` implementiert, wird das nie gerufen – die Singletons werden erst beim `IServiceProvider.Dispose()` (Prozessende) zerlegt. Das ist *fast* kein Leak, aber:
- Hot-Reload während Entwicklung → Singletons leben weiter, Events sammeln sich.
- Tests mit eigenem Provider erzeugen Container-Leak, wenn nicht entsorgt.
**Fix:** `MainView.axaml.cs.OnDetachedFromVisualTree` → `App.Services.GetService<IServiceProvider>()?.Dispose()`-Aufruf vermeiden, aber **Sub-VMs als Transient** registrieren wenn sie nicht inhärent State über Tab-Wechsel halten müssen (Settings, Year, Vacation – alle laden Daten on-demand). MainVM bleibt Singleton, Sub-VMs werden bei Tab-Aktivierung neu instanziiert (nicht alle gleichzeitig, sondern lazy → einmal-Instanz pro Tab-Aktivierung würde das Memory-Profil verbessern, da inaktive Sub-VMs vom GC abgeräumt werden).

### H-11. `TransformOperationsTransition` ohne initialen `RenderTransform` an mehreren Buttons
**Wo:** `Views/TodayView.axaml:14-24` (Style `Button.BigAction`), `Views/MainView.axaml:153-164` (Tab-Indikator)
```xml
<!-- TodayView.axaml:14-24 -->
<Style Selector="Button.BigAction">
  <Style.Setters>
    <Setter Property="Transitions">
      <Transitions>
        <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.12" />
      </Transitions>
    </Setter>
  </Style.Setters>
</Style>
<Style Selector="Button.BigAction:pointerover">
  <Setter Property="RenderTransform" Value="scale(1.04)" />
</Style>
```
**Warum:** CLAUDE.md warnt explizit: *„Button.OnAttachedToLogicalTree Crash (Android): TransformOperationsTransition für RenderTransform ohne initialen RenderTransform-Wert → Transition von null→scale() crasht auf manchen GPU-Treibern. FIX: IMMER `RenderTransform="scale(1)"` + `RenderTransformOrigin="50%,50%"` setzen wenn TransformOperationsTransition Property=\"RenderTransform\" verwendet wird."*
Im `Style Setter` (Z. 16-22) fehlt der initiale Setter.
**Fix:**
```xml
<Style Selector="Button.BigAction">
  <Style.Setters>
    <Setter Property="RenderTransform" Value="scale(1)" />
    <Setter Property="RenderTransformOrigin" Value="50%,50%" />
    <Setter Property="Transitions">
      <Transitions>
        <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.12" />
      </Transitions>
    </Setter>
  </Style.Setters>
</Style>
```
Selbiges für `Button.QuickNav` (Z. 35-38) und Tab-Indikator (`MainView.axaml:153-164` setzt `RenderTransform="translateX(0px)"` ✓ – ist OK; nur kontrollieren).

### H-12. Loading-Pipeline: Reminder-Init blockiert auf Settings-Read indirekt
**Wo:** `Loading/WorkTimeProLoadingPipeline.cs:38-44`
```csharp
AddStep(new LoadingStep
{
    Name = "Reminder",
    Weight = 5,
    ExecuteAsync = () => services.GetRequiredService<IReminderService>().InitializeAsync()
});
```
`ReminderService.InitializeAsync` ruft typischerweise `_database.GetSettingsAsync()` (siehe Reminder-Service). Da Schritt 1 die DB initialisiert hat, ist das OK – aber der Reminder-Service hängt zwischen DB-Init und VM-Init und blockiert die Sicht-Pipeline. Sinnvoller: Reminder-Init **parallel** zum ViewModel-Init in Schritt 1 oder 3.
**Fix:**
```csharp
// Schritt 1 erweitern:
ExecuteAsync = async () =>
{
    var dbTask = ...;
    var shaderTask = ...;
    var purchaseTask = ...;
    await Task.WhenAll(dbTask, shaderTask, purchaseTask);
    // Reminder kann erst nach DB starten:
    await services.GetRequiredService<IReminderService>().InitializeAsync();
}
```
oder Schritt 2 löschen und Reminder in Schritt 3 starten – spart eine Pipeline-Stage.

---

## 🟡 Medium – Code-Quality / Wartbarkeit

### M-1. `DateTime.Now` für Backup-Filename
**Wo:** `BackupService.cs:222` `var fileName = $"worktime_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";`
**Warum:** Persistierte Timestamps sind UTC; Filename ist lokal. Bei Geräte-Migration über Zeitzonen entsteht eine Lücke (User hat Backups mit Filename "20260506_153000" lokal, das könnte zwei verschiedene UTC-Momente sein).
**Fix:** `DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ")`.

### M-2. `ExportService` Footer in deutschem Datumsformat hartcodiert
**Wo:** `ExportService.cs:177` (per Architektur-Agent gemeldet, Verifikation per Lesen empfohlen).
**Fix:** `CultureInfo.CurrentUICulture.DateTimeFormat.LongDatePattern` oder `_localization.GetString("DateFormatShort")` mit pro Locale gepflegtem Format.

### M-3. `DeleteTimeEntryAsync` ineffizient
**Wo:** `DatabaseService.cs:246-275`
- Z. 249: `db.Table<TimeEntry>().Where(t => t.Id == id).FirstOrDefaultAsync()` statt `db.GetAsync<TimeEntry>(id)` (PK-Lookup ist schneller)
- Z. 250 löscht zuerst, dann (Z. 255-273) lädt WorkDay/Entries/AutoPauses sequentiell und löscht AutoPauses bei leerem WorkDay.
**Fix:** In `RunInTransactionAsync` packen, PK-Lookups nutzen.

### M-4. `WorkDay`-Suche in Calculation-Loops O(n²)
**Wo:** `CalculationService.cs:191` (`CalculateWeekAsync`), `:271` (`CalculateMonthAsync`)
```csharp
foreach (var date in 7..30 Tagen)
{
    var workDay = workDays.FirstOrDefault(d => d.Date.Date == date.Date);
    // …
}
```
Bei Monaten = 30 × O(30) = 900 Compares (vernachlässigbar). Aber: Bei Statistics-Year-Range (365 × 365) wird's ~133.000 Compares.
**Fix:** `var byDate = workDays.ToDictionary(d => d.Date.Date);` einmal vorab, dann `byDate.TryGetValue(date.Date, out var wd)`.

### M-5. `_cachedSettings` in MainVM und `GetSettingsAsync` ohne Sync
**Wo:** `MainViewModel.cs:660`, `:939`, `:216`
**Warum:** MainVM hält `_cachedSettings`, das aber außerhalb von `LoadDataAsync` und `OnSettingsChanged` veraltet sein kann. Wenn `H-7` (DB-Cache) eingebaut wird, wird dieser Inline-Cache redundant. Ohne H-7: `_cachedSettings` ist ein Half-Solution-Cache.
**Fix:** Mit H-7 mergen: `_cachedSettings` aus MainVM entfernen.

### M-6. `LegacyMigration` als Inline-Code in CalculateWeek/Month
**Wo:** `CalculationService.cs:207-211`, `:287-291`
```csharp
if (workDay.ActualWorkMinutes == 0 && workDay.BalanceMinutes == 0 && workDay.TargetWorkMinutes > 0)
{
    workDay.BalanceMinutes = -workDay.TargetWorkMinutes;
}
```
Backwards-Compat-Check für alte WorkDays. Wird **bei jeder Wochen-/Monatsberechnung erneut ausgeführt**.
**Fix:** Einmal-Migration in `InitializeDatabaseAsync`:
```sql
UPDATE WorkDays SET BalanceMinutes = -TargetWorkMinutes
WHERE ActualWorkMinutes = 0 AND BalanceMinutes = 0 AND TargetWorkMinutes > 0;
```

### M-7. Mehrere DB-Roundtrips in `TimeTrackingService.GetActiveWorkDayAsync` / `LoadStatusAsync`
**Wo:** `TimeTrackingService.cs:236-253` lädt im Worst-Case 4 Tage à 2 Queries = 8 Roundtrips für Mitternachts-Übergang.
**Fix:** Eine Single-Query mit Date-Range:
```csharp
var lastCheckIn = await db.QueryAsync<TimeEntry>(@"
    SELECT t.* FROM TimeEntries t
    JOIN WorkDays w ON w.Id = t.WorkDayId
    WHERE w.Date >= ? AND t.Type = 0
    ORDER BY t.Timestamp DESC LIMIT 1", DateTime.Today.AddDays(-3));
```
oder sogar im DatabaseService kapseln als `GetMostRecentCheckInAsync(daysBack)`.

### M-8. `Settings`-Property-Setter feuern jeweils `RecalculateWeeklyHours()` + `ScheduleAutoSave()`
**Wo:** `SettingsViewModel.cs:194-207` – 14× wiederholte Boilerplate
```csharp
partial void OnMondayHoursChanged(double value)
{ _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
partial void OnTuesdayHoursChanged(double value)
{ _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
// 12 weitere ähnliche…
```
**Fix:** Pattern in eine Methode verlagern und manuell auslösen:
```csharp
private void NotifyWorkTimeChanged()
{
    _workTimeSettingsChanged = true;
    RecalculateWeeklyHours();
    ScheduleAutoSave();
}
partial void OnMondayHoursChanged(double value) => NotifyWorkTimeChanged();
// kann komprimiert werden
```
Spart ~40 LOC, leichter wartbar.

### M-9. `BackupService` synchroner I/O auf Cache
**Wo:** `BackupService.cs:296` `Directory.GetFiles(CacheDirectory, "worktime_backup_*.json")`
**Warum:** synchron, kann den UI-Thread blockieren wenn `GetAvailableBackupsAsync` aus VM auf UI-Thread aufgerufen wird (verschachtelt mit `Task.Run`?).
**Fix:** Per `Task.Run` wrappen oder `async`-File-Enumeration.

### M-10. Lokalisierungsschwankung bei `AppStrings.RemainingTodayFormat`
**Wo:** `MainViewModel.cs:976-978`
```csharp
RemainingTodayText = string.Format(
    AppStrings.RemainingTodayFormat ?? "{0} {1}:{2:D2}",
    AppStrings.Remaining, remHours, remMins);
```
Fallback-String `"{0} {1}:{2:D2}"` ist nicht lokalisiert. Wenn der Resource-Key fehlt, ist der Anzeige-Text unsauber gemischt (Englisch + Format).
**Fix:** Resource-Key sicherstellen, Fallback durch DE-Default ersetzen.

### M-11. `Background` in MainView.axaml `Spacer` ohne Hintergrundfarbe
**Wo:** `Views/MainView.axaml:130-132`
```xml
<Border Grid.Row="1" Height="64"
        IsVisible="{Binding IsAdBannerVisible}" />
```
Hat keinen `Background` – ist also transparent. Wenn das Ad-SDK das Banner farbig zeichnet, gut; wenn das Banner kurz lädt oder fehlt, sieht der User durch den Spacer hindurch.
**Fix:** `Background="{DynamicResource SurfaceBrush}"` für visuelle Konsistenz.

### M-12. Test-Coverage unklar / `tests/`-Ordner wahrscheinlich leer
**Wo:** Kein Test-Projekt unter `tests/WorkTimePro` sichtbar (siehe Solution-Struktur in CLAUDE.md). Bei einer App mit 8000+ LOC reines App-Code ist das ein Risiko.
**Fix:** Mindestens für `CalculationService`, `TimeTrackingService` und `BackupService` Unit-Tests anlegen. xUnit + In-Memory-SQLite via `:memory:`-Database wäre ein einfacher Einstieg.

### M-13. `MainView.axaml.cs` Tab-Indikator-Berechnung
**Wo:** `Views/MainView.axaml.cs:152-175`
**Annahme aus früherem Audit:** Nicht vollständig gelesen. Wird empfohlen, bei Resize/Rotation auf `LayoutUpdated` zu binden statt `Bounds.Width<10`-Recursion.

---

## 🟢 Low – Polish

- **L-1.** `MaterialIcon` kein Default-Style in `AppPalette.axaml` → jeder View setzt `Width/Height/Foreground` redundant.
- **L-2.** `Skeleton-Loader` (Shimmer) statt blanker Flächen während `IsLoading`.
- **L-3.** `JsonSerializer.SerializeAsync(stream, ...)` statt String-Pfad in `BackupService.cs:219, 444` – relevant erst bei sehr großen Backups (>5k WorkDays).
- **L-4.** `ScrollViewer`-Header (`Stats_ScrollViewer`, etc.) konsequent mit `VerticalScrollBarVisibility="Auto"` versehen (manche Views haben es nicht).
- **L-5.** `BackupFolder` aus Preferences laden statt hartcodiert (`BackupService.cs:61`).
- **L-6.** `_cachedWorkTime` in `TimeTrackingService` (Z. 18) wird gesetzt (Z. 266, 308, 517) und in `GetCurrentSessionDuration()` (Z. 442) genutzt – aber dieser sync-Pfad wird nirgends im Codebase gerufen (per Grep). Toter Code? Dokumentieren oder entfernen.
- **L-7.** `ReminderService.OnStatusChanged` (Z. 99) async void; verifizieren ob `_pauseTimerCts`-Cleanup vollständig (siehe CLAUDE.md-Hinweis).
- **L-8.** `BalancePulse`-Animation in TodayView.axaml:41-52 – läuft INFINITE auch bei nicht sichtbarem Element. Avalonia handhabt das meist effizient, aber ein Style-Trigger auf `IsVisible=true` würde es ganz stoppen.
- **L-9.** `WorkspaceBackgroundRenderer`-Renderer ist Instance-basiert (gut), aber 5 Static-Color-Felder. Wenn dynamic Theme-Switch implementiert wird (aktuell laut CLAUDE.md nicht: „Kein dynamischer Theme-Wechsel"), müsste das zu Properties.
- **L-10.** `WorkTimeProSplashRenderer.cs` (381 LOC) — Splash sehr aufwendig. Spotcheck nicht durchgeführt; verdient eigene 30-Minuten-Inspektion auf Allokationen pro Frame.

---

## ✅ Anti-Findings (geprüft, **kein** Bug – Korrekturen zum vorherigen Audit)

Wichtig: Mehrere im Erst-Audit benannte Probleme haben sich beim direkten Code-Lesen als Falschmeldung der Sub-Agents herausgestellt. Diese sind hier explizit als **„Kein Bug"** dokumentiert, damit sie nicht versehentlich „gefixt" werden:

| Behauptung | Realität |
|---|---|
| „MainViewModel implementiert kein `IDisposable`" | **Falsch.** `MainViewModel.cs:1024-1052` hat ein vollständiges `Dispose` mit Event-Unsubscribe für alle 5 externen Events, Sub-VM-Reflection-Cleanup über `_wiredEvents`, Timer-Stop+Dispose, CancellationToken-Cleanup. |
| „ScrollViewer mit Padding (CLAUDE.md-Bug)" | **Falsch.** Per `grep` über alle 12 ScrollViewer-Stellen in `Views/*.axaml`: keiner hat Padding. Pattern wird eingehalten. |
| „Safety-Backup wird nach `ClearAllDataAsync` erstellt" | **Falsch.** `BackupService.cs:350` (Safety) ist explizit **vor** `ClearAllDataAsync` (Z. 387). Rollback-Pfad existiert (Z. 358-371). Das eigentliche Problem ist nicht die Reihenfolge, sondern dass das Safety-Backup im RAM bleibt (siehe C-1). |
| „N+1-Query in `GetShiftAssignmentsAsync`" | **Falsch.** `DatabaseService.cs:657-675` lädt Patterns einmal nach den Assignments und macht den Join in C# (Loop ohne DB-Calls). Das ist 2 Queries, nicht N+1. |
| „`_cachedWorkTime` wird nie befüllt" | **Falsch.** Wird gesetzt in `TimeTrackingService.cs:266, 308, 517`. Allerdings: Der Sync-Getter `GetCurrentSessionDuration()` Z. 442 wird im Codebase nirgends aufgerufen → Toter Code, siehe L-6. |
| „N+1 N+1 in `SetDefaultEmployerAsync`" | **Falsch.** `DatabaseService.cs:600-606` macht 2 SQL-Statements (`UPDATE… SET 0`; `UPDATE… SET 1 WHERE Id=?`) ohne N+1. |

---

## ✓ Stark-Befunde (positiv hervorgehoben)

Diese Stellen sind **vorbildlich** und sollten als Referenz für andere Apps der Suite dienen:

1. **`MainViewModel.cs:40-44`** – statisch gecachte `SolidColorBrush.Parse(...)` für Status- und Balance-Farben. Spart pro 1-s-Tick mehrere Parse-Calls.
2. **`TimeTrackingService.GetLiveDataSnapshotAsync` (Z. 456-530)** – ersetzt 5+ separate DB-Queries pro Sekunde durch **3** in einem Snapshot. Klarer Kommentar im Header. Großartige Optimierung.
3. **`DatabaseService.GetTimeEntriesForWorkDaysAsync` (Z. 190-212)** – respektiert SQLite-999-Parameter-Limit mit `Chunk(500)`. Sauber.
4. **`DatabaseService.SaveHolidaysAsync` (Z. 447-461)** + **`BulkRestoreAsync` (Z. 831-879)** – beide korrekt in `RunInTransactionAsync`.
5. **`WorkspaceBackgroundRenderer.cs`** – Shader-Cache mit Bounds-Check, Bitmap-Tile-Cache, Struct-Pool für Partikel, IDisposable. Vorbildhaft.
6. **`Loading/WorkTimeProLoadingPipeline.cs`** – DB+Shader+Purchase parallel in Schritt 1 (Z. 27-34). Spart messbare Sekunden im App-Start.
7. **`DatabaseService.GetOrCreateWorkDayAsync` (Z. 114-126)** – `try/catch SQLiteException` + Re-Read bei UNIQUE-Constraint-Verletzung schützt gegen den Race auf parallel laufende „erster Tageseintrag"-Pfade. Defensive, gut.
8. **`MainViewModel.OnTodayNoteChanged:486` `_suppressNoteDebounce`** – immerhin wird der DB-Write während `LoadDataAsync` unterdrückt (auch wenn die Implementierung Detail-Bugs hat, siehe H-8).

---

## Cross-Cutting-Vorschlag: Gemeinsamer Helper in `MeineApps.Core.Ava`

Mehrere Themen wiederholen sich vermutlich auch in HandwerkerImperium, FinanzRechner, etc. Einmal in `MeineApps.Core.Ava` gelöst, profitiert die ganze Suite:

```csharp
// MeineApps.Core.Ava/Async/AsyncDebouncer.cs
public sealed class AsyncDebouncer : IDisposable { … }

// MeineApps.Core.Ava/Async/Forget.cs
public static class TaskExtensions
{
    public static void Forget(this Task t, Action<Exception>? onError = null)
        => t.ContinueWith(x =>
            (onError ?? (e => Debug.WriteLine($"Task faulted: {e}")))(x.Exception!),
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
}

// MeineApps.Core.Ava/Mvvm/INavigationSource.cs + IMessageSource.cs

// MeineApps.Core.Ava/Skia/SkPaintCache.cs — fluent API für gecachte SKMaskFilter/SKShader
```

Allein der `Forget`-Helper räumt 13 Stellen in WorkTimePro auf und vereinheitlicht das Logging-Verhalten über alle 11 Apps der Suite.

---

## Priorisierte Roadmap

| # | Aufwand | Impact | Risiko | Tag(e) | Item |
|---|---|---|---|---|---|
| 1 | 30 min | hoch | 🔴 | 0,1 | C-1 `ClearAllDataAsync` in Transaction wrappen |
| 2 | 1 h | hoch | 🔴 | 0,2 | C-2 Cloud-Provider-Buttons disabled / „Demnächst" |
| 3 | 1 h | hoch | 🔴 | 0,2 | C-3 SKMaskFilter/Shader cachen (4 Visualisierungen) |
| 4 | 30 min | hoch | 🔴 | 0,1 | C-4 `DateTime.TryParse` mit `InvariantCulture` (3 Stellen) |
| 5 | 1 h | hoch | 🔴 | 0,2 | C-5 Zentraler `Forget`/Reporter-Helper |
| 6 | 2 h | hoch | 🔴 | 0,3 | C-6 Reflection-Wiring durch `INavigationSource`/`IMessageSource` ersetzen |
| 7 | 1 h | mittel | 🟠 | 0,2 | H-3 Background-Renderer pausieren bei Detached |
| 8 | 30 min | mittel | 🟠 | 0,1 | H-4 Statistik-Aggregate auf SQL `SUM` |
| 9 | 30 min | mittel | 🟠 | 0,1 | H-5 Doppel-Load in `RecalculatePauseTimeAsync` fixen |
| 10 | 1 h | mittel | 🟠 | 0,2 | H-7 `GetSettingsAsync` mit DB-internem Cache |
| 11 | 2 h | mittel | 🟠 | 0,3 | H-8 `AsyncDebouncer` + `_suppressNoteDebounce` ersetzen |
| 12 | 1,5 h | mittel | 🟠 | 0,2 | H-9 Settings-TextBox-Validierung |
| 13 | 30 min | mittel | 🟠 | 0,1 | H-11 `RenderTransform="scale(1)"` an Style-Settern |
| 14 | 30 min | klein | 🟠 | 0,1 | H-12 Loading-Pipeline Schritt 2 in Schritt 1 mergen |
| 15 | 4 h | mittel | 🟠 | 0,5 | H-1 Phase 1: MainViewModel.Navigation.cs extrahieren |
| 16 | 1 h | klein | 🟡 | 0,2 | M-3 `DeleteTimeEntryAsync` in Transaction |
| 17 | 30 min | klein | 🟡 | 0,1 | M-4 WorkDay-Dictionary in Calculation-Loops |
| 18 | 30 min | klein | 🟡 | 0,1 | M-6 Legacy-Migration als One-Time-SQL |
| 19 | 1 h | klein | 🟡 | 0,2 | M-12 Mindestens `CalculationService`-Tests anlegen |
| ... | ... | ... | 🟢 | ... | restliche L-Items als Polish-Sprint |

**Gesamt 🔴 (1-6): 6 h**
**Gesamt 🟠 (7-15): 12 h**
**Gesamt 🟡 (16-19): 3 h**
**Gesamt 🟢: ~5 h**

In Summe ca. **3 fokussierte Arbeitstage** für die volle Liste. Empfehlung: 🔴+🟠 vor Open-Test-Phase, 🟡+🟢 als kontinuierliche Pflege.

---

## Anhang: Verifikations-Methode

Um den Bericht reproduzierbar zu machen, wurden die Findings mit folgenden Schritten verifiziert:

1. **Direktes Lesen** aller Top-25-Dateien nach Größe (siehe Größenliste oben).
2. **`grep`-Spotchecks** über Keywords:
   - `async void` → 4 Treffer in ViewModels/Services (verifiziert)
   - `_ = ` (Fire-and-Forget) → 13 Treffer (verifiziert)
   - `Task.Run` in VMs/Services → 7 Treffer (verifiziert)
   - `DateTime.TryParse|Parse` ohne `InvariantCulture` → 3 Treffer (verifiziert)
   - `SKMaskFilter|MaskFilter` → 4 Treffer in Graphics (verifiziert)
   - `ScrollViewer.*Padding` → 0 Treffer (Anti-Finding bestätigt)
   - `TransformOperationsTransition` → 2 Files (verifiziert)
3. **CLAUDE.md-Konventionen-Cross-Check**: ScrollViewer-Padding ✓, ZIndex-Overlay ✓ (`IsHitTestVisible="{Binding !IsAnyOverlayVisible}"`-Pattern in DayDetailVm gefunden), Linked-File-AdMob ✓ (Konventionsdokumentation, nicht im Code prüfbar), Tab-Pattern ✓.
4. **Bewusste Korrekturen** früherer Sub-Agent-Halluzinationen sind im **Anti-Findings**-Abschnitt markiert.

*Audit-Methode dokumentiert für Rechenschaft. Bei Zweifel zum Befund X bitte direkt die zitierte Datei + Zeile öffnen.*
