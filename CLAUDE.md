# Meine Apps Avalonia - Projektübersicht

Multi-Plattform Apps (Android + Windows + Linux) mit Avalonia 11.3 + .NET 10.
Migriert von MAUI mit verbesserter UX, modernem Design und behobenen MAUI-Bugs.

---

## Build-Befehle

```bash
# Gesamte Solution bauen
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln

# Einzelne App (Shared/Desktop/Android) - {App} ersetzen
dotnet build src/Apps/{App}/{App}.Shared
dotnet run --project src/Apps/{App}/{App}.Desktop
dotnet build src/Apps/{App}/{App}.Android

# Desktop Release
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r win-x64
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r linux-x64

# Android Release (AAB)
dotnet publish src/Apps/{App}/{App}.Android -c Release

# AppChecker v2.0 (22 Checker, 150+ Pruefungen) - Alle 8 Apps / Einzelne App
dotnet run --project tools/AppChecker
dotnet run --project tools/AppChecker {App}

# StoreAssetGenerator - Alle / Gefiltert
dotnet run --project tools/StoreAssetGenerator
dotnet run --project tools/StoreAssetGenerator {Filter}

# SocialPostGenerator - Posts + Promo-Bilder
dotnet run --project tools/SocialPostGenerator
dotnet run --project tools/SocialPostGenerator post {App} <x|reddit>
dotnet run --project tools/SocialPostGenerator image <{App}|portfolio>
```

---

## Projektstruktur

```
F:\Meine_Apps_Ava\
├── MeineApps.Ava.sln
├── Directory.Build.props           # Globale Build-Settings
├── Directory.Packages.props        # Central Package Management
├── CLAUDE.md
├── Releases/
│   └── meineapps.keystore
│
├── src/
│   ├── Libraries/
│   │   ├── MeineApps.CalcLib/      # Calculator Engine (net10.0)
│   │   ├── MeineApps.Core.Ava/     # Themes, Services, Converters
│   │   └── MeineApps.Core.Premium.Ava/  # Ads, IAP, Trial
│   │
│   ├── UI/
│   │   └── MeineApps.UI/           # Shared UI Components
│   │
│   └── Apps/                       # 8 Apps, jeweils Shared/Android/Desktop
│       ├── RechnerPlus/            # Taschenrechner (werbefrei)
│       ├── ZeitManager/            # Timer/Stoppuhr/Alarm (werbefrei)
│       ├── FinanzRechner/          # 6 Finanzrechner + Budget-Tracker
│       ├── FitnessRechner/         # BMI/Kalorien/Barcode-Scanner
│       ├── HandwerkerRechner/      # 11 Bau-Rechner (5 Free + 6 Premium)
│       ├── WorkTimePro/            # Arbeitszeiterfassung + Export
│       ├── HandwerkerImperium/     # Idle-Game (Werkstaetten + Arbeiter)
│       └── BomberBlast/            # Bomberman-Klon (SkiaSharp, Landscape)
│
├── tools/
│   ├── AppChecker/              # 10 Check-Kategorien, 100+ Pruefungen
│   ├── StoreAssetGenerator/     # Play Store Assets (SkiaSharp)
│   └── SocialPostGenerator/     # Social-Media Posts + Promo-Bilder
│
└── tests/
```

---

## Status (17. Februar 2026)

Alle 8 Apps im geschlossenen Test, warten auf 12 Tester fuer Produktion.

| App | Version | Ads | Premium |
|-----|---------|-----|---------|
| RechnerPlus | v2.0.4 | Nein | Nein |
| ZeitManager | v2.0.4 | Nein | Nein |
| HandwerkerRechner | v2.0.4 | Banner + Rewarded | 3,99 remove_ads |
| FinanzRechner | v2.0.4 | Banner + Rewarded | 3,99 remove_ads |
| FitnessRechner | v2.0.4 | Banner + Rewarded | 3,99 remove_ads |
| WorkTimePro | v2.0.4 | Banner + Rewarded | 3,99/Mo oder 19,99 Lifetime |
| HandwerkerImperium | v2.0.8 | Banner + Rewarded | 4,99 Premium |
| BomberBlast | v2.0.7 | Banner + Rewarded | 1,99 remove_ads |

