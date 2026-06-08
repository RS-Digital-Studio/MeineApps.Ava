#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Analytics
{
    /// <summary>
    /// Analytics-Event-Taxonomie (P3 §2/§4): die kanonischen Event-Namen (snake_case, stabil) für den
    /// FTUE-Funnel + Loop-/Progressions-/Monetarisierungs-Events. Unity-frei; der Game-Layer ruft
    /// <c>IAnalyticsService.TrackEvent(name, props)</c> mit diesen Konstanten, damit Namen nicht driften.
    /// </summary>
    public static class AnalyticsEvents
    {
        public const string FtueStep = "ftue_step";
        public const string LoopCollect = "loop_collect";
        public const string UpgradeBought = "upgrade_bought";
        public const string WorkerHired = "worker_hired";
        public const string PlotUnlocked = "plot_unlocked";
        public const string LandmarkRestored = "landmark_restored";
        public const string PrestigeDone = "prestige_done";
        public const string MeistergradUp = "meistergrad_up";
        public const string AdImpression = "ad_impression";
        public const string AdReward = "ad_reward";
        public const string IapPurchase = "iap_purchase";
        public const string DailyClaimed = "daily_claimed";
        public const string SessionStart = "session_start";

        private static readonly List<string> _all = new List<string>
        {
            FtueStep, LoopCollect, UpgradeBought, WorkerHired, PlotUnlocked, LandmarkRestored,
            PrestigeDone, MeistergradUp, AdImpression, AdReward, IapPurchase, DailyClaimed, SessionStart
        };

        /// <summary>Alle bekannten Event-Namen (für Validierung/Registrierung).</summary>
        public static IReadOnlyList<string> All => _all;

        /// <summary>True, wenn der Name ein bekanntes Event ist.</summary>
        public static bool IsKnown(string name) => _all.Contains(name);
    }
}
