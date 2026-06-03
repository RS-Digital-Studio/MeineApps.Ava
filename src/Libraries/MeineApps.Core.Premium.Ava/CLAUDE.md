# MeineApps.Core.Premium.Ava — Monetization Library

Monetarisierungs-Library für die werbe-unterstützten Apps: AdMob (Banner + Rewarded), Google Play Billing v8, 14-Tage-Trial und In-App Review. Aktuell konsumieren HandwerkerRechner, FinanzRechner, FitnessRechner, WorkTimePro, HandwerkerImperium und BomberBlast die echten Ad-IDs; RebornSaga ist verdrahtet, aber die AdMob-IDs sind noch Platzhalter (`AdConfig.RebornSaga`). RechnerPlus und ZeitManager referenzieren die Library **nicht**.

Android-Linked-Files liegen im Namespace `MeineApps.Core.Premium.Ava.Droid`, der reine .NET-Code in `MeineApps.Core.Premium.Ava.Services`/`.Extensions`/`.Controls`.

## Zielframework

- .NET 10.0 (Library) — Android-Klassen sind Linked Files mit `<Compile Remove="Android\**" />` in der Library
- C# 14
- Android-Code wird per `<Compile Include="…" Link="…" />` in jedes `{App}.Android` eingebunden (siehe Linked-File-Pattern unten)

## Build

```bash
dotnet build src/Libraries/MeineApps.Core.Premium.Ava/MeineApps.Core.Premium.Ava.csproj
```

## Abhängigkeiten

| Package | Zweck |
|---------|-------|
| `MeineApps.Core.Ava` | `IPreferencesService` für Premium-State-Persistenz |
| `Xamarin.Google.UserMessagingPlatform` | GDPR-Consent (UMP) — Namespace-Typo siehe Gotchas |
| `Xamarin.GoogleAndroid.Ads.MobileAds.Lite` | AdMob Banner + Rewarded |
| `Xamarin.Android.Google.BillingClient` | Google Play Billing Client v8 |
| `Xamarin.Google.Android.Play.Core` | In-App Review |
| `Xamarin.GooglePlayServices.Games.V2` | Google Play Games Services v2 |

Versionen zentral in `Directory.Packages.props`.

---

## Komponenten

| Datei | Typ | Zweck |
|-------|-----|-------|
| `Services/IAdService.cs` | Interface | Banner + Rewarded Ad State |
| `Services/AdMobService.cs` | Service | Ad-State-Verwaltung (Singleton) |
| `Services/AdConfig.cs` | Konfiguration | Alle AdMob-IDs der 6 Apps (1 Publisher-Account) |
| `Services/IRewardedAdService.cs` | Interface | `IsAvailable`, `ShowAdAsync()` / `ShowAdAsync(placement)`, `Disable()`, `AdUnavailable`-Event |
| `Services/RewardedAdService.cs` | Desktop-Fallback | Simuliert Rewarded Ads (immer true) |
| `Services/IPurchaseService.cs` | Interface | Kauf, Restore, IsAvailable |
| `Services/PurchaseService.cs` | Basis-Klasse | Preference-basierter State, virtuelle Kauf-Methoden |
| `Services/ITrialService.cs` | Interface | Trial-Status |
| `Services/TrialService.cs` | Service | 14-Tage-Trial via Preferences + UTC-Timestamps |
| `Controls/AdBannerView.axaml` | Control | Avalonia Placeholder (nativer Ad sitzt als FrameLayout darüber) |
| `Extensions/ServiceCollectionExtensions.cs` | DI | `AddMeineAppsPremium()` |
| `Android/AdMobHelper.cs` | Linked File | Nativer Android Banner + GDPR-Consent (UMP) |
| `Android/RewardedAdHelper.cs` | Linked File | Rewarded Ad Lifecycle + JNI-Fix + Retry |
| `Android/AndroidRewardedAdService.cs` | Linked File | `IRewardedAdService` Android-Implementierung |
| `Android/AndroidPurchaseService.cs` | Linked File | Google Play Billing Client v8 |
| `Android/AndroidPlayGamesService.cs` | Linked File | Google Play Games Services v2 (Achievements + Leaderboards) |
| `Android/AndroidFileShareService.cs` | Linked File | Native Share-Sheet via `Intent.ActionSend` (für `UriLauncher.ShareText`) |

