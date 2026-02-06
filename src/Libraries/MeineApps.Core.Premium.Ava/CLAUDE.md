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
│   └── AdMobHelper.cs         # Native AdMob banner (linked file, NOT compiled here)
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

## Apps
| App | Premium | Modell |
|-----|---------|--------|
| RechnerPlus | Nein | Kostenlos |
| ZeitManager | Nein | Kostenlos |
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

**WICHTIG:** RechnerPlus und ZeitManager referenzieren NICHT Core.Premium!

## Android AdMob Integration (07.02.2026)

### AdMobHelper.cs (Linked File Pattern)
- Lebt in `Android/AdMobHelper.cs` aber wird via `<Compile Remove="Android\**" />` NICHT im net10.0 Library-Projekt kompiliert
- Stattdessen wird es per `<Compile Include="..." Link="Services\AdMobHelper.cs" />` in jedes Android-Projekt eingebunden
- **Namespace:** `MeineApps.Core.Premium.Ava.Droid`

### Ad Placement: FrameLayout Overlay
- Banner-Ad wird als nativer Android FrameLayout-Overlay positioniert (kein LinearLayout-Wrapper)
- `GravityFlags.Bottom | GravityFlags.CenterHorizontal` mit `BottomMargin = tabBarHeightDp * density`
- Positioniert die Werbung direkt UEBER der Avalonia Tab-Bar
- `AdInsetListener` passt BottomMargin fuer Navigation-Bar-Insets an (Edge-to-Edge)

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

### NuGet Packages (in Directory.Packages.props)
- `Xamarin.GooglePlayServices.Ads.Lite` 124.0.0.4
- `Xamarin.Google.UserMessagingPlatform` 4.0.0.1
- `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` 1.10.0.1 (D8 Duplicate Fix)

### D8 Duplicate Class Fix
- `Directory.Build.targets`: `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` mit `ExcludeAssets="all" PrivateAssets="all"` fuer Android-Projekte
- Behebt Konflikt zwischen `...Annotation.Jvm` und `...Annotation.Android` Transitiv-Abhaengigkeiten

### AdConfig.cs - Echte Ad-Unit-IDs
- 2 Publisher-Accounts: `ca-app-pub-2667921454778639` (FinanzRechner, FitnessRechner, HandwerkerRechner) und `ca-app-pub-2588160251469436` (WorkTimePro, BomberBlast, HandwerkerImperium)
- Alle 6 Apps haben echte Banner-IDs + App-IDs in AdConfig.cs und AndroidManifest.xml
