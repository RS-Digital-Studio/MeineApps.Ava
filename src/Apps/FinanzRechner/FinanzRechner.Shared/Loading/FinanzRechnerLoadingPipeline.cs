using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.Loading;
using MeineApps.UI.SkiaSharp.Shaders;
using Microsoft.Extensions.DependencyInjection;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Loading;

/// <summary>
/// FinanzRechner Lade-Pipeline: Shader + DB-Init + ViewModel.
/// ExpenseService.InitializeAsync() wird vorab ausgeführt (statt lazy in MainView).
/// </summary>
public sealed class FinanzRechnerLoadingPipeline : LoadingPipelineBase
{
    public FinanzRechnerLoadingPipeline(IServiceProvider services)
    {
        var loc = services.GetRequiredService<ILocalizationService>();

        // Schritt 1: Shader + DB parallel (größter Zeitblock)
        AddStep(new LoadingStep
        {
            Name = "DB+Shader",
            DisplayName = loc.GetString("SplashStep_Graphics") ?? "Grafik vorbereiten...",
            Weight = 40,
            ExecuteAsync = async () =>
            {
                var dbTask = services.GetRequiredService<IExpenseService>().InitializeAsync();
                var shaderTask = Task.Run(() => ShaderPreloader.PreloadAll());
                // Käufe mit Google Play abgleichen (Geräte-/Datenwechsel → Premium-Status wiederherstellen)
                var purchaseTask = services.GetRequiredService<IPurchaseService>().InitializeAsync();
                // Neue Services parallel initialisieren
                var accountTask = services.GetRequiredService<IAccountService>().InitializeAsync();
                var goalsTask = services.GetRequiredService<ISavingsGoalService>().InitializeAsync();
                var debtTask = services.GetRequiredService<IDebtService>().InitializeAsync();
                var categoryTask = services.GetRequiredService<ICustomCategoryService>().InitializeAsync();
                await Task.WhenAll(dbTask, shaderTask, purchaseTask, accountTask, goalsTask, debtTask, categoryTask);

                // Währung aus Preferences laden und CurrencyHelper konfigurieren
                var prefs = services.GetRequiredService<IPreferencesService>();
                var currencyCode = prefs.Get("currency_code", "EUR");
                var preset = CurrencySettings.Presets.FirstOrDefault(p => p.CurrencyCode == currencyCode);
                if (preset != null)
                    CurrencyHelper.Configure(preset);
            }
        });

        // Schritt 2: ViewModel erstellen (bindet an ExpenseService-Daten)
        AddStep(new LoadingStep
        {
            Name = "ViewModel",
            DisplayName = loc.GetString("SplashStep_Starting") ?? "App starten...",
            Weight = 15,
            ExecuteAsync = () =>
            {
                services.GetRequiredService<MainViewModel>();
                return Task.CompletedTask;
            }
        });
    }
}
