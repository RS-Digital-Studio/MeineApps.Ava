using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Hilfs-Methoden: Geld-Formatierung, Property-Notify, Header-Updates, Worker-Warnung,
/// Money-Animation, Workshop-Icon-Mapping.
/// aus MainViewModel.cs extrahiert (12.05.2026).
/// </summary>
public sealed partial class MainViewModel
{
    internal static string FormatMoney(decimal amount) => MoneyFormatter.FormatCompact(amount);

    /// <summary>
    /// Lazy-Init des Eternal-Mastery-Service (Long-Term-Engagement, 12.05.2026).
    /// Wird via App.Services aufgeloest damit der MainViewModel-Konstruktor nicht erweitert
    /// werden muss (haette das DI-Schema von ~50 anderen Apps gebrochen).
    /// </summary>
    private IEternalMasteryService? _eternalMasteryServiceLazy;

    /// <summary>
    /// Aktualisiert das Eternal-Mastery-Badge im Header. Wird bei OnPrestigeCompleted +
    /// OnStateLoaded aufgerufen.
    /// </summary>
    internal void RefreshEternalMastery()
    {
        _eternalMasteryServiceLazy ??= App.Services?.GetService(typeof(IEternalMasteryService))
            as IEternalMasteryService;
        if (_eternalMasteryServiceLazy == null) return;

        HeaderVM.HasEternalMastery = _eternalMasteryServiceLazy.IsActive;
        HeaderVM.EternalMasteryDisplay = _eternalMasteryServiceLazy.DisplayText;
    }

    /// <summary>Interner Wrapper fuer PropertyChanged-Benachrichtigung (EconomyFeatureVM Zugriff).</summary>
    internal new void OnPropertyChanged(string? propertyName = null)
        => base.OnPropertyChanged(propertyName);

    /// <summary>
    /// Aktualisiert die Netto-Einkommen-Anzeige im Dashboard-Header.
    /// Zeigt Brutto minus Kosten mit Farbindikator (rot wenn negativ).
    /// </summary>
    internal void UpdateNetIncomeHeader(GameState state)
    {
        var netIncome = state.TotalIncomePerSecond - state.TotalCostsPerSecond;
        HeaderVM.IsNetIncomeNegative = netIncome < 0;
        HeaderVM.NetIncomeColor = netIncome < 0 ? "#FF5722" : "#FFFFFFAA";

        // Gecachter Label-Text (Invalidierung in OnLanguageChanged)
        HeaderVM.NetIncomeHeaderDisplay = $"{_cachedNetIncomeLabel}: {MoneyFormatter.FormatPerSecond(netIncome, 1)}";
    }