---

## Apps und Monetarisierungsmodell

| App | Premium-Produkt | Modell |
|-----|----------------|--------|
| HandwerkerRechner | `remove_ads` | 3,99 EUR Einmalkauf |
| FinanzRechner | `remove_ads` | 3,99 EUR Einmalkauf |
| FitnessRechner | `remove_ads` | 3,99 EUR Einmalkauf |
| WorkTimePro | `premium_monthly` / `premium_lifetime` | 3,99 EUR/Mo oder 19,99 EUR |
| HandwerkerImperium | `remove_ads` | 4,99 EUR Einmalkauf |
| BomberBlast | `remove_ads` | 1,99 EUR Einmalkauf |

RechnerPlus und ZeitManager sind werbefrei und referenzieren diese Library **nicht**.

---

## Architektur-Patterns

### Linked-File-Pattern (Android-Dateien)

Die `Android/`-Dateien werden im Library-Projekt selbst **nicht** kompiliert:

```xml
<!-- Library .csproj -->
<Compile Remove="Android\**" />
```

Jedes Android-App-Projekt bindet sie explizit ein:

```xml
<!-- {App}.Android.csproj -->
<Compile Include="..\..\..\..\..\Libraries\MeineApps.Core.Premium.Ava\Android\AdMobHelper.cs"
         Link="Services\AdMobHelper.cs" />
```

Warum: Die Android-Typen (`Activity`, `View`, `BillingClient`) existieren nur im `net10.0-android`-TFM. Das Library-Projekt zielt auf `net10.0`, kann diese Typen also nicht referenzieren. Linked Files lösen das ohne Code-Duplikation.

### Factory-Override nach `AddMeineAppsPremium()`

Generisches Factory-Pattern (statische `App.*Factory`-Properties, gesetzt in `MainActivity.cs`) → Root-CLAUDE.md, Abschnitt "Android Platform-Services". Library-spezifisch ist nur, **wie** der Desktop-Default überschrieben wird:

1. `AddMeineAppsPremium()` (in `Extensions/ServiceCollectionExtensions.cs`) registriert die **Desktop-Defaults** als Singletons: `AdMobService`, `PurchaseService` (Stub), `TrialService`, `RewardedAdService` (Simulator). Es kennt die `App.*Factory`-Properties **nicht**.
2. Den Android-Override macht jede App selbst in ihrem `ConfigureServices` (`App.axaml.cs`), **nach** `AddMeineAppsPremium()` — die zuletzt registrierte `AddSingleton`-Zuweisung gewinnt:

```csharp
// App.axaml.cs (Shared) — ConfigureServices
services.AddMeineAppsPremium();
if (RewardedAdServiceFactory != null)
    services.AddSingleton<IRewardedAdService>(sp => RewardedAdServiceFactory!(sp));
if (PurchaseServiceFactory != null)
    services.AddSingleton<IPurchaseService>(sp => PurchaseServiceFactory!(sp));
```

Für eine app-eigene Purchase-Implementierung ohne Factory existiert zusätzlich
`AddMeineAppsPremium<TPurchaseService>()`.

### AdConfig Multi-Placement

`AdConfig.cs` enthält alle AdMob-IDs aller Apps unter einem Publisher-Account (`ca-app-pub-2588160251469436`). Jede App hat eine eigene `static`-Klasse mit `AppId`, `BannerAdUnitId` und placement-spezifischen Rewarded-IDs. Im `#if DEBUG`-Pfad liefern `GetBannerAdUnitId`/`GetRewardedAdUnitId` immer die Google-Test-IDs — Produktions-IDs nur im Release-Build.

```csharp
var adUnitId = AdConfig.GetRewardedAdUnitId("BomberBlast", placement);
```

