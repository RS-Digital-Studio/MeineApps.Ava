# MeineApps.Core.Premium.Ava - Premium Features Library

## Zweck
Monetarisierungs-Library fuer Avalonia Apps (Ads, IAP, Trial):
- Ad State Management (AdMob)
- In-App Purchases State Management
- 14-Day Trial System
- AdBannerView Control

**Depends on:** MeineApps.Core.Ava (IPreferencesService)

## Struktur
```
MeineApps.Core.Premium.Ava/
├── Android/
│   ├── AdMobHelper.cs              # Native AdMob banner (linked file, NOT compiled here)
│   ├── RewardedAdHelper.cs         # Rewarded Ad helper (linked file, NOT compiled here)
│   ├── AndroidRewardedAdService.cs # IRewardedAdService Android-Impl (linked file, NOT compiled here)
│   ├── AndroidPurchaseService.cs   # Google Play Billing Client (linked file, NOT compiled here)
│   └── AndroidPlayGamesService.cs # Google Play Games Services v2 (linked file, NOT compiled here)
├── Services/
│   ├── IAdService.cs          # Ad service interface
│   ├── IPurchaseService.cs    # Purchase service interface
│   ├── ITrialService.cs       # Trial service interface
│   ├── AdConfig.cs            # AdMob IDs for all apps (real IDs)
│   ├── AdMobService.cs        # Ad state management
│   ├── PurchaseService.cs     # Purchase state (virtual methods for platform-specific billing)
│   └── TrialService.cs        # 14-day trial via Preferences
├── Controls/
│   ├── AdBannerView.axaml     # Ad banner placeholder control
│   └── AdBannerView.axaml.cs  # Code-behind with StyledProperties
└── Extensions/
    └── ServiceCollectionExtensions.cs  # DI: AddMeineAppsPremium()
```

## Verwendung
```csharp
// App.axaml.cs
services.AddMeineAppsPremium();

// oder mit eigener PurchaseService-Implementierung:
services.AddMeineAppsPremium<AndroidPurchaseService>();
```

## Unterschiede zu MAUI-Version
- Nutzt IPreferencesService statt Preferences.Default
- PurchaseService hat virtual Purchase-Methoden (plattformspezifisch ueberschreibbar)
- AdBannerView ist Avalonia UserControl statt MAUI ContentView
- Kein Plugin.InAppBilling direkt - Billing wird in Android-Projekten implementiert
- DI via IServiceCollection statt MauiAppBuilder

## Apps (nur werbe-unterstuetzte)
| App | Premium | Modell |
|-----|---------|--------|
| HandwerkerRechner | Ja | 3,99 EUR remove_ads |
| FinanzRechner | Ja | 3,99 EUR remove_ads |
| FitnessRechner | Ja | 3,99 EUR remove_ads |
| WorkTimePro | Ja | 3,99 EUR/Mo oder 19,99 EUR Lifetime |
| HandwerkerImperium | Ja | 4,99 EUR Premium |
| BomberBlast | Ja | 3,99 EUR remove_ads |

## Product IDs
- `remove_ads` - Legacy non-consumable
- `premium_monthly` - Abo (WorkTimePro)
- `premium_lifetime` - Einmalkauf (WorkTimePro)

**WICHTIG:** RechnerPlus und ZeitManager sind werbefrei und referenzieren NICHT Core.Premium! Keine Ad-IDs in AdConfig.cs fuer diese Apps.

## Android AdMob Integration (07.02.2026)

### AdMobHelper.cs (Linked File Pattern)
- Lebt in `Android/AdMobHelper.cs` aber wird via `<Compile Remove="Android\**" />` NICHT im net10.0 Library-Projekt kompiliert
- Stattdessen wird es per `<Compile Include="..." Link="Services\AdMobHelper.cs" />` in jedes Android-Projekt eingebunden
- **Namespace:** `MeineApps.Core.Premium.Ava.Droid`

### Ad Placement: FrameLayout Overlay
- Banner-Ad wird als nativer Android FrameLayout-Overlay positioniert (kein LinearLayout-Wrapper)
- **Standard**: `GravityFlags.Bottom | GravityFlags.CenterHorizontal` mit `BottomMargin = tabBarHeightDp * density`
- **Top-Position**: `IAdService.SetBannerPosition(true)` wechselt auf `GravityFlags.Top` (z.B. BomberBlast GameView)
- Positioniert die Werbung direkt UEBER der Avalonia Tab-Bar (Bottom) oder am oberen Rand (Top)
- `AdInsetListener` passt BottomMargin fuer Navigation-Bar-Insets an (Edge-to-Edge)
- `OnAdsStateChanged` reagiert auf `BannerVisible` (Show/Hide) UND `IsBannerTop` (Position-Wechsel)
- **Adaptive Banner-Hoehe**: `GetCurrentOrientationAnchoredAdaptiveBannerAdSize` erzeugt Banner mit variabler Hoehe (50-60dp+ je nach Geraet). Avalonia Ad-Spacer muss 64dp sein (nicht 50dp)!