---

## 4 Themes

| Theme | Beschreibung |
|-------|--------------|
| Midnight (Default) | Dark, Indigo Primary |
| Aurora | Dark, Pink/Violet/Cyan Gradient |
| Daylight | Light, Blue Primary |
| Forest | Dark, Green Primary |

Implementierung: `MeineApps.Core.Ava/Themes/` - ThemeService laedt dynamisch via `app.Styles.Add(StyleInclude)`. KEIN statisches Theme in App.axaml. Lazy-Loading: Nur das aktive Theme wird geladen, weitere bei Bedarf.

---

## Packages (Avalonia 11.3.11)

| Package | Version | Zweck |
|---------|---------|-------|
| Avalonia | 11.3.11 | UI Framework |
| Material.Icons.Avalonia | 2.4.1 | 7000+ SVG Icons |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM |
| Xaml.Behaviors.Avalonia | 11.3.9.3 | Behaviors |
| SkiaSharp | 3.119.2 | 2D Graphics + SkSL GPU-Shader |
| SkiaSharp.Skottie | 3.119.2 | Lottie-Animations-Backend |
| Avalonia.Labs.Lottie | 11.3.1 | Lottie-Animationen (JSON) |
| Xamarin.Android.Google.BillingClient | 8.3.0.1 | Google Play Billing |
| sqlite-net-pcl | 1.9.172 | Database |

---

## Keystore

| Eigenschaft | Wert |
|-------------|------|
| Speicherort | `F:\Meine_Apps_Ava\Releases\meineapps.keystore` |
| Alias | `meineapps` |
| Passwort | `MeineApps2025` |

---

## Conventions & Patterns

### Naming Conventions

| Element | Convention | Beispiel |
|---------|-----------|----------|
| ViewModel | Suffix `ViewModel` | `MainViewModel`, `TileCalculatorViewModel` |
| View | Suffix `View` | `MainView.axaml`, `SettingsView.axaml` |
| Service Interface | `I{Name}Service` | `IThemeService`, `ILocalizationService` |
| Service Implementation | `{Name}Service` | `ThemeService`, `PreferencesService` |
| Events (Navigation) | `NavigationRequested` | `Action<string>` |
| Events (Messages) | `MessageRequested` | `Action<string, string>` |
| Events (UI-Feedback) | `FloatingTextRequested` | `EventHandler<(string, string)>` |
| Events (Celebration) | `CelebrationRequested` | `EventHandler` |

### DI-Pattern

**Service Lifetimes:**
- Services → Singleton (IPreferences, ITheme, ILocalization, Database)
- MainViewModel → Singleton (haelt Child-VMs)
- Child-ViewModels → Transient oder Singleton (je nach App)

**Constructor Injection (immer):**
- Child-VMs werden in MainViewModel per Constructor injiziert
- Keine Property Injection, keine Service-Locator

**Android Platform-Services (Factory-Pattern):**
```csharp
// App.axaml.cs
public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }
// MainActivity.cs
App.RewardedAdServiceFactory = sp => new AndroidRewardedAdService(helper, sp.GetRequiredService<IPurchaseService>());
```

### Localization Pattern

- ResourceManager-basiert via `ILocalizationService.GetString("Key")`
- AppStrings.Designer.cs: Manuell erstellt (nicht auto-generiert bei CLI-Build)
- 6 Sprachen: DE, EN, ES, FR, IT, PT
- `LanguageChanged` Event → MainViewModel benachrichtigt alle Child-VMs via `UpdateLocalizedTexts()`
- Alle View-Strings lokalisiert (keine hardcodierten Texte)

### Navigation Pattern (Event-basiert, kein Shell)

```csharp
// Child-ViewModel
public event Action<string>? NavigationRequested;
NavigationRequested?.Invoke("route");

// MainViewModel
_childVM.NavigationRequested += route => CurrentPage = route;
```
- `".."` = zurueck zum Parent
- `"../subpage"` = zum Parent, dann zu subpage

### Android Back-Button Pattern (einheitlich, alle 8 Apps)

