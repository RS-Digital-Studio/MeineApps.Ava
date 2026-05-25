#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Merit;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Merit
{
    /// <summary>
    /// Merit-Rangliste (Spielplan v5 Kap. 15.1 + Impl_KOMPLETT Kap. 14.1).
    /// Top-3 mit Gold/Silber/Bronze-Medaillon, Rest als Liste, eigener Rang sticky.
    /// </summary>
    public sealed class MeritRankingScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly MeritService _meritService;

        private VisualElement _topPodium = null!;
        private VisualElement _rankingList = null!;
        private Button _closeBtn = null!;

        public override string Id => ScreenId.MeritRanking;
        protected override string UxmlPath => "UI/MeritRankingScreen";

        public MeritRankingScreen(ScreenManager screenManager, MeritService meritService)
        {
            _screenManager = screenManager;
            _meritService = meritService;
        }

        protected override void BindElements(VisualElement root)
        {
            _topPodium   = Q<VisualElement>("merit-podium");
            _rankingList = Q<VisualElement>("merit-list");
            _closeBtn    = Q<Button>("merit-close");
            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
        }

        public override UniTask OnEnterAsync(System.Threading.CancellationToken ct)
        {
            // Mock-Daten bis Firebase integriert ist
            var mockEntries = new List<MeritRankEntry>
            {
                new() { PlayerName = "Drachenfaust", GuildTag = "KINGZ", Level = 142, MeritPoints = 199_999 },
                new() { PlayerName = "Sturmreiterin", GuildTag = "ELITE", Level = 138, MeritPoints = 187_452 },
                new() { PlayerName = "Schattenklinge", GuildTag = "KINGZ", Level = 135, MeritPoints = 172_900 },
                new() { PlayerName = "Mondbote", GuildTag = "NEXUS", Level = 130, MeritPoints = 156_320 },
                new() { PlayerName = "Eisendrache", GuildTag = "VOID", Level = 128, MeritPoints = 144_810 },
                new() { PlayerName = "Sperber", GuildTag = "KINGZ", Level = 88, MeritPoints = 89_500 }   // eigener Spieler
            };
            var ranked = _meritService.RankByMerit(mockEntries);
            BuildPodium(ranked);
            BuildList(ranked);
            return UniTask.CompletedTask;
        }

        private void BuildPodium(IReadOnlyList<MeritRankEntry> entries)
        {
            _topPodium.Clear();
            for (var i = 0; i < System.Math.Min(3, entries.Count); i++)
            {
                var entry = entries[i];
                var medalColor = i switch
                {
                    0 => new UnityEngine.Color(1.0f, 0.84f, 0.0f),   // Gold
                    1 => new UnityEngine.Color(0.75f, 0.75f, 0.78f), // Silber
                    _ => new UnityEngine.Color(0.80f, 0.50f, 0.20f)  // Bronze
                };
                var card = new VisualElement();
                card.style.width = 110; card.style.height = 140; card.style.marginRight = 8;
                card.style.backgroundColor = new StyleColor(medalColor);
                card.style.alignItems = Align.Center; card.style.justifyContent = Justify.Center;
                card.style.borderTopLeftRadius = 12; card.style.borderTopRightRadius = 12;
                card.style.borderBottomLeftRadius = 12; card.style.borderBottomRightRadius = 12;

                card.Add(new Label($"#{entry.Rank}") { style = { fontSize = 22, unityFontStyleAndWeight = UnityEngine.FontStyle.Bold, color = new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f)) } });
                card.Add(new Label($"[{entry.GuildTag}]\n{entry.PlayerName}") { style = { fontSize = 11, whiteSpace = WhiteSpace.Normal, unityTextAlign = UnityEngine.TextAnchor.MiddleCenter, color = new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f)) } });
                card.Add(new Label($"{entry.MeritPoints:N0}") { style = { fontSize = 14, marginTop = 6, color = new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f)) } });
                _topPodium.Add(card);
            }
        }

        private void BuildList(IReadOnlyList<MeritRankEntry> entries)
        {
            _rankingList.Clear();
            for (var i = 3; i < entries.Count; i++)
            {
                var entry = entries[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
                row.style.paddingLeft = 12; row.style.paddingRight = 12; row.style.paddingTop = 8; row.style.paddingBottom = 8;
                row.style.marginBottom = 4;
                row.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.10f, 0.10f, 0.18f));
                row.style.borderTopLeftRadius = 6; row.style.borderTopRightRadius = 6;
                row.style.borderBottomLeftRadius = 6; row.style.borderBottomRightRadius = 6;

                row.Add(new Label($"#{entry.Rank}") { style = { width = 40, color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f)) } });
                row.Add(new Label($"[{entry.GuildTag}] {entry.PlayerName}") { style = { flexGrow = 1, color = new StyleColor(UnityEngine.Color.white) } });
                row.Add(new Label($"LV {entry.Level}") { style = { width = 60, color = new StyleColor(new UnityEngine.Color(0.67f, 0.67f, 0.75f)) } });
                row.Add(new Label($"{entry.MeritPoints:N0}") { style = { width = 100, color = new StyleColor(new UnityEngine.Color(0.41f, 0.94f, 0.68f)) } });
                _rankingList.Add(row);
            }
        }
    }
}