### Tab-Bar-Hoehen (tabBarHeightDp Parameter)
| App | tabBarHeightDp | Grund |
|-----|---------------|-------|
| FinanzRechner | 56 | Buttons Height=56 |
| FitnessRechner | 56 | Buttons Height=56 |
| HandwerkerRechner | 56 | Buttons Height=56 |
| WorkTimePro | 56 | ~55dp (Padding 8+8 + Content) |
| HandwerkerImperium | 64 | MinHeight=48 + Padding 16 |
| BomberBlast | 0 | Keine Tabs (Landscape-Spiel) |

### UMP (GDPR Consent)
- **C# Namespace hat Typo:** `Xamarin.Google.UserMesssagingPlatform` (DREIFACHES 's')
- `ConsentRequestParameters` + `UserMessagingPlatform.LoadAndShowConsentFormIfRequired()`
- Zeigt GDPR-Consent-Dialog fuer EU-Nutzer
- **SDK-Init-Callback (10.02.2026):** `Initialize(activity, onComplete)` nutzt `IOnInitializationCompleteListener` - Ads duerfen erst nach Callback geladen werden
- `AttachToActivity` erstellt Layout + laedt Banner sofort (innerhalb Init-Callback)
- `RequestConsent()` zeigt nur GDPR-Form, keine Ad-Logik mehr im Consent-Callback
- Fehler werden geloggt (ConsentFailureListener + ConsentFormDismissedListener), nicht verschluckt

### NuGet Packages (in Directory.Packages.props)
- `Xamarin.GooglePlayServices.Ads.Lite` 124.0.0.4
- `Xamarin.Google.UserMessagingPlatform` 4.0.0.1
- `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` 1.10.0.1 (D8 Duplicate Fix)

### D8 Duplicate Class Fix
- `Directory.Build.targets`: `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` mit `ExcludeAssets="all" PrivateAssets="all"` fuer Android-Projekte
- Behebt Konflikt zwischen `...Annotation.Jvm` und `...Annotation.Android` Transitiv-Abhaengigkeiten

### AdConfig.cs - Echte Ad-Unit-IDs
- 1 Publisher-Account: `ca-app-pub-2588160251469436` (alle 6 Apps)
- Alle 6 Apps haben echte Banner-IDs + Rewarded-IDs + App-IDs in AdConfig.cs und AndroidManifest.xml

### Rewarded Ads (07.02.2026)

#### RewardedAdHelper.cs (Linked File Pattern)
- Lebt in `Android/RewardedAdHelper.cs`, wird per `<Compile Include>` in jedes Android-Projekt eingebunden
- **Namespace:** `MeineApps.Core.Premium.Ava.Droid`
- Load(Activity, adUnitId) + ShowAsync() → Task<bool> (true = Belohnung verdient)
- Automatisches Nachladen nach Ad-Dismiss
- **Retry mit exponentiellem Backoff (28.02.2026):** Bei fehlgeschlagenem Pre-Load: 5s → 15s → 30s (max 3 Versuche). `ScheduleRetry()` + `_retryCount` Feld. Reset bei erfolgreichem Load oder nach Ad-Dismiss
- **KRITISCHER JNI Fix (28.02.2026):** `FixedRewardedAdLoadCallback` abstrakte Basis-Klasse mit korrektem JNI Delegate-Wiring. Behebt Xamarin Binding Bug #425 (https://github.com/xamarin/GooglePlayServicesComponents/issues/425):
  - **Problem:** `RewardedAdLoadCallback` erbt von `AdLoadCallback<RewardedAd>`. Java Generics Erasure macht `onAdLoaded(RewardedAd)` zu `onAdLoaded(Object)`. Die Xamarin-Binding generiert `[Register("onAdLoaded", "...", "")]` mit leerem Connector → JNI Native Delegate wird NIE verdrahtet → C# `OnAdLoaded` wird nie aufgerufen → `_rewardedAd` bleibt null → 100% Match Rate aber 0 Impressions
  - **Fix:** `JNINativeWrapper.CreateDelegate()` + `GetOnAdLoadedHandler` Connector + `n_OnAdLoaded` statische Methode + `[Register("onAdLoaded", "(Lcom/google/android/gms/ads/rewarded/RewardedAd;)V", "GetOnAdLoadedHandler")]` mit echtem Connector-String
  - **Betrifft:** Alle 6 werbe-unterstützten Apps (LoadCallback + OnDemandLoadCallback erben beide von FixedRewardedAdLoadCallback)