```csharp
// MainViewModel: Double-Back-to-Exit Logik
public event Action<string>? ExitHintRequested;
private DateTime _lastBackPress = DateTime.MinValue;
private const int BackPressIntervalMs = 2000;

public bool HandleBackPressed()
{
    // 1. App-spezifische Overlays/Dialoge schließen
    // 2. Sub-Navigation zurueck
    // 3. Double-Back-to-Exit
    var now = DateTime.UtcNow;
    if ((now - _lastBackPress).TotalMilliseconds < BackPressIntervalMs) return false;
    _lastBackPress = now;
    ExitHintRequested?.Invoke(_localization.GetString("PressBackAgainToExit") ?? "...");
    return true;
}
```

```csharp
// MainActivity: VM holen, Event verdrahten, OnBackPressed delegieren
_mainVm = App.Services.GetService<MainViewModel>();
if (_mainVm != null)
    _mainVm.ExitHintRequested += msg =>
        RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());

public override void OnBackPressed()
{
    if (_mainVm != null && _mainVm.HandleBackPressed()) return;
    base.OnBackPressed();
}
```

### Error-Handling Pattern

```csharp
// MessageRequested statt Debug.WriteLine
public event Action<string, string>? MessageRequested;
try { /* ... */ }
catch (Exception) { MessageRequested?.Invoke("Fehler", "Speichern fehlgeschlagen"); }
```

### DateTime Pattern

- **Persistenz**: IMMER `DateTime.UtcNow` (NIE `DateTime.Now`)
- **Format**: ISO 8601 "O" → `dateTime.ToString("O")`
- **Parse**: IMMER `DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`
- **Tages-Tracking**: `DateTime.Today` fuer datumsbasierte Gruppierung

### Thread-Safety

```csharp
// Async: SemaphoreSlim
private readonly SemaphoreSlim _semaphore = new(1, 1);
await _semaphore.WaitAsync();
try { /* ... */ } finally { _semaphore.Release(); }

// UI-Thread: Dispatcher
Dispatcher.UIThread.Post(() => { SomeProperty = newValue; });
```

### UriLauncher (Plattformuebergreifend)

- `UriLauncher.OpenUri(uri)` statt `Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })`
- Desktop: Fallback auf Process.Start
- Android: `PlatformOpenUri` wird in MainActivity auf `Intent.ActionView` gesetzt
- Datei: `MeineApps.Core.Ava/Services/UriLauncher.cs`

### Tab-Navigation (UI)

- MainView: `Border.TabContent` + `.Active` CSS-Klassen
- Tab-Switching via `IsXxxActive` bool Properties im MainViewModel
- Fade-Transition: `DoubleTransition` auf Opacity (150ms)
- Border wrapping noetig (Child-Views haben eigenen DataContext/ViewModel)

---

## Ad-Banner Layout (WICHTIG)

- **MainView Grid**: `RowDefinitions="*,Auto,Auto"` → Row 0 Content, Row 1 Ad-Spacer (64dp), Row 2 Tab-Bar
- **Ad-Spacer 64dp**: Adaptive Banner (`GetCurrentOrientationAnchoredAdaptiveBannerAdSize`) können 50-60dp+ hoch sein → 64dp als sicherer Spacer
- **Jeder MainViewModel**: Muss `_adService.ShowBanner()` explizit aufrufen (AdMobHelper verschluckt Fehler)
- **ScrollViewer Bottom-Margin**: Mindestens 60dp Margin (NICHT Padding!) auf dem Kind-Element in ALLEN scrollbaren Sub-Views
- **Tab-Bar Heights**: FinanzRechner/FitnessRechner/HandwerkerRechner/WorkTimePro=56, HandwerkerImperium=64, BomberBlast=0

## AdMob

### Linked-File-Pattern
- `AdMobHelper.cs` + `RewardedAdHelper.cs` + `AndroidRewardedAdService.cs` + `AndroidFileShareService.cs` + `AndroidPurchaseService.cs` in Premium-Library unter `Android/`
- Per `<Compile Include="..." Link="..." />` in jedes Android-Projekt eingebunden
- `<Compile Remove="Android\**" />` verhindert Kompilierung im net10.0 Library-Projekt
- **UMP Namespace-Typo**: `Xamarin.Google.UserMesssagingPlatform` (DREIFACHES 's')
- **Java Generics Erasure**: RewardedAdHelper.LoadCallback braucht `[Register]`-Attribut

