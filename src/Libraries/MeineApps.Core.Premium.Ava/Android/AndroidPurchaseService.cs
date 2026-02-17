using Android.App;
using Android.BillingClient.Api;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace MeineApps.Core.Premium.Ava.Droid;

/// <summary>
/// Android-Implementierung von IPurchaseService mit Google Play Billing Library.
/// Linked File - wird per Compile Include in Android-Projekte eingebunden.
/// Ersetzt den Desktop-PurchaseService per DI Override in MainActivity.
/// Unterstützt InApp-Purchases, Subscriptions und Consumables.
///
/// Wichtig: IPurchasesUpdatedListener und IBillingClientStateListener erben von
/// IJavaPeerable und brauchen Java.Lang.Object als Basis. Da diese Klasse von
/// PurchaseService (C#) erbt, werden innere Callback-Klassen verwendet.
/// </summary>
public class AndroidPurchaseService : PurchaseService
{
    private const string Tag = "AndroidPurchaseService";

    private readonly Activity _activity;
    private BillingClient? _billingClient;
    private TaskCompletionSource<bool>? _purchaseTcs;
    private bool _isConnected;

    // Maximale Reconnect-Versuche
    private int _reconnectAttempts;
    private const int MaxReconnectAttempts = 5;

    // Callback-Instanzen (Java.Lang.Object-basiert)
    private readonly BillingStateListener _stateListener;
    private readonly PurchaseUpdateListener _purchaseListener;

