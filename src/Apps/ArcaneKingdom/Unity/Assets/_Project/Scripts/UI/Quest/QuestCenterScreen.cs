#nullable enable
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Quest;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Quest;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Quest
{
    /// <summary>
    /// Quest-Center (Spielplan v5 Kap. 16 + Impl_KOMPLETT Kap. 15).
    /// 5 Tabs: Taeglich | Woechentlich | Errungenschaften | Events | Login-Bonus (7-Tage-Kalender).
    /// </summary>
    public sealed class QuestCenterScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;
        private readonly QuestService _questService;

        private VisualElement _tabBar = null!;
        private VisualElement _content = null!;
        private Button _closeBtn = null!;
        private string _activeTab = "daily";

        public override string Id => ScreenId.QuestCenter;
        protected override string UxmlPath => "UI/QuestCenterScreen";

        private readonly UIAssetService _uiAssets;

        public QuestCenterScreen(ScreenManager screenManager, ILocalizationService loc, ToastService toast,
                                 QuestService questService, UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _loc = loc;
            _toast = toast;
            _questService = questService;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _tabBar   = Q<VisualElement>("quest-tabs");
            _content  = Q<VisualElement>("quest-content");
            _closeBtn = Q<Button>("quest-close");
            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
            BuildTabs();
            RenderContent();
        }

        private void BuildTabs()
        {
            _tabBar.Clear();
            string[] tabs = { "daily", "weekly", "achievements", "events", "login" };
            string[] labels = { "Taeglich", "Woechentlich", "Erfolge", "Events", "Login-Bonus" };
            for (var i = 0; i < tabs.Length; i++)
            {
                var id = tabs[i];
                var btn = new Button(() => { _activeTab = id; RenderContent(); BuildTabs(); }) { text = labels[i] };
                btn.style.flexGrow = 1; btn.style.height = 36; btn.style.marginRight = 4;
                if (id == _activeTab)
                {
                    btn.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
                    btn.style.color = new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f));
                }
                _tabBar.Add(btn);
            }
        }

        private void RenderContent()
        {
            _content.Clear();
            switch (_activeTab)
            {
                case "daily":        RenderQuestsForPeriod(QuestPeriod.Daily); break;
                case "weekly":       RenderQuestsForPeriod(QuestPeriod.Weekly); break;
                case "achievements": RenderQuestsForPeriod(QuestPeriod.Achievement); break;
                case "events":       _content.Add(new Label("Keine aktiven Events — schau spaeter wieder rein!")); break;
                case "login":        RenderLoginCalendar(); break;
            }
        }

        private void RenderQuestsForPeriod(QuestPeriod period)
        {
            var defs = _questService.AllDefinitions
                .Where(q => q.Period == period)
                .ToList();

            if (defs.Count == 0)
            {
                _content.Add(new Label("Keine Quests fuer diesen Zeitraum.") {
                    style = { color = new StyleColor(new UnityEngine.Color(0.67f, 0.67f, 0.75f)) }
                });
                return;
            }

            foreach (var def in defs)
            {
                var progress = _questService.GetProgress(def.Id);
                _content.Add(BuildQuestRow(def, progress));
            }
        }

        private VisualElement BuildQuestRow(QuestDefinition def, QuestProgress progress)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingLeft = 12; row.style.paddingRight = 12;
            row.style.paddingTop = 8; row.style.paddingBottom = 8;
            row.style.marginBottom = 6;
            row.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.10f, 0.10f, 0.18f));
            row.style.borderTopLeftRadius = 8; row.style.borderTopRightRadius = 8;
            row.style.borderBottomLeftRadius = 8; row.style.borderBottomRightRadius = 8;

            // Quest-Icon links (52x52). Achievements nutzen das spezifische Achievement-Sprite,
            // alle anderen Quests das passende Quest-Sprite.
            var icon = new VisualElement();
            icon.style.width = 52; icon.style.height = 52;
            icon.style.marginRight = 10;
            if (def.Period == QuestPeriod.Achievement)
                _uiAssets.ApplyAchievement(icon, def.Id);
            else
                _uiAssets.ApplyBackground(icon, $"Quests/{PickQuestIcon(def.Id, def.Period)}",
                                          UnityEngine.ScaleMode.ScaleToFit);
            row.Add(icon);

            // Content-Spalte
            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 1;

            // Header-Row: Titel + Belohnung
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var titleLbl = new Label(NicifyKey(def.DisplayNameKey, def.Id));
            titleLbl.style.flexGrow = 1; titleLbl.style.color = new StyleColor(UnityEngine.Color.white);
            titleLbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            header.Add(titleLbl);

            var rewardSummary = def.Rewards != null && def.Rewards.Count > 0
                ? string.Join(" • ", def.Rewards.Select(r => $"{r.Amount} {r.SubType}"))
                : "—";
            var rewardLbl = new Label(rewardSummary);
            rewardLbl.style.color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
            rewardLbl.style.fontSize = 11;
            header.Add(rewardLbl);
            content.Add(header);

            // Progress-Row: X/Y + Abholen-Button
            var bottom = new VisualElement();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.alignItems = Align.Center;
            bottom.style.marginTop = 6;

            var progressLbl = new Label($"{progress.CurrentCount}/{def.TargetCount}");
            progressLbl.style.flexGrow = 1;
            progressLbl.style.color = new StyleColor(new UnityEngine.Color(0.67f, 0.67f, 0.75f));
            bottom.Add(progressLbl);

            if (progress.RewardClaimed)
            {
                var doneLbl = new Label("✓ Eingeloest");
                doneLbl.style.color = new StyleColor(new UnityEngine.Color(0.41f, 0.94f, 0.68f));
                bottom.Add(doneLbl);
            }
            else if (progress.Completed)
            {
                var claimBtn = new Button(() => ClaimAsync(def.Id).Forget()) { text = "Abholen" };
                claimBtn.style.width = 100; claimBtn.style.height = 32;
                claimBtn.style.backgroundColor = new StyleColor(new UnityEngine.Color(1.0f, 0.48f, 0.0f));
                claimBtn.style.color = new StyleColor(UnityEngine.Color.white);
                bottom.Add(claimBtn);
            }
            else
            {
                var noteLbl = new Label("In Fortschritt");
                noteLbl.style.color = new StyleColor(new UnityEngine.Color(0.55f, 0.55f, 0.65f));
                noteLbl.style.fontSize = 11;
                bottom.Add(noteLbl);
            }
            content.Add(bottom);
            row.Add(content);

            return row;
        }

        /// <summary>
        /// Pickt ein passendes Quest-Sprite aus Resources/Quests/ basierend auf
        /// Quest-ID (Keywords im Pfad) und Period-Fallback. 20 Sprites verfuegbar:
        /// daily_battle/login/arena/card/world, weekly_arena/battle/collect/guild,
        /// monthly_legendary/mythic, achievement_bronze/silver/gold/diamond/iron,
        /// event_winter/spring/summer/autumn.
        /// </summary>
        private static string PickQuestIcon(string questId, QuestPeriod period)
        {
            var id = questId.ToLowerInvariant();
            // Keyword-Match
            if (id.Contains("login"))    return "daily_login";
            if (id.Contains("arena"))    return period == QuestPeriod.Weekly ? "weekly_arena" : "daily_arena";
            if (id.Contains("battle") || id.Contains("kampf"))
                                          return period == QuestPeriod.Weekly ? "weekly_battle" : "daily_battle";
            if (id.Contains("card") || id.Contains("karte"))
                                          return "daily_card";
            if (id.Contains("world") || id.Contains("welt"))
                                          return "daily_world";
            if (id.Contains("collect") || id.Contains("sammel"))
                                          return "weekly_collect";
            if (id.Contains("guild") || id.Contains("gilde"))
                                          return "weekly_guild";
            if (id.Contains("legend"))    return "monthly_legendary";
            if (id.Contains("myth"))      return "monthly_mythic";
            if (id.Contains("event"))     return "event_spring";
            // Achievements: Fallback nach Rarity-Stufe
            return period switch
            {
                QuestPeriod.Daily       => "daily_battle",
                QuestPeriod.Weekly      => "weekly_battle",
                QuestPeriod.Achievement => "achievement_bronze",
                _                       => "daily_battle"
            };
        }

        private async UniTask ClaimAsync(string questId)
        {
            var result = await _questService.ClaimAsync(questId);
            if (result.IsSuccess)
            {
                _toast.Show("Quest eingeloest!", ToastKind.Success);
                RenderContent();
            }
            else
            {
                _toast.Show(result.ErrorMessage ?? "Fehler beim Einloesen", ToastKind.Danger);
            }
        }

        private static string NicifyKey(string? key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            var dot = key.LastIndexOf('.');
            if (dot < 0 || dot >= key.Length - 1) return fallback;
            var raw = key.Substring(dot + 1).Replace('_', ' ');
            return raw.Length == 0 ? fallback : char.ToUpper(raw[0]) + raw.Substring(1);
        }

        private void RenderLoginCalendar()
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            for (var day = 1; day <= 7; day++)
            {
                var cell = new VisualElement();
                cell.style.width = 60; cell.style.height = 80; cell.style.marginRight = 6; cell.style.marginBottom = 6;
                cell.style.backgroundColor = day == 1
                    ? new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f))
                    : new StyleColor(new UnityEngine.Color(0.10f, 0.10f, 0.18f));
                cell.style.alignItems = Align.Center; cell.style.justifyContent = Justify.Center;
                cell.style.borderTopLeftRadius = 8; cell.style.borderTopRightRadius = 8;
                cell.style.borderBottomLeftRadius = 8; cell.style.borderBottomRightRadius = 8;

                var dayLbl = new Label($"Tag {day}");
                dayLbl.style.fontSize = 11;
                dayLbl.style.color = day == 1
                    ? new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f))
                    : new StyleColor(new UnityEngine.Color(0.67f, 0.67f, 0.75f));
                cell.Add(dayLbl);

                var reward = new Label(day == 7 ? "Epic\nKarte" : $"{day * 1000}\nGold");
                reward.style.fontSize = 10;
                reward.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
                reward.style.whiteSpace = WhiteSpace.Normal;
                reward.style.color = day == 1
                    ? new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f))
                    : new StyleColor(UnityEngine.Color.white);
                cell.Add(reward);
                grid.Add(cell);
            }
            _content.Add(grid);

            var claim = new Button(() => _toast.Show("Tages-Login abgeholt!", ToastKind.Success)) { text = "Jetzt abholen" };
            claim.style.height = 44; claim.style.marginTop = 12;
            _content.Add(claim);
        }
    }
}
