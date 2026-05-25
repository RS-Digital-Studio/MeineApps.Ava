#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.SaisonPass
{
    /// <summary>
    /// Saison-Pass mit Free + Premium Track. 30 Tiers, je 1000 XP. Premium-Upsell-Banner
    /// wenn noch nicht gekauft.
    /// </summary>
    public sealed class SaisonPassScreen : ScreenBase
    {
        private const int MaxTier = 30;
        private const int XpPerTier = 1000;

        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ToastService _toast;

        private Button _backBtn = null!;
        private Label _daysLeft = null!;
        private Label _tier = null!;
        private Label _xpText = null!;
        private VisualElement _xpFill = null!;
        private VisualElement _premiumBanner = null!;
        private Button _buyPremiumBtn = null!;
        private VisualElement _rewardTrack = null!;

        public override string Id => ScreenId.SaisonPass;
        protected override string UxmlPath => "UI/SaisonPassScreen";

        public SaisonPassScreen(ScreenManager screenManager,
                                ISaveService<PlayerSave> save, ToastService toast)
        {
            _screenManager = screenManager;
            _save = save;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _backBtn        = Q<Button>("saison-back-button");
            _daysLeft       = Q<Label>("saison-days-left");
            _tier           = Q<Label>("saison-tier");
            _xpText         = Q<Label>("saison-xp-text");
            _xpFill         = Q<VisualElement>("saison-xp-fill");
            _premiumBanner  = Q<VisualElement>("saison-premium-banner");
            _buyPremiumBtn  = Q<Button>("saison-buy-premium");
            _rewardTrack    = Q<VisualElement>("saison-reward-track");

            _backBtn.clicked += () => _screenManager.PopAsync().Forget();
            _buyPremiumBtn.clicked += () =>
                _toast.Show("Premium-IAP folgt mit Unity-IAP-Integration.", ToastKind.Info);
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            if (!result.IsSuccess || result.Value == null) return;
            var save = result.Value;

            // Vereinfachung: 1. Saison aus dict ziehen oder 0
            var seasonXp = save.SaisonPassXp.Values.Count > 0
                ? System.Linq.Enumerable.Sum(save.SaisonPassXp.Values) : 0;
            var tier = System.Math.Min(seasonXp / XpPerTier, MaxTier);
            var xpInTier = seasonXp % XpPerTier;

            _tier.text = tier.ToString();
            _xpText.text = $"{xpInTier} / {XpPerTier} XP";
            var pct = (float)xpInTier * 100f / XpPerTier;
            _xpFill.style.width = new Length(pct, LengthUnit.Percent);

            // Saison-Ende — Mock: 14 Tage
            _daysLeft.text = "14 Tage";

            BuildRewardTrack(tier);
        }

        private void BuildRewardTrack(int currentTier)
        {
            _rewardTrack.Clear();
            for (var t = 1; t <= MaxTier; t++)
            {
                _rewardTrack.Add(BuildTierColumn(t, currentTier));
            }
        }

        private static VisualElement BuildTierColumn(int tier, int currentTier)
        {
            var col = new VisualElement();
            col.style.width = 100;
            col.style.marginRight = 6;
            col.style.alignItems = Align.Center;

            // Free-Reward
            var free = new VisualElement();
            free.AddToClassList("ak-surface");
            free.style.width = 96;
            free.style.height = 80;
            free.style.alignItems = Align.Center;
            free.style.justifyContent = Justify.Center;
            if (tier > currentTier) free.style.opacity = 0.4f;
            var freeLabel = new Label(FreeRewardFor(tier));
            freeLabel.AddToClassList("ak-caption");
            freeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            free.Add(freeLabel);
            col.Add(free);

            // Tier-Number
            var tierLabel = new Label($"T {tier}");
            tierLabel.style.fontSize = 12;
            tierLabel.style.color = new StyleColor(tier <= currentTier
                ? new Color(0.95f, 0.78f, 0.30f)
                : new Color(0.55f, 0.55f, 0.55f));
            tierLabel.style.marginTop = 4;
            tierLabel.style.marginBottom = 4;
            col.Add(tierLabel);

            // Premium-Reward
            var premium = new VisualElement();
            premium.AddToClassList("ak-surface-elevated");
            premium.style.width = 96;
            premium.style.height = 80;
            premium.style.alignItems = Align.Center;
            premium.style.justifyContent = Justify.Center;
            premium.style.opacity = 0.55f; // bis Premium gekauft
            var pLabel = new Label(PremiumRewardFor(tier));
            pLabel.AddToClassList("ak-caption");
            pLabel.AddToClassList("ak-text--accent");
            pLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            premium.Add(pLabel);
            col.Add(premium);

            return col;
        }

        private static string FreeRewardFor(int tier) => (tier % 5) switch
        {
            0 => "1 Pack",
            1 => "200 Gold",
            2 => "500 Gold",
            3 => "5 Scraps",
            _ => "100 Gold"
        };

        private static string PremiumRewardFor(int tier) => (tier % 5) switch
        {
            0 => "Epic-Pack",
            1 => "50 Diamanten",
            2 => "1000 Gold",
            3 => "10 Scraps",
            _ => "200 Diamanten"
        };
    }
}