### Rewarded Ads Multi-Placement
- `AdConfig.cs`: 28 Rewarded Ad-Unit-IDs (6 Apps)
- `ShowAdAsync(string placement)` → placement-spezifische Ad-Unit-ID via AdConfig
- Jede App hat `RewardedAdServiceFactory` Property in App.axaml.cs

### Google Play Billing (In-App Purchases)
- `AndroidPurchaseService.cs`: Erbt von `PurchaseService`, implementiert Google Play Billing Client v8
- `Xamarin.Android.Google.BillingClient` 8.3.0.1 (in Directory.Packages.props)
- Jede App hat `PurchaseServiceFactory` Property in App.axaml.cs (analog zu RewardedAdServiceFactory)
- **Java-Callback-Pattern:** `IBillingClientStateListener` und `IPurchasesUpdatedListener` als innere Klassen (erben von `Java.Lang.Object`)
- Unterstützt InApp (non-consumable + consumable), Subscriptions, Auto-Reconnect

### Publisher-Account
- **ca-app-pub-2588160251469436** fuer alle 6 werbe-unterstuetzten Apps
- RechnerPlus + ZeitManager sind werbefrei

---

## Desktop Publishing

### Windows
```bash
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r win-x64
# Ausgabe: src/Apps/{App}/{App}.Desktop/bin/Release/net10.0/win-x64/publish/
```

### Linux
```bash
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r linux-x64
# Ausgabe: src/Apps/{App}/{App}.Desktop/bin/Release/net10.0/linux-x64/publish/
```

### Android (AAB fuer Play Store)
```bash
dotnet publish src/Apps/{App}/{App}.Android -c Release
# Ausgabe: src/Apps/{App}/{App}.Android/bin/Release/net10.0-android/publish/
```

---

## Troubleshooting