`GetRewardedAdUnitId(appName, placement)` matcht per `(appName, placement)`-Tupel. Unbekanntes Placement → App-Default-Placement (`(app, _)`-Arm); unbekannte App oder leere ID → Google-Test-ID (nie `null`, nie leer). **Folge:** Ein vertipptes Placement liefert still eine falsche/Default-Ad statt zu fehlschlagen — neue Placements daher zwingend mit eigenem Arm in `AdConfig.cs` eintragen. Mehrere Placements dürfen sich vorübergehend eine ID teilen (z.B. `RewardedRushBoost = RewardedGoldenScrews`), bis eigene IDs im AdMob-Dashboard existieren.

### Banner-Positionierung (FrameLayout Overlay)

Der native Android-Banner sitzt als `FrameLayout`-Overlay über Avalonia, **nicht** im Avalonia-Layout:

- Standard: `GravityFlags.Bottom | GravityFlags.CenterHorizontal` mit `BottomMargin = tabBarHeightDp * density`
- Top-Position: `IAdService.SetBannerPosition(true)` → `GravityFlags.Top` (z.B. BomberBlast GameView)
- `AdInsetListener` passt Margin für Edge-to-Edge Navigation-Bar-Insets an
- **Jeder `MainViewModel` muss `_adService.ShowBanner()` explizit aufrufen** — der `AdMobHelper`
  verschluckt Fehler still, ohne den Aufruf erscheint kein Banner.

Tab-Bar-Höhen für `tabBarHeightDp`-Parameter:

| App | tabBarHeightDp |
|-----|----------------|
| FinanzRechner, FitnessRechner, HandwerkerRechner, WorkTimePro | 56 |
| HandwerkerImperium | 64 |
| BomberBlast | 0 (kein Tab-Bar) |

### Purchase-Restore beim App-Start

Jede Loading-Pipeline einer werbe-App muss `IPurchaseService.InitializeAsync()` parallel im ersten Lade-Schritt aufrufen. Ohne diesen Aufruf sehen Premium-Nutzer nach Geräte- oder Datenwechsel wieder Werbung, weil der lokale `is_premium` Preference-Key fehlt.

```csharp
// LoadingPipeline.cs
await Task.WhenAll(
    _purchaseService.InitializeAsync(),   // Stellt Käufe/Abos via Google Play wieder her
    // ... weitere parallele Lade-Schritte
);
```

Auf Desktop ist `InitializeAsync()` ein No-Op (liest nur lokale Preferences).

---

## Kritische Gotchas

### UMP Namespace-Typo (GDPR Consent)

```csharp
// RICHTIG — dreifaches 's'
using Xamarin.Google.UserMesssagingPlatform;

// FALSCH — wird nicht gefunden
using Xamarin.Google.UserMessagingPlatform;
```

Das NuGet-Paket `Xamarin.Google.UserMessagingPlatform` (korrekte Schreibung im Paket-Namen) erzeugt intern den Namespace mit drei 's'. Nicht behebbar, muss so stehen.

### Java Generics Erasure — JNI-Delegate niemals aufgerufen

`RewardedAdLoadCallback` erbt von `AdLoadCallback<RewardedAd>`. Java-Generics werden zur Laufzeit gelöscht (`erasure`): `onAdLoaded(RewardedAd)` wird zu `onAdLoaded(Object)`. Das Xamarin-Binding generiert `[Register("onAdLoaded", "...", "")]` mit **leerem Connector-String** → JNI Native Delegate wird nie verdrahtet → `OnAdLoaded` in C# wird nie aufgerufen → `_rewardedAd` bleibt null → 100% Match Rate, 0 Impressions.

Fix: `FixedRewardedAdLoadCallback` (in `RewardedAdHelper.cs`) mit explizitem JNI-Connector:

```csharp
[Register("onAdLoaded",
    "(Lcom/google/android/gms/ads/rewarded/RewardedAd;)V",
    "GetOnAdLoadedHandler")]   // <-- nicht leer!
public abstract void OnAdLoaded(RewardedAd ad);
```

Alle `LoadCallback`-Ableitungen müssen von `FixedRewardedAdLoadCallback` erben, nicht direkt von `RewardedAdLoadCallback`.