    public AndroidPurchaseService(
        Activity activity,
        IPreferencesService preferences,
        IAdService adService) : base(preferences, adService)
    {
        _activity = activity;
        _stateListener = new BillingStateListener(this);
        _purchaseListener = new PurchaseUpdateListener(this);

        var pendingParams = PendingPurchasesParams.NewBuilder()
            .EnableOneTimeProducts()
            .Build();

        _billingClient = BillingClient.NewBuilder(activity)
            .SetListener(_purchaseListener)
            .EnablePendingPurchases(pendingParams)
            .Build();

        Android.Util.Log.Info(Tag, "BillingClient erstellt");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALISIERUNG & VERBINDUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verbindet zum Google Play Billing Service und stellt vorherige Käufe wieder her.
    /// </summary>
    public override async Task InitializeAsync()
    {
        // Basis-Initialisierung (prüft lokalen Premium-Status)
        await base.InitializeAsync();

        try
        {
            await ConnectAsync();
            if (_isConnected)
            {
                await RestorePurchasesAsync();
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"InitializeAsync Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Verbindet zum Billing Service und wartet kurz auf Verbindung.
    /// </summary>
    private async Task ConnectAsync()
    {
        if (_billingClient == null) return;
        if (_isConnected) return;

        _billingClient.StartConnection(_stateListener);

        // Warte bis zu 5 Sekunden auf Verbindung
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(200);
            if (_isConnected) return;
        }

        Android.Util.Log.Warn(Tag, "ConnectAsync: Timeout nach 5 Sekunden");
    }

    /// <summary>
    /// Stellt sicher, dass eine Verbindung besteht. Wartet bis zu 5 Sekunden.
    /// </summary>
    private async Task<bool> EnsureConnectedAsync()
    {
        if (_isConnected) return true;
        if (_billingClient == null) return false;

        _billingClient.StartConnection(_stateListener);

        // Warte bis zu 5 Sekunden auf Verbindung
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(200);
            if (_isConnected) return true;
        }

        Android.Util.Log.Warn(Tag, "Verbindung konnte nicht hergestellt werden");
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KAUF-METHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kauft das "remove_ads" Produkt (InApp, non-consumable).
    /// </summary>
    public override async Task<bool> PurchaseRemoveAdsAsync()
    {
        return await PurchaseProductAsync(RemoveAdsProductId, BillingClient.ProductType.Inapp);
    }

    /// <summary>
    /// Kauft das monatliche Abo (Subscription).
    /// </summary>
    public override async Task<bool> PurchaseMonthlyAsync()
    {
        return await PurchaseProductAsync(MonthlyProductId, BillingClient.ProductType.Subs);
    }

    /// <summary>
    /// Kauft das Lifetime-Paket (InApp, non-consumable).
    /// </summary>
    public override async Task<bool> PurchaseLifetimeAsync()
    {
        return await PurchaseProductAsync(LifetimeProductId, BillingClient.ProductType.Inapp);
    }

    /// <summary>
    /// Kauft ein Consumable-Produkt (z.B. Goldene Schrauben in HandwerkerImperium).
    /// </summary>
    public override async Task<bool> PurchaseConsumableAsync(string productId)
    {
        return await PurchaseProductAsync(productId, BillingClient.ProductType.Inapp, isConsumable: true);
    }

    /// <summary>
    /// Generische Kauf-Methode für alle Produkttypen.
    /// </summary>
    private async Task<bool> PurchaseProductAsync(string productId, string productType, bool isConsumable = false)
    {
        try
        {
            if (!await EnsureConnectedAsync())
            {
                Android.Util.Log.Error(Tag, $"Kauf fehlgeschlagen: Keine Verbindung ({productId})");
                return false;
            }

            // Produktdetails abfragen
            var queryParams = QueryProductDetailsParams.NewBuilder()
                .SetProductList(new[]
                {
                    QueryProductDetailsParams.Product.NewBuilder()
                        .SetProductId(productId)
                        .SetProductType(productType)
                        .Build()
                })
                .Build();

            var detailsResult = await _billingClient!.QueryProductDetailsAsync(queryParams);

            if (detailsResult == null ||
                detailsResult.Result?.ResponseCode != BillingResponseCode.Ok ||
                detailsResult.ProductDetails == null ||
                detailsResult.ProductDetails.Count == 0)
            {
                Android.Util.Log.Warn(Tag, $"Produkt nicht gefunden: {productId} (Response: {detailsResult?.Result?.ResponseCode})");
                return false;
            }

            var productDetails = detailsResult.ProductDetails[0];

            // BillingFlowParams erstellen
            var offerBuilder = BillingFlowParams.ProductDetailsParams.NewBuilder()
                .SetProductDetails(productDetails);

            // Für Subscriptions: Offer-Token setzen (erstes Angebot)
            if (productType == BillingClient.ProductType.Subs)
            {
                var offers = productDetails.GetSubscriptionOfferDetails();
                if (offers != null && offers.Count > 0)
                {
                    offerBuilder.SetOfferToken(offers[0].OfferToken);
                }
                else
                {
                    Android.Util.Log.Warn(Tag, $"Kein Abo-Angebot gefunden für: {productId}");
                    return false;
                }
            }

            var flowParams = BillingFlowParams.NewBuilder()
                .SetProductDetailsParamsList(new[] { offerBuilder.Build() })
                .Build();

            // TaskCompletionSource für async Warten auf OnPurchasesUpdated
            _purchaseTcs = new TaskCompletionSource<bool>();

            // Kaufdialog starten (auf UI-Thread)
            _activity.RunOnUiThread(() =>
            {
                var result = _billingClient.LaunchBillingFlow(_activity, flowParams);
                if (result.ResponseCode != BillingResponseCode.Ok)
                {
                    Android.Util.Log.Error(Tag, $"LaunchBillingFlow fehlgeschlagen: {result.ResponseCode}");
                    _purchaseTcs?.TrySetResult(false);
                }
            });

            // Warte auf Ergebnis (Timeout 60 Sekunden)
            var timeoutTask = Task.Delay(60_000);
            var completedTask = await Task.WhenAny(_purchaseTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Android.Util.Log.Warn(Tag, $"Kauf-Timeout für: {productId}");
                _purchaseTcs = null;
                return false;
            }

            return await _purchaseTcs.Task;
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"PurchaseProductAsync Fehler ({productId}): {ex.Message}");
            _purchaseTcs?.TrySetResult(false);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KAUF-VERARBEITUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verarbeitet einen erfolgreichen Kauf: Acknowledge/Consume + Status-Update.
    /// </summary>
    private async Task HandlePurchaseAsync(Purchase purchase)
    {
        try
        {
            var products = purchase.Products;
            bool isConsumable = false;

            foreach (var productId in products)
            {
                // Consumable-Check: Alles was nicht remove_ads, premium_monthly, premium_lifetime ist
                if (productId != RemoveAdsProductId &&
                    productId != MonthlyProductId &&
                    productId != LifetimeProductId)
                {
                    isConsumable = true;
                }
            }

            if (isConsumable)
            {
                // Consumable: ConsumeAsync (ermöglicht erneuten Kauf)
                var consumeParams = ConsumeParams.NewBuilder()
                    .SetPurchaseToken(purchase.PurchaseToken)
                    .Build();

                var consumeResult = await _billingClient!.ConsumeAsync(consumeParams);
                Android.Util.Log.Info(Tag, $"ConsumeAsync: {consumeResult?.BillingResult?.ResponseCode}");
            }
            else if (!purchase.IsAcknowledged)
            {
                // Non-Consumable/Subscription: AcknowledgePurchase
                var acknowledgeParams = AcknowledgePurchaseParams.NewBuilder()
                    .SetPurchaseToken(purchase.PurchaseToken)
                    .Build();

                var ackResult = await _billingClient!.AcknowledgePurchaseAsync(acknowledgeParams);
                Android.Util.Log.Info(Tag, $"AcknowledgePurchase: {ackResult?.ResponseCode}");
            }

            // Premium-Status aktualisieren
            foreach (var productId in products)
            {
                switch (productId)
                {
                    case RemoveAdsProductId:
                    case LifetimeProductId:
                        SetPremiumStatus(true);
                        Android.Util.Log.Info(Tag, $"Premium aktiviert: {productId}");
                        break;

                    case MonthlyProductId:
                        SetSubscriptionStatus(true);
                        Android.Util.Log.Info(Tag, $"Abo aktiviert: {productId}");
                        break;

                    default:
                        // Consumable → kein persistenter Status, nur TCS-Result
                        Android.Util.Log.Info(Tag, $"Consumable verbraucht: {productId}");
                        break;
                }
            }

            _purchaseTcs?.TrySetResult(true);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"HandlePurchaseAsync Fehler: {ex.Message}");
            _purchaseTcs?.TrySetResult(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KAUF-WIEDERHERSTELLUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stellt vorherige Käufe wieder her (InApp + Subscriptions).
    /// </summary>
    public override async Task<bool> RestorePurchasesAsync()
    {
        try
        {
            if (!await EnsureConnectedAsync()) return false;

            bool anyRestored = false;

            // InApp-Käufe abfragen
            var inAppParams = QueryPurchasesParams.NewBuilder()
                .SetProductType(BillingClient.ProductType.Inapp)
                .Build();

            var inAppResult = await _billingClient!.QueryPurchasesAsync(inAppParams);

            if (inAppResult?.Result?.ResponseCode == BillingResponseCode.Ok &&
                inAppResult.Purchases != null)
            {
                foreach (var purchase in inAppResult.Purchases)
                {
                    if (purchase.PurchaseState == Android.BillingClient.Api.PurchaseState.Purchased)
                    {
                        foreach (var productId in purchase.Products)
                        {
                            if (productId == RemoveAdsProductId || productId == LifetimeProductId)
                            {
                                SetPremiumStatus(true);
                                anyRestored = true;
                                Android.Util.Log.Info(Tag, $"Kauf wiederhergestellt: {productId}");
                            }
                        }

                        // Unbestätigte Käufe nachbestätigen
                        if (!purchase.IsAcknowledged)
                        {
                            var ackParams = AcknowledgePurchaseParams.NewBuilder()
                                .SetPurchaseToken(purchase.PurchaseToken)
                                .Build();
                            await _billingClient.AcknowledgePurchaseAsync(ackParams);
                        }
                    }
                }
            }

            // Subscriptions abfragen
            var subsParams = QueryPurchasesParams.NewBuilder()
                .SetProductType(BillingClient.ProductType.Subs)
                .Build();

            var subsResult = await _billingClient.QueryPurchasesAsync(subsParams);

            if (subsResult?.Result?.ResponseCode == BillingResponseCode.Ok &&
                subsResult.Purchases != null)
            {
                foreach (var purchase in subsResult.Purchases)
                {
                    if (purchase.PurchaseState == Android.BillingClient.Api.PurchaseState.Purchased)
                    {
                        foreach (var productId in purchase.Products)
                        {
                            if (productId == MonthlyProductId)
                            {
                                SetSubscriptionStatus(true);
                                anyRestored = true;
                                Android.Util.Log.Info(Tag, $"Abo wiederhergestellt: {productId}");
                            }
                        }

                        if (!purchase.IsAcknowledged)
                        {
                            var ackParams = AcknowledgePurchaseParams.NewBuilder()
                                .SetPurchaseToken(purchase.PurchaseToken)
                                .Build();
                            await _billingClient.AcknowledgePurchaseAsync(ackParams);
                        }
                    }
                }
            }

            Android.Util.Log.Info(Tag, $"Wiederherstellung abgeschlossen, Premium={IsPremium}");
            return anyRestored;
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, $"RestorePurchasesAsync Fehler: {ex.Message}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JAVA-CALLBACK-KLASSEN
    // ═══════════════════════════════════════════════════════════════════════
    // IPurchasesUpdatedListener und IBillingClientStateListener erben von
    // IJavaPeerable → brauchen Java.Lang.Object als Basis-Klasse.
    // Da AndroidPurchaseService von PurchaseService (C#) erbt, werden
    // innere Klassen verwendet (gleichen Pattern wie RewardedAdHelper.LoadCallback).

    /// <summary>
    /// Callback für BillingClient-Verbindungsstatus.
    /// </summary>
    private class BillingStateListener : Java.Lang.Object, IBillingClientStateListener
    {
        private readonly AndroidPurchaseService _owner;

        public BillingStateListener(AndroidPurchaseService owner)
        {
            _owner = owner;
        }

        public void OnBillingSetupFinished(BillingResult result)
        {
            if (result.ResponseCode == BillingResponseCode.Ok)
            {
                _owner._isConnected = true;
                _owner._reconnectAttempts = 0;
                Android.Util.Log.Info(Tag, "Billing-Verbindung hergestellt");
            }
            else
            {
                _owner._isConnected = false;
                Android.Util.Log.Warn(Tag, $"Billing-Verbindung fehlgeschlagen: {result.ResponseCode} - {result.DebugMessage}");
            }
        }

        public void OnBillingServiceDisconnected()
        {
            _owner._isConnected = false;
            Android.Util.Log.Warn(Tag, "Billing-Verbindung getrennt");

            if (_owner._reconnectAttempts < MaxReconnectAttempts)
            {
                _owner._reconnectAttempts++;
                var delay = _owner._reconnectAttempts * 2000; // 2s, 4s, 6s, 8s, 10s
                Android.Util.Log.Info(Tag, $"Reconnect-Versuch {_owner._reconnectAttempts} in {delay}ms...");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay);
                    _owner._billingClient?.StartConnection(_owner._stateListener);
                });
            }
        }
    }

    /// <summary>
    /// Callback für Kauf-Updates von Google Play.
    /// </summary>
    private class PurchaseUpdateListener : Java.Lang.Object, IPurchasesUpdatedListener
    {
        private readonly AndroidPurchaseService _owner;

        public PurchaseUpdateListener(AndroidPurchaseService owner)
        {
            _owner = owner;
        }

        public void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
        {
            if (result.ResponseCode == BillingResponseCode.Ok && purchases != null)
            {
                foreach (var purchase in purchases)
                {
                    Android.Util.Log.Info(Tag, $"Kauf erfolgreich: {string.Join(",", purchase.Products)}, State={purchase.PurchaseState}");

                    if (purchase.PurchaseState == Android.BillingClient.Api.PurchaseState.Purchased)
                    {
                        // Kauf bestätigen und Status setzen
                        _ = _owner.HandlePurchaseAsync(purchase);
                    }
                }
            }
            else if (result.ResponseCode == BillingResponseCode.UserCancelled)
            {
                Android.Util.Log.Info(Tag, "Kauf vom Benutzer abgebrochen");
                _owner._purchaseTcs?.TrySetResult(false);
            }
            else
            {
                Android.Util.Log.Warn(Tag, $"Kauf fehlgeschlagen: {result.ResponseCode} - {result.DebugMessage}");
                _owner._purchaseTcs?.TrySetResult(false);
            }
        }
    }
}
