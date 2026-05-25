#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Arena
{
    /// <summary>
    /// Full-Screen-Arena: Rank, W/N-Bilanz, Reward-Track, Match-Suche, Leaderboard.
    /// </summary>
    public sealed class ArenaScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ToastService _toast;

        private Button _backBtn = null!;
        private Label _ticketsLabel = null!;
        private Label _rankName = null!;
        private Label _rankPoints = null!;
        private VisualElement _rankFill = null!;
        private Label _record = null!;
        private VisualElement _rewardTrack = null!;
        private Button _quickMatchBtn = null!;
        private Button _leaderboardBtn = null!;

        public override string Id => ScreenId.Arena;
        protected override string UxmlPath => "UI/ArenaScreen";

        private readonly UIAssetService _uiAssets;

        public ArenaScreen(ScreenManager screenManager,
                           ISaveService<PlayerSave> save, ToastService toast,
                           UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _toast = toast;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _uiAssets.ApplyUIBackground(root, "arena");
            _backBtn        = Q<Button>("arena-back-button");
            _ticketsLabel   = Q<Label>("arena-tickets-label");
            _rankName       = Q<Label>("arena-rank-name");
            _rankPoints     = Q<Label>("arena-rank-points");
            _rankFill       = Q<VisualElement>("arena-rank-fill");
            _record         = Q<Label>("arena-record");
            _rewardTrack    = Q<VisualElement>("arena-reward-track");
            _quickMatchBtn  = Q<Button>("arena-quick-match");
            _leaderboardBtn = Q<Button>("arena-leaderboard");

            _backBtn.clicked += () => _screenManager.PopAsync().Forget();
            _quickMatchBtn.clicked += () =>
                _toast.Show("Matchmaking-Backend folgt — vorerst Mock-Battle starten.", ToastKind.Info);
            _leaderboardBtn.clicked += () =>
                _toast.Show("Server-Leaderboard kommt mit Firebase-Integration.", ToastKind.Info);
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            if (!result.IsSuccess || result.Value == null) return;

            var save = result.Value;
            _ticketsLabel.text = $"{save.Currencies.ArenaTickets} Tickets";

            // Rang berechnen
            var merit = save.Currencies.MeritPoints;
            var (name, ptsInTier, tierMax) = ComputeRank(merit);
            _rankName.text = name;
            _rankPoints.text = $"{ptsInTier} / {tierMax} Rang-Punkte";
            var pct = tierMax > 0 ? (float)ptsInTier * 100f / tierMax : 0f;
            _rankFill.style.width = new Length(System.Math.Min(pct, 100f), LengthUnit.Percent);

            // W/N — bisher nicht in PlayerSave, daher Platzhalter
            _record.text = "0 W / 0 N";

            BuildRewardTrack(merit);
        }

        private void BuildRewardTrack(long merit)
        {
            _rewardTrack.Clear();
            string[] tierNames = { "Bronze", "Silber", "Gold", "Platin", "Diamant", "Meister" };
            long[] tierThresholds = { 0, 300, 600, 900, 1200, 1500 };

            for (var i = 0; i < tierNames.Length; i++)
            {
                var achieved = merit >= tierThresholds[i];
                var tile = new VisualElement();
                tile.style.width = 110;
                tile.style.marginRight = 8;
                tile.style.marginBottom = 8;
                tile.style.alignItems = Align.Center;
                tile.AddToClassList(achieved ? "ak-surface" : "ak-surface");
                if (!achieved) tile.style.opacity = 0.45f;

                var name = new Label(tierNames[i]);
                name.AddToClassList("ak-h4");
                if (achieved) name.AddToClassList("ak-text--accent");
                tile.Add(name);

                var rewards = new Label(RewardsForTier(i));
                rewards.AddToClassList("ak-caption");
                rewards.style.whiteSpace = WhiteSpace.Normal;
                tile.Add(rewards);

                _rewardTrack.Add(tile);
            }
        }

        private static string RewardsForTier(int tierIndex) => tierIndex switch
        {
            0 => "100 Gold • 1 Pack",
            1 => "300 Gold • 1 Pack",
            2 => "800 Gold • 2 Packs",
            3 => "1500 Gold • 1 Rare-Pack",
            4 => "3000 Gold • 1 Epic-Pack",
            5 => "5000 Gold + Legendary",
            _ => ""
        };

        private static (string name, int pts, int max) ComputeRank(long merit)
        {
            string[] leagues = { "Bronze", "Silber", "Gold", "Platin", "Diamant", "Meister" };
            string[] tiers = { "III", "II", "I" };
            const int pointsPerTier = 100;
            var capped = System.Math.Min(merit, 5 * 3 * pointsPerTier);
            var totalTiers = (int)(capped / pointsPerTier);
            if (totalTiers >= 15) return ("Meister", (int)(merit - 1500), 1000);
            var league = totalTiers / 3;
            var tier = totalTiers % 3;
            return ($"{leagues[league]} {tiers[tier]}",
                    (int)(capped % pointsPerTier), pointsPerTier);
        }
    }
}
