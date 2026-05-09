# MeineApps.Core.Premium.Ava

Monetarisierungs-Library für alle 6 werbe-unterstützten Apps: AdMob (Banner + Rewarded), Google Play Billing v8, 14-Tage-Trial und In-App Review.
Abhängigkeit: `MeineApps.Core.Ava` (IPreferencesService).

---

## Komponenten

| Datei | Typ | Zweck |
|-------|-----|-------|
| `Services/IAdService.cs` | Interface | Banner + Rewarded Ad State |
| `Services/AdMobService.cs` | Service | Ad-State-Verwaltung (Singleton) |
| `Services/AdConfig.cs` | Konfiguration | Alle AdMob-IDs der 6 Apps (1 Publisher-Account) |
| `Services/IRewardedAdService.cs` | Interface | `IsAvailable`, `ShowRewardedAdAsync()` |
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
| `Android/AndroidPlayGamesService.cs` | Linked File | Google Play Games Services v2 |

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

### Factory-Pattern für Platform-Services

Plattformspezifische Services werden nicht per DI registriert, sondern über statische Factories in `App.axaml.cs` injiziert. So bleibt das Shared-Projekt frei von Android-Abhängigkeiten.

```csharp
// App.axaml.cs (Shared)
public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }
public static Func<IServiceProvider, IPurchaseService>? PurchaseServiceFactory { get; set; }

// ServiceCollectionExtensions.cs überschreibt nach AddMeineAppsPremium():
if (App.RewardedAdServiceFactory != null)
    services.AddSingleton(App.RewardedAdServiceFactory);
```

```csharp
// MainActivity.cs (Android) — VOR base.OnCreate()
App.RewardedAdServiceFactory = sp =>
    new AndroidRewardedAdService(helper, sp.GetRequiredService<IPurchaseService>());
App.PurchaseServiceFactory = sp =>
    new AndroidPurchaseService(this, sp.GetRequiredService<IPreferencesService>(),
                               sp.GetRequiredService<IAdService>());
```

### AdConfig Multi-Placement

`AdConfig.cs` enthält alle AdMob-IDs für alle 6 Apps unter einem Publisher-Account (`ca-app-pub-2588160251469436`). Jede App hat eigene Banner-IDs, App-IDs und placement-spezifische Rewarded-IDs.

```csharp
// RewardedAdHelper.cs
public async Task<bool> LoadAndShowAsync(string placement)
{
    var adUnitId = AdConfig.GetRewardedAdUnitId(AppId, placement);
    // ...
}
```

Neue Rewarded-Placements → in `AdConfig.cs` eintragen, sonst `null`-Rückgabe und kein Ad.

### Banner-Positionierung (FrameLayout Overlay)

Der native Android-Banner sitzt als `FrameLayout`-Overlay über Avalonia, **nicht** im Avalonia-Layout:

- Standard: `GravityFlags.Bottom | GravityFlags.CenterHorizontal` mit `BottomMargin = tabBarHeightDp * density`
- Top-Position: `IAdService.SetBannerPosition(true)` → `GravityFlags.Top` (z.B. BomberBlast GameView)
- `AdInsetListener` passt Margin für Edge-to-Edge Navigation-Bar-Insets an

Tab-Bar-Höhen für `tabBarHeightDp`-Parameter:

| App | tabBarHeightDp |
|-----|----------------|
| FinanzRechner, FitnessRechner, HandwerkerRechner, WorkTimePro | 56 |
| HandwerkerImperium | 64 |
| BomberBlast | 0 (kein Tab-Bar) |

### Purchase-Restore beim App-Start

Alle 6 Loading-Pipelines müssen `IPurchaseService.InitializeAsync()` parallel im ersten Lade-Schritt aufrufen. Ohne diesen Aufruf sehen Premium-Nutzer nach Geräte- oder Datenwechsel wieder Werbung, weil der lokale `is_premium` Preference-Key fehlt.

```csharp
// LoadingPipeline.cs (alle 6 Apps)
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

`Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` kollidiert mit `...Annotation.Android`. Fix in `Directory.Build.targets`:

```xml
<PackageReference Include="Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm"
                  ExcludeAssets="all" PrivateAssets="all" />
```

---

## Verweise

- AdMob-Gotchas (generisch) → `F:\Meine_Apps_Ava\CLAUDE.md` Abschnitt "AdMob"
- Billing-Gotchas (generisch) → `F:\Meine_Apps_Ava\CLAUDE.md` Abschnitt "Google Play Billing"
- App-spezifische Premium-Konfiguration → jeweilige `src/Apps/{App}/CLAUDE.md`
- NuGet-Versionen → `Directory.Packages.props`