| Problem | Ursache | Loesung |
|---------|---------|---------|
| Material Icons unsichtbar | `MaterialIconStyles` nicht in App.axaml registriert | `<materialIcons:MaterialIconStyles />` in `<Application.Styles>` |
| AdMob Crash auf Android | UMP Namespace hat Typo | `Xamarin.Google.UserMesssagingPlatform` (3x 's') |
| DateTime Timer falsch (1h) | UTC→Lokal Konvertierung | `DateTimeStyles.RoundtripKind` bei Parse |
| Release-Build crasht (Debug OK) | Meist stale Build-Artefakte oder falsche Flags | obj/bin löschen, clean rebuild |
| Mono JIT Assertion `!ji->async` Crash | Profiled AOT (SDK-Default) kompiliert nur hot Methods, Rest fällt auf JIT zurück → JIT-Bug auf manchen Geräten (z.B. Huawei P30) | `AndroidEnableProfiledAot=false` in Directory.Build.targets → Full AOT (alle Methoden kompiliert, kein JIT). `UseInterpreter=true` geht NICHT zusammen mit AOT (XA0119) |
| SKCanvasView updatet nicht | `InvalidateVisual()` verwendet | `InvalidateSurface()` verwenden |
| SKCanvasView leer bei IsVisible-Toggle | `InvalidateSurface()` auf unsichtbare Canvas wird ignoriert | Nach Sichtbar-Werden erneut Daten setzen/Calculate() aufrufen, damit PropertyChanged → InvalidateSurface() feuert |
| CSS translate() Exception | Fehlende px-Einheiten | `translate(0px, 400px)` statt `translate(0, 400)` |
| AAPT2260 Fehler | grantUriPermissions ohne 's' | `android:grantUriPermissions="true"` (mit 's') |
| ${applicationId} geht nicht | .NET Android kennt keine Gradle-Placeholder | Hardcodierte Package-Namen verwenden |
| Icons in Tab-Leiste fehlen | Material.Icons xmlns fehlt | `xmlns:materialIcons="using:Material.Icons.Avalonia"` |
| VersionCode Ablehnung | Code bereits im Play Store | VOR Release aktuelle Codes im Play Store pruefen |
| Ads Error Code 0 + "Failed to instantiate ClientApi" | Ads vor SDK-Init geladen | `Initialize(activity, callback)` nutzen, Ads erst im Callback laden |
| Release-App schließt sich beim 1. Start (VS) | VS kann in Release keinen Debugger anhängen | App manuell starten - funktioniert. Kein App-Bug, VS-Verhalten |
| Process.Start PlatformNotSupportedException | Android unterstuetzt UseShellExecute nicht | `UriLauncher.OpenUri(uri)` verwenden (MeineApps.Core.Ava) |
| `\u20ac` als Text in XAML | XAML interpretiert C#-Unicode-Escapes nicht | Direkt `€` schreiben oder `&#x20AC;` verwenden |
| TransformOperations CS0103 | `Avalonia.Media` reicht nicht | `using Avalonia.Media.Transformation;` hinzufügen |
| IsAnimating Property-Warnung | Kollidiert mit `AvaloniaObject.IsAnimating()` | Property umbenennen (z.B. `IsPulsing`) oder `new` Keyword |
| KeyFrame-Animation Crash "No animator" | `Style.Animations` hat keinen Animator fuer `RenderTransform` | NUR `Opacity`/`Width`/`Height` (double) in KeyFrames verwenden. `TransformOperationsTransition` in `Transitions` funktioniert |
| TapScaleBehavior Crash "InvalidCastException" | `animation.RunAsync(ScaleTransform)` crasht in `TransformAnimator.Apply` | DispatcherTimer-basierte Animation statt Animation API fuer ScaleTransform verwenden |
| Content hinter Ad-Banner abgeschnitten | Ad-Spacer 50dp, aber adaptive Banner 50-60dp+ | Ad-Spacer auf 64dp erhoehen (alle 6 MainViews). Adaptive Banner variieren je nach Geraet |
| Style Selector AVLN2200 "Can not find parent" | `#Name` ohne Typ-Prefix | IMMER `Typ#Name` schreiben: `Grid#ModeSelector`, `Border#DisplayBorder` |
| Enum-Werte englisch in UI | Direktes Binding an Enum-Property | Display-Property mit lokalisiertem Text verwenden, im ViewModel per `GetString()` setzen |
| Daten erscheinen kurz, verschwinden | `_ = InitializeAsync()` mit `_list.Clear()` raced mit User-Aktionen | Task speichern: `_initTask = InitializeAsync()`, in Methoden `await _initTask` |
| Sprache immer Englisch trotz Geräteeinstellung | Erkannte Sprache nicht in Preferences gespeichert | `_preferences.Set(key, lang)` nach Gerätesprach-Erkennung in `Initialize()` |
| `IsAttachedToVisualTree` Kompilierfehler | Property in Avalonia 11.3 entfernt | `using Avalonia.VisualTree;` + `control.GetVisualRoot() != null` verwenden |
| InsertAsync gibt falsche ID zurück | sqlite-net `InsertAsync()` gibt Zeilen-Count zurück (immer 1), NICHT Auto-Increment-ID. sqlite-net setzt ID direkt auf dem Objekt | Nach `await db.InsertAsync(entity)` NICHT `entity.Id = result` schreiben - `entity.Id` ist bereits korrekt gesetzt |
| ScrollViewer scrollt nicht | `Padding` auf `ScrollViewer` verhindert Scrollen in Avalonia | `Padding` entfernen, stattdessen `Margin` auf das direkte Kind-Element setzen + `VerticalScrollBarVisibility="Auto"` |
| DonutChart-Segment unsichtbar bei 100% | SkiaSharp `ArcTo` bei 360° erzeugt leeren Path (Start=Ende) | Bei `sweepAngle >= 359°` in zwei 180°-Hälften aufteilen |

---

## Releases
- **Alle im geschlossenen Test**
- Keystore: `F:\Meine_Apps_Ava\Releases\meineapps.keystore` (Alias: meineapps, Pwd: MeineApps2025)
- Store-Assets: `Releases/{AppName}/` (via StoreAssetGenerator)
