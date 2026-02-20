using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Google Play Billing v8 Patterns</summary>
class BillingChecker : IChecker
{
    public string Category => "Billing";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        // Billing-Dateien finden (Shared + Android)
        var billingFiles = ctx.CsFiles
            .Where(f => f.Content.Contains("BillingClient") || f.Content.Contains("IPurchasesUpdatedListener"))
            .ToList();

        if (billingFiles.Count == 0)
        {
            if (ctx.App.IsAdSupported)
                results.Add(new(Severity.Info, Category, "Keine Billing-Implementierung gefunden (nutzt evtl. Linked-File aus Premium-Library)"));
            else
                results.Add(new(Severity.Info, Category, "Keine Billing-Nutzung (werbefreie App)"));
            return results;
        }

        foreach (var file in billingFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // EnablePendingPurchases() parameterlos (v8 entfernt)
                if (Regex.IsMatch(trimmed, @"EnablePendingPurchases\s*\(\s*\)"))
                {
                    results.Add(new(Severity.Fail, Category, $"EnablePendingPurchases() parameterlos in {file.RelativePath}:{i + 1} → v8 erfordert PendingPurchasesParams"));
                }

                // PurchasesList statt .Purchases (v8 API-Aenderung)
                if (trimmed.Contains(".PurchasesList"))
                {
                    results.Add(new(Severity.Fail, Category, $".PurchasesList in {file.RelativePath}:{i + 1} → v8: .Purchases verwenden"));
                }

                // PurchaseStateCode.Purchased (alter API-Pfad)
                if (trimmed.Contains("PurchaseStateCode.Purchased"))
                {
                    results.Add(new(Severity.Fail, Category, $"PurchaseStateCode.Purchased in {file.RelativePath}:{i + 1} → v8: Android.BillingClient.Api.PurchaseState.Purchased"));
                }
            }

            // Billing-Callbacks muessen von Java.Lang.Object erben
            if (file.Content.Contains("IPurchasesUpdatedListener") || file.Content.Contains("IBillingClientStateListener"))
            {
                // Prüfen ob innere Klasse von Java.Lang.Object erbt
                if (!file.Content.Contains("Java.Lang.Object"))
                    results.Add(new(Severity.Fail, Category, $"Billing-Callbacks in {file.RelativePath} erben nicht von Java.Lang.Object → IJavaPeerable-Fehler"));
                else
                    results.Add(new(Severity.Pass, Category, $"Billing-Callbacks in {file.RelativePath} erben korrekt von Java.Lang.Object"));
            }
        }

        // Wenn keine spezifischen Probleme gefunden: PASS
        if (!results.Any(r => r.Severity == Severity.Fail))
            results.Add(new(Severity.Pass, Category, "Billing v8 API korrekt verwendet"));

        return results;
    }
}