    /// <summary>
    /// Prüft alle Worker auf Erschöpfung (Fatigue>80), Unzufriedenheit (Mood kleiner 30) und Kündigungsrisiko (Mood kleiner 15).
    /// Zeigt die dringendste Warnung im Dashboard-Banner.
    /// </summary>
    internal void UpdateWorkerWarning(GameState state)
    {
        int tiredCount = 0, unhappyCount = 0, quitRisk = 0;
        string? worstWorkshopName = null;
        string worstWorkerId = "";
        decimal worstScore = decimal.MaxValue;

        foreach (var ws in state.Workshops)
        {
            if (!ws.IsUnlocked) continue;
            foreach (var w in ws.Workers)
            {
                if (w.Fatigue > 80) tiredCount++;
                if (w.Mood < 30) unhappyCount++;
                if (w.Mood < 15) quitRisk++;

                // Schlimmsten Worker tracken (niedrigste Mood oder höchste Fatigue)
                decimal score = w.Mood - w.Fatigue * 0.5m;
                if (score < worstScore && (w.Fatigue > 80 || w.Mood < 30))
                {
                    worstScore = score;
                    worstWorkerId = w.Id;
                    worstWorkshopName = _localizationService.GetString(ws.Type.GetLocalizationKey()) ?? ws.Type.ToString();
                }
            }
        }

        _worstWorkerId = worstWorkerId;

        HeaderVM.HasWorkerWarning = tiredCount > 0 || unhappyCount > 0;

        // Kontext-Info: Welcher Workshop ist betroffen
        string context = worstWorkshopName != null ? $" ({worstWorkshopName})" : "";

        if (quitRisk > 0)
        {
            HeaderVM.WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerQuitRisk") ?? "{0} workers at risk of quitting!",
                quitRisk) + context;
        }
        else if (unhappyCount > 0)
        {
            HeaderVM.WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerUnhappy") ?? "{0} Arbeiter unzufrieden",
                unhappyCount) + context;
        }
        else if (tiredCount > 0)
        {
            HeaderVM.WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerTired") ?? "{0} workers exhausted",
                tiredCount) + context;
        }
    }

    /// <summary>
    /// Animierter Geld-Counter: Setzt neuen Zielwert und startet Interpolation.
    /// Die angezeigte Zahl "tickt" smooth von alt auf neu (Phase 9).
    /// </summary>
    private void AnimateMoneyTo(decimal target)
    {
        _targetMoney = target;

        // Kleiner Unterschied → direkt setzen (kein sichtbarer Tick)
        if (Math.Abs(_targetMoney - _displayedMoney) < 1m)
        {
            _displayedMoney = _targetMoney;
            HeaderVM.MoneyDisplay = FormatMoney(_displayedMoney);
            _moneyAnimActive = false;
            return;
        }

        _moneyAnimActive = true;
    }

    /// <summary>
    /// Wird vom MainView Render-Timer aufgerufen (25fps).
    /// Ersetzt den separaten Timer und reduziert UI-Thread-Callbacks.
    /// </summary>
    public void UpdateMoneyAnimation()
    {
        if (!_moneyAnimActive) return;

        var diff = _targetMoney - _displayedMoney;

        if (Math.Abs(diff) < 1m)
        {
            _displayedMoney = _targetMoney;
            HeaderVM.MoneyDisplay = FormatMoney(_displayedMoney);
            _moneyAnimActive = false;
            return;
        }

        // Exponentielles Easing: schnell am Anfang, langsamer am Ende
        _displayedMoney += diff * MoneyAnimSpeed;
        HeaderVM.MoneyDisplay = FormatMoney(_displayedMoney);
    }

    internal static GameIconKind GetWorkshopIconKind(WorkshopType type, int level = 1) => type switch
    {
        WorkshopType.Carpenter when level >= 26 => GameIconKind.Factory,
        WorkshopType.Carpenter when level >= 11 => GameIconKind.TableFurniture,
        WorkshopType.Carpenter => GameIconKind.HandSaw,
        WorkshopType.Plumber when level >= 26 => GameIconKind.WaterPump,
        WorkshopType.Plumber when level >= 11 => GameIconKind.Pipe,
        WorkshopType.Plumber => GameIconKind.Pipe,
        WorkshopType.Electrician when level >= 26 => GameIconKind.TransmissionTower,
        WorkshopType.Electrician when level >= 11 => GameIconKind.LightningBolt,
        WorkshopType.Electrician => GameIconKind.Flash,
        WorkshopType.Painter when level >= 26 => GameIconKind.Draw,
        WorkshopType.Painter when level >= 11 => GameIconKind.SprayBottle,
        WorkshopType.Painter => GameIconKind.Palette,
        WorkshopType.Roofer when level >= 26 => GameIconKind.HomeGroup,
        WorkshopType.Roofer when level >= 11 => GameIconKind.HomeRoof,
        WorkshopType.Roofer => GameIconKind.HomeRoof,
        WorkshopType.Contractor when level >= 26 => GameIconKind.DomainPlus,
        WorkshopType.Contractor when level >= 11 => GameIconKind.OfficeBuilding,
        WorkshopType.Contractor => GameIconKind.OfficeBuildingOutline,
        WorkshopType.Architect => GameIconKind.Compass,
        WorkshopType.GeneralContractor => GameIconKind.HardHat,
        _ => GameIconKind.Wrench
    };
}
