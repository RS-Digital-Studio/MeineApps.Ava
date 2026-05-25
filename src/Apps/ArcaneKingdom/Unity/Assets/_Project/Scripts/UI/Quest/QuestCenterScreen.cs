#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
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

        private VisualElement _tabBar = null!;
        private VisualElement _content = null!;
        private Button _closeBtn = null!;
        private string _activeTab = "daily";

        public override string Id => ScreenId.QuestCenter;
        protected override string UxmlPath => "UI/QuestCenterScreen";

        public QuestCenterScreen(ScreenManager screenManager, ILocalizationService loc, ToastService toast)
        {
            _screenManager = screenManager;
            _loc = loc;
            _toast = toast;
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
                case "daily":        AddSampleQuests("daily", new[] { ("Gewinne 3 Kaempfe", "0/3"), ("Spiele 5 Feuerkarten", "0/5"), ("Verbinde dich mit Freunden", "0/1") }); break;
                case "weekly":       AddSampleQuests("weekly", new[] { ("Erreiche Profi auf 5 Leveln", "0/5"), ("Gewinne 10 Arena-Kaempfe", "0/10") }); break;
                case "achievements": AddSampleQuests("achievement", new[] { ("Besiege 100 Bosse", "0/100"), ("Sammle 50 Karten", "0/50") }); break;
                case "events":       _content.Add(new Label("Keine aktiven Events — schau spaeter wieder rein!")); break;
                case "login":        RenderLoginCalendar(); break;
            }
        }

        private void AddSampleQuests(string category, (string title, string progress)[] quests)
        {
            foreach (var (title, progress) in quests)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = 12; row.style.paddingRight = 12;
                row.style.paddingTop = 8; row.style.paddingBottom = 8;
                row.style.marginBottom = 6;
                row.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.10f, 0.10f, 0.18f));
                row.style.borderTopLeftRadius = 8; row.style.borderTopRightRadius = 8;
                row.style.borderBottomLeftRadius = 8; row.style.borderBottomRightRadius = 8;

                var titleLbl = new Label(title);
                titleLbl.style.flexGrow = 1; titleLbl.style.color = new StyleColor(UnityEngine.Color.white);
                row.Add(titleLbl);

                var progressLbl = new Label(progress);
                progressLbl.style.color = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
                progressLbl.style.marginRight = 12;
                row.Add(progressLbl);

                var claimBtn = new Button(() => _toast.Show("Belohnung abgeholt!", ToastKind.Success)) { text = "Abholen" };
                claimBtn.style.width = 80; claimBtn.style.height = 32;
                row.Add(claimBtn);

                _content.Add(row);
            }
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
