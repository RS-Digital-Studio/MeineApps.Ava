#nullable enable
using System;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.SaisonPass;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.SaisonPass;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.SaisonPass
{
    /// <summary>
    /// Saison-Pass mit Free + Premium Track. 30 Stufen, non-lineare EXP-Kurve
    /// (Oekosystem v4 Kap. 4.1, Schwellen aus <see cref="SaisonPassDefinition.XpThresholds"/>).
    /// Premium-Upsell-Banner wenn noch nicht gekauft.
    /// </summary>
    public sealed class SaisonPassScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ToastService _toast;
        private readonly SaisonPassService _saisonPass;
        private readonly ILocalizationService _loc;

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

        private readonly UIAssetService _uiAssets;

        public SaisonPassScreen(ScreenManager screenManager,
                                ISaveService<PlayerSave> save, ToastService toast,
                                UIAssetService uiAssets,
                                SaisonPassService saisonPass,
                                ILocalizationService loc)
        {
            _screenManager = screenManager;
            _save = save;
            _toast = toast;
            _uiAssets = uiAssets;
            _saisonPass = saisonPass;
            _loc = loc;
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

            var def = _saisonPass.ActiveSaison;
            if (def == null) return;

            // Saison-EXP der AKTIVEN Saison (absolute Gesamt-EXP), Stufe via non-linearer Kurve.
            var seasonXp = save.SaisonPassXp.TryGetValue(def.Id, out var v) ? v : 0;
            var tier = SaisonPassEngine.TierForXp(seasonXp, def);
            var (inTier, span) = SaisonPassEngine.ProgressInTier(seasonXp, def);

            // Statisches "Saison-Stufe"-Label steht im UXML daneben -> hier nur die Zahl.
            _tier.text = tier.ToString();
            if (tier >= def.HardCapTier)
            {
                _xpText.text = _loc.Get("ui.season_pass_max", "MAX");
                _xpFill.style.width = new Length(100, LengthUnit.Percent);
            }
            else
            {
                _xpText.text = $"{inTier} / {span} XP";   // numerisch, sprachneutral
                var pct = (float)inTier * 100f / span;
                _xpFill.style.width = new Length(pct, LengthUnit.Percent);
            }

            var daysLeft = Math.Max(0, (int)Math.Ceiling((def.EndsAtUtc - DateTime.UtcNow).TotalDays));
            _daysLeft.text = _loc.GetFormatted("ui.season_days_left", daysLeft);

            BuildRewardTrack(tier, def.TotalTiers);
        }

        private void BuildRewardTrack(int currentTier, int totalTiers)
        {
            _rewardTrack.Clear();
            for (var t = 1; t <= totalTiers; t++)
            {
                _rewardTrack.Add(BuildTierColumn(t, currentTier));
            }
        }

        private VisualElement BuildTierColumn(int tier, int currentTier)
        {
            var col = new VisualElement();
            col.style.width = 100;
            col.style.marginRight = 6;
            col.style.alignItems = Align.Center;

            // Free-Reward (mit Reward-Sprite saison_reward_{tier})
            var free = new VisualElement();
            free.AddToClassList("ak-surface");
            free.style.width = 96;
            free.style.height = 80;
            free.style.alignItems = Align.Center;
            free.style.justifyContent = Justify.Center;
            if (tier > currentTier) free.style.opacity = 0.4f;

            // Reward-Sprite als Background (saison_reward_1.png ... saison_reward_30.png)
            _uiAssets.ApplyBackground(free, $"SaisonPass/saison_reward_{tier}",
                                      UnityEngine.ScaleMode.ScaleToFit);

            var freeLabel = new Label(FreeRewardFor(tier));
            freeLabel.AddToClassList("ak-caption");
            freeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            freeLabel.style.position = Position.Absolute;
            freeLabel.style.bottom = 2;
            freeLabel.style.color = new StyleColor(Color.white);
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

            // Premium-Reward (anderer Tier-Asset-Index — wir nehmen die spaeteren Sprites)
            var premium = new VisualElement();
            premium.AddToClassList("ak-surface-elevated");
            premium.style.width = 96;
            premium.style.height = 80;
            premium.style.alignItems = Align.Center;
            premium.style.justifyContent = Justify.Center;
            premium.style.opacity = 0.55f; // bis Premium gekauft

            // Premium nutzt einen verschobenen Index (Tier+15 mod 30+1)
            var premiumIdx = ((tier + 15 - 1) % 30) + 1;
            _uiAssets.ApplyBackground(premium, $"SaisonPass/saison_reward_{premiumIdx}",
                                      UnityEngine.ScaleMode.ScaleToFit);

            var pLabel = new Label(PremiumRewardFor(tier));
            pLabel.AddToClassList("ak-caption");
            pLabel.AddToClassList("ak-text--accent");
            pLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            pLabel.style.position = Position.Absolute;
            pLabel.style.bottom = 2;
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