#### AndroidRewardedAdService.cs (Linked File Pattern)
- Lebt in `Android/AndroidRewardedAdService.cs`, wird per `<Compile Include>` in jedes Android-Projekt eingebunden
- Constructor: `(RewardedAdHelper helper, IPurchaseService purchaseService)`
- Premium-Nutzer sehen keine Ads (IsAvailable = false)
- Implementiert `IRewardedAdService`

#### DI-Integration in Apps
- Jede App hat in `App.axaml.cs`: `static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory`
- Nach `services.AddMeineAppsPremium()` wird Factory als DI-Override registriert (wenn gesetzt)
- `MainActivity.cs` erstellt RewardedAdHelper, setzt Factory VOR base.OnCreate(), laedt Ad NACH DI-Build
- Lazy Resolution: IPurchaseService wird erst beim ersten Aufruf ueber IServiceProvider aufgeloest

#### IRewardedAdService Interface (Services/)
- `IsAvailable`: bool - Ob Rewarded Ad geladen und bereit ist
- `ShowRewardedAdAsync()`: Task<bool> - Zeigt Ad, gibt true bei Belohnung zurueck
- Premium-Nutzer: IsAvailable immer false (keine Ads)

#### RewardedAdService (Services/) - Desktop Fallback
- Simuliert Rewarded Ads auf Desktop (Task.Delay + immer true)
- Wird ueberschrieben durch AndroidRewardedAdService auf Android

#### DI-Registrierung
- `AddMeineAppsPremium()` registriert `IRewardedAdService` als Singleton (RewardedAdService)
- Apps ueberschreiben via `RewardedAdServiceFactory` Property fuer Android-Implementierung

### Google Play Billing Client (17.02.2026)

#### AndroidPurchaseService.cs (Linked File Pattern)
- Lebt in `Android/AndroidPurchaseService.cs`, wird per `<Compile Include>` in jedes Android-Projekt eingebunden
- **Namespace:** `MeineApps.Core.Premium.Ava.Droid`
- Erbt von `PurchaseService` (C#-Klasse) und überschreibt alle 5 virtuellen Kauf-Methoden
- **Java-Callback-Pattern:** `IPurchasesUpdatedListener` und `IBillingClientStateListener` erben von `IJavaPeerable` → innere Klassen (`BillingStateListener`, `PurchaseUpdateListener`) die von `Java.Lang.Object` erben (gleich wie `RewardedAdHelper.LoadCallback`)
- Constructor: `(Activity, IPreferencesService, IAdService)`
- Auto-Reconnect mit exponentiellem Backoff (max 5 Versuche)
- Unterstützt InApp (non-consumable + consumable) und Subscriptions
- **NuGet:** `Xamarin.Android.Google.BillingClient` 8.3.0.1

#### BillingClient v8 API-Besonderheiten
- `EnablePendingPurchases(PendingPurchasesParams)` - parameterlose Variante entfernt in v8
- `QueryPurchasesResult.Purchases` (nicht `PurchasesList`)
- `Android.BillingClient.Api.PurchaseState.Purchased` (nicht `Purchase.PurchaseStateCode.Purchased`)

#### DI-Integration (analog zu RewardedAdServiceFactory)
- Jede App hat in `App.axaml.cs`: `static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory`
- Nach `services.AddMeineAppsPremium()` wird Factory als DI-Override registriert (wenn gesetzt)
- `MainActivity.cs` setzt Factory VOR `base.OnCreate()`

### Changelog (Premium Library)

- **28.02.2026**: **KRITISCHER FIX: Rewarded Ads OnAdLoaded JNI-Callback** – FixedRewardedAdLoadCallback mit korrektem JNI Delegate-Wiring (GetOnAdLoadedHandler Connector statt leerer String). Behebt 0 Impressions trotz 100% Match Rate in ALLEN 6 Apps. + Retry mit exponentiellem Backoff (5s/15s/30s, max 3 Versuche). + 2 fehlende BomberBlast Placements (lucky_spin, dungeon_run) in AdConfig.cs.
- **17.02.2026**: AndroidPurchaseService mit Google Play Billing Client v8.3.0.1 integriert. Ersetzt AIDL-basierte Billing. Alle 6 Premium-Apps verwenden jetzt echte Google Play Billing Library.
- **10.02.2026**: TrialService DateTime.Now → DateTime.UtcNow + TryParse mit CultureInfo.InvariantCulture + DateTimeStyles.RoundtripKind. AdMobHelper TestDeviceId nur noch in DEBUG-Builds registriert.