### Google Play Billing — Java-Callback-Pattern

`IBillingClientStateListener` und `IPurchasesUpdatedListener` dürfen **nicht** als C#-Interfaces direkt implementiert werden. Sie brauchen innere Klassen, die von `Java.Lang.Object` erben:

```csharp
// RICHTIG
private class BillingStateListener : Java.Lang.Object, IBillingClientStateListener { ... }
private class PurchaseUpdateListener : Java.Lang.Object, IPurchasesUpdatedListener { ... }

// FALSCH — kein JNI-Bridge → Callbacks kommen nie an
private class BillingStateListener : IBillingClientStateListener { ... }
```

Gilt analog für alle Android-Java-Callbacks in dieser Library.

### Billing Client v8 API-Unterschiede

```csharp
// v8: EnablePendingPurchases braucht Parameter
_billingClient.EnablePendingPurchases(PendingPurchasesParams.NewBuilder().Build());

// v8: Purchases-Property heißt anders
var purchases = result.Purchases;         // RICHTIG (nicht PurchasesList)

// v8: PurchaseState-Namespace
Android.BillingClient.Api.PurchaseState.Purchased   // RICHTIG
Purchase.PurchaseStateCode.Purchased                 // FALSCH (v7)
```

### Rewarded Ad Timeout deckt nur Lade-Phase

Das 8s-Timeout in `LoadAndShowAsync()` muss **nur** die Lade-Phase abdecken, nicht die Video-Anzeige. Sobald die Ad geladen ist und `ad.Show()` aufgerufen wird, muss der Timeout-Token gecancellt werden — sonst feuert er während des 15-30s langen Videos und der Callback erhält `false` statt `true`.

```csharp
// Im OnDemandLoadCallback.OnAdLoaded:
_loadCts?.Cancel();   // Timeout stoppen, Ad ist geladen
ad.Show(_activity, this);
```

### D8 Duplicate Class (Transitiv-Abhängigkeit)

`Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` (transitiv über die Billing-/Ads-Pakete) kollidiert mit `...Annotation.Android`. Der Fix (`ExcludeAssets="all"` in `Directory.Build.targets`) ist projektweit und in der Root-CLAUDE.md dokumentiert (Build-Konfiguration, D8/DEX). Hier nur als Verursacher-Hinweis: tritt nur in Apps auf, die diese Library einbinden.

### Content hinter Ad-Banner abgeschnitten

Adaptive Banner (`GetCurrentOrientationAnchoredAdaptiveBannerAdSize`) können je nach Gerät 50–60dp+ hoch sein. Der `Ad-Spacer` in `MainView` muss 64dp sein, nicht 50dp. Pflichtgrid: `RowDefinitions="*,Auto,Auto"` (Content / 64dp Ad-Spacer / Tab-Bar). Jede scrollbare Sub-View braucht mindestens 60dp `Margin` (NICHT `Padding`!) am Kind-Element.

### Play Review — korrekter Namespace

```csharp
// FALSCH (existiert nicht)
using Com.Google.Android.Play.Core.Review;

// RICHTIG
using Xamarin.Google.Android.Play.Core.Review;
using Android.Gms.Tasks;   // für Task/IOnCompleteListener
```

`ReviewInfo` ist eine Klasse, NICHT `IReviewInfo`.

### `MediaPlayer.PrepareAsync()` gibt void zurück

Android-Java-Binding-Eigenheit: `PrepareAsync()` ist void, kein `Task`. Stattdessen `Prepare()` synchron verwenden oder mit `TaskCompletionSource` + `Prepared`-Event arbeiten.

---

## Verweise

- AdMob-, Billing- und Ad-Banner-Layout-Gotchas → Abschnitt "Kritische Gotchas" oben (kanonische Heimat)
- App-spezifische Premium-Konfiguration (welche Placements wo getriggert werden) → jeweilige `src/Apps/{App}/CLAUDE.md`
- Generisches Factory-Pattern, DI-Lifetimes, Build-/D8-Konfiguration → [Haupt-CLAUDE.md](../../../CLAUDE.md)
- NuGet-Versionen → `Directory.Packages.props`
